using System.Collections.Generic;

namespace The_Long_Dark_Save_Editor_2.Services
{
    // UI-agnostic data-transfer objects. Any frontend (web, CLI, desktop) speaks in these
    // instead of touching the raw game-data proxy graph.

    public class SaveSummary
    {
        public int Index { get; set; }
        public string Name { get; set; }
        public string Path { get; set; }
    }

    public class PlayerDto
    {
        public float Health { get; set; }
        public bool NeverDie { get; set; }
        public bool Invulnerable { get; set; }
        public bool InfiniteCarry { get; set; }
        public float CarryWeightLimit { get; set; }
        public float Calories { get; set; }
        public float Thirst { get; set; }
        public float Fatigue { get; set; }
        public float Freezing { get; set; }
        public float PositionX { get; set; }
        public float PositionY { get; set; }
        public float PositionZ { get; set; }
        public string Region { get; set; }
        public string ExperienceMode { get; set; }
        public string CustomMode { get; set; }
    }

    public class InventoryItemDto
    {
        public int InstanceId { get; set; }
        public string PrefabName { get; set; }
        public string DisplayName { get; set; }
        public string Category { get; set; }
        public float Condition { get; set; }
        // Stack quantity for stackable items (matches, ammo, tinder…); null otherwise.
        public int? Quantity { get; set; }
        // Litres of liquid/fuel/water for items that hold any; null otherwise.
        public double? Liters { get; set; }
        // Rounds loaded in the clip for firearms; null for non-weapons.
        public int? Rounds { get; set; }
    }

    public class UpdateItemRequest
    {
        public float Condition { get; set; }
        public int? Quantity { get; set; }
        public double? Liters { get; set; }
        public int? Rounds { get; set; }
    }

    public class AvailableItemDto
    {
        public string PrefabName { get; set; }
        public string DisplayName { get; set; }
        public string Category { get; set; }
    }

    public class BackupDto
    {
        public string Path { get; set; }
        public string Label { get; set; }
    }

    public class RestoreRequest
    {
        public string BackupPath { get; set; }
    }

    public class LoadedSaveDto
    {
        public string Path { get; set; }
        public string DisplayName { get; set; }
        public string Region { get; set; }
        public List<string> AvailableRegions { get; set; }
        public List<string> AvailableModes { get; set; }
        public bool HasProfile { get; set; }
    }

    public class SkillsDto
    {
        public int Firestarting { get; set; }
        public int CarcassHarvesting { get; set; }
        public int Cooking { get; set; }
        public int IceFishing { get; set; }
        public int Rifle { get; set; }
        public int Archery { get; set; }
        public int ClothingRepair { get; set; }
        public int Revolver { get; set; }
        public int Gunsmith { get; set; }
    }

    public class AfflictionDto
    {
        public int Index { get; set; }
        public bool Positive { get; set; }
        public string Type { get; set; }
        public string DisplayName { get; set; }
    }

    public class MapDto
    {
        public string Region { get; set; }
        public bool HasMap { get; set; }
        public string Image { get; set; }
        public double OrigoX { get; set; }
        public double OrigoY { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public double PixelsPerCoordinate { get; set; }
        public double PlayerX { get; set; }
        public double PlayerY { get; set; }
    }

    public class MapPositionRequest
    {
        public double X { get; set; }
        public double Y { get; set; }
    }

    public class ProfileDto
    {
        // Feat progress (units differ per feat; see the in-game requirement).
        public int BookSmartsHoursResearch { get; set; }
        public float ColdFusionElapsedDays { get; set; }
        public float EfficientMachineElapsedHours { get; set; }
        public int FireMasterFiresStarted { get; set; }
        public float FreeRunnerKilometers { get; set; }
        public float SnowWalkerKilometers { get; set; }
        public int ExpertTrapperRabbitsSnared { get; set; }
        public int StraightToHeartItemsConsumed { get; set; }
        public float BlizzardWalkerHoursOutside { get; set; }
        public bool Badge4DON { get; set; }
        public bool Badge4DON2019 { get; set; }
    }
}
