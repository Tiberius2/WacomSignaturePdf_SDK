using System;
using System.Drawing;
using System.Windows.Forms;
using PdfiumViewer;

namespace WacomSignaturePdf
{

    /// <summary>
    /// A borderless form that displays a PDF document using PdfiumViewer, 
    /// intended to be shown on a secondary monitor as a "mirror" of the main viewer.
    /// Controls are done from the mainform, this form only exposes methods to sync page, zoom and scroll position.
    /// </summary>
    public partial class MirrorForm : Form
    {
        public PdfViewer MirrorViewer { get; private set; }

        private Label lblWatermark;

        public MirrorForm()
        {
            this.FormBorderStyle = FormBorderStyle.None;
            this.WindowState = FormWindowState.Normal;
            this.StartPosition = FormStartPosition.Manual;
            this.BackColor = Color.FromArgb(30, 30, 30);
            this.ShowInTaskbar = false;
            this.TopMost = false;

            MirrorViewer = new PdfViewer
            {
                Dock = DockStyle.Fill,
                ShowToolbar = false,
                ShowBookmarks = false
            };

            lblWatermark = new Label
            {
                Text = "PREVIZUALIZARE DOCUMENT",
                Dock = DockStyle.Bottom,
                Height = 34,
                Font = new Font("Segoe UI", 10f, FontStyle.Bold),
                ForeColor = Color.FromArgb(160, 180, 220),
                BackColor = Color.FromArgb(20, 36, 64),
                TextAlign = ContentAlignment.MiddleCenter
            };

            this.Controls.Add(MirrorViewer);
            this.Controls.Add(lblWatermark);
        }

        /// Shows the form on the specified screen, maximizing it to fill the screen bounds.
        public void ShowOnScreen(Screen screen)
        {
            this.WindowState = FormWindowState.Normal;
            this.Show();
            this.Location = screen.Bounds.Location;
            this.Size = screen.Bounds.Size;
        }


        // Loads a PDF document from the specified file path into the MirrorViewer.
        public void LoadFromPath(string pdfPath)
        {
            try
            {
                var old = MirrorViewer.Document;
                MirrorViewer.Document = PdfDocument.Load(pdfPath);
                MirrorViewer.Renderer.ZoomMode = PdfViewerZoomMode.FitWidth;
                old?.Dispose();
                
            }
            catch { }
        }


        // Disposes the current PDF document in the MirrorViewer and sets it to null, effectively clearing the viewer.
        public void ClearDocument()
        {
            var old = MirrorViewer.Document;
            old?.Dispose();
            MirrorViewer.Document = null;
        }

        /// <summary>
        /// Syncs scroll position using SetDisplayRectLocation.
        /// scrollPosition is a positive Point (negated DisplayRectangle.Location from the source viewer).
        /// SetDisplayRectLocation expects negative coordinates.
        /// </summary>
        public void SyncScrollRatio(PointF ratio)
        {
            try
            {
                if (MirrorViewer.Renderer == null) return;

                var display = MirrorViewer.Renderer.DisplayRectangle;
                int totalScrollableY = display.Height - MirrorViewer.Renderer.ClientSize.Height;
                int totalScrollableX = display.Width - MirrorViewer.Renderer.ClientSize.Width;

                int targetX = totalScrollableX > 0 ? -(int)(ratio.X * totalScrollableX) : 0;
                int targetY = totalScrollableY > 0 ? -(int)(ratio.Y * totalScrollableY) : 0;

                MirrorViewer.Renderer.SetDisplayRectLocation(new Point(targetX, targetY));
            }
            catch { }
        }


        // Syncs the zoom level of the MirrorViewer to match the specified zoom value.
        public void SyncZoom(double zoom)
        {
            try
            {
                if (MirrorViewer.Renderer != null)
                    MirrorViewer.Renderer.Zoom = zoom;
            }
            catch { }
        }


        // Syncs the current page of the MirrorViewer to match the specified page number.
        public void SyncPage(int page)
        {
            try
            {
                if (MirrorViewer.Renderer != null)
                    MirrorViewer.Renderer.Page = page;
            }
            catch { }
        }


        // Overrides the OnFormClosing method to prevent the form from being closed by the user.
        // Instead, it hides the form when the user attempts to close it,
        // allowing it to be shown again later without needing to recreate it.
        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                Hide();
            }
            else
            {
                base.OnFormClosing(e);
            }
        }
    }
}