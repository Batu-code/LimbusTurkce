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
        [DataMember] public bool SetGameLanguage { get; set; } = true;

        public static string Folder => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "LimbusTurkce");
        public static string CacheFolder => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LimbusTurkce", "paketler");
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

        // DataContract yapicilari calistirmaz
        // Varsayilan buradan gelir
        [OnDeserializing]
        void OnDeserializing(StreamingContext context) => SetGameLanguage = true;
    }
}
