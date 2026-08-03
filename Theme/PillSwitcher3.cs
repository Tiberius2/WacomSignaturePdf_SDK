using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace WacomSignaturePdf.Controls
{
    public class PillSwitcher3 : Panel
    {
        private int _selectedIndex = 0;
        private bool _switchEnabled = true;

        private readonly Button _btn0;
        private readonly Button _btn1;
        private readonly Button _btn2;
        private readonly string _text0;
        private readonly string _text1;
        private readonly string _text2;

        private const int PillRadius = 8;

        public Color ActiveBg { get; set; } = Color.White;
        public Color ActiveFg { get; set; } = Color.FromArgb(74, 48, 0);
        public Color InactiveFg { get; set; } = Color.FromArgb(190, 210, 245);
        public Color HoverBg { get; set; } = Color.FromArgb(40, 255, 255, 255);

        public event EventHandler SelectionChanged;

        public int SelectedIndex
        {
            get => _selectedIndex;
            set
            {
                if (value < 0 || value > 2 || _selectedIndex == value) return;
                _selectedIndex = value;
                UpdateAppearance();
                SelectionChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        public void SetSilent(int value)
        {
            if (value < 0 || value > 2 || _selectedIndex == value) return;
            _selectedIndex = value;
            UpdateAppearance();
        }

        public new bool Enabled
        {
            get => _switchEnabled;
            set
            {
                _switchEnabled = value;
                var cur = value ? Cursors.Hand : Cursors.Default;
                _btn0.Cursor = _btn1.Cursor = _btn2.Cursor = cur;
                Invalidate();
            }
        }

        public PillSwitcher3(string text0, string text1, string text2)
        {
            _text0 = text0;
            _text1 = text1;
            _text2 = text2;

            DoubleBuffered = true;
            BackColor = Color.Transparent;

            _btn0 = MakeButton();
            _btn1 = MakeButton();
            _btn2 = MakeButton();

            _btn0.Click += (s, e) => { if (_switchEnabled) SelectedIndex = 0; };
            _btn1.Click += (s, e) => { if (_switchEnabled) SelectedIndex = 1; };
            _btn2.Click += (s, e) => { if (_switchEnabled) SelectedIndex = 2; };

            Controls.Add(_btn0);
            Controls.Add(_btn1);
            Controls.Add(_btn2);

            Paint += OnPaintContainer;

            _btn0.MouseEnter += (s, e) => Invalidate();
            _btn0.MouseLeave += (s, e) => Invalidate();
            _btn1.MouseEnter += (s, e) => Invalidate();
            _btn1.MouseLeave += (s, e) => Invalidate();
            _btn2.MouseEnter += (s, e) => Invalidate();
            _btn2.MouseLeave += (s, e) => Invalidate();

            _btn0.Paint += (s, e) => PaintBtn(e.Graphics, _btn0, _text0, _selectedIndex == 0);
            _btn1.Paint += (s, e) => PaintBtn(e.Graphics, _btn1, _text1, _selectedIndex == 1);
            _btn2.Paint += (s, e) => PaintBtn(e.Graphics, _btn2, _text2, _selectedIndex == 2);

            Resize += (s, e) => LayoutButtons();
            LayoutButtons();
        }

        private void LayoutButtons()
        {
            int third = Width / 3;
            int h = Height;
            _btn0.SetBounds(2, 2, third - 2, h - 4);
            _btn1.SetBounds(third, 2, third, h - 4);
            _btn2.SetBounds(third * 2, 2, Width - third * 2 - 2, h - 4);
        }

        private void OnPaintContainer(object sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            // Track
            var track = new Rectangle(0, 0, Width - 1, Height - 1);
            using (var br = new SolidBrush(_switchEnabled
                ? Color.FromArgb(55, 255, 255, 255)
                : Color.FromArgb(30, 255, 255, 255)))
            using (var path = MakeRoundRect(track, PillRadius + 2))
                g.FillPath(br, path);

            using (var pen = new Pen(Color.FromArgb(80, 110, 180), 1.5f))
            using (var path = MakeRoundRect(track, PillRadius + 2))
                g.DrawPath(pen, path);

            // Active pill rect
            int third = Width / 3;
            Rectangle activeRect;
            if (_selectedIndex == 0)
                activeRect = new Rectangle(2, 2, third - 3, Height - 5);
            else if (_selectedIndex == 1)
                activeRect = new Rectangle(third, 2, third - 1, Height - 5);
            else
                activeRect = new Rectangle(third * 2, 2, Width - third * 2 - 3, Height - 5);

            // Shadow sub pill activ
            var shadowRect = new Rectangle(activeRect.X + 2, activeRect.Bottom - 1, activeRect.Width - 4, 3);
            using (var br = new LinearGradientBrush(shadowRect,
                Color.FromArgb(60, 0, 0, 0), Color.Transparent, 90f))
                g.FillRectangle(br, shadowRect);

            // Culori per index:
            // 0 = Toate Semnaturile → albastru neutru
            // 1 = Semn. Angajat     → auriu
            // 2 = Semn. Interne     → violet
            Color topColor, botColor, borderColor;
            if (!_switchEnabled)
            {
                topColor = Color.FromArgb(180, 190, 210);
                botColor = Color.FromArgb(140, 155, 180);
                borderColor = Color.FromArgb(120, 140, 170);
            }
            else if (_selectedIndex == 0)
            {
                topColor = Color.FromArgb(180, 220, 255);
                botColor = Color.FromArgb(90, 160, 230);
                borderColor = Color.FromArgb(60, 130, 200);
            }
            else if (_selectedIndex == 1)
            {
                topColor = Color.FromArgb(255, 230, 128);
                botColor = Color.FromArgb(240, 184, 0);
                borderColor = Color.FromArgb(200, 160, 0);
            }
            else
            {
                topColor = Color.FromArgb(200, 190, 255);
                botColor = Color.FromArgb(130, 115, 220);
                borderColor = Color.FromArgb(100, 85, 190);
            }

            using (var br = new LinearGradientBrush(activeRect, topColor, botColor, 90f))
            using (var path = MakeRoundRect(activeRect, PillRadius))
                g.FillPath(br, path);

            using (var pen = new Pen(borderColor, 1.2f))
            using (var path = MakeRoundRect(activeRect, PillRadius))
                g.DrawPath(pen, path);
        }

        private void PaintBtn(Graphics g, Button btn, string text, bool active)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            if (!active && _switchEnabled)
            {
                bool hov = btn.ClientRectangle.Contains(btn.PointToClient(Control.MousePosition));
                if (hov)
                {
                    using (var br = new SolidBrush(HoverBg))
                    using (var path = MakeRoundRect(new Rectangle(0, 0, btn.Width - 1, btn.Height - 1), PillRadius))
                        g.FillPath(br, path);
                }
            }

            Color fg;
            if (active)
            {
                if (!_switchEnabled)
                    fg = Color.FromArgb(80, 100, 140);
                else if (_selectedIndex == 0)
                    fg = Color.FromArgb(0, 50, 110);   // albastru inchis pe albastru deschis
                else if (_selectedIndex == 1)
                    fg = Color.FromArgb(74, 48, 0);    // maro inchis pe auriu
                else
                    fg = Color.FromArgb(40, 28, 100);  // violet inchis pe violet deschis
            }
            else
            {
                fg = _switchEnabled ? InactiveFg : Color.FromArgb(100, 130, 170);
            }

            using (var font = new Font("Segoe UI", active ? 9f : 8.5f, active ? FontStyle.Bold : FontStyle.Regular))
                TextRenderer.DrawText(g, text, font, btn.ClientRectangle, fg,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
                    TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
        }

        public void SetTooltip(ToolTip toolTip, string text)
        {
            toolTip.SetToolTip(_btn0, text);
            toolTip.SetToolTip(_btn1, text);
            toolTip.SetToolTip(_btn2, text);
        }

        private void UpdateAppearance()
        {
            Invalidate();
            _btn0.Invalidate();
            _btn1.Invalidate();
            _btn2.Invalidate();
        }

        private static Button MakeButton()
        {
            var btn = new Button
            {
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.Transparent,
                UseVisualStyleBackColor = false,
                TabStop = false,
                Cursor = Cursors.Hand,
            };
            btn.FlatAppearance.BorderSize = 0;
            btn.FlatAppearance.MouseOverBackColor = Color.Transparent;
            btn.FlatAppearance.MouseDownBackColor = Color.Transparent;
            return btn;
        }

        private static GraphicsPath MakeRoundRect(Rectangle r, int radius)
        {
            if (radius <= 0) radius = 1;
            var path = new GraphicsPath();
            path.AddArc(r.X, r.Y, radius * 2, radius * 2, 180, 90);
            path.AddArc(r.Right - radius * 2, r.Y, radius * 2, radius * 2, 270, 90);
            path.AddArc(r.Right - radius * 2, r.Bottom - radius * 2, radius * 2, radius * 2, 0, 90);
            path.AddArc(r.X, r.Bottom - radius * 2, radius * 2, radius * 2, 90, 90);
            path.CloseFigure();
            return path;
        }
    }
}