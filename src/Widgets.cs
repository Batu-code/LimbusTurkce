using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Linq;
using System.Windows.Forms;

namespace LimbusTurkce
{
    // Renkler oyunun pencere ve tus gorsellerinden olculdu
    static class Palette
    {
        public static readonly Color Back = Color.FromArgb(14, 12, 11);
        public static readonly Color Gold = Color.FromArgb(240, 178, 72);
        public static readonly Color LightGold = Color.FromArgb(255, 226, 160);
        public static readonly Color Orange = Color.FromArgb(255, 132, 0);
        public static readonly Color Red = Color.FromArgb(232, 84, 60);
        public static readonly Color Ink = Color.FromArgb(239, 222, 200);
        public static readonly Color Muted = Color.FromArgb(170, 145, 120);
        public static readonly Color Dim = Color.FromArgb(112, 96, 82);
        public static readonly Color Line = Color.FromArgb(214, 178, 132);
    }

    // Yazi cizme yardimcilari
    // Butun yazilar GDI+ ile ve gri tonlu yumusatmayla
    // Saydam zemine ve yari saydam renge boyle dogru ciziliyor
    static class Lettering
    {
        public static StringFormat Format(StringAlignment align, StringAlignment line, bool wrap, bool path = false)
        {
            var format = (StringFormat)StringFormat.GenericTypographic.Clone();
            format.Alignment = align;
            format.LineAlignment = line;
            format.Trimming = path ? StringTrimming.EllipsisPath : StringTrimming.EllipsisCharacter;
            if (wrap) format.FormatFlags &= ~StringFormatFlags.NoWrap;
            else format.FormatFlags |= StringFormatFlags.NoWrap;
            format.FormatFlags |= StringFormatFlags.MeasureTrailingSpaces;
            return format;
        }

        public static void Draw(Graphics g, string text, Font font, Color color, RectangleF rect,
            StringAlignment align = StringAlignment.Near, StringAlignment line = StringAlignment.Near, bool wrap = false, bool path = false)
        {
            if (string.IsNullOrEmpty(text) || color.A == 0) return;
            using (var brush = new SolidBrush(color))
            using (var format = Format(align, line, wrap, path))
                g.DrawString(text, font, brush, rect, format);
        }

        // Harf araligi acik yazi
        // Oyundaki kucuk basliklar boyle
        public static void Tracked(Graphics g, string text, Font font, Color color, PointF origin, float spacing, StringAlignment align = StringAlignment.Near)
        {
            if (string.IsNullOrEmpty(text) || color.A == 0) return;
            var widths = Widths(g, text, font, spacing, out var total);
            var x = align == StringAlignment.Center ? origin.X - total / 2f : align == StringAlignment.Far ? origin.X - total : origin.X;
            using (var brush = new SolidBrush(color))
                for (var i = 0; i < text.Length; i++)
                {
                    if (text[i] != ' ') g.DrawString(text[i].ToString(), font, brush, x, origin.Y, StringFormat.GenericTypographic);
                    x += widths[i] + spacing;
                }
        }

        public static float TrackedWidth(Graphics g, string text, Font font, float spacing)
        {
            Widths(g, text, font, spacing, out var total);
            return total;
        }

        // Harf genislikleri bir kez olculup saklanir
        // Animasyonlu tuslar her karede ayni yaziyi yeniden olcmesin
        static readonly Dictionary<string, float[]> WidthCache = new Dictionary<string, float[]>();

        static float[] Widths(Graphics g, string text, Font font, float spacing, out float total)
        {
            var key = font.Name + "|" + font.Size + "|" + (int)font.Style + "|" + text;
            if (!WidthCache.TryGetValue(key, out var widths))
            {
                widths = new float[text.Length];
                for (var i = 0; i < text.Length; i++)
                    widths[i] = text[i] == ' ' ? font.Size * 0.32f : g.MeasureString(text[i].ToString(), font, PointF.Empty, StringFormat.GenericTypographic).Width;
                if (WidthCache.Count > 2000) WidthCache.Clear();
                WidthCache[key] = widths;
            }
            total = widths.Sum() + spacing * Math.Max(0, text.Length - 1);
            return widths;
        }

        public static SizeF Measure(Graphics g, string text, Font font, float width = 0)
        {
            using (var format = Format(StringAlignment.Near, StringAlignment.Near, width > 0))
                return width > 0 ? g.MeasureString(text, font, (int)Math.Ceiling(width), format) : g.MeasureString(text, font, PointF.Empty, format);
        }
    }

    // Bir kez cizilip saklanan resim
    // Anahtari ya da boyu degisince yeniden cizilir
    // Animasyonlu ogeler her karede bunlari ust uste koyar
    class Layer : IDisposable
    {
        Bitmap bitmap;
        string key;

        public Bitmap Get(string newKey, Size size, Action<Graphics> draw)
        {
            if (bitmap != null && key == newKey && bitmap.Size == size) return bitmap;
            bitmap?.Dispose();
            bitmap = new Bitmap(Math.Max(1, size.Width), Math.Max(1, size.Height), PixelFormat.Format32bppPArgb);
            using (var g = Graphics.FromImage(bitmap))
            {
                CanvasForm.Prepare(g);
                draw(g);
            }
            key = newKey;
            return bitmap;
        }

        public void Dispose()
        {
            bitmap?.Dispose();
            bitmap = null;
        }
    }

    // Duz yazi
    class Label : Widget
    {
        public string Value = "";
        public string Family = "Bahnschrift";
        public float Size = 13f;
        public FontStyle Style = FontStyle.Regular;
        public Color Color = Palette.Ink;
        public float Tracking;
        public StringAlignment Align = StringAlignment.Near;
        public StringAlignment LineAlign = StringAlignment.Near;
        public bool Wrap;
        public bool PathEllipsis;

        public void Set(string value, Color? color = null)
        {
            value = value ?? "";
            if (value == Value && (color == null || color.Value == Color)) return;
            Value = value;
            if (color != null) Color = color.Value;
            Invalidate();
        }

        public override void Paint(Graphics g)
        {
            var font = Canvas.Typeface(Family, Size, Style);
            if (Tracking > 0)
            {
                var r = Canvas.Px(Bounds);
                var x = Align == StringAlignment.Center ? r.Left + r.Width / 2f : Align == StringAlignment.Far ? r.Right : r.Left;
                Lettering.Tracked(g, Value, font, Color, new PointF(x, r.Top), Canvas.Px(Tracking), Align);
                return;
            }
            Lettering.Draw(g, Value, font, Color, Canvas.Px(Bounds), Align, LineAlign, Wrap, PathEllipsis);
        }
    }

    // Tiklanabilir yazi
    class Link : Label
    {
        public Color HoverColor = Palette.LightGold;
        float light;

        public override bool Clickable => true;
        public override string AccessibleName => Value;

        protected override bool Step(float dt)
        {
            var target = Hovered && Enabled ? 1f : 0f;
            light = Animator.Toward(light, target, dt / 0.15f);
            Invalidate();
            return light != target;
        }

        public override void Paint(Graphics g)
        {
            var color = Color;
            Color = Animator.Mix(color, HoverColor, light);
            base.Paint(g);
            Color = color;
            if (light <= 0.01f) return;
            // Uzerine gelince ince alt cizgi
            var font = Canvas.Typeface(Family, Size, Style);
            var width = Tracking > 0 ? Lettering.TrackedWidth(g, Value, font, Canvas.Px(Tracking)) : Lettering.Measure(g, Value, font).Width;
            var r = Canvas.Px(Bounds);
            var y = r.Top + font.Height + Canvas.Px(1);
            using (var pen = new Pen(Color.FromArgb((int)(200 * light), HoverColor), Math.Max(1f, Canvas.Zoom)))
                g.DrawLine(pen, r.Left, y, r.Left + width * light, y);
        }
    }

    // Oyunun tus gorselleriyle cizilen tus
    // Uzerine gelince parlak gorsel yumusakca yanar
    // Secili tus hep yanik kalir
    // Pulse acikken yavasca nefes alir
    class ArtButton : Widget
    {
        public string Normal = "secim";
        public string Hover = "secim-parlak";
        public Padding Slice = new Padding(34);
        public float SliceScale = 0.3f;
        public string Caption = "";
        public string Family = "Bahnschrift SemiBold";
        public float Size = 12f;
        public float Tracking = 1.2f;
        public Color TextColor = Palette.Ink;
        public Color HoverTextColor = Color.White;
        // Sol basta kucuk elmas
        // Secim tuslarinda hangisinin acik oldugunu gosterir
        public bool Diamond;

        const float FadeSeconds = 0.18f;
        const float PulseSeconds = 1.8f;

        bool selected, pulse;
        float light, phase;

        public override bool Clickable => true;
        public override string AccessibleName => Caption;

        public bool Selected
        {
            get => selected;
            set { if (selected == value) return; selected = value; Changed(); }
        }

        public bool Pulse
        {
            get => pulse;
            set { if (pulse == value) return; pulse = value; phase = 0; Changed(); }
        }

        public void SetCaption(string caption)
        {
            if (caption == Caption) return;
            Caption = caption;
            Invalidate();
        }

        float Target => Enabled && (Hovered || selected) ? 1f : 0f;
        bool Pulsing => pulse && Enabled && Visible;

        protected override bool Step(float dt)
        {
            light = Animator.Toward(light, Target, dt / FadeSeconds);
            if (Pulsing) phase = (phase + dt / PulseSeconds) % 1f;
            Invalidate();
            return light != Target || Pulsing;
        }

        // Tus zemini ve yazisi hazir resim olarak saklanir
        // Isik yanarken sadece bu resimler karisir
        readonly Layer plate = new Layer(), litPlate = new Layer(), words = new Layer(), litWords = new Layer();

        public override void Release()
        {
            plate.Dispose();
            litPlate.Dispose();
            words.Dispose();
            litWords.Dispose();
        }

        public override void Paint(Graphics g)
        {
            var rect = Canvas.Px(Bounds);
            // Basinca tus bir piksel iner
            // Boyu degismez ki hazir resimler yeniden cizilmesin
            if (Pressed) rect.Offset(0, Math.Max(1, (int)Canvas.Zoom));
            var size = rect.Size;
            var scale = SliceScale * Canvas.Zoom;
            var normal = plate.Get(Normal, size, pg => Art.DrawNine(pg, Art.Get(Normal), new Rectangle(Point.Empty, size), Slice, scale));
            if (!Enabled)
            {
                Art.Blend(g, normal, rect.Location, 0.5f);
                Art.Blend(g, CaptionImage(words, size, Color.FromArgb(120, Palette.Muted), false), rect.Location, 1f);
                return;
            }
            var breath = Pulsing ? 0.75f * (0.5f - 0.5f * (float)Math.Cos(phase * Math.PI * 2)) : 0f;
            var glow = Math.Max(light, breath);
            Art.Blend(g, normal, rect.Location, 1f);
            if (glow > 0.01f)
                Art.Blend(g, litPlate.Get(Hover, size, pg => Art.DrawNine(pg, Art.Get(Hover), new Rectangle(Point.Empty, size), Slice, scale)), rect.Location, glow);
            Art.Blend(g, CaptionImage(words, size, TextColor, false), rect.Location, 1f);
            if (glow > 0.01f) Art.Blend(g, CaptionImage(litWords, size, HoverTextColor, true), rect.Location, glow);
        }

        Bitmap CaptionImage(Layer layer, Size size, Color color, bool lit) =>
            layer.Get(Caption + "|" + color.ToArgb() + "|" + selected + "|" + lit, size, cg => DrawCaption(cg, new Rectangle(Point.Empty, size), color, lit));

        // Secili olmayan secenegin elmasi soluk
        // Isik yandikca belirginlesir
        void DrawCaption(Graphics g, Rectangle rect, Color color, bool lit)
        {
            var font = Canvas.Typeface(Family, Size);
            var spacing = Canvas.Px(Tracking);
            var width = Lettering.TrackedWidth(g, Caption, font, spacing);
            var diamond = Diamond ? Canvas.Px(10.5f) : 0f;
            var gap = Diamond ? Canvas.Px(8) : 0f;
            var x = rect.Left + (rect.Width - width - diamond - gap) / 2f;
            var y = rect.Top + (rect.Height - font.Height) / 2f + Canvas.Px(1);
            if (Diamond)
            {
                var cy = rect.Top + rect.Height / 2f;
                var image = Art.Get(selected ? "elmas" : "elmas-bos");
                Art.DrawFaded(g, image, Rectangle.Round(new RectangleF(x, cy - diamond / 2f, diamond, diamond)), selected || lit ? 1f : 0.55f);
            }
            Lettering.Tracked(g, Caption, font, color, new PointF(x + diamond + gap, y), spacing);
        }
    }

    // Baslik cubugundaki kucuk tuslar
    class ChromeButton : Widget
    {
        public enum Glyph { Minimize, Close, Gear, Back }
        public Glyph Shape;
        float light;
        bool on;

        public override bool Clickable => true;

        // Ayarlar acikken disli yanik kalir
        public bool On
        {
            get => on;
            set { if (on == value) return; on = value; Changed(); }
        }

        protected override bool Step(float dt)
        {
            var target = Hovered && Enabled ? 1f : 0f;
            light = Animator.Toward(light, target, dt / 0.15f);
            Invalidate();
            return light != target;
        }

        public override void Paint(Graphics g)
        {
            var r = Canvas.Px(Bounds);
            var accent = Shape == Glyph.Close ? Palette.Red : Palette.Gold;
            if (light > 0.01f)
                using (var brush = new SolidBrush(Color.FromArgb((int)(38 * light), accent)))
                    g.FillRectangle(brush, r);
            var color = Animator.Mix(on ? Palette.Gold : Palette.Muted, Shape == Glyph.Close ? Color.FromArgb(255, 210, 200) : Palette.LightGold, light);
            using (var pen = new Pen(color, Math.Max(1.2f, 1.4f * Canvas.Zoom)) { StartCap = LineCap.Round, EndCap = LineCap.Round })
            {
                var cx = r.Left + r.Width / 2f;
                var cy = r.Top + r.Height / 2f;
                var s = Canvas.Px(5);
                switch (Shape)
                {
                    case Glyph.Minimize:
                        g.DrawLine(pen, cx - s, cy + 1, cx + s, cy + 1);
                        break;
                    case Glyph.Close:
                        g.DrawLine(pen, cx - s, cy - s, cx + s, cy + s);
                        g.DrawLine(pen, cx - s, cy + s, cx + s, cy - s);
                        break;
                    case Glyph.Back:
                        g.DrawLine(pen, cx + s * 0.4f, cy - s, cx - s * 0.6f, cy);
                        g.DrawLine(pen, cx - s * 0.6f, cy, cx + s * 0.4f, cy + s);
                        break;
                    case Glyph.Gear:
                        DrawGear(g, pen, cx, cy, s * 1.15f);
                        break;
                }
            }
        }

        // Sekiz disli cark
        static void DrawGear(Graphics g, Pen pen, float cx, float cy, float radius)
        {
            using (var path = new GraphicsPath())
            {
                const int teeth = 8;
                var points = new List<PointF>();
                for (var i = 0; i < teeth * 4; i++)
                {
                    var a = (i / (teeth * 4f)) * Math.PI * 2 - Math.PI / 2;
                    var outer = (i % 4 == 1 || i % 4 == 2) ? radius : radius * 0.74f;
                    points.Add(new PointF(cx + (float)Math.Cos(a) * outer, cy + (float)Math.Sin(a) * outer));
                }
                path.AddPolygon(points.ToArray());
                g.DrawPath(pen, path);
                var hole = radius * 0.32f;
                g.DrawEllipse(pen, cx - hole, cy - hole, hole * 2, hole * 2);
            }
        }
    }

    // Basliktaki otobus tekerlegi
    // Is surerken hizlanip doner
    // Is bitince yavaslayip durur
    class Wheel : Widget
    {
        const float TopSpeed = 260f;
        const float SpeedUp = 420f;
        const float SlowDown = 150f;

        float angle, speed;
        bool spinning;

        public bool Spinning
        {
            get => spinning;
            set { if (spinning == value) return; spinning = value; Animate(); }
        }

        protected override bool Step(float dt)
        {
            speed = Animator.Toward(speed, spinning ? TopSpeed : 0f, dt * (spinning ? SpeedUp : SlowDown));
            angle = (angle + speed * dt) % 360f;
            Invalidate();
            return spinning || speed > 0f;
        }

        public override void Paint(Graphics g)
        {
            var r = Canvas.Px(Bounds);
            g.TranslateTransform(r.Left + r.Width / 2f, r.Top + r.Height / 2f);
            g.RotateTransform(angle);
            g.DrawImage(Art.Get("teker"), -r.Width / 2f, -r.Height / 2f, r.Width, r.Height);
        }
    }

    // Icindeki ogeleri birlikte gosterip gizleyen sayfa
    // Gecis sirasinda sayfa bir resme cizilip solarak kayar
    class Page : Widget
    {
        float shown = 1f;
        bool target = true;
        int direction = 1;

        public bool IsOpen => target;

        public void Open(bool open, int fromSide)
        {
            if (target == open && Visible == open) return;
            target = open;
            direction = fromSide;
            Visible = true;
            Animate();
        }

        public void Snap(bool open)
        {
            target = open;
            shown = open ? 1f : 0f;
            Visible = open;
        }

        protected override bool Step(float dt)
        {
            shown = Animator.Toward(shown, target ? 1f : 0f, dt / 0.24f);
            if (!target && shown <= 0f) Visible = false;
            Invalidate();
            return shown != (target ? 1f : 0f);
        }

        public override Widget HitTest(PointF p) => shown >= 1f ? base.HitTest(p) : null;

        public override void Paint(Graphics g)
        {
            if (shown >= 1f) { base.Paint(g); return; }
            if (shown <= 0f) return;
            var rect = Canvas.Px(PaintBounds);
            if (rect.Width <= 0 || rect.Height <= 0) return;
            using (var layer = new Bitmap(rect.Width, rect.Height, PixelFormat.Format32bppPArgb))
            {
                using (var lg = Graphics.FromImage(layer))
                {
                    CanvasForm.Prepare(lg);
                    lg.TranslateTransform(-rect.Left, -rect.Top);
                    base.Paint(lg);
                }
                var eased = Animator.Ease(shown);
                var offset = (1f - eased) * Canvas.Px(18) * direction;
                Art.DrawFaded(g, layer, new Rectangle(rect.Left + (int)offset, rect.Top, rect.Width, rect.Height), eased);
            }
        }
    }

    // Oyunun acilir pencere zeminiyle soru kutusu
    // Acikken arkadaki her sey kararir ve tiklanamaz
    // Esc vazgecer
    class Dialog : Widget
    {
        const float Width = 452f;
        const float Height = 214f;

        readonly ArtButton yes, no;
        string title = "", text = "";
        float shown;
        bool open;
        Action<bool> answer;

        public Dialog()
        {
            yes = Add(new ArtButton { Size = 12f });
            no = Add(new ArtButton { Size = 12f });
            yes.Click += () => Close(true);
            no.Click += () => Close(false);
            Visible = false;
        }

        public bool IsOpen => open;

        // Tehlikeli is soruluyorsa evet tusu kirmizi yazar
        public void Ask(string question, string detail, string yesText, string noText, bool danger, Action<bool> then)
        {
            title = question;
            text = detail;
            answer = then;
            yes.SetCaption(yesText);
            no.SetCaption(noText);
            yes.TextColor = danger ? Palette.Red : Palette.Ink;
            var panel = Panel;
            yes.Bounds = new RectangleF(panel.Left + panel.Width / 2f - 162f, panel.Bottom - 64f, 154f, 36f);
            no.Bounds = new RectangleF(panel.Left + panel.Width / 2f + 8f, panel.Bottom - 64f, 154f, 36f);
            open = true;
            Visible = true;
            Canvas.Overlay = this;
            Animate();
            Invalidate();
        }

        public void Close(bool result)
        {
            if (!open) return;
            open = false;
            var then = answer;
            answer = null;
            Animate();
            then?.Invoke(result);
        }

        RectangleF Panel => new RectangleF(Bounds.Left + (Bounds.Width - Width) / 2f, Bounds.Top + (Bounds.Height - Height) / 2f, Width, Height);

        public override bool Key(Keys key)
        {
            if (key != Keys.Escape) return false;
            Close(false);
            return true;
        }

        protected override bool Step(float dt)
        {
            shown = Animator.Toward(shown, open ? 1f : 0f, dt / 0.18f);
            if (!open && shown <= 0f)
            {
                Visible = false;
                if (Canvas.Overlay == this) Canvas.Overlay = null;
            }
            Invalidate();
            return shown != (open ? 1f : 0f);
        }

        // Acikken butun pencereyi tutar
        // Bos yere tiklamak arkaya gecmez
        public override Widget HitTest(PointF p)
        {
            if (!Visible) return null;
            if (shown >= 1f)
            {
                var hit = base.HitTest(p);
                if (hit != null) return hit;
            }
            return this;
        }

        public override Widget WheelTarget(PointF p) => Visible ? this : null;

        public override void Paint(Graphics g)
        {
            var eased = Animator.Ease(shown);
            using (var dim = new SolidBrush(Color.FromArgb((int)(175 * eased), 6, 3, 2)))
                g.FillRectangle(dim, Canvas.Px(Bounds));

            var area = Canvas.Px(RectangleF.Inflate(Panel, 12, 12));
            if (area.Width <= 0 || area.Height <= 0) return;
            // Kutu once ayri bir resme cizilir
            // Sonra solarak ve hafifce yukari kayarak yerine gelir
            using (var layer = new Bitmap(area.Width, area.Height, PixelFormat.Format32bppPArgb))
            {
                using (var lg = Graphics.FromImage(layer))
                {
                    CanvasForm.Prepare(lg);
                    lg.TranslateTransform(-area.Left, -area.Top);
                    PaintPanel(lg);
                    base.Paint(lg);
                }
                var offset = (int)((1f - eased) * Canvas.Px(14));
                Art.DrawFaded(g, layer, new Rectangle(area.Left, area.Top + offset, area.Width, area.Height), eased);
            }
        }

        void PaintPanel(Graphics g)
        {
            var panel = Canvas.Px(Panel);
            Art.DrawNine(g, Art.Get("zemin"), panel, 40, 0.55f * Canvas.Zoom);
            var inner = RectangleF.Inflate(Panel, -26, -20);
            var font = Canvas.Typeface("Bahnschrift SemiBold Condensed", 26f);
            Lettering.Tracked(g, title, font, Palette.Gold, new PointF(Canvas.Px(inner.Left), Canvas.Px(inner.Top)), Canvas.Px(1f));
            Art.DrawFaded(g, Art.Get("ayrac"), Canvas.Px(inner.Left, inner.Top + 38, inner.Width, 1.5f), 0.9f);
            Lettering.Draw(g, text, Canvas.Typeface("Bahnschrift", 13.5f), Palette.Ink,
                Canvas.Px(new RectangleF(inner.Left, inner.Top + 52, inner.Width, 64)), wrap: true);
        }
    }
}
