using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;

namespace LimbusTurkce
{
    // Uygulama kendi exe'sini yenisiyle degistirir
    // Windows calisan exe'yi silmeye izin vermiyor ama yeniden adlandirmaya izin veriyor
    // Eski exe .eski olur ve yenisi yerine gecer
    // Bir sonraki acilista .eski silinir
    static class SelfUpdate
    {
        public static Version Current
        {
            get
            {
                var v = Assembly.GetExecutingAssembly().GetName().Version;
                return new Version(v.Major, v.Minor, Math.Max(v.Build, 0));
            }
        }

        static string ExePath => Assembly.GetExecutingAssembly().Location;

        // Guncellemeden hemen sonra eski surec exe'yi birkac yuz milisaniye daha tutuyor
        public static void CleanUp(bool justUpdated)
        {
            var old = ExePath + ".eski";
            for (var attempt = 0; attempt < (justUpdated ? 20 : 1) && File.Exists(old); attempt++)
            {
                try { File.Delete(old); }
                catch { System.Threading.Thread.Sleep(250); }
            }
        }

        public static async Task ApplyAsync(string url, IProgress<int> progress)
        {
            var exe = ExePath;
            var fresh = exe + ".yeni";
            await Releases.DownloadAsync(url, fresh, progress);

            var old = exe + ".eski";
            if (File.Exists(old)) File.Delete(old);
            File.Move(exe, old);
            try
            {
                File.Move(fresh, exe);
            }
            catch
            {
                File.Move(old, exe);
                throw;
            }
            Process.Start(exe, "--guncellendi");
        }
    }
}
