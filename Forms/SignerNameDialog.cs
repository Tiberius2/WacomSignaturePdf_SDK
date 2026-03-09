using System;
using System.Windows.Forms;

namespace WacomSignaturePdf
{
    /// <summary>
    /// Minimal dialog to collect the signer's name before capture.
    /// </summary>
    public partial class SignerNameDialog : Form
    {
        private TextBox _txtName;
        private Button _btnOk;
        private Button _btnCancel;
        private Label _lblPrompt;

        public string SignerName => _txtName.Text.Trim();

        public SignerNameDialog()
        {
            this.Text = "Signer Name";
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.StartPosition = FormStartPosition.CenterParent;
            this.ClientSize = new System.Drawing.Size(320, 120);
            this.MaximizeBox = false;
            this.MinimizeBox = false;

            _lblPrompt = new Label
            {
                Text = "Enter signer name:",
                Location = new System.Drawing.Point(12, 14),
                Size = new System.Drawing.Size(290, 20),
                Font = new System.Drawing.Font("Segoe UI", 9f)
            };

            _txtName = new TextBox
            {
                Location = new System.Drawing.Point(12, 38),
                Size = new System.Drawing.Size(290, 24),
                Font = new System.Drawing.Font("Segoe UI", 10f)
            };

            _btnOk = new Button
            {
                Text = "OK",
                DialogResult = DialogResult.OK,
                Location = new System.Drawing.Point(145, 76),
                Size = new System.Drawing.Size(75, 28)
            };
            _btnOk.Click += (s, e) =>
            {
                if (string.IsNullOrWhiteSpace(_txtName.Text))
                {
                    MessageBox.Show("Name cannot be empty.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    this.DialogResult = DialogResult.None; // keep dialog open
                }
            };

            _btnCancel = new Button
            {
                Text = "Cancel",
                DialogResult = DialogResult.Cancel,
                Location = new System.Drawing.Point(228, 76),
                Size = new System.Drawing.Size(75, 28)
            };

            this.AcceptButton = _btnOk;
            this.CancelButton = _btnCancel;
            this.Controls.AddRange(new Control[] { _lblPrompt, _txtName, _btnOk, _btnCancel });
        }
    }
}