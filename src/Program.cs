using System;
using System.Net;
using System.Threading;
using System.Windows.Forms;

namespace LimbusTurkce
{
    static class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            var updated = Array.IndexOf(args, "--guncellendi") >= 0;

            // Ayni anda iki kopya ayni klasore kurmaya calismasin
            // Kendini guncelleyen kopya eskisi kapanana kadar biraz bekler
            var mutex = new Mutex(false, "LimbusTurkce.tek");
            bool owned;
            try { owned = mutex.WaitOne(updated ? TimeSpan.FromSeconds(10) : TimeSpan.Zero); }
            catch (AbandonedMutexException) { owned = true; }
            if (!owned)
            {
                if (!updated) MessageBox.Show("Uygulama zaten açık.", "Limbus Türkçe", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            try
            {
                SelfUpdate.CleanUp(updated);
                // .NET Framework eski Windows'larda TLS 1.2'yi kendiliginden acmayabiliyor
                // GitHub baska surum kabul etmiyor
                ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;

                // Beklenmeyen bir hata oyuncuya .NET'in teknik kutusunu gostermesin
                // Ayrintisi dosyaya yazilir ve pencerede kirmizi satirla soylenir
                Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
                Application.ThreadException += (s, e) => Crash.Report(e.Exception);

                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new MainForm(updated));
            }
            finally
            {
                mutex.ReleaseMutex();
            }
        }
    }
}
