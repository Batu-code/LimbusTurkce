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
        public static Font Font(string family, float size, FontStyle style = FontStyle.Regular)
        {
            var exists = FontFamily.Families.Any(f => f.Name == family);
            return new Font(exists ? family : "Segoe UI", size, style);
        }
    }

    // Oyunun tus gorselleriyle cizilen tus
    // Uzerine gelince parlak gorsele gecer
    class ArtButton : Control
    {
        public string Normal = "tus";
        public string Hover = "tus-parlak";
        public int Slice = 10;
        public float SliceScale = 0.45f;
        public Color TextColor = Color.FromArgb(237, 226, 210);
        public Color HoverTextColor = Color.White;

        bool hovered, pressed;

        public ArtButton()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                | ControlStyles.SupportsTransparentBackColor | ControlStyles.ResizeRedraw, true);
            BackColor = Color.Transparent;
            Cursor = Cursors.Hand;
            AccessibleRole = AccessibleRole.PushButton;
        }

        protected override void OnTextChanged(EventArgs e) { base.OnTextChanged(e); Invalidate(); }
        protected override void OnEnabledChanged(EventArgs e) { base.OnEnabledChanged(e); hovered = pressed = false; Invalidate(); }
        protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); hovered = true; Invalidate(); }
        protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); hovered = pressed = false; Invalidate(); }
        protected override void OnMouseDown(MouseEventArgs e) { base.OnMouseDown(e); if (e.Button == MouseButtons.Left) { pressed = true; Invalidate(); } }
        protected override void OnMouseUp(MouseEventArgs e) { base.OnMouseUp(e); pressed = false; Invalidate(); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            var lit = Enabled && hovered;
            var scale = SliceScale * DeviceDpi / 96f;
            var rect = new Rectangle(0, 0, Width, Height);
            if (pressed) rect.Inflate(-1, -1);
            Art.DrawNine(g, Art.Get(lit ? Hover : Normal), rect, Slice, scale, Enabled ? 1f : 0.45f);

            var color = !Enabled ? Color.FromArgb(120, TextColor) : lit ? HoverTextColor : TextColor;
            TextRenderer.DrawText(g, Text, Font, rect, color,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
        }
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
