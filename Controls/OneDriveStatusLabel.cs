using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using WacomSignaturePdf.Theme;

namespace WacomSignaturePdf.Controls
{
    public class OneDriveStatusLabel : Control
    {
        private bool _running = false;
        private readonly Timer _pollTimer;

        public OneDriveStatusLabel()
        {
            Height = 32;
            BackColor = AppTheme.SidebarTitleBg;
            DoubleBuffered = true;
            SetStyle(ControlStyles.UserPaint |
                     ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer, true);

            _pollTimer = new Timer { Interval = 5000 };
            _pollTimer.Tick += (s, e) => Poll();
        }

        public void StartPolling()
        {
            Poll();
            _pollTimer.Start();
        }

        public void StopPolling()
        {
            _pollTimer.Stop();
        }

        private void Poll()
        {
            bool running = Process.GetProcessesByName("OneDrive").Length > 0;
            if (running == _running) return;
            _running = running;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            using (var pen = new Pen(Color.FromArgb(15, 28, 52), 1f))
                g.DrawLine(pen, 0, 0, Width, 0);

            int cx = 14, cy = Height / 2, r = 5;

            if (_running)
            {
                using (var brush = new SolidBrush(Color.FromArgb(50, 150, 230)))
                    g.FillEllipse(brush, cx - r, cy - r, r * 2, r * 2);

                using (var font = new Font("Segoe UI", 8.5f))
                using (var brush = new SolidBrush(Color.FromArgb(100, 180, 240)))
                    g.DrawString("OneDrive activ", font, brush, cx + r + 8, cy - 7);
            }
            else
            {
                using (var brush = new SolidBrush(Color.FromArgb(210, 70, 50)))
                    g.FillEllipse(brush, cx - r, cy - r, r * 2, r * 2);

                using (var font = new Font("Segoe UI", 8.5f))
                using (var brush = new SolidBrush(Color.FromArgb(200, 110, 95)))
                    g.DrawString("OneDrive inactiv", font, brush, cx + r + 8, cy - 7);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) _pollTimer?.Dispose();
            base.Dispose(disposing);
        }
    }
}