using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace LimbusTurkce
{
    // Oyun klasorunu bulur ve oyunla ilgili yollari tek yerde tutar
    static class Game
    {
        public const string SteamAppId = "1973530";
        const string FolderName = "Limbus Company";
        const string ProcessName = "LimbusCompany";

        public static bool IsGameFolder(string dir) =>
            !string.IsNullOrEmpty(dir) && Directory.Exists(Path.Combine(dir, "LimbusCompany_Data"));

        // Steam'in kendi kaydindan kutuphane klasorlerini okur
        // Oyun baska bir diskte de olabilir
        public static string Find()
        {
            foreach (var library in SteamLibraries())
            {
                var dir = Path.Combine(library, "steamapps", "common", FolderName);
                if (IsGameFolder(dir)) return ActualCase(dir);
            }
            return null;
        }

        // Steam kayitta yolu kucuk harfle tutuyor
        // Oyuncuya diskteki yazilisi gosterilsin
        public static string ActualCase(string path)
        {
            try
            {
                var dir = new DirectoryInfo(path);
                if (dir.Parent == null) return dir.FullName.ToUpperInvariant();
                var parent = ActualCase(dir.Parent.FullName);
                var match = new DirectoryInfo(parent).GetDirectories(dir.Name).FirstOrDefault();
                return Path.Combine(parent, match?.Name ?? dir.Name);
            }
            catch
            {
                return path;
            }
        }

        static IEnumerable<string> SteamLibraries()
        {
            var steam = Registry.GetValue(@"HKEY_CURRENT_USER\Software\Valve\Steam", "SteamPath", null) as string;
            if (string.IsNullOrEmpty(steam)) yield break;
            steam = steam.Replace('/', '\\');
            yield return steam;

            var vdf = Path.Combine(steam, "steamapps", "libraryfolders.vdf");
            if (!File.Exists(vdf)) yield break;
            foreach (Match m in Regex.Matches(File.ReadAllText(vdf), "\"path\"\\s+\"([^\"]+)\""))
                yield return m.Groups[1].Value.Replace(@"\\", @"\");
        }

        public static bool IsRunning() => Process.GetProcessesByName(ProcessName).Length > 0;

        public static void Launch() => Process.Start("steam://rungameid/" + SteamAppId);

        public static string DataDir(string game) => Path.Combine(game, "LimbusCompany_Data");
        public static string LangDir(string game) => Path.Combine(DataDir(game), "Lang");
        public static string EnglishDir(string game) => Path.Combine(DataDir(game), "Assets", "Resources_moved", "Localize", "en");

        // Oyun guncellenince Ingilizce metin degisir ve kurulu tam set eskir
        // Dosya sayisi toplam boy ve en yeni degisiklik zamani bunu yakalamaya yetiyor
        public static string EnglishFingerprint(string game)
        {
            var dir = EnglishDir(game);
            if (!Directory.Exists(dir)) return null;
            var files = new DirectoryInfo(dir).GetFiles("*", SearchOption.AllDirectories);
            long size = files.Sum(f => f.Length);
            long newest = files.Length == 0 ? 0 : files.Max(f => f.LastWriteTimeUtc.Ticks);
            return $"{files.Length}:{size}:{newest}";
        }
    }
}
