using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace WacomSignaturePdf.Controls
{
    /// <summary>
    /// iOS-style toggle switch. Fires <see cref="Toggled"/> when the user clicks.
    /// Read <see cref="IsOn"/> to get the current state.
    /// </summary>
    public class ToggleSwitch : Control
    {
        // ── State ─────────────────────────────────────────────────────────────────
        private bool _isOn = false;

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

        // ── Appearance ────────────────────────────────────────────────────────────
        public Color ColorOn { get; set; } = Color.FromArgb(33, 150, 243);   // blue
        public Color ColorOff { get; set; } = Color.FromArgb(80, 80, 80);     // dark grey
        public Color KnobColor { get; set; } = Color.White;

        // ── Constructor ───────────────────────────────────────────────────────────
        public ToggleSwitch()
        {
            Size = new Size(56, 28);
            Cursor = Cursors.Hand;
            DoubleBuffered = true;
            SetStyle(ControlStyles.UserPaint |
                     ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer, true);
        }

        // ── Input ─────────────────────────────────────────────────────────────────
        protected override void OnClick(EventArgs e)
        {
            IsOn = !IsOn;
            base.OnClick(e);
        }

        // ── Paint ─────────────────────────────────────────────────────────────────
        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            int w = Width, h = Height;
            int radius = h / 2;

            // Track
            var trackRect = new Rectangle(0, 0, w - 1, h - 1);
            using (var path = RoundedRect(trackRect, radius))
            using (var brush = new SolidBrush(_isOn ? ColorOn : ColorOff))
                g.FillPath(brush, path);

            // Knob
            int padding = 3;
            int knobSize = h - padding * 2;
            int knobX = _isOn ? w - knobSize - padding - 1 : padding;
            var knobRect = new Rectangle(knobX, padding, knobSize, knobSize);

            using (var brush = new SolidBrush(KnobColor))
                g.FillEllipse(brush, knobRect);
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