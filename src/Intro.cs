using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;

namespace LimbusTurkce
{
    // Acilis sahnesi
    // Tekerlek soldan yuvarlanip gelir
    // Serit cekilir ve baslik acilir
    // Sonra ekran otobus kapisi gibi ortadan ayrilip launcher'i gosterir
    // Tiklayinca ya da tusa basinca kapilar hemen acilir
    class Intro : Control
    {
        public const float SceneSeconds = 1.9f;
        const float DoorSeconds = 0.55f;

        static readonly Color Gold = Color.FromArgb(240, 178, 72);
        static readonly Color Muted = Color.FromArgb(170, 145, 120);
        static readonly Color Edge = Color.FromArgb(255, 132, 0);

        public event EventHandler Finished;

        readonly Func<float, bool> step;
        float time, doors;
        bool opening;
        Bitmap still;

        // Kisa surumde sahne atlanir ve sadece kapilar acilir
        public Intro(bool full)
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.Opaque, true);
            if (!full) time = SceneSeconds;
            step = Step;
        }

        public void Start()
        {
            // Kisa surumde sahne zaten bitmis sayilir
            // Kapilar ilk karede acilmaya baslar
            if (time >= SceneSeconds) BeginOpening();
            Animator.Run(step);
        }

        public void Skip()
        {
            if (!opening) BeginOpening();
        }

        bool Step(float dt)
        {
            if (IsDisposed) return false;
            if (!opening)
            {
                time += dt;
                if (time >= SceneSeconds) BeginOpening();
                Invalidate();
                return true;
            }
            doors += dt / DoorSeconds;
            if (doors >= 1f)
            {
                Finish();
                return false;
            }
            UpdateDoors();
            Invalidate();
            return true;
        }

        // Son kare bir kez resme cizilir
        // Kapilar o resmin iki yarisini tasir
        void BeginOpening()
        {
            opening = true;
            time = SceneSeconds;
            still?.Dispose();
            still = Compose(Math.Max(1, Width), Math.Max(1, Height), SceneSeconds, DeviceDpi / 96f, false);
            UpdateDoors();
        }

        void Finish()
        {
            Visible = false;
            Finished?.Invoke(this, EventArgs.Empty);
            still?.Dispose();
            Dispose();
        }

        float DoorOffset
        {
            get
            {
                var k = 1f - (1f - Math.Min(1f, doors)) * (1f - Math.Min(1f, doors)) * (1f - Math.Min(1f, doors));
                return k * Width / 2f;
            }
        }

        // Kapilarin araliginda alttaki launcher gorunsun diye denetimin sekli de daralir
        void UpdateDoors()
        {
            var half = Width / 2;
            var offset = (int)DoorOffset;
            var region = new Region(new Rectangle(0, 0, Math.Max(0, half - offset), Height));
            region.Union(new Rectangle(half + offset, 0, Math.Max(0, Width - half - offset), Height));
            var old = Region;
            Region = region;
            old?.Dispose();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            if (!opening)
            {
                using (var frame = Compose(Width, Height, time, DeviceDpi / 96f, true))
                    g.DrawImageUnscaled(frame, 0, 0);
                return;
            }

            var half = Width / 2;
            var offset = (int)DoorOffset;
            g.DrawImage(still, new Rectangle(-offset, 0, half, Height), new Rectangle(0, 0, half, Height), GraphicsUnit.Pixel);
            g.DrawImage(still, new Rectangle(half + offset, 0, Width - half, Height), new Rectangle(half, 0, Width - half, Height), GraphicsUnit.Pixel);
            // Kapi kenarlarinda ince turuncu cizgi
            using (var pen = new Pen(Edge, Math.Max(2f, 2f * DeviceDpi / 96f)))
            {
                g.DrawLine(pen, half - offset - 1, 0, half - offset - 1, Height);
                g.DrawLine(pen, half + offset, 0, half + offset, Height);
            }
        }

        // Sahnenin t anindaki hali
        // Testte kare kare bakabilmek icin public
        public static Bitmap Compose(int width, int height, float t, float dpi, bool hint)
        {
            var bmp = new Bitmap(width, height);
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
                g.Clear(Color.FromArgb(16, 16, 13));

                Art.DrawNine(g, Art.Get("zemin"), new Rectangle(0, 0, width, height), 40, 0.7f * dpi);
                using (var shade = new SolidBrush(Color.FromArgb(150, 8, 4, 4)))
                    g.FillRectangle(shade, 0, 0, width, height);

                var cx = width / 2f;
                var cy = height / 2f;

                // Arkada soluk muhur yavasca belirir
                var sealAlpha = 0.16f * Ease(Range(t, 0.1f, 0.9f));
                if (sealAlpha > 0)
                {
                    var sh = height * 0.78f;
                    var sw = sh * 458f / 526f;
                    Art.DrawFaded(g, Art.Get("muhur"), new Rectangle((int)(cx - sw / 2), (int)(cy - sh / 2), (int)sw, (int)sh), sealAlpha);
                }

                // Yerlesim tekerlek solda baslik saginda
                var wheelSize = 104f * dpi;
                var blockWidth = wheelSize + 20f * dpi + 330f * dpi;
                var left = cx - blockWidth / 2f;
                var wheelX = left + wheelSize / 2f;
                var titleX = left + wheelSize + 20f * dpi;

                // Tekerlek ekranin solundan yuvarlanir
                // Donus aldigi yola gore oldugu icin kaymadan yuvarlaniyor gibi gorunur
                var roll = Ease(Range(t, 0.1f, 1.0f));
                var startX = -wheelSize;
                var x = startX + (wheelX - startX) * roll;
                var angle = (x - wheelX) / (float)(Math.PI * wheelSize) * 360f;
                var state = g.Save();
                g.TranslateTransform(x, cy);
                g.RotateTransform(angle);
                g.DrawImage(Art.Get("teker"), -wheelSize / 2f, -wheelSize / 2f, wheelSize, wheelSize);
                g.Restore(state);

                // Serit soldan saga cekilir
                var sweep = Ease(Range(t, 0.75f, 1.2f));
                if (sweep > 0)
                {
                    var stripWidth = 340f * dpi * sweep;
                    var strip = new Rectangle((int)(titleX - 12 * dpi), (int)(cy - 4 * dpi), (int)stripWidth, (int)(46 * dpi));
                    Art.DrawFaded(g, Art.Get("serit"), strip, 0.55f);
                }

                using (var small = new Font(Art.Family("Bahnschrift SemiBold"), 13f * dpi, FontStyle.Regular, GraphicsUnit.Pixel))
                using (var big = new Font(Art.Family("Bahnschrift SemiBold Condensed"), 54f * dpi, FontStyle.Regular, GraphicsUnit.Pixel))
                {
                    var upper = Range(t, 0.95f, 1.3f);
                    if (upper > 0)
                        using (var brush = new SolidBrush(Color.FromArgb((int)(255 * upper), Muted)))
                            g.DrawString("LIMBUS COMPANY", small, brush, titleX, cy - 30 * dpi);

                    // Baslik soldan saga acilir
                    var reveal = Ease(Range(t, 1.0f, 1.5f));
                    if (reveal > 0)
                    {
                        var size = g.MeasureString("TÜRKÇE YAMA", big);
                        var clip = g.Clip;
                        g.SetClip(new RectangleF(titleX - 4, cy - 14 * dpi, size.Width * reveal + 4, size.Height));
                        using (var brush = new SolidBrush(Gold))
                            g.DrawString("TÜRKÇE YAMA", big, brush, titleX - 4 * dpi, cy - 14 * dpi);
                        g.Clip = clip;
                    }

                    var hintAlpha = hint ? Range(t, 0.6f, 1.0f) : 0f;
                    if (hintAlpha > 0)
                        using (var tiny = new Font(small.FontFamily, 11f * dpi, FontStyle.Regular, GraphicsUnit.Pixel))
                        using (var brush = new SolidBrush(Color.FromArgb((int)(150 * hintAlpha), Muted)))
                        {
                            var text = "Geçmek için tıklayın";
                            var size = g.MeasureString(text, tiny);
                            g.DrawString(text, tiny, brush, cx - size.Width / 2f, height - 40 * dpi);
                        }
                }
            }
            return bmp;
        }

        static float Range(float t, float from, float to) => Math.Max(0f, Math.Min(1f, (t - from) / (to - from)));

        // Hizli baslayip yavaslayarak duran egri
        static float Ease(float k) => 1f - (1f - k) * (1f - k) * (1f - k);
    }
}
