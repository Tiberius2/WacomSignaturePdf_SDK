using System.Drawing;
using System.Windows.Forms;

namespace WacomSignaturePdf
{
    // Collects the signer's name before a capture.
    // Optionally shows the signature reason for context.
    public partial class SignerNameDialog : Form
    {
        private TextBox _txtName;
        private Button _btnOk;
        private Button _btnCancel;

        public string SignerName => _txtName.Text.Trim();

        public SignerNameDialog() : this(null, null) { }

        public SignerNameDialog(string reason, string prefillName = null)
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
                Controls.Add(new Label
                {
                    Text = $"Motiv: {reason}",
                    Location = new Point(12, y),
                    Size = new Size(316, 20),
                    Font = new Font("Segoe UI", 9f, FontStyle.Italic),
                    ForeColor = Color.FromArgb(80, 80, 80)
                });
                y += 26;
            }

            Controls.Add(new Label
            {
                Text = "Numele semnatarului:",
                Location = new Point(12, y),
                Size = new Size(316, 20)
            });
            y += 24;

            _txtName = new TextBox
            {
                Location = new Point(12, y),
                Size = new Size(316, 24),
                Font = new Font("Segoe UI", 10f),
                Text = prefillName ?? ""
            };
            if (!string.IsNullOrWhiteSpace(prefillName)) _txtName.SelectAll();
            Controls.Add(_txtName);
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
            Controls.AddRange(new Control[] { _btnOk, _btnCancel });
        }
    }
}
