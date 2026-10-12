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

        static readonly Color Back = Color.FromArgb(21, 18, 15);
        static readonly Color Card = Color.FromArgb(32, 27, 23);
        static readonly Color Gold = Color.FromArgb(212, 166, 74);
        static readonly Color Red = Color.FromArgb(168, 50, 45);
        static readonly Color Ink = Color.FromArgb(237, 230, 218);
        static readonly Color Muted = Color.FromArgb(156, 146, 134);
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
        readonly Button primary, play, reinstall, uninstall;
        readonly CheckBox setLanguage;
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

            Add(MakeLabel("LIMBUS COMPANY", 24, 18, 320, 18, Muted, 8.25F, FontStyle.Bold));
            Add(MakeLabel("Türkçe Yama", 22, 34, 320, 40, Gold, 20F, FontStyle.Bold));
            Add(MakeLabel("Hayran yapımıdır. Project Moon ile bağlantısı yoktur.", 24, 76, 320, 18, Muted, 8.25F));

            var card = new Panel { Location = new Point(24, 106), Size = new Size(320, 156), BackColor = Card };
            Controls.Add(card);
            card.Controls.Add(MakeLabel("Oyun klasörü", 14, 12, 200, 18, Muted, 8.25F));
            gameValue = MakeLabel("", 14, 30, 292, 20, Ink);
            gameValue.AutoEllipsis = true;
            card.Controls.Add(gameValue);
            var change = new LinkLabel
            {
                Text = "Değiştir",
                Location = new Point(244, 12),
                Size = new Size(62, 18),
                TextAlign = ContentAlignment.TopRight,
                LinkColor = Gold,
                ActiveLinkColor = Ink,
                LinkBehavior = LinkBehavior.HoverUnderline,
                Font = new Font("Segoe UI", 8.25F),
            };
            change.LinkClicked += (s, e) => { if (!busy) PickFolder(); };
            card.Controls.Add(change);
            card.Controls.Add(MakeLabel("Kurulu sürüm", 14, 64, 200, 18, Muted, 8.25F));
            installedValue = MakeLabel("", 14, 82, 292, 20, Ink);
            card.Controls.Add(installedValue);
            card.Controls.Add(MakeLabel("Son sürüm", 14, 110, 200, 18, Muted, 8.25F));
            latestValue = MakeLabel("Yükleniyor...", 14, 128, 292, 20, Ink);
            card.Controls.Add(latestValue);

            primary = MakeButton("", 24, 278, 320, 48, Gold, Back, 11F);
            primary.Name = "primary";
            primary.Click += async (s, e) => await RunPrimary();
            Add(primary);

            bar = new Bar { Location = new Point(24, 334), Size = new Size(320, 4), BackColor = Card, ForeColor = Gold };
            Add(bar);

            status = MakeLabel("", 24, 344, 320, 44, Muted, 9F);
            Add(status);

            // Gizlenen tusun yeri kendiliginden kapansin diye sira icinde
            var secondary = new FlowLayoutPanel { Location = new Point(24, 394), Size = new Size(330, 32), WrapContents = false, Margin = Padding.Empty };
            play = MakeButton("Oyna", 0, 0, 100, 32, Card, Ink, 9.75F);
            play.Click += (s, e) => Play();
            reinstall = MakeButton("Yeniden kur", 0, 0, 100, 32, Card, Ink, 9.75F);
            reinstall.Click += async (s, e) => await Reinstall();
            uninstall = MakeButton("Kaldır", 0, 0, 100, 32, Card, Ink, 9.75F);
            uninstall.Click += (s, e) => Uninstall();
            foreach (var button in new[] { play, reinstall, uninstall })
            {
                button.Margin = new Padding(0, 0, 10, 0);
                secondary.Controls.Add(button);
            }
            Add(secondary);

            setLanguage = new CheckBox
            {
                Text = "Kurunca oyunun dilini Türkçe yap",
                Location = new Point(24, 438),
                Size = new Size(320, 22),
                ForeColor = Ink,
                Checked = settings.SetGameLanguage,
            };
            setLanguage.CheckedChanged += (s, e) => { settings.SetGameLanguage = setLanguage.Checked; SaveSettings(); };
            Add(setLanguage);

            var footer = new FlowLayoutPanel { Location = new Point(24, 484), Size = new Size(320, 22), FlowDirection = FlowDirection.LeftToRight, WrapContents = false };
            footer.Controls.Add(MakeLink("GitHub", GithubUrl));
            if (DiscordUrl != "") footer.Controls.Add(MakeLink("Discord", DiscordUrl));
            footer.Controls.Add(new Label { Text = "Uygulama v" + SelfUpdate.Current.ToString(3), ForeColor = Muted, AutoSize = true, Font = new Font("Segoe UI", 8.25F), Margin = new Padding(8, 3, 0, 0) });
            Add(footer);

            Add(MakeLabel("Neler değişti", 368, 18, 300, 22, Gold, 11F, FontStyle.Bold));
            // RichTextBox'in kendi kenar boslugu yok
            // Ayni renkte bir cerceve icine konuyor
            var notesFrame = new Panel { Location = new Point(368, 48), Size = new Size(388, 458), BackColor = Card, Padding = new Padding(14, 12, 6, 12) };
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

            Shown += async (s, e) => await Startup();
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
            play.Enabled = reinstall.Enabled = uninstall.Enabled = setLanguage.Enabled = !busy;
            primary.BackColor = action == Step.Repair ? Red : Gold;
            primary.ForeColor = action == Step.Repair ? Ink : Back;
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
            var language = settings.SetGameLanguage;
            await Task.Run(() => Installer.Install(game, zip, language, report));

            settings.InstalledVersion = version.ToString(3);
            settings.InstalledFingerprint = Game.EnglishFingerprint(game);
            settings.InstalledAt = DateTime.UtcNow;
            SaveSettings();
            RemoveOldPacks(zip);
            RenderNotes();

            SetStatus(language
                ? $"v{version.ToString(3)} kuruldu. Oyna'ya basın, oyun Türkçe açılır."
                : $"v{version.ToString(3)} kuruldu. Oyunda giriş ekranından Select Custom Language > Turkish seçin.");
            keepStatus = true;
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
            RefreshState();
            try { await job(); }
            catch (Exception ex) { ShowError(ex); }
            finally
            {
                busy = false;
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

        static Label MakeLabel(string text, int x, int y, int w, int h, Color color, float size = 9.75F, FontStyle style = FontStyle.Regular) =>
            new Label { Text = text, Location = new Point(x, y), Size = new Size(w, h), ForeColor = color, Font = new Font("Segoe UI", size, style), BackColor = Color.Transparent };

        static Button MakeButton(string text, int x, int y, int w, int h, Color back, Color fore, float size)
        {
            var button = new Button
            {
                Text = text,
                Location = new Point(x, y),
                Size = new Size(w, h),
                BackColor = back,
                ForeColor = fore,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", size, FontStyle.Bold),
                Cursor = Cursors.Hand,
                UseVisualStyleBackColor = false,
            };
            button.FlatAppearance.BorderSize = 0;
            button.FlatAppearance.MouseOverBackColor = ControlPaint.Light(back, 0.15F);
            return button;
        }

        static LinkLabel MakeLink(string text, string url)
        {
            var link = new LinkLabel
            {
                Text = text,
                AutoSize = true,
                LinkColor = Gold,
                ActiveLinkColor = Ink,
                LinkBehavior = LinkBehavior.HoverUnderline,
                Font = new Font("Segoe UI", 8.25F),
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
