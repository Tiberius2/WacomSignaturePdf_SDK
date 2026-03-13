using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using WacomSignaturePdf.Theme;

namespace WacomSignaturePdf.Forms
{
    /// <summary>
    /// Asks the user whether to reset the document to the original
    /// or just unload it (keeping the signed version in place).
    /// DialogResult.OK   = user chose one of the two options
    /// DialogResult.Cancel = user dismissed
    /// Check ResetToOriginal after OK.
    /// </summary>
    public partial class ResetOrUnloadDialog : Form
    {
        public bool ResetToOriginal { get; private set; }

        public ResetOrUnloadDialog()
        {
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.CenterParent;
            BackColor = Color.FromArgb(245, 247, 250);
            DoubleBuffered = true;
            ClientSize = new Size(420, 200);

            // Header
            var header = new Panel
            {
                Dock = DockStyle.Top,
                Height = 56,
                BackColor = AppTheme.SidebarBg
            };
            header.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using (var f = new Font("Segoe UI", 11f, FontStyle.Bold))
                    e.Graphics.DrawString("Inchidere Document", f, Brushes.White, 16, 16);
            };
            Controls.Add(header);

            // Message
            var lbl = new Label
            {
                Text = "Ce doriti sa faceti cu documentul curent?",
                Location = new Point(20, 72),
                Size = new Size(380, 20),
                Font = new Font("Segoe UI", 9.5f),
                ForeColor = Color.FromArgb(40, 50, 70),
                BackColor = Color.Transparent
            };
            Controls.Add(lbl);

            // Reset button
            var btnReset = MakeButton(
                "Resetare la original",
                "Inlocuieste documentul cu originalul nesemnat.",
                new Point(20, 104),
                AppTheme.CancelBg, AppTheme.CancelFg);
            btnReset.Click += (s, e) =>
            {
                ResetToOriginal = true;
                DialogResult = DialogResult.OK;
                Close();
            };
            Controls.Add(btnReset);

            // Unload-only button
            var btnUnload = MakeButton(
                "Doar inchidere",
                "Pastreaza semnatura partiala, eliberat din aplicatie.",
                new Point(214, 104),
                AppTheme.AccentBlue, Color.White);
            btnUnload.Click += (s, e) =>
            {
                ResetToOriginal = false;
                DialogResult = DialogResult.OK;
                Close();
            };
            Controls.Add(btnUnload);

            // Cancel link
            var btnCancel = new Button
            {
                Text = "Anuleaza",
                Location = new Point(160, 165),
                Size = new Size(100, 22),
                Font = new Font("Segoe UI", 8.5f),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.Transparent,
                ForeColor = AppTheme.SidebarSub,
                DialogResult = DialogResult.Cancel,
                Cursor = Cursors.Hand
            };
            btnCancel.FlatAppearance.BorderSize = 0;
            Controls.Add(btnCancel);

            CancelButton = btnCancel;

            Paint += (s, e) =>
            {
                using (var pen = new Pen(Color.FromArgb(190, 200, 220), 1f))
                    e.Graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
            };
        }


        // Helper to create the two main buttons with custom painting for title + subtitle
        private static Button MakeButton(string title, string subtitle, Point loc,
            Color bg, Color fg)
        {
            var btn = new Button
            {
                Location = loc,
                Size = new Size(186, 52),
                FlatStyle = FlatStyle.Flat,
                BackColor = bg,
                ForeColor = fg,
                Cursor = Cursors.Hand,
                TextAlign = ContentAlignment.MiddleCenter
            };
            btn.FlatAppearance.BorderSize = 0;

            // Custom paint: title bold + subtitle small
            btn.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                var r = btn.ClientRectangle;

                using (var bgBrush = new SolidBrush(bg))
                    e.Graphics.FillRectangle(bgBrush, r);

                using (var fTitle = new Font("Segoe UI", 9f, FontStyle.Bold))
                using (var fSub = new Font("Segoe UI", 7.5f))
                using (var brush = new SolidBrush(fg))
                {
                    var titleSize = e.Graphics.MeasureString(title, fTitle, r.Width);
                    var subSize = e.Graphics.MeasureString(subtitle, fSub, r.Width - 8);
                    float totalH = titleSize.Height + subSize.Height;
                    float startY = (r.Height - totalH) / 2f;

                    e.Graphics.DrawString(title, fTitle, brush,
                        new RectangleF(4, startY, r.Width - 8, titleSize.Height),
                        new StringFormat { Alignment = StringAlignment.Center });

                    using (var subBrush = new SolidBrush(Color.FromArgb(200, fg)))
                        e.Graphics.DrawString(subtitle, fSub, subBrush,
                            new RectangleF(4, startY + titleSize.Height, r.Width - 8, subSize.Height),
                            new StringFormat { Alignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisWord });
                }
            };

            return btn;
        }

        // Draggable
        private Point _drag; private bool _dragging;
        protected override void OnMouseDown(MouseEventArgs e)
        { if (e.Button == MouseButtons.Left) { _dragging = true; _drag = e.Location; } base.OnMouseDown(e); }
        protected override void OnMouseMove(MouseEventArgs e)
        { if (_dragging) Location = new Point(Location.X + e.X - _drag.X, Location.Y + e.Y - _drag.Y); base.OnMouseMove(e); }
        protected override void OnMouseUp(MouseEventArgs e)
        { _dragging = false; base.OnMouseUp(e); }
    }
}