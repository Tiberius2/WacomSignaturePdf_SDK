using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using WacomSignaturePdf.Theme;

namespace WacomSignaturePdf.Forms
{
    public enum UnloadAction { DiscardSession, SaveAndClose, ResetToOriginal }

    // Three-option dialog shown when closing a document with an active session.
    // DialogResult.OK  → check SelectedAction.
    // DialogResult.Cancel → user dismissed.
    public partial class ResetOrUnloadDialog : DraggableForm
    {
        public UnloadAction SelectedAction { get; private set; }

        private int? _selectedOption;
        private OptionPanel _pnlDiscard;
        private OptionPanel _pnlSave;
        private OptionPanel _pnlReset;
        private Button _btnConfirm;

        public ResetOrUnloadDialog(bool canResetToOriginal = true)
        {
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.CenterParent;
            BackColor = Color.FromArgb(245, 247, 250);
            DoubleBuffered = true;
            ClientSize = new Size(420, 316);

            BuildHeader();
            BuildOptions(canResetToOriginal);
            BuildButtons();

            Paint += (s, e) =>
            {
                using (var pen = new Pen(AppTheme.AccentBorderBlue, 1.5f))
                    e.Graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
                using (var pen = new Pen(Color.FromArgb(220, 230, 245), 1f))
                    e.Graphics.DrawRectangle(pen, 1, 1, Width - 3, Height - 3);
            };

            SelectOption(0);
        }

        private void BuildHeader()
        {
            var header = new Panel { Dock = DockStyle.Top, Height = 52, BackColor = AppTheme.SidebarBg };
            header.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using (var f = new Font("Segoe UI", 11f, FontStyle.Bold))
                    e.Graphics.DrawString("Inchidere Document", f, Brushes.White, 16, 14);
            };
            Controls.Add(header);

            Controls.Add(new Label
            {
                Text = "Ce doriti sa faceti cu documentul curent?",
                Location = new Point(20, 66),
                Size = new Size(380, 18),
                Font = new Font("Segoe UI", 9.5f),
                ForeColor = Color.FromArgb(40, 50, 70),
                BackColor = Color.Transparent
            });
        }

        private void BuildOptions(bool canResetToOriginal)
        {
            _pnlDiscard = new OptionPanel(
                "Anuleaza sesiunea curenta",
                "Semnaturile din aceasta sesiune sunt sterse. Sesiunile anterioare sunt pastrate.");
            _pnlDiscard.Location = new Point(16, 92);
            _pnlDiscard.Size = new Size(388, 52);
            _pnlDiscard.OptionClicked += () => SelectOption(0);
            Controls.Add(_pnlDiscard);

            _pnlSave = new OptionPanel(
                "Inchidere si salvare",
                "Salveaza semnaturile din aceasta sesiune si elibereaza documentul.");
            _pnlSave.Location = new Point(16, 150);
            _pnlSave.Size = new Size(388, 52);
            _pnlSave.OptionClicked += () => SelectOption(1);
            Controls.Add(_pnlSave);

            if (canResetToOriginal)
            {
                _pnlReset = new OptionPanel(
                    "Resetare la original",
                    "Inlocuieste documentul cu originalul nesemnat. Toate sesiunile sunt pierdute.");
                _pnlReset.Location = new Point(16, 208);
                _pnlReset.Size = new Size(388, 52);
                _pnlReset.OptionClicked += () => SelectOption(2);
                Controls.Add(_pnlReset);
            }
        }

        private void BuildButtons()
        {
            var btnCancel = new Button
            {
                Text = "✕  Anuleaza",
                Location = new Point(16, 274),
                Size = new Size(110, 28),
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(240, 242, 246),
                ForeColor = Color.FromArgb(80, 90, 110),
                DialogResult = DialogResult.Cancel,
                Cursor = Cursors.Hand
            };
            btnCancel.FlatAppearance.BorderSize = 1;
            btnCancel.FlatAppearance.BorderColor = Color.FromArgb(190, 200, 215);
            Controls.Add(btnCancel);
            CancelButton = btnCancel;

            _btnConfirm = new Button
            {
                Text = "Confirma",
                Location = new Point(304, 274),
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
                SelectedAction = (UnloadAction)_selectedOption.Value;
                DialogResult = DialogResult.OK;
                Close();
            };
            Controls.Add(_btnConfirm);
        }

        private void SelectOption(int option)
        {
            _selectedOption = option;
            _pnlDiscard.IsSelected = option == 0;
            _pnlSave.IsSelected = option == 1;
            if (_pnlReset != null) _pnlReset.IsSelected = option == 2;
            _btnConfirm.Enabled = true;
            // Reset to original gets a red confirm button as a warning signal
            _btnConfirm.BackColor = option == 2 ? AppTheme.CancelBg : AppTheme.AccentBlue;
        }

        private class OptionPanel : Panel
        {
            public event Action OptionClicked;

            private bool _isSelected;
            private string _title;
            private string _subtitle;

            public bool IsSelected
            {
                get => _isSelected;
                set { _isSelected = value; Invalidate(); }
            }

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

                using (var brush = new SolidBrush(_isSelected ? Color.FromArgb(235, 244, 255) : Color.White))
                    g.FillRectangle(brush, 0, 0, Width, Height);

                float bw = _isSelected ? 2f : 1f;
                Color border = _isSelected ? AppTheme.AccentBlue : Color.FromArgb(200, 210, 225);
                using (var pen = new Pen(border, bw))
                    g.DrawRectangle(pen, 1, 1, Width - 3, Height - 3);

                if (_isSelected)
                    using (var brush = new SolidBrush(AppTheme.AccentBlue))
                        g.FillRectangle(brush, 1, 1, 4, Height - 3);

                int cx = 24, cy = Height / 2, r = 8;
                using (var pen = new Pen(_isSelected ? AppTheme.AccentBlue : Color.FromArgb(180, 190, 210), 1.5f))
                    g.DrawEllipse(pen, cx - r, cy - r, r * 2, r * 2);
                if (_isSelected)
                    using (var brush = new SolidBrush(AppTheme.AccentBlue))
                        g.FillEllipse(brush, cx - 4, cy - 4, 8, 8);

                using (var f = new Font("Segoe UI", 9.5f, FontStyle.Bold))
                using (var brush = new SolidBrush(Color.FromArgb(30, 40, 60)))
                    g.DrawString(_title, f, brush, 42, 8);

                using (var f = new Font("Segoe UI", 8f))
                using (var brush = new SolidBrush(Color.FromArgb(100, 115, 140)))
                    g.DrawString(_subtitle, f, brush, 42, 28);
            }
        }
    }
}