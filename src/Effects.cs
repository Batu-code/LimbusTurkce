using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace LimbusTurkce
{
    // Basliktaki otobus tekerlegi
    // Is surerken hizlanip doner
    // Is bitince yavaslayip durur
    class Wheel : Control
    {
        const float TopSpeed = 260f;
        const float SpeedUp = 420f;
        const float SlowDown = 150f;

        float angle, speed;
        bool spinning;
        readonly Func<float, bool> step;

        public Wheel()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                | ControlStyles.SupportsTransparentBackColor | ControlStyles.ResizeRedraw, true);
            BackColor = Color.Transparent;
            step = Step;
        }

        public bool Spinning
        {
            get => spinning;
            set { if (spinning == value) return; spinning = value; Animator.Run(step); }
        }

        bool Step(float dt)
        {
            if (IsDisposed) return false;
            speed = Animator.Toward(speed, spinning ? TopSpeed : 0f, dt * (spinning ? SpeedUp : SlowDown));
            angle = (angle + speed * dt) % 360f;
            Invalidate();
            return spinning || speed > 0f;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.TranslateTransform(Width / 2f, Height / 2f);
            g.RotateTransform(angle);
            g.DrawImage(Art.Get("teker"), -Width / 2f, -Height / 2f, Width, Height);
        }
    }

    // Kurulum bitince pencerenin ustune inen Limbus muhru
    // Ayri ve saydam bir pencere cunku WinForms'ta kardes denetimlerin ustune yari saydam cizilemiyor
    // Tiklamalar icinden gecer ve odagi calmaz
    class StampOverlay : Form
    {
        public const float Duration = 1.6f;
        static readonly Color Ink = Color.FromArgb(232, 84, 60);

        readonly string text;
        readonly Func<float, bool> step;
        float time;

        StampOverlay(Form owner, Rectangle screenBounds, string text)
        {
            this.text = text;
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            Bounds = screenBounds;
            Owner = owner;
            step = Step;
        }

        public static void Play(Form owner, Rectangle screenBounds, string text)
        {
            var overlay = new StampOverlay(owner, screenBounds, text);
            overlay.Show(owner);
            overlay.Render();
            Animator.Run(overlay.step);
        }

        protected override CreateParams CreateParams
        {
            get
            {
                const int Layered = 0x80000, Transparent = 0x20, ToolWindow = 0x80, NoActivate = 0x8000000;
                var cp = base.CreateParams;
                cp.ExStyle |= Layered | Transparent | ToolWindow | NoActivate;
                return cp;
            }
        }

        protected override bool ShowWithoutActivation => true;

        bool Step(float dt)
        {
            if (IsDisposed) return false;
            time += dt;
            if (time >= Duration || Owner == null || Owner.IsDisposed)
            {
                Close();
                return false;
            }
            Render();
            return true;
        }

        void Render()
        {
            using (var frame = Draw(Width, Height, time, text, DeviceDpi / 96f))
                Present(frame);
        }

        // Testte kare kare cizip bakabilmek icin pencereden ayri
        public static Bitmap Draw(int width, int height, float t, string text, float dpiScale)
        {
            // Ilk 0.2 saniye buyukten inip oturur
            // Sonra durur ve son yarim saniyede soner
            const float Drop = 0.2f, FadeStart = 1.1f;
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
                alpha = t < FadeStart ? 1f : Math.Max(0f, 1f - (t - FadeStart) / (Duration - FadeStart));
            }

            var bmp = new Bitmap(width, height, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(bmp))
            using (var attributes = new ImageAttributes())
            {
                g.Clear(Color.Transparent);
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;

                // Muhrun sekli kalir ama rengi damga kirmizisina boyanir
                attributes.SetColorMatrix(new ColorMatrix(new[]
                {
                    new float[] { 0, 0, 0, 0, 0 },
                    new float[] { 0, 0, 0, 0, 0 },
                    new float[] { 0, 0, 0, 0, 0 },
                    new float[] { 0, 0, 0, alpha * 0.9f, 0 },
                    new float[] { Ink.R / 255f, Ink.G / 255f, Ink.B / 255f, 0, 1 },
                }));

                var seal = Art.Get("muhur");
                var sealHeight = height * 0.72f;
                var sealWidth = sealHeight * seal.Width / seal.Height;
                g.TranslateTransform(width / 2f, height / 2f);
                g.RotateTransform(-9f);
                g.ScaleTransform(scale, scale);
                g.DrawImage(seal, new[]
                {
                    new PointF(-sealWidth / 2, -sealHeight / 2 - height * 0.06f),
                    new PointF(sealWidth / 2, -sealHeight / 2 - height * 0.06f),
                    new PointF(-sealWidth / 2, sealHeight / 2 - height * 0.06f),
                }, new RectangleF(0, 0, seal.Width, seal.Height), GraphicsUnit.Pixel, attributes);

                using (var font = Art.Font("Bahnschrift SemiBold Condensed", 26f * dpiScale, FontStyle.Regular))
                using (var brush = new SolidBrush(Color.FromArgb((int)(255 * alpha), Ink)))
                using (var pen = new Pen(Color.FromArgb((int)(230 * alpha), Ink), 3f * dpiScale))
                using (var shade = new SolidBrush(Color.FromArgb((int)(150 * alpha), 20, 8, 6)))
                {
                    var size = g.MeasureString(text, font);
                    var box = new RectangleF(-size.Width / 2 - 10 * dpiScale, height * 0.26f, size.Width + 20 * dpiScale, size.Height);
                    // Lastik damga gibi yazinin etrafinda cerceve
                    g.FillRectangle(shade, box);
                    g.DrawRectangle(pen, box.X, box.Y, box.Width, box.Height);
                    g.DrawString(text, font, brush, -size.Width / 2, box.Y);
                }
            }
            return bmp;
        }

        // Yari saydam pencere icin piksel basina alfa
        void Present(Bitmap frame)
        {
            var screen = GetDC(IntPtr.Zero);
            var memory = CreateCompatibleDC(screen);
            var bitmap = frame.GetHbitmap(Color.FromArgb(0));
            var old = SelectObject(memory, bitmap);
            try
            {
                var size = new NativeSize { cx = frame.Width, cy = frame.Height };
                var source = new NativePoint();
                var position = new NativePoint { x = Left, y = Top };
                var blend = new Blend { BlendOp = 0, BlendFlags = 0, SourceConstantAlpha = 255, AlphaFormat = 1 };
                UpdateLayeredWindow(Handle, screen, ref position, ref size, memory, ref source, 0, ref blend, 2);
            }
            finally
            {
                SelectObject(memory, old);
                DeleteObject(bitmap);
                DeleteDC(memory);
                ReleaseDC(IntPtr.Zero, screen);
            }
        }

        [StructLayout(LayoutKind.Sequential)] struct NativePoint { public int x, y; }
        [StructLayout(LayoutKind.Sequential)] struct NativeSize { public int cx, cy; }
        [StructLayout(LayoutKind.Sequential, Pack = 1)] struct Blend { public byte BlendOp, BlendFlags, SourceConstantAlpha, AlphaFormat; }

        [DllImport("user32.dll")] static extern bool UpdateLayeredWindow(IntPtr hwnd, IntPtr hdcDst, ref NativePoint pptDst, ref NativeSize psize, IntPtr hdcSrc, ref NativePoint pprSrc, int crKey, ref Blend pblend, int dwFlags);
        [DllImport("user32.dll")] static extern IntPtr GetDC(IntPtr hWnd);
        [DllImport("user32.dll")] static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);
        [DllImport("gdi32.dll")] static extern IntPtr CreateCompatibleDC(IntPtr hDC);
        [DllImport("gdi32.dll")] static extern bool DeleteDC(IntPtr hdc);
        [DllImport("gdi32.dll")] static extern IntPtr SelectObject(IntPtr hDC, IntPtr hObject);
        [DllImport("gdi32.dll")] static extern bool DeleteObject(IntPtr hObject);
    }
}
