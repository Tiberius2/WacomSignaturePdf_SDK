using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using WacomSignaturePdf.Theme;

namespace WacomSignaturePdf.Controls
{
    // iOS-style toggle switch.
    // Fires Toggled when the user clicks. Read IsOn for current state.
    public class ToggleSwitch : Control
    {
        private bool _isOn = true;

        public bool IsOn
        {
            get => _isOn;
            set
            {
                if (_isOn == value) return;
                _isOn = value;
                Invalidate();
                Toggled?.Invoke(this, EventArgs.Empty);
            }
        }

        public event EventHandler Toggled;

        public Color ColorOn { get; set; } = AppTheme.SwitchOn;
        public Color ColorOff { get; set; } = AppTheme.SwitchOff;
        public Color KnobColor { get; set; } = Color.White;

        public ToggleSwitch()
        {
            Size = new Size(56, 28);
            Cursor = Cursors.Hand;
            DoubleBuffered = true;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer, true);
        }

        protected override void OnClick(EventArgs e) { IsOn = !IsOn; base.OnClick(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            int w = Width, h = Height, radius = h / 2;

            using (var path = RoundedRect(new Rectangle(0, 0, w - 1, h - 1), radius))
            using (var brush = new SolidBrush(_isOn ? ColorOn : ColorOff))
                g.FillPath(brush, path);

            int padding = 3;
            int knobSize = h - padding * 2;
            int knobX = _isOn ? w - knobSize - padding - 1 : padding;

            using (var brush = new SolidBrush(KnobColor))
                g.FillEllipse(brush, knobX, padding, knobSize, knobSize);
        }

        private static GraphicsPath RoundedRect(Rectangle r, int radius)
        {
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
