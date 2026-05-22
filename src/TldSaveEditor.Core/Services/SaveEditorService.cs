using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using The_Long_Dark_Save_Editor_2.Game_data;
using The_Long_Dark_Save_Editor_2.Helpers;

namespace The_Long_Dark_Save_Editor_2.Services
{
    // UI-agnostic facade over the Core save model. Holds the currently loaded save in memory
    // and exposes editable sections as plain DTOs plus the game's domain knowledge
    // (item dictionary, regions). Consumed by the HTTP server today, reusable by a CLI later.
    //
    // A single save is edited at a time. The instance is shared (the server registers it as a
    // singleton), so every public method is serialized behind _gate: requests can arrive
    // concurrently and the in-memory object graph and the parser's static caches are not
    // otherwise thread-safe.
    public class SaveEditorService
    {
        private readonly object _gate = new object();

        private GameSave _current;
        private string _currentName;
        private Profile _profile;

        // Directory that holds the survival save files. Defaults to the game's standard
        // location, but can be overridden (e.g. for tests or non-standard installs).
        public string SavesFolder { get; set; }

        public SaveEditorService()
        {
            SavesFolder = DefaultSavesFolder();
        }

        public static string DefaultSavesFolder()
        {
            var local = Util.GetLocalPath();
            return string.IsNullOrEmpty(local)
                ? string.Empty
                : Path.Combine(local, "Hinterland", "TheLongDark", "Survival");
        }

        public bool HasLoadedSave => _current != null;

        public IReadOnlyList<SaveSummary> ListSaves()
        {
            lock (_gate)
            {
                var members = Util.GetSaveFiles(SavesFolder);
                return members
                    .Select((m, i) => new SaveSummary { Index = i, Name = m.Description, Path = (string)m.Value })
                    .ToList();
            }
        }

        public LoadedSaveDto Load(string path)
        {
            lock (_gate)
            {
                EnsureWithinSavesFolder(path);
                if (!File.Exists(path))
                    throw new FileNotFoundException("Save file not found", path);

                var save = new GameSave();
                save.LoadSave(path);
                _current = save;
                _currentName = Path.GetFileName(path);
                _profile = TryLoadProfile(path);
                return Describe();
            }
        }

        public void Save()
        {
            lock (_gate)
            {
                EnsureLoaded();
                _current.Save();
                _profile?.Save();
            }
        }

        // The global profile (feats, badges) is a user001.* file. It usually sits in the
        // game's TheLongDark folder, i.e. the parent of the Survival saves folder — but we
        // also check the save's own directory to be safe. The newest matching file wins.
        private static Profile TryLoadProfile(string savePath)
        {
            try
            {
                var saveDir = Path.GetDirectoryName(savePath);
                var parentDir = Path.GetDirectoryName(saveDir);

                var file = new[] { saveDir, parentDir }
                    .Where(d => !string.IsNullOrEmpty(d) && Directory.Exists(d))
                    .SelectMany(d => Directory.GetFiles(d, "user001.*"))
                    .Where(f => !f.EndsWith(".backup", StringComparison.OrdinalIgnoreCase))
                    .OrderByDescending(f => new FileInfo(f).LastWriteTime)
                    .FirstOrDefault();

                return file != null ? new Profile(file) : null;
            }
            catch
            {
                return null; // profile is optional; ignore load failures
            }
        }

        private LoadedSaveDto Describe()
        {
            var scene = _current.Boot.m_SceneName;
            var mode = _current.Global.ExperienceModeManager?.m_CurrentModeType;
            return new LoadedSaveDto
            {
                Path = _current.path,
                DisplayName = _currentName,
                Region = scene?.Value,
                AvailableRegions = scene?.Values?.ToList() ?? new List<string>(),
                AvailableModes = mode?.Values?.ToList() ?? new List<string>(),
                HasProfile = _profile != null,
            };
        }

        // ---- Player ---------------------------------------------------------

        public PlayerDto GetPlayer()
        {
            lock (_gate)
            {
                EnsureLoaded();
                var g = _current.Global;
                var pos = g.PlayerManager?.m_SaveGamePosition;
                return new PlayerDto
                {
                    Health = g.Condition.m_CurrentHPProxy,
                    NeverDie = g.Condition.m_NeverDieProxy,
                    Invulnerable = g.Condition.m_Invulnerable,
                    InfiniteCarry = g.Inventory.m_ForceOverrideWeight,
                    CarryWeightLimit = g.Inventory.m_OverridedWeight,
                    Calories = g.Hunger.m_CurrentReserveCaloriesProxy,
                    Thirst = g.Thirst.m_CurrentThirstProxy,
                    Fatigue = g.Fatigue.m_CurrentFatigueProxy,
                    Freezing = g.Freezing.m_CurrentFreezingProxy,
                    // UI convention from the original editor: X=[0], Y=[2], Z=[1].
                    PositionX = PosAt(pos, 0),
                    PositionY = PosAt(pos, 2),
                    PositionZ = PosAt(pos, 1),
                    Region = _current.Boot.m_SceneName?.Value,
                    ExperienceMode = g.ExperienceModeManager?.m_CurrentModeType?.Value,
                    CustomMode = g.ExperienceModeManager?.m_CustomModeString,
                };
            }
        }

        public void UpdatePlayer(PlayerDto dto)
        {
            lock (_gate)
            {
                EnsureLoaded();
                var g = _current.Global;
                g.Condition.m_CurrentHPProxy = dto.Health;
                g.Condition.m_NeverDieProxy = dto.NeverDie;
                g.Condition.m_Invulnerable = dto.Invulnerable;
                g.Inventory.m_ForceOverrideWeight = dto.InfiniteCarry;
                g.Inventory.m_OverridedWeight = dto.CarryWeightLimit;
                g.Hunger.m_CurrentReserveCaloriesProxy = dto.Calories;
                g.Thirst.m_CurrentThirstProxy = dto.Thirst;
                g.Fatigue.m_CurrentFatigueProxy = dto.Fatigue;
                g.Freezing.m_CurrentFreezingProxy = dto.Freezing;

                var pos = g.PlayerManager?.m_SaveGamePosition;
                if (pos != null && pos.Count >= 3)
                {
                    pos[0] = dto.PositionX;
                    pos[2] = dto.PositionY;
                    pos[1] = dto.PositionZ;
                }

                if (!string.IsNullOrEmpty(dto.Region) && _current.Boot.m_SceneName != null)
                    _current.Boot.m_SceneName.Value = dto.Region;

                var emm = g.ExperienceModeManager;
                if (!string.IsNullOrEmpty(dto.ExperienceMode) && emm?.m_CurrentModeType != null)
                    emm.m_CurrentModeType.Value = dto.ExperienceMode;
                if (emm != null && dto.CustomMode != null)
                    emm.m_CustomModeString = dto.CustomMode;
            }
        }

        private static float PosAt(System.Collections.ObjectModel.ObservableCollection<float> pos, int i)
            => pos != null && pos.Count > i ? pos[i] : 0f;

        // ---- Inventory ------------------------------------------------------

        public IReadOnlyList<InventoryItemDto> GetInventory()
        {
            lock (_gate)
            {
                EnsureLoaded();
                return _current.Global.Inventory.Items
                    .Select(it => new InventoryItemDto
                    {
                        InstanceId = it.Gear?.m_InstanceIDProxy ?? 0,
                        PrefabName = it.m_PrefabName,
                        DisplayName = it.InGameName,
                        Category = it.Category.ToString(),
                        Condition = it.Gear?.NormalizedCondition ?? 0f,
                        Quantity = it.Gear?.StackableItem?.m_UnitsProxy,
                        Liters = GetLiters(it.Gear),
                        Rounds = it.Gear?.WeaponItem?.m_RoundsInClipProxy,
                    })
                    .ToList();
            }
        }

        public void UpdateItem(int instanceId, UpdateItemRequest req)
        {
            lock (_gate)
            {
                EnsureLoaded();
                var item = _current.Global.Inventory.Items
                    .FirstOrDefault(it => it.Gear != null && it.Gear.m_InstanceIDProxy == instanceId);
                if (item == null)
                    throw new ArgumentException("Item not found: " + instanceId);

                item.Gear.NormalizedCondition = req.Condition;
                if (req.Quantity.HasValue && item.Gear.StackableItem != null)
                    item.Gear.StackableItem.m_UnitsProxy = req.Quantity.Value;
                if (req.Liters.HasValue)
                    SetLiters(item.Gear, req.Liters.Value);
                if (req.Rounds.HasValue && item.Gear.WeaponItem != null)
                    item.Gear.WeaponItem.m_RoundsInClipProxy = req.Rounds.Value;
            }
        }

        // Items that hold liquid expose it through one of three proxies (1 L == 1e9 internally,
        // handled by the .Liters helpers). Returns null for items that hold no liquid.
        private static double? GetLiters(GearItemSaveDataProxy g)
        {
            if (g == null) return null;
            if (g.LiquidItem != null) return g.LiquidItem.Liters;
            if (g.WaterSupply != null) return g.WaterSupply.Liters;
            if (g.KeroseneLampItem != null) return g.KeroseneLampItem.Liters;
            return null;
        }

        private static void SetLiters(GearItemSaveDataProxy g, double liters)
        {
            if (g == null) return;
            if (g.LiquidItem != null) g.LiquidItem.Liters = liters;
            else if (g.WaterSupply != null) g.WaterSupply.Liters = liters;
            else if (g.KeroseneLampItem != null) g.KeroseneLampItem.Liters = liters;
        }

        public void AddItem(string prefabName)
        {
            lock (_gate)
            {
                EnsureLoaded();
                if (!ItemDictionary.itemInfo.TryGetValue(prefabName, out var info))
                    throw new ArgumentException("Unknown item: " + prefabName);

                var item = new InventoryItemSaveData { m_PrefabName = prefabName };
                var gear = GearItemSaveDataProxy.Create(_current.Global);
                JsonConvert.PopulateObject(info.defaultSerialized, gear);
                item.Gear = gear;
                _current.Global.Inventory.Items.Add(item);
            }
        }

        public void RemoveItem(int instanceId)
        {
            lock (_gate)
            {
                EnsureLoaded();
                var items = _current.Global.Inventory.Items;
                var match = items.FirstOrDefault(it => it.Gear != null && it.Gear.m_InstanceIDProxy == instanceId);
                if (match != null)
                    items.Remove(match);
            }
        }

        public IReadOnlyList<AvailableItemDto> GetAvailableItems()
        {
            // ItemDictionary.itemInfo is immutable after its static initialiser, so no lock needed.
            return ItemDictionary.itemInfo
                .Where(e => !e.Value.hide)
                .Select(e => new AvailableItemDto
                {
                    PrefabName = e.Key,
                    DisplayName = ItemDictionary.GetInGameName(e.Key),
                    Category = e.Value.category.ToString(),
                })
                .OrderBy(e => e.Category)
                .ThenBy(e => e.DisplayName)
                .ToList();
        }

        // ---- Skills ---------------------------------------------------------

        public SkillsDto GetSkills()
        {
            lock (_gate)
            {
                EnsureLoaded();
                var s = _current.Global.SkillsManager;
                return new SkillsDto
                {
                    Firestarting = s.Firestarting.m_Points,
                    CarcassHarvesting = s.CarcassHarvesting.m_Points,
                    Cooking = s.Cooking.m_Points,
                    IceFishing = s.IceFishing.m_Points,
                    Rifle = s.Rifle.m_Points,
                    Archery = s.Archery.m_Points,
                    ClothingRepair = s.ClothingRepair.m_Points,
                    Revolver = s.Revolver.m_Points,
                    Gunsmith = s.Gunsmith.m_Points,
                };
            }
        }

        public void UpdateSkills(SkillsDto dto)
        {
            lock (_gate)
            {
                EnsureLoaded();
                var s = _current.Global.SkillsManager;
                s.Firestarting.m_Points = dto.Firestarting;
                s.CarcassHarvesting.m_Points = dto.CarcassHarvesting;
                s.Cooking.m_Points = dto.Cooking;
                s.IceFishing.m_Points = dto.IceFishing;
                s.Rifle.m_Points = dto.Rifle;
                s.Archery.m_Points = dto.Archery;
                s.ClothingRepair.m_Points = dto.ClothingRepair;
                s.Revolver.m_Points = dto.Revolver;
                s.Gunsmith.m_Points = dto.Gunsmith;
            }
        }

        // ---- Afflictions ----------------------------------------------------

        public IReadOnlyList<AfflictionDto> GetAfflictions()
        {
            lock (_gate)
            {
                EnsureLoaded();
                var result = new List<AfflictionDto>();
                var neg = _current.Afflictions.Negative;
                for (int i = 0; i < neg.Count; i++)
                    result.Add(ToAfflictionDto(neg[i], i, positive: false));
                var pos = _current.Afflictions.Positive;
                for (int i = 0; i < pos.Count; i++)
                    result.Add(ToAfflictionDto(pos[i], i, positive: true));
                return result;
            }
        }

        public void RemoveAffliction(bool positive, int index)
        {
            lock (_gate)
            {
                EnsureLoaded();
                var list = positive ? _current.Afflictions.Positive : _current.Afflictions.Negative;
                if (index >= 0 && index < list.Count)
                    list.RemoveAt(index);
            }
        }

        public void CureAllAfflictions()
        {
            lock (_gate)
            {
                EnsureLoaded();
                _current.Afflictions.Negative.Clear();
            }
        }

        private static AfflictionDto ToAfflictionDto(Affliction a, int index, bool positive)
        {
            var type = a.AfflictionType.ToString();
            // Best-effort friendly name; falls back to the raw enum name if no resource exists.
            var display = Properties.Resources.ResourceManager.GetString("AfflictionType_" + type) ?? type;
            return new AfflictionDto { Index = index, Positive = positive, Type = type, DisplayName = display };
        }

        // ---- Map ------------------------------------------------------------

        public MapDto GetMap()
        {
            lock (_gate)
            {
                EnsureLoaded();
                var region = _current.Boot.m_SceneName?.Value;
                var pos = _current.Global.PlayerManager?.m_SaveGamePosition;
                var dto = new MapDto
                {
                    Region = region,
                    HasMap = MapDictionary.MapExists(region),
                    PlayerX = PosAt(pos, 0),
                    PlayerY = PosAt(pos, 2),
                };
                if (dto.HasMap)
                {
                    var info = MapDictionary.GetMapInfo(region);
                    dto.Image = info.image;
                    dto.OrigoX = info.origoX;
                    dto.OrigoY = info.origoY;
                    dto.Width = info.width;
                    dto.Height = info.height;
                    dto.PixelsPerCoordinate = info.pixelsPerCoordinate;
                }
                return dto;
            }
        }

        public void SetMapPosition(double x, double y)
        {
            lock (_gate)
            {
                EnsureLoaded();
                var pos = _current.Global.PlayerManager?.m_SaveGamePosition;
                if (pos != null && pos.Count >= 3)
                {
                    pos[0] = (float)x;
                    pos[2] = (float)y;
                }
            }
        }

        // ---- Profile (feats / badges) --------------------------------------

        public ProfileDto GetProfile()
        {
            lock (_gate)
            {
                EnsureProfile();
                var f = _profile.State.Feats;
                return new ProfileDto
                {
                    BookSmartsHoursResearch = f.BookSmarts.m_HoursResearch,
                    ColdFusionElapsedDays = f.ColdFusion.m_ElapsedDays,
                    EfficientMachineElapsedHours = f.EfficientMachine.m_ElapsedHours,
                    FireMasterFiresStarted = f.FireMaster.m_NumFiresStarted,
                    FreeRunnerKilometers = f.FreeRunner.m_ElapsedKilometers,
                    SnowWalkerKilometers = f.SnowWalker.m_ElapsedKilometers,
                    ExpertTrapperRabbitsSnared = f.ExpertTrapper.m_RabbitSnaredCount,
                    StraightToHeartItemsConsumed = f.StraightToHeart.m_ItemConsumedCount,
                    BlizzardWalkerHoursOutside = f.BlizzardWalker.m_BlizzardHoursOutside,
                    Badge4DON = AllTrue(_profile.State.m_DaysCompleted4DON),
                    Badge4DON2019 = AllTrue(_profile.State.m_DaysCompleted4DON2019),
                };
            }
        }

        public void UpdateProfile(ProfileDto dto)
        {
            lock (_gate)
            {
                EnsureProfile();
                var f = _profile.State.Feats;
                f.BookSmarts.m_HoursResearch = dto.BookSmartsHoursResearch;
                f.ColdFusion.m_ElapsedDays = dto.ColdFusionElapsedDays;
                f.EfficientMachine.m_ElapsedHours = dto.EfficientMachineElapsedHours;
                f.FireMaster.m_NumFiresStarted = dto.FireMasterFiresStarted;
                f.FreeRunner.m_ElapsedKilometers = dto.FreeRunnerKilometers;
                f.SnowWalker.m_ElapsedKilometers = dto.SnowWalkerKilometers;
                f.ExpertTrapper.m_RabbitSnaredCount = dto.ExpertTrapperRabbitsSnared;
                f.StraightToHeart.m_ItemConsumedCount = dto.StraightToHeartItemsConsumed;
                f.BlizzardWalker.m_BlizzardHoursOutside = dto.BlizzardWalkerHoursOutside;
                _profile.State.m_DaysCompleted4DON = FourBadge(dto.Badge4DON);
                _profile.State.m_DaysCompleted4DON2019 = FourBadge(dto.Badge4DON2019);
            }
        }

        private const int FourDaysOfNight = 4;
        private static bool AllTrue(List<bool> l) => l != null && l.Count == FourDaysOfNight && l.All(b => b);
        private static List<bool> FourBadge(bool on) => Enumerable.Repeat(on, FourDaysOfNight).ToList();

        // ---- Backups --------------------------------------------------------

        // Lists the auto-backups the editor made of the currently loaded save (newest first).
        // Backups live in <saveDir>/backups and are named "<timestamp>-<filename>[(n)].backup".
        public IReadOnlyList<BackupDto> ListBackups()
        {
            lock (_gate)
            {
                EnsureLoaded();
                var dir = Path.Combine(Path.GetDirectoryName(_current.path), "backups");
                if (!Directory.Exists(dir))
                    return new List<BackupDto>();

                var name = Path.GetFileName(_current.path);
                var rx = new Regex("-" + Regex.Escape(name) + @"(\(\d+\))?\.backup$");
                return new DirectoryInfo(dir).GetFiles("*.backup")
                    .Where(f => rx.IsMatch(f.Name))
                    .OrderByDescending(f => f.LastWriteTime)
                    .Select(f => new BackupDto
                    {
                        Path = f.FullName,
                        Label = f.LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss") + $"  ({f.Length / 1024} KB)",
                    })
                    .ToList();
            }
        }

        // Restores a backup over the current save file, snapshotting the current state first
        // (GameSave.Backup, which also prunes to MAX_BACKUPS) so the restore is reversible,
        // then reloads the restored file.
        public LoadedSaveDto RestoreBackup(string backupPath)
        {
            lock (_gate)
            {
                EnsureLoaded();
                EnsureWithinSavesFolder(backupPath);
                if (!File.Exists(backupPath))
                    throw new FileNotFoundException("Backup not found.", backupPath);

                var target = _current.path;
                _current.Backup();
                File.Copy(backupPath, target, overwrite: true);
                return Load(target);
            }
        }

        // Rejects paths that resolve outside the configured saves folder (defence-in-depth for
        // the file-path endpoints, on top of the loopback-only binding).
        private void EnsureWithinSavesFolder(string path)
        {
            if (string.IsNullOrEmpty(SavesFolder))
                return;
            var root = Path.GetFullPath(SavesFolder);
            var full = Path.GetFullPath(path);
            if (full != root && !full.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal))
                throw new ArgumentException("Path is outside the saves folder.");
        }

        private void EnsureLoaded()
        {
            if (_current == null)
                throw new InvalidOperationException("No save is currently loaded.");
        }

        private void EnsureProfile()
        {
            if (_profile == null)
                throw new InvalidOperationException("No profile (user001) was found next to this save.");
        }
    }
}
