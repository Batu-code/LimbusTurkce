using System;
using System.IO;

namespace LimbusTurkce
{
    // Beklenmeyen hatalarin kaydi
    // Ayni hata her karede tekrar ederse dosya sismesin diye sadece ilk yirmisi yazilir
    static class Crash
    {
        static int count;

        public static event Action<string> Reported;

        public static string FilePath => Path.Combine(Settings.Folder, "hata.txt");

        public static void Report(Exception ex)
        {
            if (++count > 20) return;
            try
            {
                Directory.CreateDirectory(Settings.Folder);
                File.AppendAllText(FilePath, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss}  v{SelfUpdate.Current.ToString(3)}\r\n{ex}\r\n\r\n");
            }
            catch { }
            Reported?.Invoke(FilePath);
        }
    }
}
