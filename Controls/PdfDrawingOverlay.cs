using PdfiumViewer;
using PdfSharp.Fonts;
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Windows.Forms;
using PdfViewerDocument = PdfiumViewer.PdfDocument;

namespace WacomSignaturePdf.Controls
{
    /// <summary>
    /// Wrapper peste PdfViewer cu suport pentru desenat dreptunghiuri (FreeForm)
    /// si preview ghost slots (Template + FreeForm) via PDF temporar.
    ///
    /// Ghost slots: desenam dreptunghiurile direct in PDF via PdfSharp → fisier temp.
    /// Avantaj: zoom/scroll/redraw perfect, zero cod de coordonate viewport.
    /// </summary>
    public class PdfDrawingOverlay : Panel
    {
        public event Action<DrawnRectangle> RectangleDrawn;
        public event Action DrawingAborted;
        public bool DrawingEnabled { get; private set; }

        private readonly PdfViewer _viewer;
        private PdfViewerDocument _document;
        private DrawingOverlayControl _overlay;

        private string _realPdfPath;
        private string _previewTempPath;

        private DrawnRectangle[] _previewSlots = new DrawnRectangle[0];
        private bool[] _previewSlotsSign = new bool[0];

        public PdfDrawingOverlay()
        {
            DoubleBuffered = true;
            _viewer = new PdfViewer
            {
                Dock = DockStyle.Fill,
                ShowToolbar = false,
                ShowBookmarks = false
            };
            Controls.Add(_viewer);
        }

        // ── Document ──────────────────────────────────────────────────────────────

        public void LoadDocument(string pdfPath, bool fitPage = false)
        {
            if (_viewer.Parent == null) Controls.Add(_viewer);
            CleanupPreviewTemp();
            UnloadDocument();
            _realPdfPath = pdfPath;
            LoadIntoViewer(pdfPath);
            _viewer.Renderer.ZoomMode = fitPage ? PdfViewerZoomMode.FitBest : PdfViewerZoomMode.FitWidth;
            RemountOverlay();
        }

        private void LoadIntoViewer(string pdfPath)
        {
            var ms = new MemoryStream(File.ReadAllBytes(pdfPath));
            _document?.Dispose();
            _document = PdfViewerDocument.Load(ms);
            _viewer.Document = _document;
        }

        private void RemountOverlay()
        {
            if (_overlay == null) return;
            if (_overlay.Parent != null)
                _overlay.Parent.Controls.Remove(_overlay);
            if (_viewer.Renderer != null)
            {
                _viewer.Renderer.Controls.Add(_overlay);
                _overlay.Visible = false;
            }
        }

        // ── Scroll restore ────────────────────────────────────────────────────────

        private double _pendingZoom;
        private int _pendingPage;
        private PdfPoint _pendingPdfCenter;
        private bool _hasPendingRestore;
        private System.Windows.Forms.Timer _restoreTimer;

        private void ScheduleScrollRestore(double zoom, int page, PdfPoint pdfCenter, int delayMs = 80)
        {
            _pendingZoom = zoom;
            _pendingPage = page;
            _pendingPdfCenter = pdfCenter;
            _hasPendingRestore = true;

            if (_restoreTimer == null)
            {
                _restoreTimer = new System.Windows.Forms.Timer();
                _restoreTimer.Tick += RestoreTimer_Tick;
            }
            _restoreTimer.Stop();
            _restoreTimer.Interval = delayMs;
            _restoreTimer.Start();
        }

        private void RestoreTimer_Tick(object sender, EventArgs e)
        {
            _restoreTimer.Stop();
            if (!_hasPendingRestore) return;
            _hasPendingRestore = false;

            var renderer = _viewer.Renderer;
            if (renderer == null) return;

            try { renderer.Zoom = _pendingZoom; } catch { }

            try
            {
                if (_pendingPdfCenter.Page >= 0)
                {
                    var target = new PdfiumViewer.PdfRectangle(
                        _pendingPdfCenter.Page,
                        new RectangleF(_pendingPdfCenter.Location.X, _pendingPdfCenter.Location.Y, 1f, 1f));
                    renderer.ScrollIntoView(target);
                }
                else
                {
                    renderer.Page = _pendingPage;
                }
            }
            catch { try { renderer.Page = _pendingPage; } catch { } }
        }

        public void ReloadDocument(string pdfPath)
        {
            if (_viewer.Parent == null) Controls.Add(_viewer);

            var renderer = _viewer.Renderer;
            double savedZoom = 1.0;
            int savedPage = 0;
            PdfPoint savedCenter = default;

            try
            {
                savedZoom = renderer.Zoom;
                savedPage = renderer.Page;
                var center = new Point(renderer.Width / 2, renderer.Height / 2);
                savedCenter = renderer.PointToPdf(center);
            }
            catch { }

            _realPdfPath = pdfPath;

            if (_previewSlots.Length > 0)
            {
                GenerateAndLoadPreview();
                ScheduleScrollRestore(savedZoom, savedPage, savedCenter, 400);
            }
            else
            {
                _document?.Dispose();
                var ms = new MemoryStream(File.ReadAllBytes(pdfPath));
                _document = PdfViewerDocument.Load(ms);
                renderer.Load(_document);
                ScheduleScrollRestore(savedZoom, savedPage, savedCenter, 80);
            }
        }

        private static byte[] _emptyPdfBytes;
        private static byte[] GetEmptyPdfBytes()
        {
            if (_emptyPdfBytes != null) return _emptyPdfBytes;
            using (var ms = new MemoryStream())
            {
                var doc = new PdfSharp.Pdf.PdfDocument();
                doc.AddPage();
                doc.Save(ms, closeStream: false);
                _emptyPdfBytes = ms.ToArray();
            }
            return _emptyPdfBytes;
        }

        public void UnloadDocument()
        {
            EnableDrawing(false);
            CleanupPreviewTemp();
            _realPdfPath = null;
            _previewSlots = new DrawnRectangle[0];
            _previewSlotsSign = new bool[0];

            var oldDoc = _document;
            _document = null;

            try
            {
                var ms = new MemoryStream(GetEmptyPdfBytes());
                var emptyDoc = PdfViewerDocument.Load(ms);
                _viewer.Document = emptyDoc;
            }
            catch { }

            oldDoc?.Dispose();
        }

        public void UnloadViewerOnly()
        {
            if (_viewer.Parent != null) Controls.Remove(_viewer);
            _viewer.Document = null;
            _document?.Dispose();
            _document = null;
        }

        public bool HasDocument => _document != null;
        public void ZoomIn() => _viewer.Renderer?.ZoomIn();
        public void ZoomOut() => _viewer.Renderer?.ZoomOut();
        public PdfRenderer Renderer => _viewer.Renderer;

        public void SaveScrollState() { }
        public void LoadDocumentRestoring(string pdfPath) => ReloadDocument(pdfPath);

        // ── Ghost slot preview via PDF temporar ───────────────────────────────────

        public void SetPreviewSlots(DrawnRectangle[] slots, bool[] signed = null)
        {
            _previewSlots = slots ?? new DrawnRectangle[0];
            _previewSlotsSign = signed ?? new bool[_previewSlots.Length];

            if (_previewSlots.Length == 0 || _realPdfPath == null)
            {
                ClearPreviewSlots();
                return;
            }

            GenerateAndLoadPreview();
        }

        public void ClearPreviewSlots()
        {
            _previewSlots = new DrawnRectangle[0];
            _previewSlotsSign = new bool[0];
            CleanupPreviewTemp();

            if (_realPdfPath != null && File.Exists(_realPdfPath))
            {
                try
                {
                    var ms = new MemoryStream(File.ReadAllBytes(_realPdfPath));
                    _document?.Dispose();
                    _document = PdfViewerDocument.Load(ms);
                    _viewer.Renderer?.Load(_document);
                }
                catch { }
            }
        }

        private void GenerateAndLoadPreview()
        {
            if (_realPdfPath == null || !File.Exists(_realPdfPath)) return;

            var slotsSnap = _previewSlots;
            var signedSnap = _previewSlotsSign;
            var realPathSnap = _realPdfPath;

            double savedZoom = 1.0;
            int savedPage = 0;
            PdfPoint savedCenter = default;
            try
            {
                var renderer = _viewer.Renderer;
                savedZoom = renderer.Zoom;
                savedPage = renderer.Page;
                var center = new Point(renderer.Width / 2, renderer.Height / 2);
                savedCenter = renderer.PointToPdf(center);
            }
            catch { }

            var tempPath = Path.Combine(
                Path.GetTempPath(),
                $"wacom_preview_{DateTime.Now:yyyyMMdd_HHmmss_fff}.pdf");

            System.Threading.ThreadPool.QueueUserWorkItem(_ =>
            {
                try
                {
                    GeneratePreviewPdf(realPathSnap, tempPath, slotsSnap, signedSnap);

                    if (!File.Exists(tempPath)) return;

                    this.BeginInvoke(new Action(() =>
                    {
                        try
                        {
                            CleanupPreviewTemp();
                            _previewTempPath = tempPath;

                            var renderer = _viewer.Renderer;
                            if (renderer == null) return;

                            renderer.SuspendLayout();
                            try
                            {
                                var ms = new MemoryStream(File.ReadAllBytes(tempPath));
                                _document?.Dispose();
                                _document = PdfViewerDocument.Load(ms);
                                renderer.Load(_document);
                            }
                            finally
                            {
                                renderer.ResumeLayout(false);
                            }

                            ScheduleScrollRestore(savedZoom, savedPage, savedCenter);
                        }
                        catch { }
                    }));
                }
                catch
                {
                    try { File.Delete(tempPath); } catch { }
                }
            });
        }

        private static bool _fontResolverSet = false;
        private static readonly object _fontResolverLock = new object();

        private static void EnsureFontResolver()
        {
            if (_fontResolverSet) return;
            lock (_fontResolverLock)
            {
                if (_fontResolverSet) return;
                if (GlobalFontSettings.FontResolver == null)
                    GlobalFontSettings.FontResolver = new WindowsFontResolver();
                _fontResolverSet = true;
            }
        }

        private void GeneratePreviewPdf(string sourcePath, string destPath,
            DrawnRectangle[] slots, bool[] signedArr)
        {
            EnsureFontResolver();

            string tempWithGroup = destPath + "_grp.pdf";
            try
            {
                var psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "gswin32c.exe",
                    Arguments = $"-sDEVICE=pdfwrite -dCompatibilityLevel=1.4 -dPDFSETTINGS=/ebook " +
                                $"-dNOPAUSE -dQUIET -dBATCH " +
                                $"-sOutputFile=\"{tempWithGroup}\" \"{sourcePath}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardError = true
                };
                using (var proc = System.Diagnostics.Process.Start(psi))
                {
                    proc.WaitForExit();
                    if (proc.ExitCode != 0 || !File.Exists(tempWithGroup))
                        tempWithGroup = sourcePath;
                }
            }
            catch
            {
                tempWithGroup = sourcePath;
            }

            try
            {
                byte[] sourceBytes = File.ReadAllBytes(tempWithGroup);
                using (var ms = new MemoryStream(sourceBytes))
                using (var doc = PdfSharp.Pdf.IO.PdfReader.Open(ms, PdfSharp.Pdf.IO.PdfDocumentOpenMode.Modify))
                {
                    foreach (var pg in doc.Pages)
                    {
                        if (!pg.Elements.ContainsKey("/Group"))
                        {
                            var grp = new PdfSharp.Pdf.PdfDictionary();
                            grp.Elements["/S"] = new PdfSharp.Pdf.PdfName("/Transparency");
                            grp.Elements["/CS"] = new PdfSharp.Pdf.PdfName("/DeviceRGB");
                            grp.Elements["/I"] = new PdfSharp.Pdf.PdfBoolean(true);
                            pg.Elements["/Group"] = grp;
                        }
                    }

                    for (int idx = 0; idx < slots.Length; idx++)
                    {
                        var slot = slots[idx];
                        int pageIndex = slot.Page - 1;
                        if (pageIndex < 0 || pageIndex >= doc.PageCount) continue;

                        bool signed = idx < signedArr.Length && signedArr[idx];
                        bool accessible = slot.IsAccessible;

                        var page = doc.Pages[pageIndex];
                        double pageH = page.Height.Point;
                        double x = slot.X, y = pageH - slot.Y - slot.H, w = slot.W, h = slot.H;

                        // ── Culori per stare ──
                        // Semnat          → verde
                        // Nesemnat Official → violet
                        // Nesemnat Candidat → galben
                        // Restrictionat     → rosu
                        PdfSharp.Drawing.XColor fillColor, borderColor, textColor, badgeBg;
                        bool isOfficial = slot.Party == "Official";

                        if (signed)
                        {
                            fillColor = PdfSharp.Drawing.XColor.FromArgb(40, 80, 180, 110);
                            borderColor = PdfSharp.Drawing.XColor.FromArgb(160, 80, 160, 110);
                            textColor = PdfSharp.Drawing.XColor.FromArgb(130, 60, 140, 90);
                            badgeBg = PdfSharp.Drawing.XColor.FromArgb(200, 70, 150, 100);
                        }
                        else if (!accessible)
                        {
                            fillColor = PdfSharp.Drawing.XColor.FromArgb(40, 210, 80, 80);
                            borderColor = PdfSharp.Drawing.XColor.FromArgb(160, 180, 80, 80);
                            textColor = PdfSharp.Drawing.XColor.FromArgb(130, 150, 70, 70);
                            badgeBg = PdfSharp.Drawing.XColor.FromArgb(200, 160, 70, 70);
                        }
                        else if (isOfficial)
                        {
                            // Violet — semnaturi interne
                            fillColor = PdfSharp.Drawing.XColor.FromArgb(40, 127, 119, 221);
                            borderColor = PdfSharp.Drawing.XColor.FromArgb(160, 100, 90, 200);
                            textColor = PdfSharp.Drawing.XColor.FromArgb(140, 60, 50, 160);
                            badgeBg = PdfSharp.Drawing.XColor.FromArgb(200, 83, 74, 183);
                        }
                        else
                        {
                            // Auriu — semnaturi candidat
                            fillColor = PdfSharp.Drawing.XColor.FromArgb(35, 210, 180, 40);
                            borderColor = PdfSharp.Drawing.XColor.FromArgb(150, 180, 150, 30);
                            textColor = PdfSharp.Drawing.XColor.FromArgb(140, 130, 110, 20);
                            badgeBg = PdfSharp.Drawing.XColor.FromArgb(190, 190, 160, 30);
                        }

                        using (var gfx = PdfSharp.Drawing.XGraphics.FromPdfPage(page))
                        {
                            var rect = new PdfSharp.Drawing.XRect(x, y, w, h);

                            gfx.DrawRectangle(new PdfSharp.Drawing.XSolidBrush(fillColor), rect);

                            var pen = new PdfSharp.Drawing.XPen(borderColor, 0.8);
                            pen.DashStyle = PdfSharp.Drawing.XDashStyle.Dash;
                            gfx.DrawRectangle(pen, rect);

                            var badgeFont = new PdfSharp.Drawing.XFont("Arial", 6, PdfSharp.Drawing.XFontStyleEx.Bold);
                            string badge = $"#{idx + 1}";
                            var badgeSize = gfx.MeasureString(badge, badgeFont);
                            gfx.DrawRectangle(new PdfSharp.Drawing.XSolidBrush(badgeBg),
                                new PdfSharp.Drawing.XRect(x + 1, y + 1, badgeSize.Width + 4, badgeSize.Height + 2));
                            gfx.DrawString(badge, badgeFont,
                                new PdfSharp.Drawing.XSolidBrush(PdfSharp.Drawing.XColors.White),
                                new PdfSharp.Drawing.XRect(x + 3, y + 2, badgeSize.Width, badgeSize.Height),
                                PdfSharp.Drawing.XStringFormats.TopLeft);

                            if (!signed && !string.IsNullOrEmpty(slot.RoleLabel))
                            {
                                // Porneste de la un font bazat pe inaltime, apoi reduce daca textul nu incape in latime
                                double fontSize = Math.Max(7, Math.Min(18, h * 0.22));
                                var roleFont = new PdfSharp.Drawing.XFont("Arial", fontSize, PdfSharp.Drawing.XFontStyleEx.Bold);
                                var textSize = gfx.MeasureString(slot.RoleLabel, roleFont);
                                if (textSize.Width > w * 0.88)
                                {
                                    fontSize = Math.Max(7, fontSize * (w * 0.88) / textSize.Width);
                                    roleFont = new PdfSharp.Drawing.XFont("Arial", fontSize, PdfSharp.Drawing.XFontStyleEx.Bold);
                                }
                                gfx.DrawString(slot.RoleLabel, roleFont,
                                    new PdfSharp.Drawing.XSolidBrush(textColor),
                                    rect, PdfSharp.Drawing.XStringFormats.Center);
                            }
                        }
                    }
                    doc.Save(destPath);
                }
            }
            finally
            {
                if (tempWithGroup != sourcePath && File.Exists(tempWithGroup))
                    try { File.Delete(tempWithGroup); } catch { }
            }
        }

        private void CleanupPreviewTemp()
        {
            if (_previewTempPath != null && File.Exists(_previewTempPath))
            {
                try { File.Delete(_previewTempPath); } catch { }
                _previewTempPath = null;
            }
        }

        // ── Drawing (FreeForm) ────────────────────────────────────────────────────

        public void EnableDrawing(bool enable)
        {
            if (DrawingEnabled == enable) return;
            DrawingEnabled = enable;

            EnsureOverlay();

            if (enable)
            {
                _overlay.Dock = DockStyle.Fill;
                _overlay.Visible = true;
                _overlay.BringToFront();
                _overlay.SetDrawingMode(true);
            }
            else
            {
                _overlay.SetDrawingMode(false);
                _overlay.Visible = false;
            }
        }

        private void EnsureOverlay()
        {
            if (_overlay != null) return;
            _overlay = new DrawingOverlayControl();
            _viewer.Renderer.Controls.Add(_overlay);
            _overlay.RectangleDrawn += OnOverlayRectDrawn;
        }

        private void OnOverlayRectDrawn(Rectangle rendererLocalRect)
        {
            DrawingEnabled = false;
            _overlay.SetDrawingMode(false);
            _overlay.Visible = false;

            if (_document == null || _viewer.Renderer == null) return;
            if (rendererLocalRect.Width < 10 || rendererLocalRect.Height < 10) return;

            var result = ConvertToPdfCoords(rendererLocalRect);
            if (result == null)
            {
                MessageBox.Show(
                    "Te rog sa desenezi dreptunghiul clar in interiorul aceleiasi pagini PDF.",
                    "Desen Invalid", MessageBoxButtons.OK, MessageBoxIcon.Information);
                DrawingAborted?.Invoke();
                return;
            }
            RectangleDrawn?.Invoke(result);
        }

        // ── Coordinate conversion ─────────────────────────────────────────────────

        private DrawnRectangle ConvertToPdfCoords(Rectangle overlayRect)
        {
            if (_document == null || _viewer.Renderer == null) return null;

            var scrollOffset = _viewer.Renderer.DisplayRectangle;
            int scrollX = -scrollOffset.X;
            int scrollY = -scrollOffset.Y;

            Point ToViewport(Point p) => new Point(p.X - scrollX, p.Y - scrollY);

            var vpTopLeft = ToViewport(new Point(overlayRect.Left, overlayRect.Top));
            var vpBottomRight = ToViewport(new Point(overlayRect.Right, overlayRect.Bottom));

            PdfPoint pdfStart = _viewer.Renderer.PointToPdf(vpTopLeft);
            PdfPoint pdfEnd = _viewer.Renderer.PointToPdf(vpBottomRight);

            if (pdfStart.Page < 0 && pdfEnd.Page < 0) return null;

            if (pdfStart.Page < 0) pdfStart = _viewer.Renderer.PointToPdf(
                ToViewport(new Point(overlayRect.Left, overlayRect.Bottom)));
            if (pdfEnd.Page < 0) pdfEnd = _viewer.Renderer.PointToPdf(
                ToViewport(new Point(overlayRect.Right, overlayRect.Top)));

            int pageIndex;
            if (pdfStart.Page >= 0 && pdfEnd.Page >= 0 && pdfStart.Page != pdfEnd.Page)
            {
                var vpCenter = ToViewport(new Point(
                    overlayRect.Left + overlayRect.Width / 2,
                    overlayRect.Top + overlayRect.Height / 2));
                var pdfCenter = _viewer.Renderer.PointToPdf(vpCenter);
                if (pdfCenter.Page < 0) return null;
                pageIndex = pdfCenter.Page;
            }
            else
            {
                pageIndex = pdfStart.Page >= 0 ? pdfStart.Page : pdfEnd.Page;
            }

            if (pageIndex < 0 || pageIndex >= _document.PageSizes.Count) return null;

            if (pdfStart.Page < 0 || pdfStart.Page != pageIndex)
                pdfStart = _viewer.Renderer.PointToPdf(
                    ToViewport(new Point(overlayRect.Left, overlayRect.Bottom)));
            if (pdfEnd.Page < 0 || pdfEnd.Page != pageIndex)
                pdfEnd = _viewer.Renderer.PointToPdf(
                    ToViewport(new Point(overlayRect.Right, overlayRect.Top)));

            if (pdfStart.Page < 0 || pdfEnd.Page < 0) return null;

            float pdfX = pdfStart.Location.X;
            float pdfY = pdfEnd.Location.Y;
            float pdfW = Math.Abs(pdfEnd.Location.X - pdfStart.Location.X);
            float pdfH = Math.Abs(pdfStart.Location.Y - pdfEnd.Location.Y);

            var pageSize = _document.PageSizes[pageIndex];
            pdfX = Math.Max(0, Math.Min(pdfX, (float)pageSize.Width - 1));
            pdfY = Math.Max(0, Math.Min(pdfY, (float)pageSize.Height - 1));
            pdfW = Math.Min(pdfW, (float)pageSize.Width - pdfX);
            pdfH = Math.Min(pdfH, (float)pageSize.Height - pdfY);

            return new DrawnRectangle
            {
                Page = pageIndex + 1,
                X = (float)Math.Round(pdfX, 2),
                Y = (float)Math.Round(pdfY, 2),
                W = (float)Math.Round(pdfW, 2),
                H = (float)Math.Round(pdfH, 2)
            };
        }

        // ── Cleanup ───────────────────────────────────────────────────────────────

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _restoreTimer?.Stop();
                _restoreTimer?.Dispose();
                CleanupPreviewTemp();
                _document?.Dispose();
                _viewer?.Dispose();
            }
            base.Dispose(disposing);
        }
    }

    // ── Overlay Control (drawing only) ────────────────────────────────────────────

    internal class DrawingOverlayControl : Control
    {
        public event Action<Rectangle> RectangleDrawn;

        private Point _startPoint;
        private Rectangle _rect;
        private bool _isDrawing;
        private bool _drawingMode = false;

        public void SetDrawingMode(bool active)
        {
            _drawingMode = active;
            Cursor = active ? Cursors.Cross : Cursors.Default;
            Invalidate();
        }

        public DrawingOverlayControl()
        {
            SetStyle(ControlStyles.SupportsTransparentBackColor, true);
            SetStyle(ControlStyles.Opaque, false);
            BackColor = Color.Transparent;
            Cursor = Cursors.Cross;
            DoubleBuffered = true;
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (!_drawingMode || e.Button != MouseButtons.Left) return;
            _isDrawing = true;
            _startPoint = e.Location;
            _rect = new Rectangle(e.X, e.Y, 0, 0);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            if (!_drawingMode || !_isDrawing) return;

            var oldRect = _rect;
            int x = Math.Min(_startPoint.X, e.X);
            int y = Math.Min(_startPoint.Y, e.Y);
            int w = Math.Abs(_startPoint.X - e.X);
            int h = Math.Abs(_startPoint.Y - e.Y);
            _rect = new Rectangle(x, y, w, h);

            var dirty = Rectangle.Union(
                oldRect.IsEmpty ? _rect : oldRect,
                _rect);
            dirty.Inflate(3, 3);
            Invalidate(dirty);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            if (!_drawingMode || !_isDrawing) return;
            _isDrawing = false;
            Invalidate();

            var result = _rect;
            _rect = Rectangle.Empty;

            if (result.Width >= 90 && result.Height >= 45)
                RectangleDrawn?.Invoke(result);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;

            if (_drawingMode)
                using (var tint = new SolidBrush(Color.FromArgb(55, 245, 235, 195)))
                    g.FillRectangle(tint, ClientRectangle);

            if (!_drawingMode || _rect.Width <= 0 || _rect.Height <= 0) return;

            g.SmoothingMode = SmoothingMode.AntiAlias;

            using (var brush = new SolidBrush(Color.FromArgb(50, 145, 192, 230)))
                g.FillRectangle(brush, _rect);
            using (var pen = new Pen(Color.FromArgb(0, 120, 215), 2))
            {
                pen.DashStyle = System.Drawing.Drawing2D.DashStyle.Dash;
                g.DrawRectangle(pen, _rect);
            }

            string dims = $"{_rect.Width} × {_rect.Height} px";
            using (var font = new Font("Segoe UI", 8f, FontStyle.Bold))
            using (var bg = new SolidBrush(Color.FromArgb(200, 0, 120, 215)))
            using (var fg = new SolidBrush(Color.White))
            {
                var sz = g.MeasureString(dims, font);
                var lr = new RectangleF(_rect.Left + 4, _rect.Top + 4, sz.Width + 8, sz.Height + 4);
                g.FillRectangle(bg, lr);
                g.DrawString(dims, font, fg, lr.Left + 4, lr.Top + 2);
            }
        }
    }

    public class DrawnRectangle
    {
        public int Page { get; set; }
        public float X { get; set; }
        public float Y { get; set; }
        public float W { get; set; }
        public float H { get; set; }
        public string RoleLabel { get; set; }
        public string Party { get; set; } // "Candidate" sau "Official"
        // True = slotul nesemnat e accesibil rolului curent, False = restrictionat (rosu)
        public bool IsAccessible { get; set; } = true;
    }

    internal class WindowsFontResolver : IFontResolver
    {
        private static readonly string FontsFolder =
            Environment.GetFolderPath(Environment.SpecialFolder.Fonts);

        public string DefaultFontName => "Arial";

        public byte[] GetFont(string faceName)
        {
            var map = new System.Collections.Generic.Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Arial"] = "arial.ttf",
                ["Arial#b"] = "arialbd.ttf",
                ["Arial#i"] = "ariali.ttf",
                ["Arial#bi"] = "arialbi.ttf",
                ["Segoe UI"] = "segoeui.ttf",
                ["Segoe UI#b"] = "segoeuib.ttf",
                ["Segoe UI#i"] = "segoeuii.ttf",
                ["Segoe UI#bi"] = "segoeuiz.ttf",
            };

            if (map.TryGetValue(faceName, out var fileName))
            {
                var path = Path.Combine(FontsFolder, fileName);
                if (File.Exists(path)) return File.ReadAllBytes(path);
            }

            var fallback = Path.Combine(FontsFolder, "arial.ttf");
            if (File.Exists(fallback)) return File.ReadAllBytes(fallback);

            return null;
        }

        public FontResolverInfo ResolveTypeface(string familyName, bool isBold, bool isItalic)
        {
            string key = familyName;
            if (isBold && isItalic) key += "#bi";
            else if (isBold) key += "#b";
            else if (isItalic) key += "#i";
            return new FontResolverInfo(key);
        }
    }
}