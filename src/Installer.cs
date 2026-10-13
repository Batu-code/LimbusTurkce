using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;

namespace LimbusTurkce
{
    // Yamayi oyunun Lang klasorune kurar ve kaldirir
    //
    // Oyun ozel dili ancak eksiksiz bir set ve kendi fontuyla birlikte kullaniyor
    // O yuzden kurulum uc katmanli
    //   1 oyunun kendi Ingilizce metni
    //     dagitmiyoruz ve oyuncunun kendi oyunundan aliniyor
    //   2 bizim ceviriler ustune
    //   3 font
    //     pakette yoksa Windows'un Segoe UI'i
    //
    // Oyun Lang icinde buldugu paketi kendisi secebiliyor
    // config.json silinse bile Turkce acildi (2026-10-13 Batu denedi)
    // O yuzden Ingilizce secilince paket Lang disindaki kendi klasorumuze cekilir
    // Kurulum da orada hazirlanir ki yarim kalan bir klasor oyuna dil diye gorunmesin
    static class Installer
    {
        public const string PackName = "Turkish";

        public static string Target(string game) => Path.Combine(Game.LangDir(game), PackName);
        static string ConfigPath(string game) => Path.Combine(Game.LangDir(game), "config.json");

        // Oyunun okumadigi kendi calisma klasorumuz
        static string Work(string game) => Path.Combine(Game.DataDir(game), "LimbusTurkce");
        public static string Parked(string game) => Path.Combine(Work(game), PackName);

        // Kapali paket de kurulu sayilir
        public static bool IsInstalled(string game) => Directory.Exists(Target(game)) || Directory.Exists(Parked(game));

        // Oyun Turkce mi acilacak
        // Paket Lang icindeyse ve ayar baska bir paketi gostermiyorsa evet
        // Ayar yoksa ya da bossa oyun Lang icindeki paketi kendisi seciyor
        public static bool IsTurkishActive(string game)
        {
            if (!Directory.Exists(Target(game))) return false;
            var selected = SelectedLanguage(game);
            return string.IsNullOrEmpty(selected) || selected == PackName;
        }

        public static void Install(string game, string packZip, bool setLanguage, IProgress<string> status)
        {
            if (Game.IsRunning())
                throw new InvalidOperationException("Oyun açık. Önce Limbus Company'yi kapatın.");

            var english = Game.EnglishDir(game);
            if (!Directory.Exists(english))
                throw new InvalidOperationException("Oyunun İngilizce metni bulunamadı. Oyun Steam'den doğrulanmalı olabilir.\n" + english);

            Directory.CreateDirectory(Game.LangDir(game));
            // Ingilizce seciliyken guncellenirse oyuncunun secimi korunur
            var keepEnglish = Directory.Exists(Parked(game)) && !Directory.Exists(Target(game));

            // Yeni kurulum once yan klasorde hazirlanir
            // Eskisi en son degistirilir
            // Yarida kesilirse oyuncunun calisan kurulumu bozulmaz
            var work = Work(game);
            Directory.CreateDirectory(work);
            var staging = Path.Combine(work, PackName + ".yeni");
            DeleteFolder(staging);
            Directory.CreateDirectory(staging);
            try
            {
                status.Report("Oyunun İngilizce metni kopyalanıyor...");
                CopyEnglish(english, staging);

                status.Report("Türkçe çeviriler yerleştiriliyor...");
                Extract(packZip, staging);

                status.Report("Font ekleniyor...");
                AddFallbackFonts(staging);
            }
            catch
            {
                try { DeleteFolder(staging); } catch { }
                throw;
            }

            status.Report("Kurulum tamamlanıyor...");
            var target = Target(game);
            var old = Path.Combine(work, PackName + ".eski");
            DeleteFolder(old);
            if (Directory.Exists(target)) Directory.Move(target, old);
            Directory.Move(staging, target);
            DeleteFolder(old);
            DeleteFolder(Parked(game));

            if (keepEnglish) Park(game);
            else if (setLanguage) SelectLanguage(game);
            RemoveWork(game);
        }

        public static void Uninstall(string game)
        {
            if (Game.IsRunning())
                throw new InvalidOperationException("Oyun açık. Önce Limbus Company'yi kapatın.");

            DeleteFolder(Target(game));
            DeleteFolder(Parked(game));
            RemoveWork(game);
            ForgetLanguage(game);
        }

        // Yamayi kaldirmadan oyunun dilini degistirir
        // Ingilizcede paket Lang disina cekilir ve tek tikla geri gelir
        // Ayni disk icinde tasima oldugu icin aninda olur
        public static void SetLanguage(string game, bool turkish)
        {
            if (Game.IsRunning())
                throw new InvalidOperationException("Oyun açıkken dil değiştirilemez. Önce Limbus Company'yi kapatın.");
            if (turkish)
            {
                if (!IsInstalled(game)) throw new InvalidOperationException("Önce yamayı kurun.");
                Unpark(game);
                SelectLanguage(game);
            }
            else Park(game);
        }

        // Paket oyunun goremeyecegi yere cekilir
        // Turkceyi gosteren ayar da silinir
        // Ayar yokken Lang icinde paket olmadigi icin oyun Ingilizce baslar
        static void Park(string game)
        {
            var target = Target(game);
            if (Directory.Exists(target))
            {
                var parked = Parked(game);
                DeleteFolder(parked);
                Directory.CreateDirectory(Work(game));
                Directory.Move(target, parked);
            }
            ForgetLanguage(game);
        }

        static void Unpark(string game)
        {
            var parked = Parked(game);
            if (!Directory.Exists(parked)) return;
            // Iki yerde de varsa oyunun gordugu kalir
            if (Directory.Exists(Target(game))) DeleteFolder(parked);
            else Directory.Move(parked, Target(game));
            RemoveWork(game);
        }

        // Ici bosalan calisma klasoru oyunun yaninda kalmasin
        static void RemoveWork(string game)
        {
            var work = Work(game);
            if (Directory.Exists(work) && Directory.GetFileSystemEntries(work).Length == 0) Directory.Delete(work);
        }

        // Turkceyi gosteren ayar silinir
        // Paket Lang disindayken ayar Turkceyi gostermesin
        static void ForgetLanguage(string game)
        {
            var config = ConfigPath(game);
            if (File.Exists(config) && SelectedLanguage(game) == PackName) File.Delete(config);
        }

        public static string SelectedLanguage(string game)
        {
            var config = ConfigPath(game);
            if (!File.Exists(config)) return null;
            var m = Regex.Match(File.ReadAllText(config), "\"lang\"\\s*:\\s*\"([^\"]*)\"");
            return m.Success ? m.Groups[1].Value : null;
        }

        // Oyun bu dosyayi dil secilince kendisi yaziyor
        // Ayni seyi biz yapinca oyuncu giris ekraninda secmek zorunda kalmaz
        // Diger alanlara dokunmamak icin sadece lang degeri degistirilir
        static void SelectLanguage(string game)
        {
            var config = ConfigPath(game);
            var pattern = new Regex("(\"lang\"\\s*:\\s*)\"[^\"]*\"");
            if (File.Exists(config))
            {
                var text = File.ReadAllText(config);
                if (pattern.IsMatch(text))
                {
                    File.WriteAllText(config, pattern.Replace(text, "$1\"" + PackName + "\"", 1), new UTF8Encoding(false));
                    return;
                }
            }
            // Oyunun kendi yazdigi hali
            // 2026-10-06'da bu makinede goruldu
            var fresh = "{\n    \"lang\": \"" + PackName + "\",\n    \"titleFont\": \"\",\n    \"contextFont\": \"\",\n    \"samplingPointSize\": 78,\n    \"padding\": 5\n}";
            File.WriteAllText(config, fresh, new UTF8Encoding(false));
        }

        // Oyunda dosyalar EN_ onekli
        // Dil paketinde oneksiz bekleniyor
        static void CopyEnglish(string from, string to)
        {
            foreach (var file in Directory.GetFiles(from, "*", SearchOption.AllDirectories))
            {
                var relative = file.Substring(from.Length).TrimStart('\\');
                var folder = Path.GetDirectoryName(relative);
                var name = Path.GetFileName(relative);
                if (name.StartsWith("EN_")) name = name.Substring(3);
                var destination = Path.Combine(to, folder, name);
                Directory.CreateDirectory(Path.GetDirectoryName(destination));
                File.Copy(file, destination, true);
            }
        }

        static void Extract(string zip, string to)
        {
            var root = Path.GetFullPath(to) + Path.DirectorySeparatorChar;
            using (var archive = ZipFile.OpenRead(zip))
            {
                foreach (var entry in archive.Entries)
                {
                    if (string.IsNullOrEmpty(entry.Name)) continue;
                    var destination = Path.GetFullPath(Path.Combine(to, entry.FullName.Replace('/', '\\')));
                    // Zip icinde ../ ile klasor disina yazmaya calisan girdi olmasin
                    if (!destination.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                        throw new InvalidDataException("Paket bozuk: " + entry.FullName);
                    Directory.CreateDirectory(Path.GetDirectoryName(destination));
                    entry.ExtractToFile(destination, true);
                }
            }
        }

        // Yayin fontu secilince pakete girecek
        // O zamana kadar Windows'un fontu kullaniliyor
        // Segoe UI'in lisansi dagitima izin vermiyor
        // Bu yuzden zip'e degil oyuncunun kendi Windows'undan aliniyor
        static void AddFallbackFonts(string pack)
        {
            var fonts = Environment.GetFolderPath(Environment.SpecialFolder.Fonts);
            AddFont(pack, "Context", Path.Combine(fonts, "segoeui.ttf"));
            AddFont(pack, "Title", Path.Combine(fonts, "segoeuib.ttf"));
        }

        static void AddFont(string pack, string slot, string windowsFont)
        {
            var folder = Path.Combine(pack, "Font", slot);
            if (Directory.Exists(folder) && Directory.GetFiles(folder).Length > 0) return;
            if (!File.Exists(windowsFont))
                throw new InvalidOperationException("Windows fontu bulunamadı: " + windowsFont);
            Directory.CreateDirectory(folder);
            File.Copy(windowsFont, Path.Combine(folder, Path.GetFileName(windowsFont)), true);
        }

        static void DeleteFolder(string dir)
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
        }
    }
}
