using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Windows.Forms;

namespace AkkeohsVegas.Extension
{
    internal static class UiColors
    {
        public static readonly Color Bg = Color.FromArgb(241, 245, 249);
        public static readonly Color Card = Color.White;
        public static readonly Color Accent = Color.FromArgb(15, 118, 110);
        public static readonly Color AccentHover = Color.FromArgb(13, 148, 136);
        public static readonly Color AccentDown = Color.FromArgb(17, 94, 89);
        public static readonly Color Text = Color.FromArgb(15, 23, 42);
        public static readonly Color Muted = Color.FromArgb(100, 116, 139);
        public static readonly Color Border = Color.FromArgb(203, 213, 225);
        public static readonly Color InputBg = Color.FromArgb(255, 255, 255);
        public static readonly Color InputBorder = Color.FromArgb(148, 163, 184);
        public static readonly Color Track = Color.FromArgb(226, 232, 240);
        public static readonly Color Arrow = Color.FromArgb(71, 85, 105);
    }

    internal static class UiDraw
    {
        public static GraphicsPath RoundedRect(Rectangle bounds, int radius)
        {
            var path = new GraphicsPath();
            if (bounds.Width <= 0 || bounds.Height <= 0)
            {
                path.AddRectangle(new Rectangle(bounds.X, bounds.Y, Math.Max(0, bounds.Width), Math.Max(0, bounds.Height)));
                return path;
            }

            int maxR = Math.Min(bounds.Width, bounds.Height) / 2;
            if (radius > maxR)
                radius = maxR;
            if (radius <= 0)
            {
                path.AddRectangle(bounds);
                return path;
            }

            int d = radius * 2;
            path.AddArc(bounds.X, bounds.Y, d, d, 180, 90);
            path.AddArc(bounds.Right - d, bounds.Y, d, d, 270, 90);
            path.AddArc(bounds.Right - d, bounds.Bottom - d, d, d, 0, 90);
            path.AddArc(bounds.X, bounds.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        public static void ApplyRoundedRegion(Control control, int radius)
        {
            if (control == null)
                return;

            if (control.Width < 2 || control.Height < 2)
            {
                Region oldEmpty = control.Region;
                control.Region = null;
                if (oldEmpty != null)
                    oldEmpty.Dispose();
                return;
            }

            using (var path = RoundedRect(new Rectangle(0, 0, control.Width, control.Height), radius))
            {
                Region old = control.Region;
                control.Region = new Region(path);
                if (old != null)
                    old.Dispose();
            }
        }

        public static void FillParentBackground(Control control, Graphics g)
        {
            Color bg = UiColors.Card;
            if (control.Parent != null)
                bg = control.Parent.BackColor;
            using (var brush = new SolidBrush(bg))
                g.FillRectangle(brush, control.ClientRectangle);
        }

        public static void DrawChevronDown(Graphics g, Rectangle bounds, Color color)
        {
            int cx = bounds.X + bounds.Width / 2;
            int cy = bounds.Y + bounds.Height / 2;
            Point[] pts =
            {
                new Point(cx - 4, cy - 1),
                new Point(cx + 4, cy - 1),
                new Point(cx, cy + 3)
            };
            using (var brush = new SolidBrush(color))
                g.FillPolygon(brush, pts);
        }

        public static void DrawChevronUp(Graphics g, Rectangle bounds, Color color)
        {
            int cx = bounds.X + bounds.Width / 2;
            int cy = bounds.Y + bounds.Height / 2;
            Point[] pts =
            {
                new Point(cx - 4, cy + 1),
                new Point(cx + 4, cy + 1),
                new Point(cx, cy - 3)
            };
            using (var brush = new SolidBrush(color))
                g.FillPolygon(brush, pts);
        }
    }

    internal sealed class RoundedButton : Control
    {
        private readonly int _radius;
        private readonly Timer _spinTimer;
        private bool _hover;
        private bool _down;
        private bool _showLoading;
        private float _spinAngle;
        private string _idleText;

        public Color AccentColor { get; set; }
        public Color AccentHoverColor { get; set; }
        public Color AccentDownColor { get; set; }
        public bool IsPrimary { get; set; }

        public bool ShowLoading
        {
            get { return _showLoading; }
            set
            {
                if (_showLoading == value)
                    return;
                _showLoading = value;
                if (value)
                {
                    _spinTimer.Start();
                    Cursor = Cursors.Default;
                }
                else
                {
                    _spinTimer.Stop();
                    Cursor = Cursors.Hand;
                    Text = _idleText;
                }
                Invalidate();
            }
        }

        public RoundedButton(string text, int width, bool primary)
        {
            _idleText = text ?? string.Empty;
            Text = _idleText;
            Width = width;
            Height = 36;
            IsPrimary = primary;
            _radius = 10;
            SetStyle(
                ControlStyles.AllPaintingInWmPaint
                | ControlStyles.UserPaint
                | ControlStyles.OptimizedDoubleBuffer
                | ControlStyles.ResizeRedraw
                | ControlStyles.Selectable,
                true);
            Cursor = Cursors.Hand;
            Font = new Font("Segoe UI", 9f, FontStyle.Bold);
            AccentColor = UiColors.Accent;
            AccentHoverColor = UiColors.AccentHover;
            AccentDownColor = UiColors.AccentDown;
            ForeColor = primary ? Color.White : UiColors.Text;
            TabStop = true;

            _spinTimer = new Timer { Interval = 16 };
            _spinTimer.Tick += (s, e) =>
            {
                _spinAngle = (_spinAngle + 8f) % 360f;
                Invalidate();
            };
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _spinTimer.Stop();
                _spinTimer.Dispose();
            }
            base.Dispose(disposing);
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            if (_showLoading)
                return;
            _hover = true;
            Invalidate();
            base.OnMouseEnter(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            _hover = false;
            _down = false;
            Invalidate();
            base.OnMouseLeave(e);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (_showLoading || !Enabled)
                return;
            if (e.Button == MouseButtons.Left)
            {
                _down = true;
                Focus();
                Invalidate();
            }
            base.OnMouseDown(e);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            if (_showLoading || !Enabled)
            {
                _down = false;
                return;
            }
            bool fire = _down && e.Button == MouseButtons.Left && ClientRectangle.Contains(e.Location);
            _down = false;
            Invalidate();
            base.OnMouseUp(e);
            if (fire)
                OnClick(EventArgs.Empty);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            UiDraw.FillParentBackground(this, e.Graphics);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle rect = new Rectangle(0, 0, Width - 1, Height - 1);

            Color fill;
            Color border;
            Color text = Enabled ? ForeColor : UiColors.Muted;

            if (IsPrimary)
            {

                fill = _showLoading ? AccentColor
                    : !Enabled ? Color.FromArgb(148, 163, 184)
                    : _down ? AccentDownColor
                    : _hover ? AccentHoverColor
                    : AccentColor;
                border = fill;
                text = Color.White;
            }
            else
            {
                fill = _down ? UiColors.Track : (_hover ? Color.FromArgb(248, 250, 252) : Color.White);
                border = UiColors.Border;
                if (!Enabled)
                    text = UiColors.Muted;
            }

            using (var path = UiDraw.RoundedRect(rect, _radius))
            using (var brush = new SolidBrush(fill))
            using (var pen = new Pen(border, 1.5f))
            {
                e.Graphics.FillPath(brush, path);
                e.Graphics.DrawPath(pen, path);
            }

            if (_showLoading)
            {
                int size = 18;
                int x = (Width - size) / 2;
                int y = (Height - size) / 2;
                var spinBounds = new Rectangle(x, y, size, size);
                using (var track = new Pen(Color.FromArgb(90, 255, 255, 255), 2.5f))
                using (var arc = new Pen(Color.White, 2.5f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                {
                    e.Graphics.DrawEllipse(track, spinBounds);
                    e.Graphics.DrawArc(arc, spinBounds, _spinAngle, 100f);
                }
                return;
            }

            TextRenderer.DrawText(
                e.Graphics,
                Text,
                Font,
                ClientRectangle,
                text,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }
    }

    internal sealed class ColorSwatchButton : Control
    {
        private readonly int _radius = 8;
        private Color _color = Color.White;
        private bool _hover;

        public event EventHandler ColorChanged;

        public Color SelectedColor
        {
            get { return _color; }
            set
            {
                if (_color.ToArgb() == value.ToArgb())
                    return;
                _color = value;
                Invalidate();
                if (ColorChanged != null)
                    ColorChanged(this, EventArgs.Empty);
            }
        }

        public ColorSwatchButton()
        {
            Height = 32;
            Width = 72;
            Cursor = Cursors.Hand;
            SetStyle(
                ControlStyles.AllPaintingInWmPaint
                | ControlStyles.UserPaint
                | ControlStyles.OptimizedDoubleBuffer
                | ControlStyles.ResizeRedraw,
                true);
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            _hover = true;
            Invalidate();
            base.OnMouseEnter(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            _hover = false;
            Invalidate();
            base.OnMouseLeave(e);
        }

        protected override void OnClick(EventArgs e)
        {
            using (var dlg = new ColorDialog())
            {
                dlg.Color = Color.FromArgb(255, _color.R, _color.G, _color.B);
                dlg.FullOpen = true;
                dlg.AnyColor = true;
                if (dlg.ShowDialog(FindForm()) == DialogResult.OK)
                    SelectedColor = dlg.Color;
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            UiDraw.FillParentBackground(this, e.Graphics);
            if (Width < 2 || Height < 2)
                return;

            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle rect = new Rectangle(0, 0, Width - 1, Height - 1);
            using (var path = UiDraw.RoundedRect(rect, _radius))
            using (var brush = new SolidBrush(_color))
            using (var pen = new Pen(_hover ? UiColors.Accent : UiColors.InputBorder, 1.5f))
            {
                e.Graphics.FillPath(brush, path);
                e.Graphics.DrawPath(pen, path);
            }

            if (_color.GetBrightness() > 0.92f)
            {
                using (var pen = new Pen(Color.FromArgb(180, UiColors.Border)))
                    e.Graphics.DrawRectangle(pen, 6, 6, Width - 13, Height - 13);
            }
        }
    }

    internal sealed class FlatComboBox : ComboBox
    {
        private const int ArrowWidth = 22;

        public FlatComboBox()
        {
            FlatStyle = FlatStyle.Flat;
            DrawMode = DrawMode.OwnerDrawFixed;
            DropDownStyle = ComboBoxStyle.DropDownList;
            IntegralHeight = false;
            ItemHeight = 22;
            BackColor = UiColors.InputBg;
            ForeColor = UiColors.Text;
            Font = new Font("Segoe UI", 9f);
            SetStyle(ControlStyles.OptimizedDoubleBuffer, true);
        }

        protected override void OnDrawItem(DrawItemEventArgs e)
        {
            Color bg = UiColors.InputBg;
            Color fg = Enabled ? UiColors.Text : UiColors.Muted;
            using (var brush = new SolidBrush(bg))
                e.Graphics.FillRectangle(brush, e.Bounds);

            if (e.Index >= 0 && e.Index < Items.Count)
            {
                string text = GetItemText(Items[e.Index]);
                var textBounds = new Rectangle(e.Bounds.X + 4, e.Bounds.Y, Math.Max(0, e.Bounds.Width - ArrowWidth - 4), e.Bounds.Height);
                TextRenderer.DrawText(
                    e.Graphics,
                    text,
                    Font,
                    textBounds,
                    fg,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            }

            if ((e.State & DrawItemState.ComboBoxEdit) == DrawItemState.ComboBoxEdit)
                PaintArrow(e.Graphics, e.Bounds);
        }

        protected override void WndProc(ref Message m)
        {
            base.WndProc(ref m);
            if (m.Msg == 0x000F && IsHandleCreated)
            {
                try
                {
                    using (Graphics g = Graphics.FromHwnd(Handle))
                    {
                        var bounds = new Rectangle(0, 0, Width, Height);
                        using (var brush = new SolidBrush(UiColors.InputBg))
                        {
                            var arrowArea = new Rectangle(Math.Max(0, Width - ArrowWidth), 0, ArrowWidth, Height);
                            g.FillRectangle(brush, arrowArea);
                        }
                        PaintArrow(g, bounds);
                    }
                }
                catch
                {

                }
            }
        }

        private void PaintArrow(Graphics g, Rectangle bounds)
        {
            var arrowBounds = new Rectangle(bounds.Right - ArrowWidth, bounds.Y, ArrowWidth, bounds.Height);
            using (var brush = new SolidBrush(UiColors.InputBg))
                g.FillRectangle(brush, arrowBounds);
            UiDraw.DrawChevronDown(g, arrowBounds, Enabled ? UiColors.Arrow : UiColors.Muted);
        }
    }

    internal sealed class RoundedComboHost : Panel
    {
        private readonly FlatComboBox _combo;
        private readonly int _radius = 8;

        public ComboBox ComboBox { get { return _combo; } }

        public RoundedComboHost(ComboBox combo)
        {
            _combo = combo as FlatComboBox;
            if (_combo == null)
                throw new ArgumentException("RoundedComboHost requires FlatComboBox.", "combo");

            SetStyle(
                ControlStyles.AllPaintingInWmPaint
                | ControlStyles.UserPaint
                | ControlStyles.OptimizedDoubleBuffer
                | ControlStyles.ResizeRedraw,
                true);
            BackColor = UiColors.InputBg;
            Controls.Add(_combo);
            Height = 32;
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            UiDraw.ApplyRoundedRegion(this, _radius);
            if (_combo != null)
            {
                int textH = Math.Max(20, Height - 4);
                int textY = Math.Max(1, (Height - textH) / 2);
                _combo.SetBounds(2, textY, Math.Max(20, Width - 4), textH);
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            if (Width < 2 || Height < 2)
                return;

            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle rect = new Rectangle(0, 0, Width - 1, Height - 1);
            using (var path = UiDraw.RoundedRect(rect, _radius))
            using (var brush = new SolidBrush(UiColors.InputBg))
            using (var pen = new Pen(UiColors.InputBorder, 1.5f))
            {
                e.Graphics.FillPath(brush, path);
                e.Graphics.DrawPath(pen, path);
            }
        }
    }

    internal sealed class StyledNumericHost : Panel
    {
        private readonly int _radius = 8;
        private readonly TextBox _text;
        private readonly decimal _min;
        private readonly decimal _max;
        private readonly decimal _increment;
        private decimal _value;
        private bool _upHover;
        private bool _downHover;
        private bool _upDown;
        private bool _downDown;

        public event EventHandler ValueChanged;

        public decimal Value
        {
            get { return _value; }
            set { SetValue(value, true); }
        }

        public decimal Minimum { get { return _min; } }
        public decimal Maximum { get { return _max; } }

        public StyledNumericHost(decimal min, decimal max, decimal value, decimal increment = 1m)
        {
            _min = min;
            _max = max;
            _increment = increment <= 0 ? 1m : increment;
            _value = Clamp(value);

            SetStyle(
                ControlStyles.AllPaintingInWmPaint
                | ControlStyles.UserPaint
                | ControlStyles.OptimizedDoubleBuffer
                | ControlStyles.ResizeRedraw,
                true);
            BackColor = UiColors.InputBg;

            _text = new TextBox
            {
                BorderStyle = BorderStyle.None,
                BackColor = UiColors.InputBg,
                ForeColor = UiColors.Text,
                Font = new Font("Segoe UI", 9f),
                TextAlign = HorizontalAlignment.Left,
                Text = Format(_value)
            };
            _text.Leave += (s, e) => CommitText();
            _text.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Enter)
                {
                    CommitText();
                    e.SuppressKeyPress = true;
                }
                else if (e.KeyCode == Keys.Up)
                {
                    Nudge(_increment);
                    e.SuppressKeyPress = true;
                }
                else if (e.KeyCode == Keys.Down)
                {
                    Nudge(-_increment);
                    e.SuppressKeyPress = true;
                }
            };
            Controls.Add(_text);
            Height = 32;
        }

        private static string Format(decimal v)
        {
            return v.ToString("0", CultureInfo.InvariantCulture);
        }

        private decimal Clamp(decimal v)
        {
            if (v < _min) return _min;
            if (v > _max) return _max;
            return v;
        }

        private void SetValue(decimal v, bool updateText)
        {
            decimal next = Clamp(v);
            if (next == _value)
            {
                if (updateText && _text != null)
                    _text.Text = Format(_value);
                return;
            }
            _value = next;
            if (updateText && _text != null)
                _text.Text = Format(_value);
            if (ValueChanged != null)
                ValueChanged(this, EventArgs.Empty);
            Invalidate();
        }

        private void CommitText()
        {
            decimal parsed;
            if (decimal.TryParse(_text.Text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed)
                || decimal.TryParse(_text.Text.Trim(), NumberStyles.Integer, CultureInfo.CurrentCulture, out parsed))
            {
                SetValue(parsed, true);
            }
            else
            {
                _text.Text = Format(_value);
            }
        }

        private void Nudge(decimal delta)
        {
            SetValue(_value + delta, true);
        }

        private Rectangle UpBounds
        {
            get { return new Rectangle(Width - 22, 1, 20, Height / 2 - 1); }
        }

        private Rectangle DownBounds
        {
            get { return new Rectangle(Width - 22, Height / 2, 20, Height / 2 - 1); }
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            UiDraw.ApplyRoundedRegion(this, _radius);
            if (_text == null)
                return;
            int textH = Math.Max(16, Font.Height + 2);
            int textY = Math.Max(0, (Height - textH) / 2);
            _text.SetBounds(10, textY, Math.Max(20, Width - 36), textH);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            if (Width < 2 || Height < 2)
                return;

            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle rect = new Rectangle(0, 0, Width - 1, Height - 1);
            using (var path = UiDraw.RoundedRect(rect, _radius))
            using (var brush = new SolidBrush(UiColors.InputBg))
            using (var pen = new Pen(UiColors.InputBorder, 1.5f))
            {
                e.Graphics.FillPath(brush, path);
                e.Graphics.DrawPath(pen, path);
            }

            Color upColor = !Enabled ? UiColors.Muted
                : _upDown ? UiColors.Accent
                : _upHover ? UiColors.AccentHover
                : UiColors.Arrow;
            Color downColor = !Enabled ? UiColors.Muted
                : _downDown ? UiColors.Accent
                : _downHover ? UiColors.AccentHover
                : UiColors.Arrow;

            UiDraw.DrawChevronUp(e.Graphics, UpBounds, upColor);
            UiDraw.DrawChevronDown(e.Graphics, DownBounds, downColor);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            bool uh = UpBounds.Contains(e.Location);
            bool dh = DownBounds.Contains(e.Location);
            if (uh != _upHover || dh != _downHover)
            {
                _upHover = uh;
                _downHover = dh;
                Invalidate();
            }
            base.OnMouseMove(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            _upHover = _downHover = _upDown = _downDown = false;
            Invalidate();
            base.OnMouseLeave(e);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (!Enabled || e.Button != MouseButtons.Left)
                return;

            if (UpBounds.Contains(e.Location))
            {
                _upDown = true;
                Nudge(_increment);
                Invalidate();
            }
            else if (DownBounds.Contains(e.Location))
            {
                _downDown = true;
                Nudge(-_increment);
                Invalidate();
            }
            base.OnMouseDown(e);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            _upDown = _downDown = false;
            Invalidate();
            base.OnMouseUp(e);
        }
    }

    internal sealed class RoundedFieldHost : Panel
    {
        private readonly Control _inner;
        private readonly int _radius = 8;

        public RoundedFieldHost(Control inner, int height = 32)
        {
            _inner = inner;
            SetStyle(
                ControlStyles.AllPaintingInWmPaint
                | ControlStyles.UserPaint
                | ControlStyles.OptimizedDoubleBuffer
                | ControlStyles.ResizeRedraw,
                true);
            BackColor = UiColors.InputBg;
            if (_inner != null)
                Controls.Add(_inner);
            Height = height;
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            UiDraw.ApplyRoundedRegion(this, _radius);
            if (_inner == null)
                return;
            int textH = Math.Max(16, _inner.Font != null ? _inner.Font.Height + 2 : 18);
            int textY = Math.Max(0, (Height - textH) / 2);
            _inner.SetBounds(10, textY, Math.Max(20, Width - 20), textH);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            if (Width < 2 || Height < 2)
                return;

            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle rect = new Rectangle(0, 0, Width - 1, Height - 1);
            using (var path = UiDraw.RoundedRect(rect, _radius))
            using (var brush = new SolidBrush(UiColors.InputBg))
            using (var pen = new Pen(UiColors.InputBorder, 1.5f))
            {
                e.Graphics.FillPath(brush, path);
                e.Graphics.DrawPath(pen, path);
            }
        }
    }
}
