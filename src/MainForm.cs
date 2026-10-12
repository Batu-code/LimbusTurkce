using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace LimbusTurkce
{
    // Tek pencere
    // Solda durum ve tuslar sagda surum notlari
    // Oyuncu ne yapacagini dusunmesin diye buyuk tus her an tek dogru isi gosterir
    class MainForm : Form
    {
        const string GithubUrl = "https://github.com/" + Releases.Repo;
        // Sunucu acilinca davet baglantisi buraya
        // Bos kalirsa baglanti gizlenir
        static readonly string DiscordUrl = "";

        // Renkler oyunun ortak pencere gorsellerinden olculdu
        static readonly Color Back = Color.FromArgb(16, 16, 13);
        static readonly Color Card = Color.FromArgb(9, 5, 9);
        static readonly Color Gold = Color.FromArgb(240, 178, 72);
        static readonly Color Orange = Color.FromArgb(255, 132, 0);
        static readonly Color Red = Color.FromArgb(232, 84, 60);
        static readonly Color Ink = Color.FromArgb(239, 222, 200);
        static readonly Color Muted = Color.FromArgb(170, 145, 120);
        static readonly CultureInfo Turkish = new CultureInfo("tr-TR");

        enum Step { AppUpdate, PickFolder, Install, Update, Repair, Play, Wait }

        readonly Settings settings = Settings.Load();
        readonly bool justUpdated;
        string game;
        List<Release> releases;
        string releaseError;
        Step action = Step.Wait;
        bool busy;

        readonly Label gameValue, installedValue, latestValue, status;
        readonly ArtButton primary, play, reinstall, uninstall;
        Bitmap backdrop;
        readonly Wheel wheel;
        readonly Label languageLabel;
        readonly ArtButton turkishOn, turkishOff;
        readonly Bar bar;
        readonly RichTextBox notes;

        public MainForm(bool justUpdated)
        {
            this.justUpdated = justUpdated;

            Text = "Limbus Company Türkçe Yama";
            Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            AutoScaleDimensions = new SizeF(96F, 96F);
            AutoScaleMode = AutoScaleMode.Dpi;
            ClientSize = new Size(780, 520);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Back;
            ForeColor = Ink;
            Font = new Font("Segoe UI", 9.75F);
            DoubleBuffered = true;
            // Acilista yumusakca belirir
            Opacity = 0;

            // Baslik bolgesi
            // Zemin cizimi OnPaintBackground icinde
            // Otobus tekerlegi ayri denetim cunku is surerken donuyor
            wheel = new Wheel { Location = new Point(24, 24), Size = new Size(52, 52) };
            Add(wheel);
            Add(MakeLabel("LIMBUS COMPANY", 86, 24, 260, 16, Muted, Art.Font("Bahnschrift SemiBold", 8.5F)));
            Add(MakeLabel("Türkçe Yama", 83, 37, 270, 40, Gold, Art.Font("Bahnschrift SemiBold Condensed", 24F)));
            Add(MakeLabel("Hayran yapımıdır. Project Moon ile bağlantısı yoktur.", 26, 86, 318, 18, Muted, Art.Font("Bahnschrift Light", 8.5F)));

            var card = new ArtPanel { Location = new Point(24, 112), Size = new Size(320, 150) };
            Controls.Add(card);
            card.Controls.Add(MakeLabel("OYUN KLASÖRÜ", 16, 12, 200, 16, Muted, Art.Font("Bahnschrift SemiBold", 8F)));
            gameValue = MakeLabel("", 16, 29, 290, 20, Ink);
            gameValue.AutoEllipsis = true;
            card.Controls.Add(gameValue);
            var change = new LinkLabel
            {
                Text = "Değiştir",
                Location = new Point(240, 11),
                Size = new Size(66, 18),
                TextAlign = ContentAlignment.TopRight,
                LinkColor = Gold,
                ActiveLinkColor = Ink,
                LinkBehavior = LinkBehavior.HoverUnderline,
                BackColor = Color.Transparent,
                Font = Art.Font("Bahnschrift", 8.5F),
            };
            change.LinkClicked += (s, e) => { if (!busy) PickFolder(); };
            card.Controls.Add(change);
            card.Controls.Add(MakeLabel("KURULU SÜRÜM", 16, 59, 200, 16, Muted, Art.Font("Bahnschrift SemiBold", 8F)));
            installedValue = MakeLabel("", 16, 76, 290, 20, Ink);
            card.Controls.Add(installedValue);
            card.Controls.Add(MakeLabel("SON SÜRÜM", 16, 104, 200, 16, Muted, Art.Font("Bahnschrift SemiBold", 8F)));
            latestValue = MakeLabel("Yükleniyor...", 16, 121, 290, 20, Ink);
            card.Controls.Add(latestValue);

            // Buyuk tus oyunun afis cercevesi
            // Uzerine gelince altin rengi yanar
            primary = new ArtButton
            {
                Normal = "afis",
                Hover = "afis-parlak",
                Slice = 26,
                SliceScale = 0.36f,
                Location = new Point(24, 276),
                Size = new Size(320, 52),
                Font = Art.Font("Bahnschrift SemiBold", 13F),
                TextColor = Gold,
                HoverTextColor = Color.FromArgb(255, 236, 190),
            };
            primary.Name = "primary";
            primary.Click += async (s, e) => await RunPrimary();
            Add(primary);

            bar = new Bar { Location = new Point(28, 334), Size = new Size(312, 3), BackColor = Color.FromArgb(48, 28, 20), ForeColor = Orange };
            Add(bar);

            status = MakeLabel("", 24, 343, 320, 44, Muted, new Font("Segoe UI", 9F));
            Add(status);

            // Gizlenen tusun yeri kendiliginden kapansin diye sira icinde
            var secondary = new FlowLayoutPanel { Location = new Point(24, 392), Size = new Size(330, 34), WrapContents = false, Margin = Padding.Empty, BackColor = Color.Transparent };
            play = MakeButton("Oyna");
            play.Click += (s, e) => Play();
            reinstall = MakeButton("Yeniden kur");
            reinstall.Click += async (s, e) => await Reinstall();
            uninstall = MakeButton("Kaldır");
            uninstall.Click += (s, e) => Uninstall();
            foreach (var button in new[] { play, reinstall, uninstall })
            {
                button.Margin = new Padding(0, 0, 10, 0);
                secondary.Controls.Add(button);
            }
            Add(secondary);

            // Yamayi kaldirmadan dil degistirme
            // Secili olan dugme hep yanik durur
            languageLabel = MakeLabel("OYUN DİLİ", 24, 446, 80, 16, Muted, Art.Font("Bahnschrift SemiBold", 8F));
            Add(languageLabel);
            turkishOn = MakeButton("Türkçe");
            turkishOn.Location = new Point(104, 437);
            turkishOn.Size = new Size(96, 30);
            turkishOn.Click += (s, e) => ChangeLanguage(true);
            Add(turkishOn);
            turkishOff = MakeButton("İngilizce");
            turkishOff.Location = new Point(208, 437);
            turkishOff.Size = new Size(96, 30);
            turkishOff.Click += (s, e) => ChangeLanguage(false);
            Add(turkishOff);

            var footer = new FlowLayoutPanel { Location = new Point(24, 480), Size = new Size(320, 22), FlowDirection = FlowDirection.LeftToRight, WrapContents = false, BackColor = Color.Transparent };
            footer.Controls.Add(MakeLink("GitHub", GithubUrl));
            if (DiscordUrl != "") footer.Controls.Add(MakeLink("Discord", DiscordUrl));
            footer.Controls.Add(new Label { Text = "Uygulama v" + SelfUpdate.Current.ToString(3), ForeColor = Muted, AutoSize = true, BackColor = Color.Transparent, Font = Art.Font("Bahnschrift", 8.5F), Margin = new Padding(8, 3, 0, 0) });
            Add(footer);

            Add(MakeLabel("NELER DEĞİŞTİ", 368, 22, 300, 24, Gold, Art.Font("Bahnschrift SemiBold", 12F)));
            // RichTextBox saydam olamiyor
            // Panelin ortasindaki renkle boyanip panelin icine konuyor
            var notesFrame = new ArtPanel { Location = new Point(368, 56), Size = new Size(388, 446), Padding = new Padding(16, 14, 8, 14) };
            notes = new RichTextBox
            {
                Dock = DockStyle.Fill,
                BackColor = Card,
                ForeColor = Ink,
                BorderStyle = BorderStyle.None,
                ReadOnly = true,
                // 1.212 gibi sayilari baglanti saniyor
                DetectUrls = false,
                ScrollBars = RichTextBoxScrollBars.Vertical,
            };
            notesFrame.Controls.Add(notes);
            Add(notesFrame);

            // Acilis sahnesi her seyin ustunde durur
            // Bitince kendini kaldirir
            var fullIntro = settings.IntroSeenVersion != SelfUpdate.Current.ToString(3);
            var intro = new Intro(fullIntro) { Dock = DockStyle.Fill };
            Add(intro);
            intro.BringToFront();
            intro.Click += (s, e) => intro.Skip();
            KeyPreview = true;
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
                SetStatus($"Uygulama v{SelfUpdate.Current.ToString(3)} sürümüne güncellendi.");
                keepStatus = true;
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

        void RefreshState()
        {
            var installed = game != null && Installer.IsInstalled(game);
            var installedVersion = InstalledVersion;
            var gameChanged = installed && settings.InstalledFingerprint != null
                && settings.InstalledFingerprint != Game.EnglishFingerprint(game);

            gameValue.Text = game ?? "Bulunamadı";
            installedValue.Text = !installed ? "Kurulu değil"
                : installedVersion == null ? "Eski kurulum (zip ile)"
                : "v" + installedVersion.ToString(3) + (settings.InstalledAt is DateTime at ? "  ·  " + at.ToLocalTime().ToString("d MMMM yyyy", Turkish) : "");
            latestValue.Text = Latest != null ? Latest.Label + "  ·  " + Latest.Date.ToLocalTime().ToString("d MMMM yyyy", Turkish)
                : releaseError != null ? "Alınamadı" : "Yükleniyor...";

            var appUpdate = AppUpdate;
            if (appUpdate != null) Set(Step.AppUpdate, "Uygulamayı güncelle  ·  v" + appUpdate.AppVersion.ToString(3),
                $"Uygulamanın yeni sürümü var (sizdeki v{SelfUpdate.Current.ToString(3)}). Güncelleyince kendini yeniden açar.");
            else if (game == null) Set(Step.PickFolder, "Oyun klasörünü seç", "Limbus Company bu bilgisayarda bulunamadı. Klasörünü elle seçin.");
            else if (!installed && Latest == null) Set(Step.Wait, "Kur", releaseError != null ? "Sürümler alınamadı: " + releaseError : "Sürümler yükleniyor...");
            else if (!installed) Set(Step.Install, "Kur  ·  " + Latest.Label, "Yama kurulu değil.");
            else if (Latest != null && (installedVersion == null || Latest.Version > installedVersion)) Set(Step.Update, "Güncelle  ·  " + Latest.Label, "Yeni sürüm var. Değişenler sağda.");
            else if (gameChanged) Set(Step.Repair, "Yamayı yenile", "Oyun güncellenmiş. Yamanın yeni oyun metniyle yeniden kurulması gerekiyor.");
            else Set(Step.Play, "Oyna", Latest == null && releaseError != null ? "Güncelleme kontrol edilemedi: " + releaseError : "Yama güncel.");

            reinstall.Visible = uninstall.Visible = installed;
            // Buyuk tus zaten Oyna diyorsa ikincisine gerek yok
            play.Visible = installed && action != Step.Play;
            primary.Enabled = !busy && action != Step.Wait;
            play.Enabled = reinstall.Enabled = uninstall.Enabled = turkishOn.Enabled = turkishOff.Enabled = !busy;
            primary.TextColor = action == Step.Repair ? Red : Gold;
            // Oyuncudan bir sey bekleniyorsa buyuk tus nefes alir
            primary.Pulse = !busy && (action == Step.AppUpdate || action == Step.Install || action == Step.Update || action == Step.Repair);
            primary.Invalidate();

            languageLabel.Visible = turkishOn.Visible = turkishOff.Visible = installed;
            var turkish = installed && Installer.SelectedLanguage(game) == Installer.PackName;
            turkishOn.Selected = turkish;
            turkishOff.Selected = installed && !turkish;
        }

        void Set(Step next, string label, string message)
        {
            action = next;
            primary.Text = label;
            // Calisan bir islemin mesajini ezme
            if (!busy && !keepStatus) SetStatus(message);
        }

        bool keepStatus;

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
            var installed = InstalledVersion;
            var release = releases?.FirstOrDefault(r => r.Version == installed);
            if (release == null && installed != null && File.Exists(CachedPack(installed)))
            {
                await Work(() => InstallFrom(CachedPack(installed), installed));
                return;
            }
            await Install(release ?? Latest);
        }

        async Task Install(Release release)
        {
            if (release == null) { SetStatus("Sürümler alınamadı, internet bağlantısını kontrol edin."); return; }
            await Work(async () =>
            {
                var zip = CachedPack(release.Version);
                if (!File.Exists(zip) || (release.PackSha256 != null && Releases.Sha256(zip) != release.PackSha256))
                {
                    SetStatus($"{release.Label} indiriliyor...");
                    await Releases.DownloadAsync(release.PackUrl, zip, new Progress<int>(p => bar.Value = p));
                    if (release.PackSha256 != null && Releases.Sha256(zip) != release.PackSha256)
                    {
                        File.Delete(zip);
                        throw new InvalidOperationException("İndirilen dosya bozuk çıktı. Tekrar deneyin.");
                    }
                }
                await InstallFrom(zip, release.Version);
            });
        }

        async Task InstallFrom(string zip, Version version)
        {
            bar.Value = 100;
            var report = new Progress<string>(SetStatus);
            await Task.Run(() => Installer.Install(game, zip, true, report));

            settings.InstalledVersion = version.ToString(3);
            settings.InstalledFingerprint = Game.EnglishFingerprint(game);
            settings.InstalledAt = DateTime.UtcNow;
            SaveSettings();
            RemoveOldPacks(zip);
            RenderNotes();

            SetStatus($"v{version.ToString(3)} kuruldu. Oyna'ya basın, oyun Türkçe açılır.");
            keepStatus = true;
            Stamp("KURULDU");
        }

        // Limbus muhru kartin ve buyuk tusun ustune damga gibi iner
        void Stamp(string text)
        {
            var scale = DeviceDpi / 96f;
            var area = new Rectangle((int)(24 * scale), (int)(96 * scale), (int)(320 * scale), (int)(250 * scale));
            try { StampOverlay.Play(this, RectangleToScreen(area), text); }
            catch { }
        }

        void ChangeLanguage(bool turkish)
        {
            if (busy || game == null) return;
            try
            {
                Installer.SetLanguage(game, turkish);
                SetStatus(turkish
                    ? "Oyun bir sonraki açılışta Türkçe başlar."
                    : "Oyun bir sonraki açılışta İngilizce başlar. Yama dosyaları yerinde duruyor.");
                keepStatus = true;
            }
            catch (Exception ex) { ShowError(ex); }
            RefreshState();
        }

        void Uninstall()
        {
            var answer = MessageBox.Show(this, "Türkçe yama kaldırılsın mı? Oyun bir sonraki açılışta İngilizce başlar.", "Kaldır", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (answer != DialogResult.Yes) return;
            try
            {
                Installer.Uninstall(game);
                settings.InstalledVersion = null;
                settings.InstalledFingerprint = null;
                settings.InstalledAt = null;
                SaveSettings();
                RenderNotes();
                SetStatus("Yama kaldırıldı.");
                keepStatus = true;
            }
            catch (Exception ex) { ShowError(ex); }
            RefreshState();
        }

        void Play()
        {
            if (Game.IsRunning()) { SetStatus("Oyun zaten açık."); return; }
            try
            {
                Game.Launch();
                WindowState = FormWindowState.Minimized;
            }
            catch (Exception ex) { ShowError(ex); }
        }

        void PickFolder()
        {
            using (var dialog = new FolderBrowserDialog { Description = "Limbus Company klasörünü seçin (içinde LimbusCompany_Data olan klasör)", ShowNewFolderButton = false })
            {
                if (game != null) dialog.SelectedPath = game;
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                var picked = dialog.SelectedPath;
                // Kutuphane klasoru secildiyse oyunu icinde ara
                var nested = Path.Combine(picked, "steamapps", "common", "Limbus Company");
                if (!Game.IsGameFolder(picked) && Game.IsGameFolder(nested)) picked = nested;
                if (!Game.IsGameFolder(picked))
                {
                    MessageBox.Show(this, "Bu klasörde LimbusCompany_Data yok:\n" + picked, "Oyun bulunamadı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                game = picked;
                settings.GamePath = picked;
                SaveSettings();
                keepStatus = false;
                RefreshState();
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
            await Work(async () =>
            {
                SetStatus("Uygulama güncelleniyor...");
                await SelfUpdate.ApplyAsync(release.AppUrl, new Progress<int>(p => bar.Value = p));
                done = true;
            });
            if (done) Close();
        }

        // Uzun isler sirasinda tuslar kilitlenir
        // Hata olursa oyuncuya anlasilir dille soylenir
        async Task Work(Func<Task> job)
        {
            busy = true;
            keepStatus = false;
            bar.Value = 0;
            wheel.Spinning = true;
            RefreshState();
            try { await job(); }
            catch (Exception ex) { ShowError(ex); }
            finally
            {
                busy = false;
                wheel.Spinning = false;
                bar.Value = 0;
                RefreshState();
            }
        }

        void ShowError(Exception ex)
        {
            SetStatus("Hata: " + Explain(ex));
            keepStatus = true;
            status.ForeColor = Red;
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

        void SetStatus(string message)
        {
            if (InvokeRequired) { BeginInvoke(new Action<string>(SetStatus), message); return; }
            status.ForeColor = Muted;
            status.Text = message;
        }

        // Notlar dogrudan RTF olarak yaziliyor
        // Secim konumuyla renk vermek kayiyordu
        // RichTextBox satir sonunu iceride 1 disarida 2 karakter sayiyor
        void RenderNotes()
        {
            var rtf = new StringBuilder();
            rtf.Append(@"{\rtf1\ansi\deff0{\fonttbl{\f0\fnil Segoe UI;}}");
            rtf.Append(@"{\colortbl ;").Append(RtfColor(Gold)).Append(RtfColor(Ink)).Append(RtfColor(Muted)).Append("}");
            rtf.Append(@"\uc1");

            if (releases == null || releases.Count == 0)
            {
                Paragraph(rtf, releaseError != null ? "Sürüm notları alınamadı." : "Henüz yayımlanmış sürüm yok.", 3, false, 20);
                if (releaseError != null) Paragraph(rtf, releaseError, 3, false, 20);
            }
            else
            {
                var installed = InstalledVersion;
                foreach (var release in releases)
                {
                    var head = release.Label + "  ·  " + release.Date.ToLocalTime().ToString("d MMMM yyyy", Turkish);
                    if (release.Version == installed) head += "  ·  kurulu";
                    Paragraph(rtf, head, 1, true, 21);
                    // Baslik surum numarasiyla basliyor
                    // Surum zaten ustte yaziyor
                    var name = release.Title.StartsWith(release.Label)
                        ? release.Title.Substring(release.Label.Length).TrimStart(' ', '·', '-', '—')
                        : release.Title;
                    if (name != "") Paragraph(rtf, name, 2, true, 20, spaceAfter: 80);
                    foreach (var raw in release.Notes.Replace("\r", "").Split('\n'))
                    {
                        var line = raw.Replace("**", "").Trim();
                        if (line == "") continue;
                        if (line.StartsWith("#")) Paragraph(rtf, line.TrimStart('#', ' '), 2, true, 20);
                        else if (line.StartsWith("- ") || line.StartsWith("* ")) Paragraph(rtf, line.Substring(2), 2, false, 20, bullet: true);
                        else Paragraph(rtf, line, 2, false, 20);
                    }
                    Paragraph(rtf, "", 2, false, 12, spaceAfter: 160);
                }
            }

            rtf.Append("}");
            notes.Rtf = rtf.ToString();
            notes.Select(0, 0);
        }

        // Renk numarasi colortbl sirasi
        // 1 altin 2 metin 3 soluk
        // Boy yarim punto
        static void Paragraph(StringBuilder rtf, string text, int color, bool bold, int halfPoints, bool bullet = false, int spaceAfter = 40)
        {
            rtf.Append(@"\pard\sa").Append(spaceAfter);
            // Madde satiri kayinca ikinci satir yazinin hizasindan baslasin
            if (bullet) rtf.Append(@"\li300\fi-300\tx300");
            rtf.Append(@"\cf").Append(color).Append(bold ? @"\b" : @"\b0").Append(@"\fs").Append(halfPoints).Append(' ');
            if (bullet) rtf.Append(@"\bullet\tab ");
            foreach (var c in text)
            {
                if (c == '\\' || c == '{' || c == '}') rtf.Append('\\').Append(c);
                else if (c > 127) rtf.Append(@"\u").Append((short)c).Append('?');
                else rtf.Append(c);
            }
            rtf.Append(@"\par").Append('\n');
        }

        static string RtfColor(Color c) => $@"\red{c.R}\green{c.G}\blue{c.B};";

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

        static void Open(string url)
        {
            try { Process.Start(url); } catch { }
        }

        void Add(Control control) => Controls.Add(control);

        // Zemin tek seferde bir resme cizilip saklanir
        // Saydam yazilar ve tuslar her boyanista bu resmin ilgili parcasini kullanir
        protected override void OnPaintBackground(PaintEventArgs e)
        {
            if (backdrop == null || backdrop.Size != ClientSize) BuildBackdrop();
            e.Graphics.DrawImageUnscaled(backdrop, 0, 0);
        }

        protected override void OnDpiChanged(DpiChangedEventArgs e)
        {
            base.OnDpiChanged(e);
            backdrop?.Dispose();
            backdrop = null;
            Invalidate(true);
        }

        void BuildBackdrop()
        {
            backdrop?.Dispose();
            backdrop = new Bitmap(Math.Max(1, ClientSize.Width), Math.Max(1, ClientSize.Height));
            var scale = DeviceDpi / 96f;
            Rectangle At(int x, int y, int w, int h) => new Rectangle((int)(x * scale), (int)(y * scale), (int)(w * scale), (int)(h * scale));

            using (var g = Graphics.FromImage(backdrop))
            {
                g.Clear(Back);
                Art.DrawNine(g, Art.Get("zemin"), new Rectangle(Point.Empty, ClientSize), 40, 0.7f * scale);
                // Limbus muhru sol altta soluk bir filigran
                Art.DrawFaded(g, Art.Get("muhur"), At(176, 318, 168, 194), 0.42f);
                // Oyundaki alt baslik seridi
                Art.DrawFaded(g, Art.Get("serit"), At(20, 84, 320, 22), 0.35f);
                Art.DrawFaded(g, Art.Get("ayrac"), At(368, 48, 388, 2), 1f);
            }
        }

        static Label MakeLabel(string text, int x, int y, int w, int h, Color color, Font font = null) =>
            new Label { Text = text, Location = new Point(x, y), Size = new Size(w, h), ForeColor = color, Font = font ?? new Font("Segoe UI", 9.75F), BackColor = Color.Transparent };

        static ArtButton MakeButton(string text) =>
            new ArtButton { Text = text, Size = new Size(100, 34), Font = Art.Font("Bahnschrift SemiBold", 10F) };

        static LinkLabel MakeLink(string text, string url)
        {
            var link = new LinkLabel
            {
                Text = text,
                AutoSize = true,
                LinkColor = Gold,
                ActiveLinkColor = Ink,
                LinkBehavior = LinkBehavior.HoverUnderline,
                BackColor = Color.Transparent,
                Font = Art.Font("Bahnschrift", 8.5F),
                Margin = new Padding(0, 3, 8, 0),
            };
            link.LinkClicked += (s, e) => Open(url);
            return link;
        }

        // Standart ilerleme cubugu Windows temasinda yesil kaliyor
        // Yerine temaya uyan ince bir cizgi
        class Bar : Control
        {
            int value;
            public int Value
            {
                get => value;
                set { this.value = Math.Max(0, Math.Min(100, value)); Invalidate(); }
            }

            public Bar() => SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);

            protected override void OnPaint(PaintEventArgs e)
            {
                e.Graphics.Clear(BackColor);
                using (var brush = new SolidBrush(ForeColor))
                    e.Graphics.FillRectangle(brush, 0, 0, Width * value / 100, Height);
            }
        }
    }
}
