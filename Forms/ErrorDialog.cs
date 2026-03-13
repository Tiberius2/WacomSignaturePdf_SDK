using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using WacomSignaturePdf.Theme;

namespace WacomSignaturePdf.Forms
{
    public enum ErrorKind
    {
        FileNotFound,
        DeviceNotConnected,
        DocumentFinalized,
        DocumentSignedNotSealed,
        General
    }

    /// <summary>
    /// Styled error dialog that matches the app theme.
    /// We use ErrorDialog.Show(...) instead of MessageBox.Show for errors.
    /// </summary>
    public partial class ErrorDialog : Form
    {
        private const int DialogWidth = 460;
        private const int HeaderHeight = 72;


        // ── Static entry points ──
        public static void Show(IWin32Window owner, string message,
            ErrorKind kind = ErrorKind.General)
        {
            using (var dlg = new ErrorDialog(message, kind))
                dlg.ShowDialog(owner);
        }

        // Overload without owner for convenience (will center on screen)
        public static void Show(string message, ErrorKind kind = ErrorKind.General)
        {
            using (var dlg = new ErrorDialog(message, kind))
                dlg.ShowDialog();
        }

        // ── Constructor ──
        private ErrorDialog(string message, ErrorKind kind)
        {
            // ── Form shell ──
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.CenterParent;
            BackColor = Color.FromArgb(245, 247, 250);
            DoubleBuffered = true;
            TopMost = true;

            // Measure message to size dialog correctly
            int msgH;
            using (var g = CreateGraphics())
            using (var f = new Font("Segoe UI", 10f))
            {
                var size = g.MeasureString(message, f, DialogWidth - 80);
                msgH = (int)size.Height + 8;
            }

            int totalH = HeaderHeight + 20 + msgH + 20 + 44 + 16;
            ClientSize = new Size(DialogWidth, totalH);

            // ── Coloured header panel ──
            var header = new Panel
            {
                Dock = DockStyle.Top,
                Height = HeaderHeight,
                BackColor = HeaderColor(kind) // we paint the header according to the error kind , but we also set a base color here for the panel background
            };
            header.Paint += (s, e) => PaintHeader(e.Graphics, kind, header.Size);
            Controls.Add(header);

            // ── Message label ──
            var lblMsg = new Label
            {
                Text = message,
                Location = new Point(24, HeaderHeight + 20),
                Size = new Size(DialogWidth - 48, msgH),
                Font = new Font("Segoe UI", 10f),
                ForeColor = Color.FromArgb(30, 40, 60),
                BackColor = Color.Transparent
            };
            Controls.Add(lblMsg);

            // ── OK button ──
            var btnOk = new Button
            {
                Text = "OK",
                Size = new Size(90, 34),
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                FlatStyle = FlatStyle.Flat,
                BackColor = HeaderColor(kind),
                ForeColor = Color.White,
                DialogResult = DialogResult.OK,
                Cursor = Cursors.Hand
            };
            btnOk.FlatAppearance.BorderSize = 0;
            btnOk.Location = new Point(DialogWidth - btnOk.Width - 20,
                                       HeaderHeight + 20 + msgH + 20);
            Controls.Add(btnOk);

            AcceptButton = btnOk;

            // ── Drop shadow illusion — border ──
            Paint += (s, e) =>
            {
                using (var pen = new Pen(Color.FromArgb(190, 200, 220), 1f))
                    e.Graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
            };
        }

        // ── Header paint — icon + title , just ui stuff ──
        private static void PaintHeader(Graphics g, ErrorKind kind, Size size)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;

            // Icon circle
            int cx = 36, cy = size.Height / 2;
            int r = 18;
            using (var brush = new SolidBrush(Color.FromArgb(60, 255, 255, 255)))
                g.FillEllipse(brush, cx - r, cy - r, r * 2, r * 2);

            using (var pen = new Pen(Color.White, 2f))
                g.DrawEllipse(pen, cx - r, cy - r, r * 2, r * 2);

            // Icon glyph
            using (var f = new Font("Segoe UI", 16f, FontStyle.Bold))
            {
                string glyph = kind == ErrorKind.DeviceNotConnected ? "⚠"
                             : kind == ErrorKind.DocumentFinalized ? "✓"
                             : kind == ErrorKind.DocumentSignedNotSealed ? "⚠"
                             : "✕";
                var glyphSize = g.MeasureString(glyph, f);
                g.DrawString(glyph, f, Brushes.White,
                    cx - glyphSize.Width / 2,
                    cy - glyphSize.Height / 2);
            }

            // Title text
            string title = TitleFor(kind);
            using (var f = new Font("Segoe UI", 12f, FontStyle.Bold))
                g.DrawString(title, f, Brushes.White, 68, size.Height / 2f - 10);
        }

        // ── Helpers ──
        // Header Painter
        private static Color HeaderColor(ErrorKind kind)
        {
            switch (kind)
            {
                case ErrorKind.FileNotFound: return AppTheme.FileNotFoundHeaderColor;
                case ErrorKind.DeviceNotConnected: return AppTheme.DeviceNotConnectedHeaderColor;
                case ErrorKind.DocumentFinalized: return AppTheme.DocumentFinalizedHeaderColor;
                case ErrorKind.DocumentSignedNotSealed: return AppTheme.DocumentSignedNotSealedHeaderColor;
                default: return AppTheme.DefaultHeaderColor;
            }
        }

        // Title text for header
        private static string TitleFor(ErrorKind kind)
        {
            switch (kind)
            {
                case ErrorKind.FileNotFound: return "Document negasit";
                case ErrorKind.DeviceNotConnected: return "Dispozitiv wacom neconectat";
                case ErrorKind.DocumentFinalized: return "Document deja finalizat";
                case ErrorKind.DocumentSignedNotSealed: return "Document semnat — nesigilat";
                default: return "Eroare";
            }
        }

        // ── Allow dragging the borderless form ──

        private Point _dragStart;
        private bool _dragging;

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left) { _dragging = true; _dragStart = e.Location; }
            base.OnMouseDown(e);
        }
        protected override void OnMouseMove(MouseEventArgs e)
        {
            if (_dragging)
                Location = new Point(Location.X + e.X - _dragStart.X,
                                     Location.Y + e.Y - _dragStart.Y);
            base.OnMouseMove(e);
        }
        protected override void OnMouseUp(MouseEventArgs e)
        {
            _dragging = false;
            base.OnMouseUp(e);
        }
    }
}