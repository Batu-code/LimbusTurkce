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
    static class Installer
    {
        public const string PackName = "Turkish";

        public static string Target(string game) => Path.Combine(Game.LangDir(game), PackName);
        static string ConfigPath(string game) => Path.Combine(Game.LangDir(game), "config.json");

        public static bool IsInstalled(string game) => Directory.Exists(Target(game));

        public static void Install(string game, string packZip, bool setLanguage, IProgress<string> status)
        {
            if (Game.IsRunning())
                throw new InvalidOperationException("Oyun açık. Önce Limbus Company'yi kapatın.");

            var english = Game.EnglishDir(game);
            if (!Directory.Exists(english))
                throw new InvalidOperationException("Oyunun İngilizce metni bulunamadı. Oyun Steam'den doğrulanmalı olabilir.\n" + english);

            var lang = Game.LangDir(game);
            Directory.CreateDirectory(lang);

            // Yeni kurulum once yan klasorde hazirlanir
            // Eskisi en son degistirilir
            // Yarida kesilirse oyuncunun calisan kurulumu bozulmaz
            var staging = Path.Combine(lang, PackName + ".yeni");
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
            var old = Path.Combine(lang, PackName + ".eski");
            DeleteFolder(old);
            if (Directory.Exists(target)) Directory.Move(target, old);
            Directory.Move(staging, target);
            DeleteFolder(old);

            if (setLanguage) SelectLanguage(game);
        }

        public static void Uninstall(string game)
        {
            if (Game.IsRunning())
                throw new InvalidOperationException("Oyun açık. Önce Limbus Company'yi kapatın.");

            DeleteFolder(Target(game));
            // Oyun secili dili bulamazsa ne yapacagini bilmiyoruz
            // Ingilizceye donmesi icin ayari siliyoruz
            ForgetLanguage(game);
        }

        // Yamayi kaldirmadan oyunun dilini degistirir
        // Dosyalar yerinde kalir ve tek tikla geri acilir
        public static void SetLanguage(string game, bool turkish)
        {
            if (Game.IsRunning())
                throw new InvalidOperationException("Oyun açıkken dil değiştirilemez. Önce Limbus Company'yi kapatın.");
            if (turkish)
            {
                if (!IsInstalled(game)) throw new InvalidOperationException("Önce yamayı kurun.");
                SelectLanguage(game);
            }
            else ForgetLanguage(game);
        }

        // Kaldirmada da kullanilan yol
        // Ayar silinince oyun Ingilizce basliyor
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
