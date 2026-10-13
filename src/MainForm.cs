using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace LimbusTurkce
{
    // Tek pencere
    // Solda oyunun klasorunde surum notlari
    // Sagda durum dil secimi ve oyunun kirmizi bileti
    // Oyuncu ne yapacagini dusunmesin diye bilet her an tek dogru isi gosterir
    class MainForm : CanvasForm
    {
        const string GithubUrl = "https://github.com/" + Releases.Repo;
        // Sunucu acilinca davet baglantisi buraya
        // Bos kalirsa baglanti gizlenir
        static readonly string DiscordUrl = "";

        static readonly CultureInfo Turkish = new CultureInfo("tr-TR");

        // Yerlesim 96 dpi pikseli
        static readonly RectangleF FolderArea = new RectangleF(18, 130, 466, 412);
        static readonly RectangleF PageArea = new RectangleF(36, 186, 410, 318);
        const float Column = 506f;
        const float ColumnWidth = 372f;

        enum Step { AppUpdate, PickFolder, Install, Update, Repair, Play, Wait }

        readonly Settings settings = Settings.Load();
        readonly bool justUpdated;
        string game;
        List<Release> releases;
        string releaseError;
        Step action = Step.Wait;
        bool busy, keepMessage;
        string busyTitle;

        readonly Wheel wheel;
        readonly Status status;
        readonly ArtButton turkishOn, turkishOff;
        readonly Ticket ticket;
        readonly Label message, folderTitle;
        readonly ChromeButton gear;
        readonly Page notesPage, settingsPage;
        readonly NotesView notes;
        readonly Label pathValue, patchValue;
        readonly ArtButton reinstall, uninstall;
        readonly Dialog dialog;

        public MainForm(bool justUpdated)
        {
            this.justUpdated = justUpdated;
            Text = "Limbus Company Türkçe Yama";
            Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            BackColor = Palette.Back;
            // Acilista yumusakca belirir
            Opacity = 0;

            // Baslik cubugu tuslari
            gear = new ChromeButton { Shape = ChromeButton.Glyph.Gear, Bounds = new RectangleF(782, 10, 30, 26), Tip = "Ayarlar" };
            gear.Click += () => ShowSettings(!settingsPage.IsOpen);
            Add(gear);
            var minimize = new ChromeButton { Shape = ChromeButton.Glyph.Minimize, Bounds = new RectangleF(814, 10, 30, 26), Tip = "Küçült" };
            minimize.Click += () => WindowState = FormWindowState.Minimized;
            Add(minimize);
            var close = new ChromeButton { Shape = ChromeButton.Glyph.Close, Bounds = new RectangleF(846, 10, 30, 26), Tip = "Kapat" };
            close.Click += Close;
            Add(close);

            // Otobus tekerlegi is surerken doner
            wheel = new Wheel { Bounds = new RectangleF(26, 22, 54, 54) };
            Add(wheel);

            // Klasorun sekmesindeki baslik sayfayla degisir
            folderTitle = new Label
            {
                Value = "NELER DEĞİŞTİ",
                Bounds = new RectangleF(FolderArea.Left + 12, FolderArea.Top + 12, 120, 16),
                Family = "Bahnschrift SemiBold",
                Size = 11.5f,
                Tracking = 1.4f,
                Color = Palette.Gold,
            };
            Add(folderTitle);

            notesPage = new Page { Bounds = PageArea };
            notes = notesPage.Add(new NotesView { Bounds = PageArea });
            Add(notesPage);

            settingsPage = new Page { Bounds = PageArea };
            Add(settingsPage);
            BuildSettings(settingsPage, out pathValue, out patchValue, out reinstall, out uninstall);
            settingsPage.Snap(false);

            status = new Status { Bounds = new RectangleF(Column - 6, 146, ColumnWidth + 6, 82) };
            Add(status);

            // Yamayi kaldirmadan dil degistirme
            // Secili olan hep yanik durur
            turkishOn = new ArtButton { Caption = "TÜRKÇE", Diamond = true, Bounds = new RectangleF(Column, 264, 182, 36), Name = "turkce" };
            turkishOn.Click += () => ChangeLanguage(true);
            Add(turkishOn);
            turkishOff = new ArtButton { Caption = "İNGİLİZCE", Diamond = true, Bounds = new RectangleF(Column + 190, 264, 182, 36), Name = "ingilizce" };
            turkishOff.Click += () => ChangeLanguage(false);
            Add(turkishOff);

            ticket = new Ticket { Bounds = new RectangleF(Column, 324, ColumnWidth, 160), Name = "bilet" };
            ticket.Click += async () => await RunPrimary();
            Add(ticket);

            message = new Label
            {
                Bounds = new RectangleF(Column, 496, ColumnWidth, 48),
                Family = "Bahnschrift",
                Size = 12.5f,
                Color = Palette.Muted,
                Align = StringAlignment.Center,
                Wrap = true,
            };
            Add(message);

            // Soru kutusu her seyin ustunde
            dialog = new Dialog { Bounds = new RectangleF(0, 0, LogicalSize.Width, LogicalSize.Height) };
            Add(dialog);

            // Oyuna donunce koparilan kocan yerine gelir
            Activated += (s, e) => ticket.Mend();

            // Beklenmeyen bir hata olursa oyuncu kirmizi satirda gorur
            Crash.Reported += file =>
            {
                // Uzun kullanici yolu yerine Gezgin'e yazilabilen kisa hali
                var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                if (file.StartsWith(appData, StringComparison.OrdinalIgnoreCase)) file = "%APPDATA%" + file.Substring(appData.Length);
                SetMessage("Beklenmeyen bir hata oldu. Ayrıntısı şu dosyada: " + file, Palette.Red);
                keepMessage = true;
            };

            // Acilis sahnesi her seyin ustunde durur
            // Bitince kendini kaldirir
            var fullIntro = settings.IntroSeenVersion != SelfUpdate.Current.ToString(3);
            var intro = new Intro(fullIntro) { Dock = DockStyle.Fill };
            Controls.Add(intro);
            intro.BringToFront();
            intro.Click += (s, e) => intro.Skip();
            KeyDown += (s, e) => { if (!intro.IsDisposed) intro.Skip(); };
            intro.Finished += (s, e) =>
            {
                if (!fullIntro) return;
                settings.IntroSeenVersion = SelfUpdate.Current.ToString(3);
                SaveSettings();
            };

            Shown += async (s, e) =>
            {
                FadeIn();
                intro.Start();
                await Startup();
            };
        }

        void BuildSettings(Page page, out Label path, out Label patch, out ArtButton again, out ArtButton remove)
        {
            var x = PageArea.Left;
            var y = PageArea.Top + 4;
            page.Add(Heading("OYUN KLASÖRÜ", x, y));
            path = page.Add(new Label { Bounds = new RectangleF(x, y + 20, PageArea.Width - 10, 18), PathEllipsis = true, Size = 13f });
            var change = page.Add(SmallButton("DEĞİŞTİR", x, y + 46, "klasor-degistir"));
            change.Click += () => { if (!busy) PickFolder(); };
            var open = page.Add(SmallButton("KLASÖRÜ AÇ", x + 146, y + 46, "klasor-ac"));
            open.Click += () => { if (game != null) Open(game); };

            y += 104;
            page.Add(Heading("YAMA", x, y));
            patch = page.Add(new Label { Bounds = new RectangleF(x, y + 20, PageArea.Width - 10, 18), Size = 13f });
            again = page.Add(SmallButton("YENİDEN KUR", x, y + 46, "yeniden-kur"));
            again.Click += async () => await Reinstall();
            remove = page.Add(SmallButton("KALDIR", x + 146, y + 46, "kaldir"));
            remove.Click += Uninstall;

            y += 104;
            page.Add(Heading("UYGULAMA", x, y));
            page.Add(new Label { Value = "Sürüm v" + SelfUpdate.Current.ToString(3), Bounds = new RectangleF(x, y + 20, 160, 18), Size = 13f });
            var links = new List<(string, string)> { ("GitHub", GithubUrl) };
            if (DiscordUrl != "") links.Add(("Discord", DiscordUrl));
            var lx = x;
            foreach (var (name, url) in links)
            {
                var link = page.Add(new Link { Value = name, Bounds = new RectangleF(lx, y + 44, 70, 18), Size = 13f, Color = Palette.Gold, Name = name.ToLowerInvariant() });
                link.Click += () => Open(url);
                lx += 80;
            }
            page.Add(new Label
            {
                Value = "Hayran yapımıdır. Limbus Company ve bütün içeriği Project Moon'a aittir.",
                Bounds = new RectangleF(x, y + 76, PageArea.Width - 10, 34),
                Size = 11.5f,
                Color = Palette.Dim,
                Wrap = true,
            });
        }

        static Label Heading(string text, float x, float y) =>
            new Label { Value = text, Bounds = new RectangleF(x, y, 200, 14), Family = "Bahnschrift SemiBold", Size = 10.5f, Tracking = 2.2f, Color = Palette.Muted };

        static ArtButton SmallButton(string text, float x, float y, string name) =>
            new ArtButton { Caption = text, Bounds = new RectangleF(x, y, 138, 32), Size = 11f, Name = name };

        void ShowSettings(bool open)
        {
            if (open) RefreshSettings();
            gear.On = open;
            folderTitle.Set(open ? "AYARLAR" : "NELER DEĞİŞTİ");
            notesPage.Open(!open, open ? -1 : 1);
            settingsPage.Open(open, open ? 1 : -1);
        }

        void FadeIn()
        {
            Animator.Run(dt =>
            {
                if (IsDisposed) return false;
                Opacity = Math.Min(1.0, Opacity + dt / 0.3);
                return Opacity < 1.0;
            });
        }

        async Task Startup()
        {
            game = Game.IsGameFolder(settings.GamePath) ? Game.ActualCase(settings.GamePath) : Game.Find();
            if (game != settings.GamePath && game != null) { settings.GamePath = game; SaveSettings(); }
            if (justUpdated)
            {
                SetMessage($"Uygulama v{SelfUpdate.Current.ToString(3)} sürümüne güncellendi.");
                keepMessage = true;
            }
            RefreshState();

            try { releases = await Releases.FetchAsync(); }
            catch (Exception ex) { releaseError = Explain(ex); }
            RenderNotes();
            RefreshState();
        }

        Version InstalledVersion =>
            game != null && Installer.IsInstalled(game) && Releases.TryParseVersion(settings.InstalledVersion, out var v) ? v : null;

        Release Latest => releases?.FirstOrDefault();

        string InstalledText(Version version) =>
            "v" + version.ToString(3) + (settings.InstalledAt is DateTime at ? " · " + at.ToLocalTime().ToString("d MMMM yyyy", Turkish) : "");

        static string ReleaseText(Release release) =>
            release.Label + " · " + release.Date.ToLocalTime().ToString("d MMMM yyyy", Turkish);

        void RefreshState()
        {
            var installed = game != null && Installer.IsInstalled(game);
            var installedVersion = InstalledVersion;
            var gameChanged = installed && settings.InstalledFingerprint != null
                && settings.InstalledFingerprint != Game.EnglishFingerprint(game);
            var turkish = installed && Installer.IsTurkishActive(game);
            var installedText = installedVersion != null ? InstalledText(installedVersion) : "Eski kurulum (zip ile)";

            var appUpdate = AppUpdate;
            if (appUpdate != null)
                Set(Step.AppUpdate, "GÜNCELLE", "Uygulama v" + appUpdate.AppVersion.ToString(3),
                    "UYGULAMA GÜNCELLEMESİ", Palette.Orange,
                    "Uygulamanın yeni sürümü çıktı: v" + appUpdate.AppVersion.ToString(3),
                    $"Sizdeki v{SelfUpdate.Current.ToString(3)}. Güncelleyince kendini yeniden açar.");
            else if (game == null)
                Set(Step.PickFolder, "OYUNU BUL", "Klasörü seç",
                    "OYUN BULUNAMADI", Palette.Red,
                    "Limbus Company bu bilgisayarda bulunamadı.", "Oyunun klasörünü elle seçin.");
            else if (!installed && Latest == null)
                Set(Step.Wait, "KUR", releaseError != null ? "Bağlantı yok" : "Bekleniyor",
                    releaseError != null ? "BAĞLANILAMADI" : "BAĞLANIYOR", releaseError != null ? Palette.Red : Palette.Muted,
                    releaseError != null ? "Sürümler alınamadı." : "Sürümler yükleniyor...", releaseError ?? "");
            else if (!installed)
                Set(Step.Install, "KUR", Latest.Label + " · " + FormatSize(Latest.PackSize),
                    "KURULU DEĞİL", Palette.Ink,
                    "Türkçe yama bu oyunda kurulu değil.", "Son sürüm " + ReleaseText(Latest));
            else if (Latest != null && (installedVersion == null || Latest.Version > installedVersion))
                Set(Step.Update, "GÜNCELLE", "Yeni sürüm " + Latest.Label,
                    "GÜNCELLEME VAR", Palette.Orange,
                    "Yeni sürüm " + ReleaseText(Latest), "Kurulu " + installedText + ". Değişenler solda.");
            else if (gameChanged)
                Set(Step.Repair, "YENİLE", "Oyun güncellendi",
                    "OYUN GÜNCELLENDİ", Palette.Red,
                    "Oyunun metni değişmiş.", "Yamanın yeni metinle yeniden kurulması gerekiyor.");
            else
                Set(Step.Play, "OYNA", !turkish ? "İngilizce açılır" : installedVersion != null ? "Türkçe · v" + installedVersion.ToString(3) : "Türkçe",
                    "YAMA GÜNCEL", Palette.Gold,
                    "Kurulu " + installedText,
                    Latest == null && releaseError != null ? "Güncelleme kontrol edilemedi: " + releaseError
                        : turkish ? "Oyun Türkçe açılacak." : "Oyun dili İngilizce. Türkçe için aşağıdan seçin.");

            if (busy) status.Set(busyTitle ?? "ÇALIŞIYOR", Palette.Orange, status.Line1, status.Line2);

            ticket.Enabled = !busy && action != Step.Wait;
            // Oyuncudan bir sey bekleniyorsa bilet nefes alir
            ticket.Pulse = !busy && (action == Step.AppUpdate || action == Step.Install || action == Step.Update || action == Step.Repair);
            ticket.Shines = action == Step.Play;

            turkishOn.Enabled = turkishOff.Enabled = installed && !busy;
            turkishOn.Selected = turkish;
            turkishOff.Selected = installed && !turkish;
            reinstall.Enabled = uninstall.Enabled = installed && !busy;
            gear.Enabled = true;
            if (settingsPage.IsOpen) RefreshSettings();
        }

        void Set(Step next, string caption, string detail, string title, Color color, string line1, string line2)
        {
            action = next;
            ticket.Set(caption, detail);
            status.Set(title, color, line1, line2);
            // Calisan bir islemin mesajini ezme
            if (!busy && !keepMessage) SetMessage("");
        }

        void RefreshSettings()
        {
            pathValue.Set(game ?? "Bulunamadı");
            var installedVersion = InstalledVersion;
            var installed = game != null && Installer.IsInstalled(game);
            patchValue.Set(!installed ? "Kurulu değil" : installedVersion == null ? "Eski kurulum (zip ile)" : "Kurulu " + InstalledText(installedVersion));
        }

        static string FormatSize(long bytes) =>
            bytes <= 0 ? "" : bytes < 1024 * 1024 ? (bytes / 1024 + 1) + " KB" : (bytes / 1024f / 1024f).ToString("0.0", Turkish) + " MB";

        async Task RunPrimary()
        {
            switch (action)
            {
                case Step.AppUpdate: await UpdateApp(AppUpdate); break;
                case Step.PickFolder: PickFolder(); break;
                case Step.Install:
                case Step.Update: await Install(Latest); break;
                case Step.Repair: await Reinstall(); break;
                case Step.Play: Play(); break;
            }
        }

        // Oyun guncellenince ya da oyuncu isteyince kurulu surum tekrar kurulur
        // Paket onbellekte varsa internet gerekmez
        async Task Reinstall()
        {
            if (busy) return;
            var installed = InstalledVersion;
            var release = releases?.FirstOrDefault(r => r.Version == installed);
            if (release == null && installed != null && File.Exists(CachedPack(installed)))
            {
                await Work("KURULUYOR", () => InstallFrom(CachedPack(installed), installed));
                return;
            }
            await Install(release ?? Latest);
        }

        async Task Install(Release release)
        {
            if (busy) return;
            if (release == null) { SetMessage("Sürümler alınamadı, internet bağlantısını kontrol edin."); return; }
            await Work("KURULUYOR", async () =>
            {
                var zip = CachedPack(release.Version);
                if (!File.Exists(zip) || (release.PackSha256 != null && Releases.Sha256(zip) != release.PackSha256))
                {
                    busyTitle = "İNDİRİLİYOR";
                    RefreshState();
                    SetMessage($"{release.Label} indiriliyor...");
                    ticket.Progress = 0f;
                    await Releases.DownloadAsync(release.PackUrl, zip, new Progress<int>(p => ticket.Progress = p / 100f));
                    if (release.PackSha256 != null && Releases.Sha256(zip) != release.PackSha256)
                    {
                        File.Delete(zip);
                        throw new InvalidOperationException("İndirilen dosya bozuk çıktı. Tekrar deneyin.");
                    }
                    busyTitle = "KURULUYOR";
                    RefreshState();
                }
                await InstallFrom(zip, release.Version);
            });
        }

        async Task InstallFrom(string zip, Version version)
        {
            ticket.Progress = -1f;
            var report = new Progress<string>(SetMessage);
            await Task.Run(() => Installer.Install(game, zip, true, report));

            settings.InstalledVersion = version.ToString(3);
            settings.InstalledFingerprint = Game.EnglishFingerprint(game);
            settings.InstalledAt = DateTime.UtcNow;
            SaveSettings();
            RemoveOldPacks(zip);
            RenderNotes();

            SetMessage($"v{version.ToString(3)} kuruldu. Bilete basın, oyun Türkçe açılır.");
            keepMessage = true;
            ticket.Progress = null;
            ticket.Stamp("KURULDU");
        }

        void ChangeLanguage(bool turkish)
        {
            if (busy || game == null) return;
            try
            {
                Installer.SetLanguage(game, turkish);
                SetMessage(turkish
                    ? "Oyun bir sonraki açılışta Türkçe başlar."
                    : "Oyun bir sonraki açılışta İngilizce başlar. Türkçe dosyalar kenarda bekliyor, tek tıkla geri gelir.");
                keepMessage = true;
            }
            catch (Exception ex) { ShowError(ex); }
            RefreshState();
        }

        // Once oyunun soru kutusuyla sorulur
        void Uninstall()
        {
            if (busy) return;
            dialog.Ask("YAMA KALDIRILSIN MI?",
                "Türkçe dosyalar silinir ve oyun bir sonraki açılışta İngilizce başlar. İsterseniz sonra tek tuşla yeniden kurabilirsiniz.",
                "KALDIR", "VAZGEÇ", true, yes => { if (yes) RemovePatch(); });
        }

        void RemovePatch()
        {
            try
            {
                Installer.Uninstall(game);
                settings.InstalledVersion = null;
                settings.InstalledFingerprint = null;
                settings.InstalledAt = null;
                SaveSettings();
                RenderNotes();
                SetMessage("Yama kaldırıldı.");
                keepMessage = true;
            }
            catch (Exception ex) { ShowError(ex); }
            RefreshState();
        }

        // Bilet yirtilir ve oyun acilir
        void Play()
        {
            if (Game.IsRunning()) { SetMessage("Oyun zaten açık."); keepMessage = true; return; }
            ticket.Tear(() =>
            {
                try
                {
                    Game.Launch();
                    WindowState = FormWindowState.Minimized;
                }
                catch (Exception ex)
                {
                    ShowError(ex);
                    ticket.Mend();
                }
            });
        }

        void PickFolder()
        {
            using (var browser = new FolderBrowserDialog { Description = "Limbus Company klasörünü seçin (içinde LimbusCompany_Data olan klasör)", ShowNewFolderButton = false })
            {
                if (game != null) browser.SelectedPath = game;
                if (browser.ShowDialog(this) != DialogResult.OK) return;
                var picked = browser.SelectedPath;
                // Kutuphane klasoru secildiyse oyunu icinde ara
                var nested = Path.Combine(picked, "steamapps", "common", "Limbus Company");
                if (!Game.IsGameFolder(picked) && Game.IsGameFolder(nested)) picked = nested;
                if (!Game.IsGameFolder(picked))
                {
                    SetMessage("Seçilen klasörde LimbusCompany_Data yok. Oyunun kendi klasörünü seçin.", Palette.Red);
                    keepMessage = true;
                    return;
                }
                game = picked;
                settings.GamePath = picked;
                SaveSettings();
                keepMessage = false;
                RefreshState();
                RenderNotes();
            }
        }

        // Uygulamanin daha yeni surumu olan en yeni release
        // Yeni paket yeni uygulama isteyebilir
        // O yuzden yamadan once bu guncellenir
        Release AppUpdate => releases?
            .Where(r => r.AppUrl != null && r.AppVersion != null && r.AppVersion > SelfUpdate.Current)
            .OrderByDescending(r => r.AppVersion)
            .FirstOrDefault();

        async Task UpdateApp(Release release)
        {
            var done = false;
            await Work("GÜNCELLENİYOR", async () =>
            {
                SetMessage("Uygulama güncelleniyor...");
                ticket.Progress = 0f;
                await SelfUpdate.ApplyAsync(release.AppUrl, new Progress<int>(p => ticket.Progress = p / 100f));
                done = true;
            });
            if (done) Close();
        }

        // Uzun isler sirasinda tuslar kilitlenir
        // Hata olursa oyuncuya anlasilir dille soylenir
        async Task Work(string title, Func<Task> job)
        {
            busy = true;
            busyTitle = title;
            keepMessage = false;
            wheel.Spinning = true;
            RefreshState();
            try { await job(); }
            catch (Exception ex) { ShowError(ex); }
            finally
            {
                busy = false;
                busyTitle = null;
                wheel.Spinning = false;
                ticket.Progress = null;
                RefreshState();
            }
        }

        void ShowError(Exception ex)
        {
            SetMessage("Hata: " + Explain(ex), Palette.Red);
            keepMessage = true;
        }

        static string Explain(Exception ex)
        {
            if (ex is AggregateException agg && agg.InnerException != null) ex = agg.InnerException;
            if (ex is UnauthorizedAccessException)
                return "Oyun klasörüne yazılamadı. Uygulamayı sağ tıklayıp Yönetici olarak çalıştırın.";
            if (ex is System.Net.Http.HttpRequestException || ex is System.Net.WebException || ex is TaskCanceledException)
                return "İnternete bağlanılamadı.";
            if (ex is IOException && Game.IsRunning())
                return "Oyun açık. Önce Limbus Company'yi kapatın.";
            return ex.Message;
        }

        void SetMessage(string text) => SetMessage(text, Palette.Muted);

        void SetMessage(string text, Color color)
        {
            if (InvokeRequired) { BeginInvoke(new Action<string, Color>(SetMessage), text, color); return; }
            message.Set(text, color);
        }

        void RenderNotes() => notes.Show(releases, InstalledVersion, releaseError);

        static string CachedPack(Version version) => Path.Combine(Settings.CacheFolder, "v" + version.ToString(3) + ".zip");

        // Sadece kurulu surumun paketi kalir
        // Yeniden kurmak icin yeter
        static void RemoveOldPacks(string keep)
        {
            try
            {
                foreach (var file in Directory.GetFiles(Settings.CacheFolder))
                    if (!string.Equals(file, keep, StringComparison.OrdinalIgnoreCase)) File.Delete(file);
            }
            catch { }
        }

        void SaveSettings()
        {
            try { settings.Save(); } catch { }
        }

        static void Open(string target)
        {
            try { Process.Start(target); } catch { }
        }

        // Degismeyen her sey bir kez cizilir
        // Zemin cerceve masa klasor baslik ve sabit etiketler
        protected override void PaintBackdrop(Graphics g)
        {
            var full = Px(0, 0, LogicalSize.Width, LogicalSize.Height);
            g.Clear(Palette.Back);
            Art.DrawNine(g, Art.Get("zemin"), full, 40, 0.7f * Zoom);

            // Dante'nin masasi soluk bir zemin olarak
            var inner = Px(9, 9, LogicalSize.Width - 18, LogicalSize.Height - 18);
            var desk = Art.Get("masa");
            var k = desk.Width / (float)inner.Width;
            var source = new Rectangle(0, (int)(DeskTop * desk.Height), desk.Width, (int)(inner.Height * k));
            Art.DrawFaded(g, desk, inner, source, 0.62f);
            Shade(g, inner);

            // Baslik
            Lettering.Tracked(g, "LIMBUS COMPANY", Typeface("Bahnschrift SemiBold", 11f), Palette.Muted, new PointF(Px(93), Px(22)), Px(3.2f));
            DrawTitle(g, "TÜRKÇE YAMA", new PointF(Px(90), Px(33)));

            // Kirmizi seritte hayran yapimi uyarisi
            Art.DrawFaded(g, Art.Get("kirmizi-serit"), Px(20, 100, 464, 20), 0.95f);
            Lettering.Tracked(g, "HAYRAN YAPIMIDIR  ·  PROJECT MOON İLE BAĞLANTISI YOKTUR", Typeface("Bahnschrift SemiBold", 9.5f),
                Color.FromArgb(255, 232, 214), new PointF(Px(34), Px(104.5f)), Px(1.5f));

            // Surum notlarinin klasoru
            Art.DrawNine(g, Art.Get("dosya"), Px(FolderArea), new Padding(300, 230, 67, 151), 0.47f * Zoom);

            Lettering.Tracked(g, "DURUM", Typeface("Bahnschrift SemiBold", 10.5f), Palette.Muted, new PointF(Px(Column), Px(132)), Px(2.4f));
            Lettering.Tracked(g, "OYUN DİLİ", Typeface("Bahnschrift SemiBold", 10.5f), Palette.Muted, new PointF(Px(Column), Px(246)), Px(2.4f));
        }

        // Masanin hangi yuksekliginden kesilecegi
        // 0 en ust
        const float DeskTop = 0.2f;

        // Kenarlara dogru kararan golge
        // Yazilar masanin cizgileriyle karismasin diye sag sutun biraz daha koyu
        void Shade(Graphics g, Rectangle inner)
        {
            var edge = (int)Px(70);
            using (var top = new LinearGradientBrush(new Rectangle(inner.Left, inner.Top - 1, inner.Width, edge + 2), Color.FromArgb(150, 0, 0, 0), Color.FromArgb(0, 0, 0, 0), LinearGradientMode.Vertical))
                g.FillRectangle(top, inner.Left, inner.Top, inner.Width, edge);
            using (var bottom = new LinearGradientBrush(new Rectangle(inner.Left, inner.Bottom - edge - 1, inner.Width, edge + 2), Color.FromArgb(0, 0, 0, 0), Color.FromArgb(160, 0, 0, 0), LinearGradientMode.Vertical))
                g.FillRectangle(bottom, inner.Left, inner.Bottom - edge, inner.Width, edge);
            var column = Px(Column - 24, 9, ColumnWidth + 40, LogicalSize.Height - 18);
            var ramp = (int)Px(40);
            using (var shade = new LinearGradientBrush(new Rectangle(column.Left - ramp - 1, column.Top, ramp + 2, column.Height), Color.FromArgb(0, 0, 0, 0), Color.FromArgb(110, 8, 5, 4), LinearGradientMode.Horizontal))
                g.FillRectangle(shade, column.Left - ramp, column.Top, ramp, column.Height);
            using (var shade = new SolidBrush(Color.FromArgb(110, 8, 5, 4)))
                g.FillRectangle(shade, column.Left, column.Top, inner.Right - column.Left, column.Height);
        }

        // Altin rengi buyuk baslik
        // Ustten alta acik altindan koyu turuncuya
        void DrawTitle(Graphics g, string text, PointF origin)
        {
            var font = Typeface("Bahnschrift SemiBold Condensed", 44f);
            var spacing = Px(1.5f);
            var width = Lettering.TrackedWidth(g, text, font, spacing);
            Lettering.Tracked(g, text, font, Color.FromArgb(170, 0, 0, 0), new PointF(origin.X + Px(2), origin.Y + Px(3)), spacing);
            var bounds = new RectangleF(origin.X, origin.Y, width, font.Height);
            using (var brush = new LinearGradientBrush(bounds, Palette.LightGold, Color.FromArgb(226, 132, 36), LinearGradientMode.Vertical))
            // Bahnschrift degisken bir font ve harflerin parcalari ust uste biniyor
            // Varsayilan doldurma ust uste binen yeri bos birakip harfte delik aciyordu
            using (var path = new GraphicsPath(FillMode.Winding))
            {
                var x = origin.X;
                foreach (var c in text)
                {
                    if (c != ' ') path.AddString(c.ToString(), font.FontFamily, (int)font.Style, font.Size, new PointF(x, origin.Y), StringFormat.GenericTypographic);
                    x += (c == ' ' ? font.Size * 0.32f : g.MeasureString(c.ToString(), font, PointF.Empty, StringFormat.GenericTypographic).Width) + spacing;
                }
                g.FillPath(brush, path);
            }
        }

        // Durum blogu
        // Solda renkli ince isik
        // Ustte buyuk durum sonra iki satir ayrinti
        class Status : Widget
        {
            public string Title = "", Line1 = "", Line2 = "";
            Color color = Palette.Muted;

            public void Set(string title, Color tint, string line1, string line2)
            {
                if (title == Title && tint == color && line1 == Line1 && line2 == Line2) return;
                Title = title;
                color = tint;
                Line1 = line1;
                Line2 = line2;
                Invalidate();
            }

            public override void Paint(Graphics g)
            {
                var r = Canvas.Px(Bounds);
                var bar = new RectangleF(r.Left, r.Top + Canvas.Px(5), Canvas.Px(3), Canvas.Px(28));
                using (var glow = new SolidBrush(Color.FromArgb(60, color)))
                    g.FillRectangle(glow, RectangleF.Inflate(bar, Canvas.Px(2), Canvas.Px(1)));
                using (var brush = new SolidBrush(color))
                    g.FillRectangle(brush, bar);
                var x = r.Left + Canvas.Px(12);
                var title = Canvas.Typeface("Bahnschrift SemiBold Condensed", 30f);
                Lettering.Tracked(g, Title, title, color, new PointF(x, r.Top), Canvas.Px(1f));
                Lettering.Draw(g, Line1, Canvas.Typeface("Bahnschrift", 13f), Palette.Ink, new RectangleF(x, r.Top + Canvas.Px(40), r.Right - x, Canvas.Px(18)));
                Lettering.Draw(g, Line2, Canvas.Typeface("Bahnschrift", 12.5f), Palette.Muted, new RectangleF(x, r.Top + Canvas.Px(60), r.Right - x, Canvas.Px(18)));
            }
        }
    }
}
