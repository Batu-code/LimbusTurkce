using System;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;

namespace LimbusTurkce
{
    // Oyuncunun bilgisayarinda hatirlanan seyler
    // Oyun klasorune yazmiyoruz
    // Oyun sadece dil dosyalarini bekliyor
    [DataContract]
    class Settings
    {
        [DataMember] public string GamePath { get; set; }
        [DataMember] public string InstalledVersion { get; set; }
        [DataMember] public string InstalledFingerprint { get; set; }
        [DataMember] public DateTime? InstalledAt { get; set; }
        // Tam acilis sahnesi ilk acilista ve uygulama guncellenince bir kez oynar
        [DataMember] public string IntroSeenVersion { get; set; }

        // Test icin ayarlar ve paketler baska bir klasorde tutulabilir
        // Gercek kurulumun ayarlarina dokunmadan denemek icin
        static string TestFolder => Environment.GetEnvironmentVariable("LIMBUSTURKCE_KLASOR");

        public static string Folder => !string.IsNullOrEmpty(TestFolder) ? TestFolder
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "LimbusTurkce");

        public static string CacheFolder => !string.IsNullOrEmpty(TestFolder) ? Path.Combine(TestFolder, "paketler")
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LimbusTurkce", "paketler");
        static string FilePath => Path.Combine(Folder, "ayarlar.json");

        public static Settings Load()
        {
            try
            {
                if (!File.Exists(FilePath)) return new Settings();
                using (var stream = File.OpenRead(FilePath))
                    return (Settings)new DataContractJsonSerializer(typeof(Settings)).ReadObject(stream) ?? new Settings();
            }
            catch
            {
                // Bozuk ayar dosyasi yuzunden uygulama acilmamazlik etmesin
                return new Settings();
            }
        }

        public void Save()
        {
            Directory.CreateDirectory(Folder);
            using (var memory = new MemoryStream())
            {
                new DataContractJsonSerializer(typeof(Settings)).WriteObject(memory, this);
                File.WriteAllText(FilePath, Encoding.UTF8.GetString(memory.ToArray()), new UTF8Encoding(false));
            }
        }
    }
}
