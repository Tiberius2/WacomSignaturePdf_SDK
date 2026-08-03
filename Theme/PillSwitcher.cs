using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace WacomSignaturePdf.Controls
{
    public class PillSwitcher : Panel
    {
        private bool _isOn;
        private bool _switchEnabled = true;

        private readonly Button _btnLeft;
        private readonly Button _btnRight;
        private readonly string _leftText;
        private readonly string _rightText;

        private const int PillRadius = 8;

        public Color TrackBg { get; set; } = Color.FromArgb(55, 255, 255, 255);
        public Color ActiveBg { get; set; } = Color.White;
        public Color ActiveFg { get; set; } = Color.FromArgb(33, 41, 82);
        public Color InactiveFg { get; set; } = Color.FromArgb(180, 255, 255, 255);
        public Color HoverBg { get; set; } = Color.FromArgb(40, 255, 255, 255);

        public event EventHandler Toggled;

        public bool IsOn
        {
            get => _isOn;
            set
            {
                if (_isOn == value) return;
                _isOn = value;
                UpdateAppearance();
                Toggled?.Invoke(this, EventArgs.Empty);
            }
        }

        public void SetSilent(bool value)
        {
            if (_isOn == value) return;
            _isOn = value;
            UpdateAppearance();
        }

        public new bool Enabled
        {
            get => _switchEnabled;
            set
            {
                _switchEnabled = value;
                _btnLeft.Cursor = value ? Cursors.Hand : Cursors.Default;
                _btnRight.Cursor = value ? Cursors.Hand : Cursors.Default;
                Invalidate();
            }
        }

        public PillSwitcher(string leftText, string rightText)
        {
            _leftText = leftText;
            _rightText = rightText;

            DoubleBuffered = true;
            BackColor = Color.Transparent;

            _btnLeft = MakeButton();
            _btnLeft.Click += (s, e) => { if (_switchEnabled && _isOn) IsOn = false; };

            _btnRight = MakeButton();
            _btnRight.Click += (s, e) => { if (_switchEnabled && !_isOn) IsOn = true; };

            Controls.Add(_btnLeft);
            Controls.Add(_btnRight);

            Paint += OnPaintContainer;

            _btnLeft.MouseEnter += (s, e) => Invalidate();
            _btnLeft.MouseLeave += (s, e) => Invalidate();
            _btnRight.MouseEnter += (s, e) => Invalidate();
            _btnRight.MouseLeave += (s, e) => Invalidate();

            _btnLeft.Paint += (s, e) => PaintBtn(e.Graphics, _btnLeft, _leftText, !_isOn);
            _btnRight.Paint += (s, e) => PaintBtn(e.Graphics, _btnRight, _rightText, _isOn);

            Resize += (s, e) => LayoutButtons();
            LayoutButtons();
        }

        private void LayoutButtons()
        {
            int half = Width / 2;
            int h = Height;
            _btnLeft.SetBounds(2, 2, half - 2, h - 4);
            _btnRight.SetBounds(half, 2, Width - half - 2, h - 4);
        }

        private void OnPaintContainer(object sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            // Track
            var track = new Rectangle(0, 0, Width - 1, Height - 1);
            using (var br = new SolidBrush(Color.FromArgb(55, 255, 255, 255)))
            using (var path = MakeRoundRect(track, PillRadius + 2))
                g.FillPath(br, path);

            using (var pen = new Pen(Color.FromArgb(80, 110, 180), 1.5f))
            using (var path = MakeRoundRect(track, PillRadius + 2))
                g.DrawPath(pen, path);

            // Active pill rect
            int half = Width / 2;
            var activeRect = !_isOn
                ? new Rectangle(2, 2, half - 3, Height - 5)
                : new Rectangle(half, 2, Width - half - 3, Height - 5);

            // Shadow sub pill activ
            var shadowRect = new Rectangle(activeRect.X + 2, activeRect.Bottom - 1, activeRect.Width - 4, 3);
            using (var br = new LinearGradientBrush(shadowRect,
                Color.FromArgb(60, 0, 0, 0), Color.Transparent, 90f))
                g.FillRectangle(br, shadowRect);

            // Gradient pe pill activ — verde mint (sus ActiveBg, jos mai inchis)
            Color topColor = _switchEnabled ? ActiveBg : Color.FromArgb(180, 190, 210);
            Color botColor = _switchEnabled
                ? Color.FromArgb(
                    Math.Max(0, ActiveBg.R - 30),
                    Math.Max(0, ActiveBg.G - 20),
                    Math.Max(0, ActiveBg.B - 10))
                : Color.FromArgb(140, 155, 180);

            using (var br = new LinearGradientBrush(activeRect, topColor, botColor, 90f))
            using (var path = MakeRoundRect(activeRect, PillRadius))
                g.FillPath(br, path);

            // Border pe pill activ
            using (var pen = new Pen(Color.FromArgb(160, Math.Max(0, ActiveBg.R - 60), Math.Max(0, ActiveBg.G - 30), Math.Max(0, ActiveBg.B - 20)), 1.2f))
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

            Color fg = active
                ? (_switchEnabled ? ActiveFg : Color.FromArgb(80, 100, 140))
                : (_switchEnabled ? InactiveFg : Color.FromArgb(100, 130, 170));

            using (var font = new Font("Segoe UI", active ? 9f : 8.5f, active ? FontStyle.Bold : FontStyle.Regular))
                TextRenderer.DrawText(g, text, font, btn.ClientRectangle, fg,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
                    TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
        }

        public void SetTooltip(ToolTip toolTip, string text)
        {
            toolTip.SetToolTip(_btnLeft, text);
            toolTip.SetToolTip(_btnRight, text);
        }

        private void UpdateAppearance()
        {
            Invalidate();
            _btnLeft.Invalidate();
            _btnRight.Invalidate();
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