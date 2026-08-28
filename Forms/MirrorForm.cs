using PdfiumViewer;
using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace WacomSignaturePdf
{
    // Read-only PDF viewer shown on a secondary monitor.
    // All navigation is driven from MainForm via the Sync* methods.
    public partial class MirrorForm : Form
    {
        public PdfViewer MirrorViewer { get; private set; }
        private string _tempPath;

        public MirrorForm()
        {
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            BackColor = Color.FromArgb(30, 30, 30);
            ShowInTaskbar = false;

            MirrorViewer = new PdfViewer
            {
                Dock = DockStyle.Fill,
                ShowToolbar = false,
                ShowBookmarks = false
            };

            var lblWatermark = new Label
            {
                Text = "PREVIZUALIZARE DOCUMENT",
                Dock = DockStyle.Bottom,
                Height = 34,
                Font = new Font("Segoe UI", 10f, FontStyle.Bold),
                ForeColor = Color.FromArgb(160, 180, 220),
                BackColor = Color.FromArgb(20, 36, 64),
                TextAlign = ContentAlignment.MiddleCenter
            };

            Controls.Add(MirrorViewer);
            Controls.Add(lblWatermark);
        }

        public void ShowOnScreen(Screen screen)
        {
            WindowState = FormWindowState.Normal;
            Show();
            Location = screen.Bounds.Location;
            Size = screen.Bounds.Size;
        }

        public void LoadFromPath(string pdfPath)
        {
            try
            {
                // Lucreaza pe o copie temp ca sa nu blocheze fisierul original
                string newTemp = Path.Combine(Path.GetTempPath(),
                    $"wacom_mirror_{DateTime.Now:yyyyMMdd_HHmmss_fff}.pdf");
                File.Copy(pdfPath, newTemp, overwrite: true);

                var old = MirrorViewer.Document;
                MirrorViewer.Document = PdfDocument.Load(newTemp);
                MirrorViewer.Renderer.ZoomMode = PdfViewerZoomMode.FitBest;
                old?.Dispose();

                // Sterge temp-ul anterior
                if (_tempPath != null && File.Exists(_tempPath) && _tempPath != newTemp)
                    try { File.Delete(_tempPath); } catch { }
                _tempPath = newTemp;
            }
            catch { }
        }

        public void ClearDocument()
        {
            var old = MirrorViewer.Document;
            MirrorViewer.Document = null;
            old?.Dispose();

            if (_tempPath != null && File.Exists(_tempPath))
                try { File.Delete(_tempPath); } catch { }
            _tempPath = null;
        }

        // SyncScrollRatio uses SetDisplayRectLocation which expects negative coordinates.
        public void SyncScrollRatio(PointF ratio)
        {
            try
            {
                if (MirrorViewer.Renderer == null) return;

                var display = MirrorViewer.Renderer.DisplayRectangle;
                int scrollableY = display.Height - MirrorViewer.Renderer.ClientSize.Height;
                int scrollableX = display.Width - MirrorViewer.Renderer.ClientSize.Width;

                MirrorViewer.Renderer.SetDisplayRectLocation(new Point(
                    scrollableX > 0 ? -(int)(ratio.X * scrollableX) : 0,
                    scrollableY > 0 ? -(int)(ratio.Y * scrollableY) : 0));
            }
            catch { }
        }

        public void SyncZoom(double zoom)
        {
            try { if (MirrorViewer.Renderer != null) MirrorViewer.Renderer.Zoom = zoom; }
            catch { }
        }

        public void SyncPage(int page)
        {
            try { if (MirrorViewer.Renderer != null) MirrorViewer.Renderer.Page = page; }
            catch { }
        }

        // Hide on user close so the form can be reused without recreation.
        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (e.CloseReason == CloseReason.UserClosing) { e.Cancel = true; Hide(); }
            else base.OnFormClosing(e);
        }
    }
}