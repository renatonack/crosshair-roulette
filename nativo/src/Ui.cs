using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace CrosshairRoulette
{
    static class Theme
    {
        static Color Hex(string h) => ColorTranslator.FromHtml(h);
        public static readonly Color Bg = Hex("#111216"), Panel = Hex("#191b21"), Panel2 = Hex("#20232b"), Line = Hex("#2b2f39"),
            Text = Hex("#e9ebf0"), Muted = Hex("#a3a8b5"), Accent = Hex("#b6ff3b"), AccentHover = Hex("#c8ff6a"), AccentInk = Hex("#172000"),
            Ok = Hex("#4ade80"), Warn = Hex("#fbbf24"), Bad = Hex("#f87171"), Track = Hex("#3a3f4b");

        public static float Dpi = 1;
        public static int D(double px) => (int)Math.Round(px * Dpi);

        public static readonly Font Body = new Font("Segoe UI", 9.75f);
        public static readonly Font BodyBold = new Font("Segoe UI Semibold", 9.75f);
        public static readonly Font Small = new Font("Segoe UI", 9f);
        public static readonly Font H1 = new Font("Bahnschrift SemiBold", 14f);
        public static readonly Font H2 = new Font("Bahnschrift SemiBold", 12f);
        public static readonly Font Big = new Font("Bahnschrift SemiBold", 15f);
        public static readonly Font Mono = new Font("Consolas", 9.75f);
        public static readonly Font MonoBig = new Font("Consolas", 11.5f);

        // a plain stand-in for a CS2 map behind the crosshair
        public static void Scene(Graphics g, Rectangle r)
        {
            using (var sky = new LinearGradientBrush(new Rectangle(r.X, r.Y, r.Width, (int)(r.Height * 0.55) + 1), Hex("#6d8fb3"), Hex("#cdbb98"), 90f))
                g.FillRectangle(sky, r.X, r.Y, r.Width, (int)(r.Height * 0.55) + 1);
            using (var ground = new LinearGradientBrush(new Rectangle(r.X, r.Y + (int)(r.Height * 0.55), r.Width, r.Height - (int)(r.Height * 0.55) + 1), Hex("#8c775c"), Hex("#5a4a38"), 90f))
                g.FillRectangle(ground, r.X, r.Y + (int)(r.Height * 0.55), r.Width, r.Height - (int)(r.Height * 0.55));
            using (var b = new SolidBrush(Hex("#b39d7a")))
            {
                g.FillRectangle(b, r.X + r.Width * 0.06f, r.Y + r.Height * 0.3f, r.Width * 0.24f, r.Height * 0.255f);
                g.FillRectangle(b, r.X + r.Width * 0.7f, r.Y + r.Height * 0.22f, r.Width * 0.26f, r.Height * 0.335f);
            }
            using (var b = new SolidBrush(Hex("#7d684f")))
                g.FillRectangle(b, r.X + r.Width * 0.44f, r.Y + r.Height * 0.42f, r.Width * 0.07f, r.Height * 0.135f);
        }

        // crosshair centred in r at scale k (design px -> screen px)
        public static void DrawSpec(Graphics g, Rectangle r, Spec spec, double k, double spread)
        {
            if (spec == null) return;
            var state = g.Save();
            g.SetClip(r);
            float cx = r.X + r.Width / 2f, cy = r.Y + r.Height / 2f;
            g.TranslateTransform(cx, cy);
            g.ScaleTransform((float)k, (float)k);
            var bounds = RectangleF.FromLTRB((float)(-r.Width / 2.0 / k), (float)(-r.Height / 2.0 / k), (float)(r.Width / 2.0 / k), (float)(r.Height / 2.0 / k));
            Crosshairs.Draw(g, spec, spread, bounds);
            g.Restore(state);
        }

        public static GraphicsPath Round(RectangleF r, float radius)
        {
            var p = new GraphicsPath();
            float d = radius * 2;
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }
    }

    sealed class Btn : Button
    {
        public Btn(string text, bool accent = false)
        {
            FlatStyle = FlatStyle.Flat;
            UseVisualStyleBackColor = false;
            Cursor = Cursors.Hand;
            Font = accent ? Theme.BodyBold : Theme.Body;
            BackColor = accent ? Theme.Accent : Theme.Panel2;
            ForeColor = accent ? Theme.AccentInk : Theme.Text;
            FlatAppearance.BorderColor = accent ? Theme.Accent : Theme.Line;
            FlatAppearance.MouseOverBackColor = accent ? Theme.AccentHover : Color.FromArgb(39, 42, 51);
            FlatAppearance.MouseDownBackColor = accent ? Theme.Accent : Theme.Panel;
            SetText(text);
        }

        public void SetText(string text)
        {
            Text = text;
            Size = new Size(TextRenderer.MeasureText(text, Font).Width + Theme.D(28), Theme.D(34));
        }
    }

    class Lbl : Label
    {
        public Lbl(string text, Font font, Color color, int width = 0)
        {
            Text = text;
            Font = font;
            ForeColor = color;
            BackColor = Color.Transparent;
            UseMnemonic = false;
            if (width > 0)
            {
                AutoSize = false;
                Width = width;
                FitHeight();
            }
            else AutoSize = true;
        }

        public void FitHeight()
        {
            var size = TextRenderer.MeasureText(Text, Font, new Size(Width, int.MaxValue), TextFormatFlags.WordBreak);
            Height = size.Height + 2;
        }
    }

    sealed class Slider : Control
    {
        public double Min = 0, Max = 1, Step = 0.05;
        double value;
        public event EventHandler ValueChanged;

        public Slider()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.Selectable, true);
            Height = Theme.D(24);
            Cursor = Cursors.Hand;
            TabStop = true;
        }

        public double Value
        {
            get => value;
            set
            {
                double v = Math.Max(Min, Math.Min(Max, Math.Round(value / Step) * Step));
                if (Math.Abs(v - this.value) < 1e-9) return;
                this.value = v;
                Invalidate();
                ValueChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        int Pad => Theme.D(9);

        void SetFromX(int x) => Value = Min + (Max - Min) * Math.Max(0, Math.Min(1, (x - Pad) / (double)(Width - 2 * Pad)));

        protected override void OnMouseDown(MouseEventArgs e) { Focus(); SetFromX(e.X); base.OnMouseDown(e); }
        protected override void OnMouseMove(MouseEventArgs e) { if (e.Button == MouseButtons.Left) SetFromX(e.X); base.OnMouseMove(e); }

        protected override bool IsInputKey(Keys k) => k == Keys.Left || k == Keys.Right || base.IsInputKey(k);

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Left) Value -= Step;
            if (e.KeyCode == Keys.Right) Value += Step;
            base.OnKeyDown(e);
        }

        protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
        protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(Parent?.BackColor ?? Theme.Bg);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            float y = Height / 2f, x0 = Pad, x1 = Width - Pad;
            float px = x0 + (float)((value - Min) / (Max - Min)) * (x1 - x0);
            using (var track = new Pen(Theme.Track, Theme.D(4)) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                g.DrawLine(track, x0, y, x1, y);
            using (var fill = new Pen(Theme.Accent, Theme.D(4)) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                g.DrawLine(fill, x0, y, px, y);
            float r = Theme.D(8);
            using (var b = new SolidBrush(Theme.Accent)) g.FillEllipse(b, px - r, y - r, 2 * r, 2 * r);
            if (Focused) using (var p = new Pen(Theme.Text, 1.5f)) g.DrawEllipse(p, px - r - 2, y - r - 2, 2 * r + 4, 2 * r + 4);
        }
    }

    sealed class Toggle : Control
    {
        bool on;
        public event EventHandler CheckedChanged;

        public Toggle(string text)
        {
            Text = text;
            Font = Theme.Body;
            ForeColor = Theme.Text;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.Selectable, true);
            Cursor = Cursors.Hand;
            TabStop = true;
            var size = TextRenderer.MeasureText(text, Font);
            Size = new Size(Theme.D(48) + size.Width, Math.Max(Theme.D(24), size.Height + 4));
        }

        public bool Checked
        {
            get => on;
            set { if (on == value) return; on = value; Invalidate(); }
        }

        void Flip()
        {
            Checked = !Checked;
            CheckedChanged?.Invoke(this, EventArgs.Empty);
        }

        protected override void OnClick(EventArgs e) { Focus(); Flip(); base.OnClick(e); }
        protected override void OnKeyDown(KeyEventArgs e) { if (e.KeyCode == Keys.Space) Flip(); base.OnKeyDown(e); }
        protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
        protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(Parent?.BackColor ?? Theme.Bg);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            float w = Theme.D(36), h = Theme.D(20), y = (Height - h) / 2f;
            using (var path = Theme.Round(new RectangleF(1, y, w, h), h / 2))
            using (var b = new SolidBrush(on ? Theme.Accent : Theme.Track))
                g.FillPath(b, path);
            if (Focused) using (var p = new Pen(Theme.Text, 1.5f)) using (var path = Theme.Round(new RectangleF(0, y - 1, w + 2, h + 2), h / 2 + 1)) g.DrawPath(p, path);
            float d = h - Theme.D(6);
            float kx = on ? 1 + w - d - Theme.D(3) : 1 + Theme.D(3);
            using (var b = new SolidBrush(on ? Theme.AccentInk : Color.White)) g.FillEllipse(b, kx, y + Theme.D(3), d, d);
            TextRenderer.DrawText(g, Text, Font, new Rectangle(Theme.D(46), 0, Width - Theme.D(46), Height), ForeColor, TextFormatFlags.VerticalCenter | TextFormatFlags.Left);
        }
    }

    // the current crosshair at real size over the stand-in map
    sealed class Preview : Control
    {
        public Spec Spec;
        public double Zoom = 1, Spread;

        public Preview()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var r = new Rectangle(0, 0, Width, Height);
            Theme.Scene(e.Graphics, r);
            Theme.DrawSpec(e.Graphics, r, Spec, Zoom, Spread);
            using (var p = new Pen(Theme.Line)) e.Graphics.DrawRectangle(p, 0, 0, Width - 1, Height - 1);
        }
    }

    // one crosshair in the list: click to use it, tick to keep it in the roulette
    sealed class Card : Control
    {
        public readonly Spec Spec;
        public bool InRoulette = true, IsCurrent;
        public event EventHandler UseClicked, ToggleClicked;
        bool hover;

        public Card(Spec spec)
        {
            Spec = spec;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            Cursor = Cursors.Hand;
        }

        Rectangle Thumb => new Rectangle(1, 1, Width - 2, (int)((Width - 2) * 0.75));
        Rectangle Box => new Rectangle(Width - Theme.D(26), Height - Theme.D(26), Theme.D(18), Theme.D(18));

        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); base.OnMouseLeave(e); }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            var hit = Box;
            hit.Inflate(Theme.D(6), Theme.D(6));
            if (hit.Contains(e.Location)) ToggleClicked?.Invoke(this, EventArgs.Empty);
            else UseClicked?.Invoke(this, EventArgs.Empty);
            base.OnMouseClick(e);
        }

        static double ThumbScale(Spec s, Rectangle r)
        {
            double reach = Crosshairs.Reach(s);
            if (double.IsInfinity(reach)) return 0.35;
            return Math.Max(0.2, Math.Min(2.2, (Math.Min(r.Width, r.Height) / 2.0 - 8) / reach));
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(Theme.Panel);
            var t = Thumb;
            Theme.Scene(g, t);
            Theme.DrawSpec(g, t, Spec, ThumbScale(Spec, t), Spec.Type == "flood" ? 60 : 0);
            var nameRect = new Rectangle(Theme.D(10), t.Bottom + Theme.D(6), Width - Theme.D(44), Height - t.Bottom - Theme.D(8));
            TextRenderer.DrawText(g, Spec.Name, Theme.Small, nameRect, Theme.Text, TextFormatFlags.WordBreak | TextFormatFlags.Left | TextFormatFlags.Top);
            if (!InRoulette)
                using (var dim = new SolidBrush(Color.FromArgb(165, Theme.Panel))) g.FillRectangle(dim, 0, 0, Width, Height);

            g.SmoothingMode = SmoothingMode.AntiAlias;
            var box = Box;
            using (var path = Theme.Round(box, Theme.D(4)))
            {
                if (InRoulette)
                {
                    using (var b = new SolidBrush(Theme.Accent)) g.FillPath(b, path);
                    using (var p = new Pen(Theme.AccentInk, Theme.D(2)) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                        g.DrawLines(p, new[] { new PointF(box.X + box.Width * 0.24f, box.Y + box.Height * 0.52f), new PointF(box.X + box.Width * 0.43f, box.Y + box.Height * 0.72f), new PointF(box.X + box.Width * 0.78f, box.Y + box.Height * 0.3f) });
                }
                else using (var p = new Pen(Theme.Muted, Theme.D(1.5))) g.DrawPath(p, path);
            }
            g.SmoothingMode = SmoothingMode.None;
            var border = IsCurrent ? Theme.Accent : hover ? Color.FromArgb(61, 66, 80) : Theme.Line;
            using (var p = new Pen(border, IsCurrent ? 2 : 1)) g.DrawRectangle(p, IsCurrent ? 1 : 0, IsCurrent ? 1 : 0, Width - (IsCurrent ? 3 : 1), Height - (IsCurrent ? 3 : 1));
        }
    }

    // read-only box that records a key combination
    sealed class HotkeyBox : TextBox
    {
        public string Value = "";
        public event EventHandler Captured;

        public HotkeyBox()
        {
            ReadOnly = true;
            BackColor = Theme.Panel2;
            ForeColor = Theme.Text;
            BorderStyle = BorderStyle.FixedSingle;
            Font = Theme.MonoBig;
            Cursor = Cursors.Hand;
        }

        public void SetValue(string value)
        {
            Value = value ?? "";
            Text = Value.Length > 0 ? Value : "nenhum";
        }

        protected override void OnEnter(EventArgs e) { Text = "Aperte as teclas…"; base.OnEnter(e); }
        protected override void OnLeave(EventArgs e) { SetValue(Value); base.OnLeave(e); }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            var e = new KeyEventArgs(keyData);
            if (e.KeyCode == Keys.Escape) { Parent?.Focus(); return true; }
            if (e.KeyCode == Keys.Tab) return base.ProcessCmdKey(ref msg, keyData);
            string acc = e.KeyCode == Keys.Back ? "" : Hotkeys.FromKeyEvent(e);
            if (acc == null) return true;
            if (acc == "" && e.KeyCode != Keys.Back) { Text = "Use Ctrl, Alt ou Shift junto"; return true; }
            Value = acc;
            Captured?.Invoke(this, EventArgs.Empty);
            Parent?.Focus();
            return true;
        }
    }

    sealed class StatusLabel : Control
    {
        public Color Dot = Color.FromArgb(91, 96, 109);

        public StatusLabel()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);
            Font = Theme.BodyBold;
        }

        public void Set(string text, Color dot, Color fore)
        {
            Text = text;
            Dot = dot;
            ForeColor = fore;
            var size = TextRenderer.MeasureText(text, Font);
            Size = new Size(size.Width + Theme.D(22), Math.Max(Theme.D(22), size.Height));
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(Parent?.BackColor ?? Theme.Bg);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            float d = Theme.D(9);
            using (var b = new SolidBrush(Dot)) e.Graphics.FillEllipse(b, 0, (Height - d) / 2f, d, d);
            TextRenderer.DrawText(e.Graphics, Text, Font, new Rectangle(Theme.D(16), 0, Width, Height), ForeColor, TextFormatFlags.VerticalCenter | TextFormatFlags.Left);
        }
    }

    // a flat panel with a hairline border
    class Box : Panel
    {
        public Box(Color back)
        {
            BackColor = back;
            DoubleBuffered = true;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            using (var p = new Pen(Theme.Line)) e.Graphics.DrawRectangle(p, 0, 0, Width - 1, Height - 1);
        }
    }
}
