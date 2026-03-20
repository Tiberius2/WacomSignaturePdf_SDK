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
    /// DialogResult.OK   = user confirmed a selection
    /// DialogResult.Cancel = user dismissed
    /// Check ResetToOriginal after OK.
    /// </summary>
    public partial class ResetOrUnloadDialog : Form
    {
        public bool ResetToOriginal { get; private set; }

        private int? _selectedOption = null; // 0 = reset, 1 = unload
        private OptionPanel _pnlReset;
        private OptionPanel _pnlUnload;
        private Button _btnConfirm;

        public ResetOrUnloadDialog()
        {
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.CenterParent;
            BackColor = Color.FromArgb(245, 247, 250);
            DoubleBuffered = true;
            ClientSize = new Size(420, 256);

            // ── Header ──
            var header = new Panel
            {
                Dock = DockStyle.Top,
                Height = 52,
                BackColor = AppTheme.SidebarBg
            };
            header.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using (var f = new Font("Segoe UI", 11f, FontStyle.Bold))
                    e.Graphics.DrawString("Inchidere Document", f, Brushes.White, 16, 14);
            };
            Controls.Add(header);

            // ── Subtitle ──
            var lbl = new Label
            {
                Text = "Ce doriti sa faceti cu documentul curent?",
                Location = new Point(20, 66),
                Size = new Size(380, 18),
                Font = new Font("Segoe UI", 9.5f),
                ForeColor = Color.FromArgb(40, 50, 70),
                BackColor = Color.Transparent
            };
            Controls.Add(lbl);

            // ── Option panels ──
            _pnlReset = new OptionPanel(
                "Resetare la original",
                "Inlocuieste documentul cu originalul nesemnat.");
            _pnlReset.Location = new Point(16, 150);
            _pnlReset.Size = new Size(388, 52);
            _pnlReset.OptionClicked += () => SelectOption(0);
            Controls.Add(_pnlReset);

            _pnlUnload = new OptionPanel(
                "Doar inchidere",
                "Pastreaza semnatura partiala, elibereaza din aplicatie.");
            _pnlUnload.Location = new Point(16, 92);
            _pnlUnload.Size = new Size(388, 52);
            _pnlUnload.OptionClicked += () => SelectOption(1);
            Controls.Add(_pnlUnload);

            // ── Buttons ──
            var btnCancel = new Button
            {
                Text = "Anuleaza",
                Location = new Point(16, 216),
                Size = new Size(100, 28),
                Font = new Font("Segoe UI", 9f),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.Transparent,
                ForeColor = AppTheme.SidebarSub,
                DialogResult = DialogResult.Cancel,
                Cursor = Cursors.Hand
            };
            btnCancel.FlatAppearance.BorderSize = 0;
            Controls.Add(btnCancel);
            CancelButton = btnCancel;

            _btnConfirm = new Button
            {
                Text = "Confirma",
                Location = new Point(304, 216),
                Size = new Size(100, 28),
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(160, 185, 220),
                ForeColor = Color.White,
                Enabled = false,
                Cursor = Cursors.Hand
            };
            _btnConfirm.FlatAppearance.BorderSize = 0;
            _btnConfirm.Click += (s, e) =>
            {
                ResetToOriginal = _selectedOption == 0;
                DialogResult = DialogResult.OK;
                Close();
            };
            Controls.Add(_btnConfirm);

            Paint += (s, e) =>
            {
                // Outer border
                using (var pen = new Pen(AppTheme.AccentBorderBlue, 1.5f))
                    e.Graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
                // Inner inset line for depth
                using (var pen = new Pen(Color.FromArgb(220, 230, 245), 1f))
                    e.Graphics.DrawRectangle(pen, 1, 1, Width - 3, Height - 3);
            };

            // Default to "Doar inchidere" — safer option
            SelectOption(1);
        }

        private void SelectOption(int option)
        {
            _selectedOption = option;
            _pnlReset.IsSelected = option == 0;
            _pnlUnload.IsSelected = option == 1;
            _btnConfirm.Enabled = true;
            _btnConfirm.BackColor = AppTheme.AccentBlue;
        }

        // ── Draggable ──
        private Point _drag;
        private bool _dragging;

        protected override void OnMouseDown(MouseEventArgs e)
        { if (e.Button == MouseButtons.Left) { _dragging = true; _drag = e.Location; } base.OnMouseDown(e); }
        protected override void OnMouseMove(MouseEventArgs e)
        { if (_dragging) Location = new Point(Location.X + e.X - _drag.X, Location.Y + e.Y - _drag.Y); base.OnMouseMove(e); }
        protected override void OnMouseUp(MouseEventArgs e)
        { _dragging = false; base.OnMouseUp(e); }

        // ── Inner option panel ──
        private class OptionPanel : Panel
        {
            public event Action OptionClicked;

            private bool _isSelected;
            public bool IsSelected
            {
                get => _isSelected;
                set { _isSelected = value; Invalidate(); }
            }

            private readonly string _title;
            private readonly string _subtitle;

            public OptionPanel(string title, string subtitle)
            {
                _title = title;
                _subtitle = subtitle;
                BackColor = Color.White;
                Cursor = Cursors.Hand;
                DoubleBuffered = true;

                MouseClick += (s, e) => OptionClicked?.Invoke();
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;

                // Background
                using (var brush = new SolidBrush(_isSelected
                    ? Color.FromArgb(235, 244, 255)
                    : Color.White))
                    g.FillRectangle(brush, 0, 0, Width, Height);

                // Border
                Color borderColor = _isSelected
                    ? AppTheme.AccentBlue
                    : Color.FromArgb(200, 210, 225);
                float borderWidth = _isSelected ? 2f : 1f;
                using (var pen = new Pen(borderColor, borderWidth))
                    g.DrawRectangle(pen, 1, 1, Width - 3, Height - 3);

                // Left accent bar when selected
                if (_isSelected)
                    using (var brush = new SolidBrush(AppTheme.AccentBlue))
                        g.FillRectangle(brush, 1, 1, 4, Height - 3);

                // Radio circle
                int cx = 24, cy = Height / 2, r = 8;
                using (var pen = new Pen(_isSelected ? AppTheme.AccentBlue : Color.FromArgb(180, 190, 210), 1.5f))
                    g.DrawEllipse(pen, cx - r, cy - r, r * 2, r * 2);
                if (_isSelected)
                    using (var brush = new SolidBrush(AppTheme.AccentBlue))
                        g.FillEllipse(brush, cx - 4, cy - 4, 8, 8);

                // Title
                using (var fTitle = new Font("Segoe UI", 9.5f, FontStyle.Bold))
                using (var brush = new SolidBrush(Color.FromArgb(30, 40, 60)))
                    g.DrawString(_title, fTitle, brush, 42, 8);

                // Subtitle
                using (var fSub = new Font("Segoe UI", 8f))
                using (var brush = new SolidBrush(Color.FromArgb(100, 115, 140)))
                    g.DrawString(_subtitle, fSub, brush, 42, 28);
            }
        }
    }
}