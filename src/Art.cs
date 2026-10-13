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
            var assembly = Assembly.GetExecutingAssembly();
            // Saydamligi olmayan buyuk zeminler jpg
            using (var stream = assembly.GetManifestResourceStream("LimbusTurkce.art." + name + ".png")
                ?? assembly.GetManifestResourceStream("LimbusTurkce.art." + name + ".jpg"))
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
        public static void DrawNine(Graphics g, Image image, Rectangle dest, int slice, float scale, float opacity = 1f) =>
            DrawNine(g, image, dest, new Padding(slice), scale, opacity);

        // Her kenarin kalinligi ayri
        // Ust ve alt sifir verilirse resim sadece yatayda uzar
        public static void DrawNine(Graphics g, Image image, Rectangle dest, Padding slice, float scale, float opacity = 1f, bool center = true)
        {
            if (dest.Width <= 0 || dest.Height <= 0 || opacity <= 0.003f) return;
            int w = image.Width, h = image.Height;
            var left = (int)Math.Round(slice.Left * scale);
            var right = (int)Math.Round(slice.Right * scale);
            var top = (int)Math.Round(slice.Top * scale);
            var bottom = (int)Math.Round(slice.Bottom * scale);
            // Hedef kenarlardan kucukse kenarlar orantili daralir
            if (left + right > dest.Width) { var k = dest.Width / (float)(left + right); left = (int)(left * k); right = dest.Width - left; }
            if (top + bottom > dest.Height) { var k = dest.Height / (float)(top + bottom); top = (int)(top * k); bottom = dest.Height - top; }

            int[] sx = { 0, slice.Left, w - slice.Right, w };
            int[] sy = { 0, slice.Top, h - slice.Bottom, h };
            int[] dx = { dest.Left, dest.Left + left, dest.Right - right, dest.Right };
            int[] dy = { dest.Top, dest.Top + top, dest.Bottom - bottom, dest.Bottom };

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
                        if (!center && row == 1 && col == 1) continue;
                        var target = Rectangle.FromLTRB(dx[col], dy[row], dx[col + 1], dy[row + 1]);
                        var sw = sx[col + 1] - sx[col];
                        var sh = sy[row + 1] - sy[row];
                        if (target.Width <= 0 || target.Height <= 0 || sw <= 0 || sh <= 0) continue;
                        g.DrawImage(image, target, sx[col], sy[row], sw, sh, GraphicsUnit.Pixel, attributes);
                    }
                g.InterpolationMode = oldMode;
                g.PixelOffsetMode = oldOffset;
            }
        }

        public static void DrawFaded(Graphics g, Image image, Rectangle dest, float opacity) =>
            DrawFaded(g, image, dest, new Rectangle(0, 0, image.Width, image.Height), opacity);

        public static void DrawFaded(Graphics g, Image image, Rectangle dest, Rectangle source, float opacity)
        {
            if (opacity <= 0.003f) return;
            using (var attributes = new ImageAttributes())
            {
                attributes.SetWrapMode(WrapMode.TileFlipXY);
                if (opacity < 1f) attributes.SetColorMatrix(new ColorMatrix { Matrix33 = opacity });
                var oldMode = g.InterpolationMode;
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.DrawImage(image, dest, source.X, source.Y, source.Width, source.Height, GraphicsUnit.Pixel, attributes);
                g.InterpolationMode = oldMode;
            }
        }

        // Onceden olceklenmis resmi bire bir ve hizli kopyalar
        // Ornekleme yapilmadigi icin her karede cizilebilir
        public static void Blend(Graphics g, Image image, Point location, float opacity)
        {
            if (opacity <= 0.003f) return;
            var mode = g.InterpolationMode;
            var offset = g.PixelOffsetMode;
            g.InterpolationMode = InterpolationMode.NearestNeighbor;
            g.PixelOffsetMode = PixelOffsetMode.None;
            var dest = new Rectangle(location, image.Size);
            if (opacity >= 0.997f) g.DrawImage(image, dest, 0, 0, image.Width, image.Height, GraphicsUnit.Pixel);
            else
                using (var attributes = new ImageAttributes())
                {
                    attributes.SetColorMatrix(new ColorMatrix { Matrix33 = opacity });
                    g.DrawImage(image, dest, 0, 0, image.Width, image.Height, GraphicsUnit.Pixel, attributes);
                }
            g.InterpolationMode = mode;
            g.PixelOffsetMode = offset;
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
                if (i < Steps.Count && !Steps[i](dt)) Steps.RemoveAt(i);
            if (Steps.Count == 0) Timer.Stop();
        }

        public static float Toward(float value, float target, float amount) =>
            value < target ? Math.Min(target, value + amount) : Math.Max(target, value - amount);

        public static Color Mix(Color a, Color b, float t) =>
            Color.FromArgb((int)(a.A + (b.A - a.A) * t), (int)(a.R + (b.R - a.R) * t), (int)(a.G + (b.G - a.G) * t), (int)(a.B + (b.B - a.B) * t));

        public static float Range(float t, float from, float to) => Math.Max(0f, Math.Min(1f, (t - from) / (to - from)));

        // Hizli baslayip yavaslayarak duran egri
        public static float Ease(float k) => 1f - (1f - k) * (1f - k) * (1f - k);
    }
}
