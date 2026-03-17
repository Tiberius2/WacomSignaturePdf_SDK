using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using WacomSignaturePdf.Theme;
using wgssSTU;

namespace WacomSignaturePdf.Controls
{
    /// <summary>
    /// Styled status label that checks STU device connection every 2 seconds
    /// using wgssSTU.UsbDevices 
    /// </summary>
    public class DeviceStatusLabel : Control
    {
        private bool _connected = false;

        private readonly Timer _pollTimer;
        private readonly Timer _pulseTimer;
        private float _pulseAlpha = 1f;
        private bool _pulseUp = false;

        // ── Constructor ──
        public DeviceStatusLabel()
        {
            Height = 32;
            Dock = DockStyle.Bottom;
            BackColor = AppTheme.SidebarTitleBg;
            DoubleBuffered = true;
            SetStyle(ControlStyles.UserPaint |
                     ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer, true);

            _pollTimer = new Timer { Interval = 2000 };
            _pollTimer.Tick += (s, e) => Poll();

            _pulseTimer = new Timer { Interval = 40 };
            _pulseTimer.Tick += (s, e) =>
            {
                _pulseAlpha += _pulseUp ? 0.04f : -0.04f;
                if (_pulseAlpha >= 1f) { _pulseAlpha = 1f; _pulseUp = false; }
                if (_pulseAlpha <= 0.3f) { _pulseAlpha = 0.3f; _pulseUp = true; }
                Invalidate();
            };
        }


        // ── Polling ──
        public void StartPolling()
        {
            Poll();             // immediate check on startup
            _pollTimer.Start();
        }


        // We call this when the form is closing to stop timers and avoid cross-thread issues
        public void StopPolling()
        {
            _pollTimer.Stop();
            _pulseTimer.Stop();
        }


        // Checks for connected STU devices and updates state. Called every 2 seconds by _pollTimer.
        private void Poll()
        {
            bool connected = false;
            try
            {
                var devices = new UsbDevices();
                connected = devices.Count > 0;
            }
            catch { }

            if (connected == _connected) return;

            _connected = connected;

            if (_connected) _pulseTimer.Start();
            else _pulseTimer.Stop();

            Invalidate();
        }

        // ── Paint ── Just some simple custom drawing to show connection status with a coloured dot and text (used claude)
        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            using (var pen = new Pen(Color.FromArgb(15, 28, 52), 1f))
                g.DrawLine(pen, 0, 0, Width, 0);

            int cx = 14, cy = Height / 2, r = 5;

            if (_connected)
            {
                using (var brush = new SolidBrush(Color.FromArgb((int)(_pulseAlpha * 255), 50, 210, 100)))
                    g.FillEllipse(brush, cx - r, cy - r, r * 2, r * 2);

                using (var pen = new Pen(Color.FromArgb((int)(_pulseAlpha * 90), 50, 220, 110), 1.5f))
                    g.DrawEllipse(pen, cx - r - 3, cy - r - 3, (r + 3) * 2, (r + 3) * 2);

                using (var font = new Font("Segoe UI", 8.5f))
                using (var brush = new SolidBrush(Color.FromArgb(100, 220, 140)))
                    g.DrawString("STU-540 conectat", font, brush, cx + r + 8, cy - 7);
            }
            else
            {
                using (var brush = new SolidBrush(Color.FromArgb(210, 70, 50)))
                    g.FillEllipse(brush, cx - r, cy - r, r * 2, r * 2);

                using (var font = new Font("Segoe UI", 8.5f))
                using (var brush = new SolidBrush(Color.FromArgb(200, 110, 95)))
                    g.DrawString("Dispozitiv deconectat", font, brush, cx + r + 8, cy - 7);
            }
        }


        /// ── Cleanup ── Dispose timers when the control is disposed to avoid cross-thread issues
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _pollTimer?.Dispose();
                _pulseTimer?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}