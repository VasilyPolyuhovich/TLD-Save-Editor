using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using The_Long_Dark_Save_Editor_2.Game_data;

namespace The_Long_Dark_Save_Editor_2.Helpers
{

    public class SlotDataDisplayNameProxy
    {
        public string m_DisplayName { get; set; }
    }

    public static class Util
    {
        public static T DeserializeObject<T>(string json) where T : class
        {

            if (json == null)
                return null;

            return JsonConvert.DeserializeObject<T>(json);
        }

        public static T DeserializeObjectOrDefault<T>(string json) where T : class, new()
        {
            if (json == null)
                return new T();
            return JsonConvert.DeserializeObject<T>(json);
        }

        public static string SerializeObject(object o)
        {
            if (o == null)
                return null;
            return JsonConvert.SerializeObject(o);
        }

        public static ObservableCollection<EnumerationMember> GetSaveFiles(string folder)
        {

            Regex reg = new Regex("^(ep[0-9])?(sandbox|challenge|story|relentless)[0-9]+$");
            var saves = new List<string>();
            if (Directory.Exists(folder))
                saves.AddRange((from f in Directory.GetFiles(folder) orderby new FileInfo(f).LastWriteTime descending where reg.IsMatch(Path.GetFileName(f)) select f).ToList<string>());

            var result = new ObservableCollection<EnumerationMember>();
            foreach (string saveFile in saves)
            {
                try
                {
                    var member = CreateSaveEnumerationMember(saveFile, Path.GetFileName(saveFile));
                    result.Add(member);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine(ex.ToString());
                    continue;
                }
            }

            return result;
        }

        private static EnumerationMember CreateSaveEnumerationMember(string file, string name)
        {
            var member = new EnumerationMember();
            member.Value = file;

            var slotJson = EncryptString.Decompress(File.ReadAllBytes(file));
            var slotData = JsonConvert.DeserializeObject<SlotDataDisplayNameProxy>(slotJson);

            member.Description = slotData.m_DisplayName + " (" + name + ")";

            return member;
        }

        // The Long Dark's Steam app id, used to locate the Proton prefix on Linux.
        private const string TldSteamAppId = "305620";

        // Returns the directory that contains "Hinterland/TheLongDark/Survival".
        // On Windows that is %LOCALAPPDATA% (AppData\Local). On Linux the game runs through
        // Proton, so the same files live inside the Steam compatibility prefix. macOS keeps
        // them under ~/Library/Application Support.
        public static string GetLocalPath()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                return Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    "Library", "Application Support");

            // Linux: look for the Proton prefix's drive_c/users/steamuser/AppData/Local.
            var candidates = GetLinuxProtonLocalAppDataCandidates().ToList();
            return candidates.FirstOrDefault(Directory.Exists)
                ?? candidates.FirstOrDefault()
                ?? string.Empty;
        }

        private static IEnumerable<string> GetLinuxProtonLocalAppDataCandidates()
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            string customSteam = Environment.GetEnvironmentVariable("STEAM_ROOT")
                ?? Environment.GetEnvironmentVariable("STEAM_BASE_FOLDER");

            var steamRoots = new List<string>();
            if (!string.IsNullOrEmpty(customSteam))
                steamRoots.Add(customSteam);
            steamRoots.Add(Path.Combine(home, ".steam", "steam"));
            steamRoots.Add(Path.Combine(home, ".local", "share", "Steam"));
            steamRoots.Add(Path.Combine(home, ".var", "app", "com.valvesoftware.Steam", ".local", "share", "Steam"));

            var seen = new HashSet<string>();
            foreach (var root in steamRoots)
            {
                foreach (var libRoot in EnumerateSteamLibraryRoots(root))
                {
                    var compat = Path.Combine(libRoot, "steamapps", "compatdata", TldSteamAppId,
                        "pfx", "drive_c", "users", "steamuser", "AppData", "Local");
                    if (seen.Add(compat))
                        yield return compat;
                }
            }
        }

        // Yields a Steam install root plus any extra library folders declared in libraryfolders.vdf.
        private static IEnumerable<string> EnumerateSteamLibraryRoots(string steamRoot)
        {
            yield return steamRoot;

            string vdf = Path.Combine(steamRoot, "steamapps", "libraryfolders.vdf");
            if (!File.Exists(vdf))
                yield break;

            string content = null;
            try { content = File.ReadAllText(vdf); }
            catch { /* unreadable library index – ignore */ }
            if (content == null)
                yield break;

            // Match "path"  "/some/library/folder" entries (Steam escapes backslashes).
            foreach (Match m in Regex.Matches(content, "\"path\"\\s*\"([^\"]+)\""))
            {
                var path = m.Groups[1].Value.Replace("\\\\", "/");
                if (!string.IsNullOrEmpty(path))
                    yield return path;
            }
        }
    }
}
