using System;
using System.Drawing;
using System.Windows.Forms;

namespace WacomSignaturePdf
{
    /// <summary>
    /// Minimal dialog to collect the signer's name before capture.
    /// Optionally shows the signature reason so the signer knows what they're signing for.
    /// </summary>
    public partial class SignerNameDialog : Form
    {
        private Label _lblReason;
        private Label _lblPrompt;
        private TextBox _txtName;
        private Button _btnOk;
        private Button _btnCancel;

        public string SignerName => _txtName.Text.Trim();

        // ── No reason (generic prompt) ──

        public SignerNameDialog() : this(null) { }

        // ── With reason context ──

        public SignerNameDialog(string reason)
        {
            bool hasReason = !string.IsNullOrWhiteSpace(reason);

            Text = "Introduceti Semnatarul";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(340, hasReason ? 148 : 116);
            MaximizeBox = false;
            MinimizeBox = false;
            Font = new Font("Segoe UI", 9f);

            int y = 12;

            if (hasReason)
            {
                _lblReason = new Label
                {
                    Text = $"Motiv: {reason}",
                    Location = new Point(12, y),
                    Size = new Size(316, 20),
                    Font = new Font("Segoe UI", 9f, FontStyle.Italic),
                    ForeColor = Color.FromArgb(80, 80, 80)
                };
                Controls.Add(_lblReason);
                y += 26;
            }

            _lblPrompt = new Label
            {
                Text = "Numele semnatarului:",
                Location = new Point(12, y),
                Size = new Size(316, 20),
                Font = new Font("Segoe UI", 9f)
            };
            y += 24;

            _txtName = new TextBox
            {
                Location = new Point(12, y),
                Size = new Size(316, 24),
                Font = new Font("Segoe UI", 10f)
            };
            y += 36;

            _btnOk = new Button
            {
                Text = "OK",
                DialogResult = DialogResult.OK,
                Location = new Point(152, y),
                Size = new Size(75, 28)
            };
            _btnOk.Click += (s, e) =>
            {
                if (string.IsNullOrWhiteSpace(_txtName.Text))
                {
                    MessageBox.Show("Numele nu poate fi gol.", "Validare",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    DialogResult = DialogResult.None;
                }
            };

            _btnCancel = new Button
            {
                Text = "Anuleaza",
                DialogResult = DialogResult.Cancel,
                Location = new Point(252, y),
                Size = new Size(75, 28)
            };

            AcceptButton = _btnOk;
            CancelButton = _btnCancel;
            Controls.AddRange(new Control[] { _lblPrompt, _txtName, _btnOk, _btnCancel });
        }
    }
}