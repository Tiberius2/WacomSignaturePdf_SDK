using System.Collections.Generic;
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

    // Themed error dialog. Use ErrorDialog.Show() instead of MessageBox for all errors.
    public partial class ErrorDialog : DraggableForm
    {
        private const int DialogWidth = 460;
        private const int HeaderHeight = 72;

        // Per-kind config: (header colour, glyph, title)
        private static readonly Dictionary<ErrorKind, (Color Color, string Glyph, string Title)> _config =
            new Dictionary<ErrorKind, (Color, string, string)>
            {
                { ErrorKind.FileNotFound,           (AppTheme.FileNotFoundHeaderColor,           "✕", "Document negasit")              },
                { ErrorKind.DeviceNotConnected,     (AppTheme.DeviceNotConnectedHeaderColor,     "⚠", "Dispozitiv wacom neconectat")   },
                { ErrorKind.DocumentFinalized,      (AppTheme.DocumentFinalizedHeaderColor,      "✓", "Document deja finalizat")        },
                { ErrorKind.DocumentSignedNotSealed,(AppTheme.DocumentSignedNotSealedHeaderColor,"⚠", "Document semnat — nesigilat")   },
                { ErrorKind.General,                (AppTheme.DefaultHeaderColor,                "✕", "Eroare")                        },
            };

        // ── Static entry points ───────────────────────────────────────────────────

        public static void Show(IWin32Window owner, string message, ErrorKind kind = ErrorKind.General)
        {
            using (var dlg = new ErrorDialog(message, kind))
                dlg.ShowDialog(owner);
        }

        public static void Show(string message, ErrorKind kind = ErrorKind.General)
        {
            using (var dlg = new ErrorDialog(message, kind))
                dlg.ShowDialog();
        }

        // ── Constructor ───────────────────────────────────────────────────────────

        private ErrorDialog(string message, ErrorKind kind)
        {
            var cfg = _config.TryGetValue(kind, out var c) ? c : _config[ErrorKind.General];

            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.CenterParent;
            BackColor = Color.FromArgb(245, 247, 250);
            DoubleBuffered = true;
            TopMost = true;

            int msgH;
            using (var g = CreateGraphics())
            using (var f = new Font("Segoe UI", 10f))
                msgH = (int)g.MeasureString(message, f, DialogWidth - 80).Height + 8;

            ClientSize = new Size(DialogWidth, HeaderHeight + 20 + msgH + 20 + 44 + 16);

            var header = new Panel { Dock = DockStyle.Top, Height = HeaderHeight, BackColor = cfg.Color };
            header.Paint += (s, e) => PaintHeader(e.Graphics, cfg.Glyph, cfg.Title, cfg.Color, header.Size);
            Controls.Add(header);

            Controls.Add(new Label
            {
                Text = message,
                Location = new Point(24, HeaderHeight + 20),
                Size = new Size(DialogWidth - 48, msgH),
                Font = new Font("Segoe UI", 10f),
                ForeColor = Color.FromArgb(30, 40, 60),
                BackColor = Color.Transparent
            });

            var btnOk = new Button
            {
                Text = "OK",
                Size = new Size(90, 34),
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                FlatStyle = FlatStyle.Flat,
                BackColor = cfg.Color,
                ForeColor = Color.White,
                DialogResult = DialogResult.OK,
                Cursor = Cursors.Hand,
                Location = new Point(DialogWidth - 90 - 20, HeaderHeight + 20 + msgH + 20)
            };
            btnOk.FlatAppearance.BorderSize = 0;
            Controls.Add(btnOk);
            AcceptButton = btnOk;

            Paint += (s, e) =>
            {
                using (var pen = new Pen(Color.FromArgb(190, 200, 220), 1f))
                    e.Graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
            };
        }

        // ── Header paint ──────────────────────────────────────────────────────────

        private static void PaintHeader(Graphics g, string glyph, string title, Color color, Size size)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;

            int cx = 36, cy = size.Height / 2, r = 18;

            using (var brush = new SolidBrush(Color.FromArgb(60, 255, 255, 255)))
                g.FillEllipse(brush, cx - r, cy - r, r * 2, r * 2);
            using (var pen = new Pen(Color.White, 2f))
                g.DrawEllipse(pen, cx - r, cy - r, r * 2, r * 2);

            using (var f = new Font("Segoe UI", 16f, FontStyle.Bold))
            {
                var sz = g.MeasureString(glyph, f);
                g.DrawString(glyph, f, Brushes.White, cx - sz.Width / 2, cy - sz.Height / 2);
            }

            using (var f = new Font("Segoe UI", 12f, FontStyle.Bold))
                g.DrawString(title, f, Brushes.White, 68, size.Height / 2f - 10);
        }
    }
}
