using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Windows.Forms;

namespace LimbusTurkce
{
    // Ana tus oyunun kirmizi bileti
    // Solda TEAR yazan kocani var
    // Oyna'ya basinca kocan yirtilir ve oyun acilir
    //
    // Gorselin ortasindaki dar serit uzatilir
    // Boylece amblem ve kocan bozulmadan bilet genisler ve yaziya yer acilir
    class Ticket : Widget
    {
        // Biletin uzerindeki yerler 591 piksel yuksekligindeki olcuyle
        // Gorsel hangi boyda kaydedilirse kaydedilsin bu olcuye cevrilir
        const int SourceHeight = 591;
        const float Perforation = 300f;
        static readonly Padding Slice = new Padding(770, 0, 222, 0);

        // Bilet golgesi ve yazilari ekrandaki boyunda bir kez cizilip saklanir
        // Nefes ve isik her karede bu hazir resimleri karistirir
        // Buyuk gorseli ve yaziyi her karede yeniden cizmek bir cekirdegi dolduruyordu
        readonly Layer normalArt = new Layer(), brightArt = new Layer(), shadowArt = new Layer();
        readonly Layer label = new Layer(), litLabel = new Layer();

        void DrawImage(Graphics g, string name, Rectangle rect, float opacity)
        {
            if (opacity <= 0.003f) return;
            var layer = name == "bilet" ? normalArt : brightArt;
            var bitmap = layer.Get(name, rect.Size, bg =>
            {
                // Ortadaki dar serit uzar
                // Kocan amblem ve sag kenar oldugu gibi kalir
                var image = Art.Get(name);
                var s = image.Height / (float)SourceHeight;
                var slice = new Padding((int)Math.Round(Slice.Left * s), 0, (int)Math.Round(Slice.Right * s), 0);
                Art.DrawNine(bg, image, new Rectangle(Point.Empty, rect.Size), slice, rect.Height / (float)image.Height);
            });
            Put(g, bitmap, rect.Location, opacity);
        }

        // Kocan koparken resim donuyor
        // O zaman yumusak orneklemeyle cizilir
        static void Put(Graphics g, Bitmap bitmap, Point location, float opacity)
        {
            using (var matrix = g.Transform)
                if (matrix.IsIdentity) Art.Blend(g, bitmap, location, opacity);
                else Art.DrawFaded(g, bitmap, new Rectangle(location, bitmap.Size), opacity);
        }

        public override void Release()
        {
            normalArt.Dispose();
            brightArt.Dispose();
            shadowArt.Dispose();
            label.Dispose();
            litLabel.Dispose();
        }

        public string Caption = "";
        public string Detail = "";

        const float FadeSeconds = 0.2f;
        const float PulseSeconds = 1.9f;
        const float TearSeconds = 0.55f;
        const float StampSeconds = 2.2f;
        const float ShineSeconds = 1.1f;
        const float ShineEvery = 5.5f;

        bool pulse, shines;
        float? progress;
        float light, phase, tear = -1f, mend = -1f, stamp = -1f, shine = -1f, sweep;
        string stampText;
        Action torn;

        public Ticket() => Overflow = 40f;

        public override bool Clickable => true;
        public override string AccessibleName => (Caption + " " + Detail).Trim();

        // Oyuncudan bir sey bekleniyor
        public bool Pulse
        {
            get => pulse;
            set { if (pulse == value) return; pulse = value; phase = 0; Changed(); }
        }

        // Hazir beklerken arada bir uzerinden isik gecer
        public bool Shines
        {
            get => shines;
            set { if (shines == value) return; shines = value; Changed(); }
        }

        // null is yok
        // 0 ile 1 arasi ilerleme
        // eksi deger suresi belli olmayan is
        public float? Progress
        {
            get => progress;
            set
            {
                if (progress == value) return;
                progress = value;
                Animate();
                Invalidate();
            }
        }

        public void Set(string caption, string detail)
        {
            if (caption == Caption && detail == Detail) return;
            Caption = caption;
            Detail = detail;
            Invalidate();
        }

        // Kocani koparir
        // Animasyon bitince verilen is yapilir
        public void Tear(Action then)
        {
            if (tear >= 0f) return;
            torn = then;
            tear = 0f;
            mend = -1f;
            Animate();
        }

        // Pencereye donulunce kocan yerine geri gelir
        public void Mend()
        {
            if (tear < 0f) return;
            tear = -1f;
            mend = 0f;
            Animate();
        }

        public void Stamp(string text)
        {
            stampText = text;
            stamp = 0f;
            Animate();
        }

        bool Idle => Enabled && progress == null && tear < 0f;
        bool Pulsing => pulse && Idle && Visible;
        float Target => Enabled && Hovered && progress == null ? 1f : 0f;

        // Pencere arkadayken ya da kucukken nefes ve isik durur
        // Oyuncu donunce kaldigi yerden devam eder
        bool Active => Canvas.ContainsFocus && Canvas.WindowState != FormWindowState.Minimized;

        protected override void OnAttached()
        {
            Canvas.Activated += (s, e) => Animate();
            shineTimer = new Timer { Interval = (int)(ShineEvery * 1000) };
            shineTimer.Tick += (s, e) =>
            {
                if (!shines || !Idle || Pulsing || !Active || shine >= 0f) return;
                shine = 0f;
                Animate();
            };
            shineTimer.Start();
            Canvas.Disposed += (s, e) => shineTimer.Dispose();
        }

        Timer shineTimer;

        protected override bool Step(float dt)
        {
            var active = Active;
            light = Animator.Toward(light, Target, dt / FadeSeconds);
            if (Pulsing && active) phase = (phase + dt / PulseSeconds) % 1f;
            if (progress != null) sweep = (sweep + dt / 1.4f) % 1f;

            if (tear >= 0f && tear < 1f)
            {
                tear = Math.Min(1f, tear + dt / TearSeconds);
                // Oyunu baslatmak animasyon karesinin icinde yapilmaz
                // Kare bittikten sonra pencerenin kendi sirasinda calisir
                if (tear >= 1f && torn != null)
                {
                    var then = torn;
                    torn = null;
                    Canvas.BeginInvoke(then);
                }
            }
            if (mend >= 0f) { mend += dt / 0.35f; if (mend >= 1f) mend = -1f; }
            if (stamp >= 0f) { stamp += dt; if (stamp >= StampSeconds) stamp = -1f; }
            if (shine >= 0f) { shine += dt / ShineSeconds; if (shine >= 1f || !Idle) shine = -1f; }

            Invalidate();
            return light != Target || (Pulsing && active) || progress != null || (tear >= 0f && tear < 1f)
                || mend >= 0f || stamp >= 0f || shine >= 0f;
        }

        public override void Paint(Graphics g)
        {
            var lift = Canvas.Px(2f) * light;
            var bounds = Bounds;
            var rect = Canvas.Px(bounds);
            rect.Offset(0, -(int)Math.Round(lift));
            // Basinca bilet hafifce asagi iner
            // Boyu degismez ki hazir resim yeniden olceklenmesin
            if (Pressed && Enabled) rect.Offset(0, (int)Math.Round(Canvas.Px(2)));
            var k = rect.Height / (float)SourceHeight;

            DrawShadow(g, rect, light);
            var caption = progress != null && progress >= 0f ? "%" + (int)Math.Round(progress.Value * 100) : Caption;

            if (!Enabled && progress == null)
            {
                DrawArt(g, rect, k, 0.4f, 0f);
                var dim = label.Get("soluk|" + caption + "|" + Detail, rect.Size,
                    lg => DrawLabels(lg, new Rectangle(Point.Empty, rect.Size), k, caption, Color.FromArgb(150, Palette.Muted), Color.FromArgb(110, Palette.Muted)));
                Put(g, dim, rect.Location, 1f);
                return;
            }

            var breath = Pulsing ? 0.85f * (0.5f - 0.5f * (float)Math.Cos(phase * Math.PI * 2)) : 0f;
            var glow = Math.Max(light, breath);

            if (tear > 0f) PaintTorn(g, rect, k, glow);
            else
            {
                var appear = mend >= 0f ? Animator.Ease(mend) : 1f;
                if (appear < 1f)
                {
                    // Kocan geri gelirken sadece govde tam gorunur
                    var perforation = rect.Left + Perforation * k;
                    var state = g.Save();
                    g.SetClip(RectangleF.FromLTRB(perforation, rect.Top - 4, rect.Right + 4, rect.Bottom + 4));
                    DrawArt(g, rect, k, 1f, glow);
                    g.Restore(state);
                    state = g.Save();
                    g.SetClip(RectangleF.FromLTRB(rect.Left - 4, rect.Top - 4, perforation, rect.Bottom + 4));
                    DrawArt(g, rect, k, appear, glow * appear);
                    g.Restore(state);
                }
                else DrawArt(g, rect, k, 1f, glow);
            }

            if (progress != null) PaintProgress(g, rect, k);
            if (shine >= 0f) PaintShine(g, rect, k, shine);

            // Altin yazi parladikca beyaza doner
            // Iki hazir yazi resmi ust uste karisir
            var detail = Color.FromArgb(230, 255, 214, 180);
            var gold = label.Get("altin|" + caption + "|" + Detail, rect.Size,
                lg => DrawLabels(lg, new Rectangle(Point.Empty, rect.Size), k, caption, Palette.LightGold, detail));
            Put(g, gold, rect.Location, 1f);
            var white = progress != null ? 0f : glow;
            if (white > 0.01f)
                Put(g, litLabel.Get("beyaz|" + caption + "|" + Detail, rect.Size,
                    lg => DrawLabels(lg, new Rectangle(Point.Empty, rect.Size), k, caption, Color.White, detail)), rect.Location, white);
            if (stamp >= 0f) PaintStamp(g, rect, stamp);
        }

        void DrawArt(Graphics g, Rectangle rect, float k, float opacity, float glow)
        {
            DrawImage(g, "bilet", rect, opacity);
            if (glow > 0.01f) DrawImage(g, "bilet-parlak", rect, opacity * glow);
        }

        // Biletin altinda yumusak golge
        // Ust uste binen yari saydam kutular bulanik golge gibi gorunur
        // Bilet kalktikca golge asagi kayar
        void DrawShadow(Graphics g, Rectangle rect, float raise)
        {
            var margin = (int)Canvas.Px(12);
            var size = new Size(rect.Width + margin * 2, rect.Height + margin * 2);
            var bitmap = shadowArt.Get("golge", size, sg =>
            {
                var spread = Canvas.Px(4.5f);
                var area = new RectangleF(margin, margin, rect.Width, rect.Height);
                using (var brush = new SolidBrush(Color.FromArgb(16, 0, 0, 0)))
                    for (var i = 0; i < 7; i++)
                        sg.FillRectangle(brush, RectangleF.Inflate(area, -Canvas.Px(6) + i * spread / 3f, -Canvas.Px(8) + i * spread / 4f));
            });
            var drop = (int)Math.Round(Canvas.Px(5) + raise * Canvas.Px(3));
            Put(g, bitmap, new Point(rect.Left - margin, rect.Top - margin + drop), 1f);
        }

        void DrawLabels(Graphics g, Rectangle rect, float k, string caption, Color main, Color detail)
        {
            // Yazi amblemin sagindaki bosluga ortalanir
            var left = rect.Left + 772f * k;
            var right = rect.Right - 52f * k;
            var width = right - left;

            var size = 40f;
            Font font;
            while (true)
            {
                font = Canvas.Typeface("Bahnschrift SemiBold Condensed", size);
                if (size <= 18f || Lettering.TrackedWidth(g, caption, font, Canvas.Px(1.5f)) <= width) break;
                size -= 2f;
            }
            var small = Canvas.Typeface("Bahnschrift SemiBold", 11.5f);
            var hasDetail = !string.IsNullOrEmpty(Detail);
            var total = font.Height * 0.86f + (hasDetail ? small.Height + Canvas.Px(2) : 0);
            var top = rect.Top + (rect.Height - total) / 2f - Canvas.Px(1);
            var center = left + width / 2f;

            using (var shadow = new SolidBrush(Color.FromArgb(main.A * 160 / 255, 30, 4, 2)))
            {
                var spacing = Canvas.Px(1.5f);
                var offset = Canvas.Px(1.5f);
                Lettering.Tracked(g, caption, font, shadow.Color, new PointF(center + offset, top + offset), spacing, StringAlignment.Center);
                Lettering.Tracked(g, caption, font, main, new PointF(center, top), spacing, StringAlignment.Center);
            }
            if (hasDetail)
                Lettering.Draw(g, Detail, small, detail, new RectangleF(left - Canvas.Px(8), top + font.Height * 0.86f + Canvas.Px(2), width + Canvas.Px(16), small.Height + 2),
                    StringAlignment.Center);
        }

        // Koparilan kocan donup duser
        // Govde hafifce saga kayar
        void PaintTorn(Graphics g, Rectangle rect, float k, float glow)
        {
            var e = Animator.Ease(tear);
            var perforation = rect.Left + Perforation * k;
            var pad = Canvas.Px(30);

            var state = g.Save();
            g.TranslateTransform(Canvas.Px(5) * e, 0);
            g.SetClip(RectangleF.FromLTRB(perforation, rect.Top - pad, rect.Right + pad, rect.Bottom + pad));
            DrawArt(g, rect, k, 1f, glow);
            g.Restore(state);

            state = g.Save();
            g.TranslateTransform(perforation, rect.Bottom);
            g.RotateTransform(-16f * e);
            g.TranslateTransform(-perforation - Canvas.Px(8) * e, -rect.Bottom + Canvas.Px(16) * e * e);
            g.SetClip(RectangleF.FromLTRB(rect.Left - pad, rect.Top - pad, perforation, rect.Bottom + pad));
            DrawArt(g, rect, k, 1f - Animator.Range(tear, 0.45f, 1f), glow);
            g.Restore(state);
        }

        // Ilerledikce bilet soldan saga yanar
        void PaintProgress(Graphics g, Rectangle rect, float k)
        {
            if (progress >= 0f)
            {
                var edge = rect.Left + rect.Width * progress.Value;
                var state = g.Save();
                g.SetClip(RectangleF.FromLTRB(rect.Left - 2, rect.Top - 2, edge, rect.Bottom + 2));
                DrawImage(g, "bilet-parlak", rect, 1f);
                g.Restore(state);
                if (progress > 0f && progress < 1f)
                    using (var brush = new LinearGradientBrush(new RectangleF(edge - Canvas.Px(10), rect.Top, Canvas.Px(12), rect.Height),
                        Color.FromArgb(0, 255, 230, 170), Color.FromArgb(150, 255, 240, 200), LinearGradientMode.Horizontal))
                        g.FillRectangle(brush, edge - Canvas.Px(10), rect.Top + 14f * k, Canvas.Px(10), rect.Height - 28f * k);
            }
            // Suresi belli olmayan iste parlak bilet dar bir bantta soldan saga gecer
            else PaintBand(g, rect, k, sweep, 0.95f, 0.32f, 0.08f);
        }

        // Hazir beklerken biletin ustunden arada bir isik gecer
        void PaintShine(Graphics g, Rectangle rect, float k, float t) => PaintBand(g, rect, k, t, 0.8f, 0.1f, 0.05f);

        // Parlak bilet egik bir bandin icinde gorunur
        // Ic ice dort bant kenarlari yumusatir
        // Duz renk yerine gorselin kendisi parladigi icin doku bozulmaz
        void PaintBand(Graphics g, Rectangle rect, float k, float t, float width, float opacity, float step)
        {
            using (var clip = Inner(rect, k))
            {
                var inner = clip.GetBounds(g);
                for (var i = 0; i < 4; i++)
                {
                    var state = g.Save();
                    g.Clip = clip;
                    using (var band = Band(rect, inner, t, rect.Height * (width - i * width * 0.2f)))
                        g.SetClip(band, CombineMode.Intersect);
                    DrawImage(g, "bilet-parlak", rect, opacity + i * step);
                    g.Restore(state);
                }
            }
        }

        // Biletin ic kismi
        // Koselerdeki yirtik ve kocandaki oyuklar disarida kalir
        Region Inner(Rectangle rect, float k)
        {
            var inset = 26f * k;
            var region = new Region(RectangleF.FromLTRB(rect.Left + inset, rect.Top + inset, rect.Right - inset, rect.Bottom - inset));
            var notch = 46f * k;
            var perforation = rect.Left + Perforation * k;
            using (var hole = new GraphicsPath())
            {
                hole.AddEllipse(perforation - notch, rect.Top - notch, notch * 2, notch * 2);
                hole.AddEllipse(perforation - notch, rect.Bottom - notch, notch * 2, notch * 2);
                region.Exclude(hole);
            }
            return region;
        }

        // t 0 iken bant solda disarida 1 iken sagda disarida
        static GraphicsPath Band(Rectangle rect, RectangleF inner, float t, float width)
        {
            var skew = rect.Height * 0.45f;
            var travel = inner.Width + skew + width * 2f;
            var x = inner.Left - skew - width + travel * t;
            var path = new GraphicsPath();
            path.AddPolygon(new[]
            {
                new PointF(x + skew, rect.Top), new PointF(x + skew + width, rect.Top),
                new PointF(x + width, rect.Bottom), new PointF(x, rect.Bottom),
            });
            return path;
        }

        // Limbus muhru biletin ustune damga gibi iner
        void PaintStamp(Graphics g, Rectangle rect, float t)
        {
            const float Drop = 0.2f, FadeStart = 1.6f;
            float scale, alpha;
            if (t < Drop)
            {
                var k = t / Drop;
                scale = 1.9f - 0.9f * k * k;
                alpha = k;
            }
            else
            {
                // Yere vurunca kucuk bir sekme
                var bounce = t < Drop + 0.12f ? 0.06f * (float)Math.Sin((t - Drop) / 0.12f * Math.PI) : 0f;
                scale = 1f - bounce;
                alpha = t < FadeStart ? 1f : Math.Max(0f, 1f - (t - FadeStart) / (StampSeconds - FadeStart));
            }

            // Kirmizi bilette kirmizi murekkep kaybolur
            // Muhur koyu murekkeple yazi altinla basilir
            var ink = Color.FromArgb(34, 10, 6);
            var seal = Art.Get("muhur");
            var height = rect.Height * 1.05f;
            var width = height * seal.Width / seal.Height;
            var cx = rect.Left + rect.Width * 0.47f;
            var cy = rect.Top + rect.Height / 2f;

            var state = g.Save();
            g.TranslateTransform(cx, cy);
            g.RotateTransform(-9f);
            g.ScaleTransform(scale, scale);
            using (var attributes = new ImageAttributes())
            {
                // Muhrun sekli kalir ama rengi murekkebe boyanir
                attributes.SetColorMatrix(new ColorMatrix(new[]
                {
                    new float[] { 0, 0, 0, 0, 0 },
                    new float[] { 0, 0, 0, 0, 0 },
                    new float[] { 0, 0, 0, 0, 0 },
                    new float[] { 0, 0, 0, alpha * 0.7f, 0 },
                    new float[] { ink.R / 255f, ink.G / 255f, ink.B / 255f, 0, 1 },
                }));
                g.DrawImage(seal, new[]
                {
                    new PointF(-width / 2, -height / 2), new PointF(width / 2, -height / 2), new PointF(-width / 2, height / 2),
                }, new RectangleF(0, 0, seal.Width, seal.Height), GraphicsUnit.Pixel, attributes);
            }

            var font = Canvas.Typeface("Bahnschrift SemiBold Condensed", 26f);
            var size = Lettering.Measure(g, stampText, font);
            var box = new RectangleF(-size.Width / 2 - Canvas.Px(10), -size.Height / 2 + height * 0.04f, size.Width + Canvas.Px(20), size.Height + Canvas.Px(2));
            using (var shade = new SolidBrush(Color.FromArgb((int)(215 * alpha), 22, 7, 4)))
            using (var pen = new Pen(Color.FromArgb((int)(240 * alpha), Palette.LightGold), Canvas.Px(2.5f)))
            {
                // Lastik damga gibi yazinin etrafinda cerceve
                g.FillRectangle(shade, box);
                g.DrawRectangle(pen, box.X, box.Y, box.Width, box.Height);
            }
            Lettering.Draw(g, stampText, font, Color.FromArgb((int)(255 * alpha), Palette.LightGold), box, StringAlignment.Center, StringAlignment.Center);
            g.Restore(state);
        }
    }
}
