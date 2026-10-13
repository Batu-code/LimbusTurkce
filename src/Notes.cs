using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Globalization;
using System.Runtime.InteropServices;

namespace LimbusTurkce
{
    // Surum notlari
    // Klasorun icinde kayan bir liste
    // Ust ve alt kenarda yazi solarak kaybolur
    class NotesView : Widget
    {
        enum Kind { Header, Title, Heading, Bullet, Paragraph, Divider, Message }

        class Item
        {
            public Kind Kind;
            public string Text;
            public string Date;
            public bool Installed;
            public float Top, Height;
        }

        static readonly CultureInfo Turkish = new CultureInfo("tr-TR");

        const float BarWidth = 14f;
        const float Indent = 17f;
        const float Fade = 16f;

        readonly List<Item> items = new List<Item>();
        float content, scroll, target, laidOutWidth, laidOutZoom;
        float barLight;
        bool dragging;
        float dragOffset;
        Bitmap view;

        public NotesView() => Show(null, null, null);

        public void Show(IList<Release> releases, Version installed, string error)
        {
            items.Clear();
            if (releases == null || releases.Count == 0)
            {
                items.Add(new Item
                {
                    Kind = Kind.Message,
                    Text = error != null ? "Sürüm notları alınamadı.\n" + error
                        : releases == null ? "Sürüm notları yükleniyor..." : "Henüz yayımlanmış sürüm yok.",
                });
            }
            else
            {
                for (var i = 0; i < releases.Count; i++)
                {
                    var release = releases[i];
                    if (i > 0) items.Add(new Item { Kind = Kind.Divider });
                    items.Add(new Item
                    {
                        Kind = Kind.Header,
                        Text = release.Label,
                        Date = release.Date.ToLocalTime().ToString("d MMMM yyyy", Turkish),
                        Installed = release.Version == installed,
                    });
                    // Baslik surum numarasiyla basliyor
                    // Surum zaten ustte yaziyor
                    var name = release.Title.StartsWith(release.Label)
                        ? release.Title.Substring(release.Label.Length).TrimStart(' ', '·', '-', '—')
                        : release.Title;
                    if (name != "") items.Add(new Item { Kind = Kind.Title, Text = name });
                    foreach (var raw in release.Notes.Replace("\r", "").Split('\n'))
                    {
                        var line = raw.Replace("**", "").Trim();
                        if (line == "") continue;
                        if (line.StartsWith("#")) items.Add(new Item { Kind = Kind.Heading, Text = line.TrimStart('#', ' ') });
                        else if (line.StartsWith("- ") || line.StartsWith("* ")) items.Add(new Item { Kind = Kind.Bullet, Text = line.Substring(2) });
                        else items.Add(new Item { Kind = Kind.Paragraph, Text = line });
                    }
                }
            }
            laidOutWidth = 0;
            scroll = target = 0;
            Invalidate();
        }

        float Viewport => Bounds.Height;
        float MaxScroll => Math.Max(0f, content - Viewport);
        bool Scrollable => MaxScroll > 0.5f;
        float TextWidth => Bounds.Width - BarWidth;

        // Yazi olculeri ekran olcegine bagli
        // Olcek ya da genislik degisince yeniden olculur
        void Layout()
        {
            if (laidOutWidth == Bounds.Width && laidOutZoom == Canvas.Zoom) return;
            laidOutWidth = Bounds.Width;
            laidOutZoom = Canvas.Zoom;
            using (var bitmap = new Bitmap(1, 1))
            using (var g = Graphics.FromImage(bitmap))
            {
                CanvasForm.Prepare(g);
                var y = 4f;
                foreach (var item in items)
                {
                    item.Top = y;
                    item.Height = Measure(g, item);
                    y += item.Height;
                }
                content = y + 6f;
            }
            target = Math.Min(target, MaxScroll);
            scroll = Math.Min(scroll, MaxScroll);
        }

        Font FontFor(Kind kind)
        {
            switch (kind)
            {
                case Kind.Title: return Canvas.Typeface("Bahnschrift SemiBold Condensed", 21f);
                case Kind.Heading: return Canvas.Typeface("Bahnschrift SemiBold", 13f);
                case Kind.Message: return Canvas.Typeface("Bahnschrift", 13f);
                default: return Canvas.Typeface("Segoe UI", 13f);
            }
        }

        // Yukseklikler 96 dpi pikseli
        float Measure(Graphics g, Item item)
        {
            switch (item.Kind)
            {
                case Kind.Header: return 30f;
                case Kind.Divider: return 30f;
                case Kind.Bullet:
                    return Lettering.Measure(g, item.Text, FontFor(item.Kind), Canvas.Px(TextWidth - Indent)).Height / Canvas.Zoom + 6f;
                case Kind.Title:
                    return Lettering.Measure(g, item.Text, FontFor(item.Kind), Canvas.Px(TextWidth)).Height / Canvas.Zoom + 8f;
                case Kind.Message:
                    return Lettering.Measure(g, item.Text, FontFor(item.Kind), Canvas.Px(TextWidth)).Height / Canvas.Zoom + 8f;
                default:
                    return Lettering.Measure(g, item.Text, FontFor(item.Kind), Canvas.Px(TextWidth)).Height / Canvas.Zoom + 6f;
            }
        }

        public override Widget HitTest(PointF p)
        {
            if (!Visible || !Scrollable) return null;
            // Sadece kaydirma cubugu tutulabilir
            // Yazinin ustunden pencere tasinabilir
            return p.X >= Bounds.Right - BarWidth && p.X <= Bounds.Right + 2 && p.Y >= Bounds.Top && p.Y <= Bounds.Bottom ? this : null;
        }

        public override bool Wheel(int delta)
        {
            if (!Scrollable) return false;
            target = Math.Max(0f, Math.Min(MaxScroll, target - delta / 120f * 66f));
            Animate();
            return true;
        }

        public override void MouseDown(PointF p)
        {
            var thumb = Thumb();
            if (p.Y >= thumb.Top && p.Y <= thumb.Bottom) dragOffset = p.Y - thumb.Top;
            else
            {
                // Bos yere tiklayinca bir sayfa kayar
                target = Math.Max(0f, Math.Min(MaxScroll, target + (p.Y < thumb.Top ? -Viewport : Viewport) * 0.85f));
                dragOffset = thumb.Height / 2f;
                Animate();
            }
            dragging = true;
        }

        public override void MouseMove(PointF p)
        {
            if (!dragging) return;
            var track = Track();
            var thumb = Thumb();
            var room = track.Height - thumb.Height;
            if (room <= 0) return;
            var k = Math.Max(0f, Math.Min(1f, (p.Y - dragOffset - track.Top) / room));
            target = scroll = k * MaxScroll;
            Invalidate();
        }

        public override void MouseUp(PointF p) => dragging = false;

        protected override bool Step(float dt)
        {
            var light = Hovered || dragging ? 1f : 0f;
            barLight = Animator.Toward(barLight, light, dt / 0.15f);
            if (Math.Abs(target - scroll) < 0.3f) scroll = target;
            else scroll += (target - scroll) * Math.Min(1f, dt * 14f);
            Invalidate();
            return scroll != target || barLight != light;
        }

        RectangleF Track() => new RectangleF(Bounds.Right - 4f, Bounds.Top + 6f, 3f, Bounds.Height - 12f);

        RectangleF Thumb()
        {
            var track = Track();
            var height = Math.Max(28f, track.Height * Viewport / Math.Max(Viewport, content));
            var top = track.Top + (track.Height - height) * (MaxScroll > 0 ? scroll / MaxScroll : 0f);
            return new RectangleF(track.Left, top, track.Width, height);
        }

        public override void Paint(Graphics g)
        {
            Layout();
            var rect = Canvas.Px(Bounds);
            if (rect.Width <= 0 || rect.Height <= 0) return;
            if (view == null || view.Size != rect.Size)
            {
                view?.Dispose();
                view = new Bitmap(rect.Width, rect.Height, PixelFormat.Format32bppPArgb);
            }
            using (var vg = Graphics.FromImage(view))
            {
                vg.Clear(Color.Transparent);
                CanvasForm.Prepare(vg);
                foreach (var item in items)
                {
                    var top = item.Top - scroll;
                    if (top + item.Height < 0 || top > Viewport) continue;
                    PaintItem(vg, item, Canvas.Px(top));
                }
            }
            FadeEdges(view, scroll > 0.5f, scroll < MaxScroll - 0.5f);
            g.DrawImageUnscaled(view, rect.Location);
            if (Scrollable) PaintBar(g);
        }

        void PaintItem(Graphics g, Item item, float top)
        {
            var width = Canvas.Px(TextWidth);
            switch (item.Kind)
            {
                case Kind.Header:
                    PaintHeader(g, item, top + Canvas.Px(4));
                    break;
                case Kind.Title:
                    Lettering.Draw(g, item.Text, FontFor(item.Kind), Palette.Gold, new RectangleF(0, top, width, Canvas.Px(item.Height)), wrap: true);
                    break;
                case Kind.Heading:
                    Lettering.Draw(g, item.Text, FontFor(item.Kind), Palette.LightGold, new RectangleF(0, top, width, Canvas.Px(item.Height)), wrap: true);
                    break;
                case Kind.Bullet:
                {
                    var font = FontFor(item.Kind);
                    // Oyundaki gibi kucuk turuncu elmas
                    var size = Canvas.Px(5.5f);
                    var cx = Canvas.Px(4.5f);
                    var cy = top + font.Height / 2f + Canvas.Px(0.5f);
                    using (var brush = new SolidBrush(Palette.Orange))
                        g.FillPolygon(brush, new[] { new PointF(cx, cy - size / 2), new PointF(cx + size / 2, cy), new PointF(cx, cy + size / 2), new PointF(cx - size / 2, cy) });
                    var indent = Canvas.Px(Indent);
                    Lettering.Draw(g, item.Text, font, Palette.Ink, new RectangleF(indent, top, width - indent, Canvas.Px(item.Height)), wrap: true);
                    break;
                }
                case Kind.Paragraph:
                    Lettering.Draw(g, item.Text, FontFor(item.Kind), Palette.Ink, new RectangleF(0, top, width, Canvas.Px(item.Height)), wrap: true);
                    break;
                case Kind.Message:
                    Lettering.Draw(g, item.Text, FontFor(item.Kind), Palette.Muted, new RectangleF(0, top + Canvas.Px(8), width, Canvas.Px(item.Height)), wrap: true);
                    break;
                case Kind.Divider:
                {
                    var y = top + Canvas.Px(item.Height) / 2f;
                    Art.DrawFaded(g, Art.Get("ayrac"), Rectangle.Round(new RectangleF(0, y, width, Math.Max(1f, Canvas.Px(1.5f)))), 0.9f);
                    break;
                }
            }
        }

        // Surum numarasi kirmizi seritte
        // Kurulu surumun yaninda altin cerceveli etiket
        void PaintHeader(Graphics g, Item item, float top)
        {
            var tagFont = Canvas.Typeface("Bahnschrift SemiBold", 12f);
            var spacing = Canvas.Px(1.2f);
            var height = Canvas.Px(20f);
            var tagWidth = Lettering.TrackedWidth(g, item.Text, tagFont, spacing) + Canvas.Px(30);
            Art.DrawFaded(g, Art.Get("kirmizi-serit"), Rectangle.Round(new RectangleF(-Canvas.Px(4), top, tagWidth + Canvas.Px(8), height)), 1f);
            Lettering.Tracked(g, item.Text, tagFont, Color.FromArgb(255, 240, 228), new PointF(Canvas.Px(11), top + (height - tagFont.Height) / 2f + Canvas.Px(1)), spacing);

            var dateFont = Canvas.Typeface("Bahnschrift", 12.5f);
            Lettering.Draw(g, item.Date, dateFont, Palette.Muted, new RectangleF(tagWidth + Canvas.Px(10), top, Canvas.Px(200), height), line: StringAlignment.Center);

            if (!item.Installed) return;
            var small = Canvas.Typeface("Bahnschrift SemiBold", 10f);
            var label = "KURULU";
            var labelSpacing = Canvas.Px(1.6f);
            var labelWidth = Lettering.TrackedWidth(g, label, small, labelSpacing);
            var box = new RectangleF(Canvas.Px(TextWidth) - labelWidth - Canvas.Px(18), top + Canvas.Px(1.5f), labelWidth + Canvas.Px(16), height - Canvas.Px(3));
            using (var fill = new SolidBrush(Color.FromArgb(40, Palette.Gold)))
            using (var pen = new Pen(Color.FromArgb(200, Palette.Gold), Math.Max(1f, Canvas.Zoom)))
            {
                g.FillRectangle(fill, box);
                g.DrawRectangle(pen, box.X, box.Y, box.Width, box.Height);
            }
            Lettering.Tracked(g, label, small, Palette.Gold, new PointF(box.Left + Canvas.Px(8), box.Top + (box.Height - small.Height) / 2f + Canvas.Px(1)), labelSpacing);
        }

        void PaintBar(Graphics g)
        {
            var track = Canvas.Px(Track());
            var thumb = Canvas.Px(Thumb());
            var x = track.Left + track.Width / 2f;
            using (var pen = new Pen(Color.FromArgb(70, Palette.Muted), Math.Max(1f, Canvas.Zoom)))
                g.DrawLine(pen, x, track.Top, x, track.Bottom);
            using (var pen = new Pen(Animator.Mix(Color.FromArgb(150, Palette.Gold), Palette.LightGold, barLight), Canvas.Px(3f)) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                g.DrawLine(pen, x, thumb.Top + Canvas.Px(2), x, thumb.Bottom - Canvas.Px(2));
        }

        // Kenar satirlarinin saydamligi asagi yukari azaltilir
        // Resim onceden carpilmis alfa oldugu icin dort kanal ayni oranla carpilir
        void FadeEdges(Bitmap bitmap, bool top, bool bottom)
        {
            if (!top && !bottom) return;
            var fade = (int)Math.Min(bitmap.Height / 3f, Canvas.Px(Fade));
            if (fade <= 0) return;
            var data = bitmap.LockBits(new Rectangle(0, 0, bitmap.Width, bitmap.Height), ImageLockMode.ReadWrite, PixelFormat.Format32bppPArgb);
            try
            {
                var row = new byte[bitmap.Width * 4];
                for (var i = 0; i < fade; i++)
                {
                    var k = (i + 0.5f) / fade;
                    k = k * k * (3f - 2f * k);
                    if (top) Scale(data, i, row, k);
                    if (bottom) Scale(data, bitmap.Height - 1 - i, row, k);
                }
            }
            finally { bitmap.UnlockBits(data); }
        }

        static void Scale(BitmapData data, int y, byte[] row, float k)
        {
            var address = data.Scan0 + y * data.Stride;
            Marshal.Copy(address, row, 0, row.Length);
            for (var i = 0; i < row.Length; i++) row[i] = (byte)(row[i] * k);
            Marshal.Copy(row, 0, address, row.Length);
        }
    }
}
