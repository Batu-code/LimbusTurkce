using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;

namespace LimbusTurkce
{
    // Oyunun arayuzunden alinan gorseller
    // exe'nin icine gomulu ve art klasorundeki NOTICE dosyasindaki sartlara tabi
    static class Art
    {
        static readonly Dictionary<string, Image> Cache = new Dictionary<string, Image>();

        public static Image Get(string name)
        {
            if (Cache.TryGetValue(name, out var image)) return image;
            using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("LimbusTurkce.art." + name + ".png"))
            {
                if (stream == null) throw new InvalidOperationException("Gorsel yok: " + name);
                // Akis kapaninca GDI+ resmi okuyamaz
                // O yuzden bellekteki bir kopyaya aliniyor
                image = new Bitmap(Image.FromStream(stream));
            }
            Cache[name] = image;
            return image;
        }

        // Koseleri bozmadan istenen boya cizer
        // slice kaynak resimde kenarin kac piksel oldugu
        // scale kenarin hedefte ne kadar buyuk cizilecegi
        public static void DrawNine(Graphics g, Image image, Rectangle dest, int slice, float scale, float opacity = 1f)
        {
            if (dest.Width <= 0 || dest.Height <= 0) return;
            var d = (int)Math.Round(slice * scale);
            d = Math.Min(d, Math.Min(dest.Width, dest.Height) / 2);
            int w = image.Width, h = image.Height;

            int[] sx = { 0, slice, w - slice, w };
            int[] sy = { 0, slice, h - slice, h };
            int[] dx = { dest.Left, dest.Left + d, dest.Right - d, dest.Right };
            int[] dy = { dest.Top, dest.Top + d, dest.Bottom - d, dest.Bottom };

            using (var attributes = new ImageAttributes())
            {
                // Dilim kenarlarinda komsu pikselden renk sizmasin
                attributes.SetWrapMode(WrapMode.TileFlipXY);
                if (opacity < 1f)
                    attributes.SetColorMatrix(new ColorMatrix { Matrix33 = opacity });

                var oldMode = g.InterpolationMode;
                var oldOffset = g.PixelOffsetMode;
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = PixelOffsetMode.Half;
                for (var row = 0; row < 3; row++)
                    for (var col = 0; col < 3; col++)
                    {
                        var target = Rectangle.FromLTRB(dx[col], dy[row], dx[col + 1], dy[row + 1]);
                        if (target.Width <= 0 || target.Height <= 0) continue;
                        g.DrawImage(image, target, sx[col], sy[row], sx[col + 1] - sx[col], sy[row + 1] - sy[row], GraphicsUnit.Pixel, attributes);
                    }
                g.InterpolationMode = oldMode;
                g.PixelOffsetMode = oldOffset;
            }
        }

        public static void DrawFaded(Graphics g, Image image, Rectangle dest, float opacity)
        {
            using (var attributes = new ImageAttributes())
            {
                attributes.SetColorMatrix(new ColorMatrix { Matrix33 = opacity });
                var oldMode = g.InterpolationMode;
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.DrawImage(image, dest, 0, 0, image.Width, image.Height, GraphicsUnit.Pixel, attributes);
                g.InterpolationMode = oldMode;
            }
        }

        // Bahnschrift Windows 10 ile geliyor
        // Yoksa Segoe UI kullanilir
        public static Font Font(string family, float size, FontStyle style = FontStyle.Regular) =>
            new Font(Family(family), size, style);

        public static FontFamily Family(string family) =>
            FontFamily.Families.FirstOrDefault(f => f.Name == family) ?? new FontFamily("Segoe UI");
    }

    // Butun animasyonlari tek zamanlayicidan surer
    // Her adim gecen sureyi alir ve daha surecekse true doner
    // Hareket eden bir sey kalmayinca zamanlayici durur
    static class Animator
    {
        static readonly Timer Timer = new Timer { Interval = 15 };
        static readonly List<Func<float, bool>> Steps = new List<Func<float, bool>>();
        static readonly System.Diagnostics.Stopwatch Clock = System.Diagnostics.Stopwatch.StartNew();
        static double last;

        static Animator() => Timer.Tick += Tick;

        public static void Run(Func<float, bool> step)
        {
            if (!Steps.Contains(step)) Steps.Add(step);
            if (Timer.Enabled) return;
            last = Clock.Elapsed.TotalSeconds;
            Timer.Start();
        }

        static void Tick(object sender, EventArgs e)
        {
            var now = Clock.Elapsed.TotalSeconds;
            // Pencere suruklenirken takilan kareler sicrama yapmasin
            var dt = (float)Math.Min(0.1, now - last);
            last = now;
            for (var i = Steps.Count - 1; i >= 0; i--)
                if (!Steps[i](dt)) Steps.RemoveAt(i);
            if (Steps.Count == 0) Timer.Stop();
        }

        public static float Toward(float value, float target, float amount) =>
            value < target ? Math.Min(target, value + amount) : Math.Max(target, value - amount);

        public static Color Mix(Color a, Color b, float t) =>
            Color.FromArgb((int)(a.R + (b.R - a.R) * t), (int)(a.G + (b.G - a.G) * t), (int)(a.B + (b.B - a.B) * t));
    }

    // Oyunun tus gorselleriyle cizilen tus
    // Uzerine gelince parlak gorsel ceyrek saniyede yanar
    // Pulse acikken kendiliginden yavasca yanip soner
    class ArtButton : Control
    {
        public string Normal = "tus";
        public string Hover = "tus-parlak";
        public int Slice = 10;
        public float SliceScale = 0.45f;
        public Color TextColor = Color.FromArgb(237, 226, 210);
        public Color HoverTextColor = Color.White;

        const float FadeSeconds = 0.18f;
        const float PulseSeconds = 1.8f;
        const float PulseStrength = 0.75f;

        bool hovered, pressed, selected, pulse;
        float glow, pulsePhase;
        readonly Func<float, bool> step;

        public ArtButton()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                | ControlStyles.SupportsTransparentBackColor | ControlStyles.ResizeRedraw, true);
            BackColor = Color.Transparent;
            Cursor = Cursors.Hand;
            AccessibleRole = AccessibleRole.PushButton;
            step = Step;
        }

        // Iki secenekli dugmede secili olan hep yanik kalir
        public bool Selected
        {
            get => selected;
            set { if (selected == value) return; selected = value; Animate(); }
        }

        public bool Pulse
        {
            get => pulse;
            set { if (pulse == value) return; pulse = value; pulsePhase = 0; Animate(); }
        }

        float Target => Enabled && (hovered || selected) ? 1f : 0f;
        bool Pulsing => pulse && Enabled && Visible && !IsDisposed;

        void Animate()
        {
            if (IsHandleCreated) Animator.Run(step);
            Invalidate();
        }

        bool Step(float dt)
        {
            if (IsDisposed) return false;
            glow = Animator.Toward(glow, Target, dt / FadeSeconds);
            if (Pulsing) pulsePhase = (pulsePhase + dt / PulseSeconds) % 1f;
            Invalidate();
            return glow != Target || Pulsing;
        }

        // Standart tus mesajina da cevap verir
        // Ekran okuyucular ve otomasyon araclari tusa boyle basar
        protected override void WndProc(ref Message m)
        {
            const int ButtonClick = 0x00F5;
            if (m.Msg == ButtonClick)
            {
                if (Enabled) OnClick(EventArgs.Empty);
                return;
            }
            base.WndProc(ref m);
        }

        protected override void OnHandleCreated(EventArgs e) { base.OnHandleCreated(e); Animate(); }
        protected override void OnTextChanged(EventArgs e) { base.OnTextChanged(e); Invalidate(); }
        protected override void OnEnabledChanged(EventArgs e) { base.OnEnabledChanged(e); hovered = pressed = false; Animate(); }
        protected override void OnVisibleChanged(EventArgs e) { base.OnVisibleChanged(e); Animate(); }
        protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); hovered = true; Animate(); }
        protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); hovered = pressed = false; Animate(); }
        protected override void OnMouseDown(MouseEventArgs e) { base.OnMouseDown(e); if (e.Button == MouseButtons.Left) { pressed = true; Invalidate(); } }
        protected override void OnMouseUp(MouseEventArgs e) { base.OnMouseUp(e); pressed = false; Invalidate(); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            var scale = SliceScale * DeviceDpi / 96f;
            var rect = new Rectangle(0, 0, Width, Height);
            if (pressed) rect.Inflate(-1, -1);

            if (!Enabled)
            {
                Art.DrawNine(g, Art.Get(Normal), rect, Slice, scale, 0.45f);
                DrawLabel(g, rect, Animator.Mix(Color.Black, TextColor, 0.45f));
                return;
            }

            // Nefes egrisi 0 ile 1 arasinda yumusak gidip gelir
            var breath = Pulsing ? PulseStrength * (0.5f - 0.5f * (float)Math.Cos(pulsePhase * Math.PI * 2)) : 0f;
            var light = Math.Max(glow, breath);
            Art.DrawNine(g, Art.Get(Normal), rect, Slice, scale);
            if (light > 0.01f) Art.DrawNine(g, Art.Get(Hover), rect, Slice, scale, light);
            DrawLabel(g, rect, Animator.Mix(TextColor, HoverTextColor, light));
        }

        void DrawLabel(Graphics g, Rectangle rect, Color color) =>
            TextRenderer.DrawText(g, Text, Font, rect, color,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
    }

    // Oyunun koyu ic paneliyle cizilen kutu
    // Icindeki saydam yazilar bu zeminin ustune cizilir
    class ArtPanel : Panel
    {
        public string Image = "panel";
        public int Slice = 12;
        public float SliceScale = 0.6f;

        public ArtPanel()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                | ControlStyles.SupportsTransparentBackColor | ControlStyles.ResizeRedraw, true);
            BackColor = Color.Transparent;
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            base.OnPaintBackground(e);
            Art.DrawNine(e.Graphics, Art.Get(Image), new Rectangle(0, 0, Width, Height), Slice, SliceScale * DeviceDpi / 96f);
        }
    }
}
