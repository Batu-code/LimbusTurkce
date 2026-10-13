using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace LimbusTurkce
{
    // Pencerenin icindeki her sey tek bir tuvale cizilir
    // Tuslar yazilar ve paneller birer Widget
    // Boylece yari saydam isiklar ve yazilar birbirinin ustune binebiliyor
    //
    // Windows'un beyaz baslik cubugu yok
    // Pencere bos yerinden tutulup tasinir
    // Gorev cubugu ve Alt+Tab icin pencere stili korunur
    class CanvasForm : Form
    {
        // Butun olculer 96 dpi'daki piksel
        // Ekran olcegi ne olursa olsun yerlesim ayni gorunur
        protected Size LogicalSize = new Size(900, 560);

        protected readonly List<Widget> Widgets = new List<Widget>();
        readonly Dictionary<string, Font> fonts = new Dictionary<string, Font>();
        Bitmap backdrop;
        Widget hovered, pressed, focused;
        bool keyboardUsed;
        int dpi = 96;

        // Simgeli tuslarin ne yaptigi uzerinde biraz durunca yazar
        readonly Timer tipTimer = new Timer { Interval = 550 };
        Widget tipWidget;
        Rectangle tipRect;

        public CanvasForm()
        {
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            AutoScaleMode = AutoScaleMode.None;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            KeyPreview = true;
            Size = LogicalSize;
            tipTimer.Tick += (s, e) =>
            {
                tipTimer.Stop();
                if (hovered == null || string.IsNullOrEmpty(hovered.Tip) || pressed != null) return;
                tipWidget = hovered;
                Invalidate();
            };
        }

        public float Zoom => dpi / 96f;

        protected override CreateParams CreateParams
        {
            get
            {
                const int MinimizeBox = 0x20000, SystemMenu = 0x80000, DropShadow = 0x20000;
                var cp = base.CreateParams;
                // Gorev cubugundaki simgeye tiklayinca kucultulup geri acilsin
                cp.Style |= MinimizeBox | SystemMenu;
                // Menulerdeki gibi yumusak golge
                cp.ClassStyle |= DropShadow;
                return cp;
            }
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            try { dpi = (int)GetDpiForWindow(Handle); }
            catch { dpi = DeviceDpi; }
            if (dpi <= 0) dpi = 96;
            ApplySize(null);
        }

        // Pencere boyu olcege gore ayarlanir ve calisma alaninda ortalanir
        void ApplySize(Rectangle? suggested)
        {
            var size = new Size((int)Math.Round(LogicalSize.Width * Zoom), (int)Math.Round(LogicalSize.Height * Zoom));
            var area = suggested ?? Screen.FromHandle(Handle).WorkingArea;
            var location = suggested?.Location
                ?? new Point(area.Left + Math.Max(0, (area.Width - size.Width) / 2), area.Top + Math.Max(0, (area.Height - size.Height) / 2));
            Bounds = new Rectangle(location, size);
            fonts.Values.ToList().ForEach(f => f.Dispose());
            fonts.Clear();
            backdrop?.Dispose();
            backdrop = null;
            OnZoomChanged();
            Invalidate();
        }

        protected virtual void OnZoomChanged() { }

        protected override void WndProc(ref Message m)
        {
            const int NcHitTest = 0x84, DpiChanged = 0x02E0;
            const int Client = 1, Caption = 2;
            if (m.Msg == NcHitTest)
            {
                base.WndProc(ref m);
                if ((int)m.Result != Client) return;
                var lp = m.LParam.ToInt64();
                var screen = new Point((short)(lp & 0xFFFF), (short)((lp >> 16) & 0xFFFF));
                var p = Logical(PointToClient(screen));
                // Tus olmayan her yerden pencere tutulup tasinabilir
                if (HitTest(p) == null) m.Result = (IntPtr)Caption;
                return;
            }
            if (m.Msg == DpiChanged)
            {
                // Pencere baska olcekteki ekrana tasindi
                dpi = (int)(m.WParam.ToInt64() & 0xFFFF);
                var rect = (NativeRect)Marshal.PtrToStructure(m.LParam, typeof(NativeRect));
                ApplySize(new Rectangle(rect.Left, rect.Top, rect.Right - rect.Left, rect.Bottom - rect.Top));
                m.Result = IntPtr.Zero;
                return;
            }
            base.WndProc(ref m);
        }

        // Olcek donusumleri
        public float Px(float v) => v * Zoom;

        // Kenarlar ayri ayri yuvarlanir
        // Yan yana iki dikdortgenin ortak kenari ayni piksele duser ve arada cizgi kalmaz
        public Rectangle Px(RectangleF r)
        {
            var left = (int)Math.Round(r.Left * Zoom);
            var top = (int)Math.Round(r.Top * Zoom);
            return Rectangle.FromLTRB(left, top, (int)Math.Round(r.Right * Zoom), (int)Math.Round(r.Bottom * Zoom));
        }

        public Rectangle Px(float x, float y, float w, float h) => Px(new RectangleF(x, y, w, h));

        PointF Logical(Point p) => new PointF(p.X / Zoom, p.Y / Zoom);

        // Boy 96 dpi'daki piksel
        public Font Typeface(string family, float size, FontStyle style = FontStyle.Regular)
        {
            var key = family + "|" + size + "|" + style;
            if (fonts.TryGetValue(key, out var font)) return font;
            font = new Font(Art.Family(family), size * Zoom, style, GraphicsUnit.Pixel);
            fonts[key] = font;
            return font;
        }

        public void Add(Widget widget)
        {
            widget.Attach(this);
            Widgets.Add(widget);
        }

        public void InvalidateLogical(RectangleF r)
        {
            var rect = Px(r);
            rect.Inflate(2, 2);
            Invalidate(rect);
        }

        // Zemin bir kez resme cizilir
        // Her boyanista sadece degisen parcasi kopyalanir
        protected virtual void PaintBackdrop(Graphics g) { }

        public void RebuildBackdrop()
        {
            backdrop?.Dispose();
            backdrop = null;
            Invalidate();
        }

        protected override void OnPaintBackground(PaintEventArgs e) { }

        protected override void OnPaint(PaintEventArgs e) => PaintScene(e.Graphics, e.ClipRectangle);

        public void PaintScene(Graphics g, Rectangle clip)
        {
            var size = new Size(Math.Max(1, (int)Math.Round(LogicalSize.Width * Zoom)), Math.Max(1, (int)Math.Round(LogicalSize.Height * Zoom)));
            if (backdrop == null || backdrop.Size != size)
            {
                backdrop?.Dispose();
                backdrop = new Bitmap(size.Width, size.Height, System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
                using (var bg = Graphics.FromImage(backdrop))
                {
                    Prepare(bg);
                    PaintBackdrop(bg);
                }
            }
            // Zemin oldugu gibi kopyalanir
            // Karistirma ve ornekleme yok
            g.CompositingMode = CompositingMode.SourceCopy;
            g.InterpolationMode = InterpolationMode.NearestNeighbor;
            g.PixelOffsetMode = PixelOffsetMode.None;
            g.DrawImage(backdrop, clip, clip, GraphicsUnit.Pixel);
            g.CompositingMode = CompositingMode.SourceOver;
            Prepare(g);
            foreach (var widget in Widgets)
                PaintWidget(g, widget, clip);
            if (focused != null && keyboardUsed && focused.Visible)
                PaintFocus(g, focused);
            if (tipWidget != null && tipWidget.Visible)
                PaintTip(g);
        }

        static void PaintWidget(Graphics g, Widget widget, Rectangle clip)
        {
            if (!widget.Visible) return;
            var area = widget.Canvas.Px(widget.PaintBounds);
            area.Inflate(2, 2);
            if (!area.IntersectsWith(clip)) return;
            var state = g.Save();
            widget.Paint(g);
            g.Restore(state);
        }

        public static void Prepare(Graphics g)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = PixelOffsetMode.Half;
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            g.CompositingQuality = CompositingQuality.HighSpeed;
        }

        // Klavyeyle gezilince secili ogenin etrafinda ince cerceve
        void PaintFocus(Graphics g, Widget widget)
        {
            var rect = Px(RectangleF.Inflate(widget.Bounds, 3, 3));
            using (var pen = new Pen(Color.FromArgb(200, 255, 200, 120), Math.Max(1f, Zoom)) { DashStyle = DashStyle.Dot })
                g.DrawRectangle(pen, rect);
        }

        // En ustteki etkilesimli oge
        // Acik bir soru kutusu varsa tiklama klavye ve tekerlek sadece ona gider
        public Widget Overlay;

        bool OverlayOpen => Overlay != null && Overlay.Visible;

        // Tiklanan nokta ya da tus tuvalin neresine dusuyor
        IEnumerable<Widget> Reachable() => OverlayOpen ? Overlay.Flatten() : Widgets.SelectMany(w => w.Flatten());

        Widget HitTest(PointF p)
        {
            if (OverlayOpen) return Overlay.HitTest(p);
            for (var i = Widgets.Count - 1; i >= 0; i--)
            {
                var hit = Widgets[i].HitTest(p);
                if (hit != null) return hit;
            }
            return null;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            var p = Logical(e.Location);
            if (pressed != null)
            {
                pressed.MouseMove(p);
                SetHovered(pressed.HitTest(p) == pressed ? pressed : null);
                return;
            }
            SetHovered(HitTest(p));
            hovered?.MouseMove(p);
        }

        void SetHovered(Widget widget)
        {
            if (hovered == widget) return;
            hovered?.SetHovered(false);
            hovered = widget;
            hovered?.SetHovered(true);
            Cursor = hovered != null && hovered.Enabled && hovered.Clickable ? Cursors.Hand : Cursors.Default;
            HideTip();
            if (hovered != null && !string.IsNullOrEmpty(hovered.Tip)) tipTimer.Start();
        }

        void HideTip()
        {
            tipTimer.Stop();
            if (tipWidget == null) return;
            tipWidget = null;
            Invalidate(tipRect);
        }

        // Ipucu ogenin altinda
        // Pencereden tasarsa ustune
        void PaintTip(Graphics g)
        {
            var font = Typeface("Bahnschrift", 12f);
            var size = Lettering.Measure(g, tipWidget.Tip, font);
            var anchor = Px(tipWidget.Bounds);
            var width = (int)Math.Ceiling(size.Width + Px(16));
            var height = (int)Math.Ceiling(size.Height + Px(8));
            var x = Math.Min(anchor.Left + anchor.Width / 2 - width / 2, ClientSize.Width - width - (int)Px(10));
            var y = anchor.Bottom + (int)Px(6);
            if (y + height > ClientSize.Height - Px(10)) y = anchor.Top - height - (int)Px(6);
            tipRect = new Rectangle(Math.Max((int)Px(10), x), y, width, height);
            using (var fill = new SolidBrush(Color.FromArgb(235, 18, 12, 10)))
            using (var pen = new Pen(Color.FromArgb(180, Palette.Gold), Math.Max(1f, Zoom)))
            {
                g.FillRectangle(fill, tipRect);
                g.DrawRectangle(pen, tipRect.X, tipRect.Y, tipRect.Width - 1, tipRect.Height - 1);
            }
            Lettering.Draw(g, tipWidget.Tip, font, Palette.Ink, tipRect, StringAlignment.Center, StringAlignment.Center);
            tipRect.Inflate(2, 2);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (pressed == null) SetHovered(null);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left) return;
            var p = Logical(e.Location);
            var hit = HitTest(p);
            keyboardUsed = false;
            HideTip();
            if (hit == null || !hit.Enabled) return;
            pressed = hit;
            pressed.SetPressed(true);
            pressed.MouseDown(p);
            if (hit.Focusable) SetFocus(hit);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (pressed == null) return;
            var p = Logical(e.Location);
            var widget = pressed;
            pressed = null;
            widget.SetPressed(false);
            widget.MouseUp(p);
            if (widget.HitTest(p) == widget) widget.PerformClick();
            SetHovered(HitTest(p));
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            if (OverlayOpen) return;
            var p = Logical(e.Location);
            for (var i = Widgets.Count - 1; i >= 0; i--)
            {
                var target = Widgets[i].WheelTarget(p);
                if (target != null && target.Wheel(e.Delta)) return;
            }
        }

        void SetFocus(Widget widget)
        {
            if (focused == widget) return;
            if (focused != null) focused.Invalidate();
            focused = widget;
            if (focused != null) focused.Invalidate();
        }

        IEnumerable<Widget> Focusables() => Reachable().Where(w => w.Focusable && w.Visible && w.Enabled && w.ParentsVisible);

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (OverlayOpen && Overlay.Key(keyData)) return true;
            if (focused != null && !Focusables().Contains(focused)) SetFocus(null);
            if (keyData == Keys.Tab || keyData == (Keys.Tab | Keys.Shift))
            {
                var list = Focusables().ToList();
                if (list.Count == 0) return true;
                var index = focused == null ? -1 : list.IndexOf(focused);
                index = keyData == Keys.Tab ? (index + 1) % list.Count : (index <= 0 ? list.Count - 1 : index - 1);
                keyboardUsed = true;
                SetFocus(list[index]);
                return true;
            }
            if ((keyData == Keys.Enter || keyData == Keys.Space) && focused != null && keyboardUsed && focused.Visible && focused.Enabled)
            {
                focused.PerformClick();
                return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                tipTimer.Dispose();
                foreach (var widget in Widgets.SelectMany(w => w.Flatten())) widget.Release();
                backdrop?.Dispose();
                foreach (var font in fonts.Values) font.Dispose();
                fonts.Clear();
            }
            base.Dispose(disposing);
        }

        // Ekran okuyucular ve otomasyon araclari tuslari tek tek gorsun
        // Tuvalde ayri pencere olmadigi icin her tus burada tarif edilir
        protected override AccessibleObject CreateAccessibilityInstance() => new CanvasAccessible(this);

        IEnumerable<Widget> AccessibleWidgets() => Reachable().Where(w => w.Clickable && w.Visible && w.ParentsVisible);

        class CanvasAccessible : ControlAccessibleObject
        {
            readonly CanvasForm form;

            public CanvasAccessible(CanvasForm form) : base(form) => this.form = form;

            public override int GetChildCount() => form.AccessibleWidgets().Count();

            public override AccessibleObject GetChild(int index)
            {
                var widget = form.AccessibleWidgets().ElementAtOrDefault(index);
                return widget == null ? null : new WidgetAccessible(this, widget);
            }
        }

        class WidgetAccessible : AccessibleObject
        {
            readonly AccessibleObject parent;
            readonly Widget widget;

            public WidgetAccessible(AccessibleObject parent, Widget widget)
            {
                this.parent = parent;
                this.widget = widget;
            }

            public override string Name => widget.AccessibleName;
            public override AccessibleRole Role => AccessibleRole.PushButton;
            public override AccessibleObject Parent => parent;
            public override string DefaultAction => "Bas";
            public override Rectangle Bounds => widget.Canvas.RectangleToScreen(widget.Canvas.Px(widget.Bounds));
            public override AccessibleStates State => widget.Enabled ? AccessibleStates.Focusable : AccessibleStates.Unavailable;
            public override void DoDefaultAction() => widget.PerformClick();
        }

        [StructLayout(LayoutKind.Sequential)] struct NativeRect { public int Left, Top, Right, Bottom; }
        [DllImport("user32.dll")] static extern uint GetDpiForWindow(IntPtr hwnd);
    }

    // Tuvalin ustundeki her sey
    // Konum ve boy 96 dpi pikseli
    abstract class Widget
    {
        public CanvasForm Canvas { get; private set; }
        public Widget Parent { get; private set; }
        public RectangleF Bounds;
        // Isik ve golge sinirin disina tasabilir
        public float Overflow;
        public string Name;
        // Simgeli tuslarda uzerine gelince cikan aciklama
        public string Tip;
        public event Action Click;

        // Ekran okuyucunun soyleyecegi ad
        public virtual string AccessibleName => Tip ?? Name;

        bool visible = true, enabled = true;
        protected bool Hovered, Pressed;
        readonly List<Widget> children = new List<Widget>();
        Func<float, bool> step;

        public IReadOnlyList<Widget> Children => children;

        public bool Visible
        {
            get => visible;
            set { if (visible == value) return; visible = value; Invalidate(); }
        }

        public bool Enabled
        {
            get => enabled;
            set { if (enabled == value) return; enabled = value; if (!value) Hovered = Pressed = false; Changed(); }
        }

        public bool ParentsVisible => Parent == null || (Parent.Visible && Parent.ParentsVisible);

        // Fareyle tiklanabilir mi
        public virtual bool Clickable => false;
        public virtual bool Focusable => Clickable;
        public RectangleF PaintBounds => RectangleF.Inflate(Bounds, Overflow, Overflow);

        public void Attach(CanvasForm canvas)
        {
            Canvas = canvas;
            foreach (var child in children) child.Attach(canvas);
            OnAttached();
        }

        protected virtual void OnAttached() { }

        public T Add<T>(T child) where T : Widget
        {
            child.Parent = this;
            children.Add(child);
            if (Canvas != null) child.Attach(Canvas);
            return child;
        }

        public IEnumerable<Widget> Flatten()
        {
            yield return this;
            foreach (var child in children)
                foreach (var item in child.Flatten())
                    yield return item;
        }

        public void Invalidate()
        {
            if (Canvas == null) return;
            Canvas.InvalidateLogical(PaintBounds);
        }

        // Cocuklar ustte durur
        // Etkilesimsiz ogeler tiklamayi altina gecirir
        public virtual Widget HitTest(PointF p)
        {
            if (!visible || !PaintBounds.Contains(p)) return null;
            for (var i = children.Count - 1; i >= 0; i--)
            {
                var hit = children[i].HitTest(p);
                if (hit != null) return hit;
            }
            return Clickable && Bounds.Contains(p) ? this : null;
        }

        public virtual Widget WheelTarget(PointF p)
        {
            if (!visible || !Bounds.Contains(p)) return null;
            for (var i = children.Count - 1; i >= 0; i--)
            {
                var hit = children[i].WheelTarget(p);
                if (hit != null) return hit;
            }
            return this;
        }

        public virtual bool Wheel(int delta) => false;

        // Klavye tusu
        // Isledi ise true
        public virtual bool Key(Keys key) => false;

        // Pencere kapanirken sakli resimleri birakir
        public virtual void Release() { }

        public virtual void Paint(Graphics g)
        {
            foreach (var child in children)
                if (child.Visible)
                {
                    var state = g.Save();
                    child.Paint(g);
                    g.Restore(state);
                }
        }

        public void SetHovered(bool value) { if (Hovered == value) return; Hovered = value; Changed(); }
        public void SetPressed(bool value) { if (Pressed == value) return; Pressed = value; Changed(); }
        public virtual void MouseDown(PointF p) { }
        public virtual void MouseMove(PointF p) { }
        public virtual void MouseUp(PointF p) { }

        public void PerformClick()
        {
            if (Enabled && Visible) Click?.Invoke();
        }

        // Durum degisti
        // Animasyonu olan oge yumusakca yeni haline gecer
        protected virtual void Changed()
        {
            Animate();
            Invalidate();
        }

        protected void Animate()
        {
            if (step == null) step = dt => Canvas != null && !Canvas.IsDisposed && Step(dt);
            Animator.Run(step);
        }

        // Daha surecekse true doner
        protected virtual bool Step(float dt) => false;
    }
}
