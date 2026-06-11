using System;
using System.Drawing;
using System.Windows.Forms;
using WacomSignaturePdf.Theme;

namespace WacomSignaturePdf.Forms
{
    public class FreeFormSlotDialog : Form
    {
        public int SignatureId { get; private set; }
        public string SignerName { get; private set; }
        public string Reason { get; private set; }
        public string Party { get; private set; }
        public string OfficialRole { get; private set; }
        public bool Required { get; private set; }
        public bool Biometric => true; // intotdeauna true

        private TextBox txtSignerName;
        private TextBox txtReason;
        private ComboBox cmbParty;
        private ComboBox cmbOfficialRole;
        private Label lblOfficialRole;
        private CheckBox chkRequired;
        private Button btnCancel;
        private Button btnAdd;
        private Button btnSign;

        public bool SignImmediately { get; private set; }

        public FreeFormSlotDialog(int nextId, float x, float y, float w, float h, int page)
        {
            Text = $"Configurare Semnatura — Pagina {page}  [{x:F0}, {y:F0}, {w:F0}\u00d7{h:F0} pt]";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MaximizeBox = false;
            MinimizeBox = false;
            Font = new Font("Segoe UI", 9f);
            BackColor = Color.FromArgb(248, 249, 252);

            const int lx = 14;
            const int fx = 148;
            const int lw = 128;
            const int fw = 310;
            const int rowH = 38;
            int y0 = 14;

            // ── Header band ──
            var header = new Panel
            {
                Dock = DockStyle.Top,
                Height = 44,
                BackColor = AppTheme.FreeForm.TitleBg
            };
            header.Paint += (s, e) =>
            {
                using (var f2 = new Font("Segoe UI", 10f, FontStyle.Bold))
                    e.Graphics.DrawString("Configurare Slot Semnatura", f2, Brushes.White, 14, 10);
            };
            Controls.Add(header);
            y0 = 58;

            // ── ID Semnatura (readonly) ──
            AddLabel("ID Semnatura:", lx, y0, lw);
            var txtId = new TextBox
            {
                Location = new Point(fx, y0 - 2),
                Size = new Size(70, 24),
                Font = new Font("Segoe UI", 10f, FontStyle.Bold),
                Text = nextId.ToString(),
                ReadOnly = true,
                BackColor = Color.FromArgb(235, 237, 242),
                BorderStyle = BorderStyle.FixedSingle,
                ForeColor = Color.FromArgb(60, 80, 120),
                TabStop = false,
            };
            Controls.Add(txtId);
            y0 += rowH;

            // ── Nume Semnatar ──
            AddLabel("Nume Semnatar:", lx, y0, lw);
            txtSignerName = new TextBox
            {
                Location = new Point(fx, y0 - 2),
                Size = new Size(fw, 24),
                Font = new Font("Segoe UI", 9.5f)
            };
            Controls.Add(txtSignerName);
            y0 += rowH;

            // ── Motiv ──
            AddLabel("Motiv:", lx, y0, lw);
            txtReason = new TextBox
            {
                Location = new Point(fx, y0 - 2),
                Size = new Size(fw, 24),
                Font = new Font("Segoe UI", 9.5f)
            };
            Controls.Add(txtReason);
            y0 += rowH;

            // ── Tip Semnatar ──
            AddLabel("Tip Semnatar:", lx, y0, lw);
            cmbParty = new ComboBox
            {
                Location = new Point(fx, y0 - 2),
                Size = new Size(fw, 24),
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = new Font("Segoe UI", 9.5f)
            };
            cmbParty.Items.Add("Candidat");
            cmbParty.Items.Add("Oficial (intern)");
            cmbParty.SelectedIndex = 0;
            cmbParty.SelectedIndexChanged += (s, e) => UpdateOfficialRoleVisibility();
            Controls.Add(cmbParty);
            y0 += rowH;

            // ── Rol Oficial ──
            lblOfficialRole = AddLabel("Rol Oficial:", lx, y0, lw);
            cmbOfficialRole = new ComboBox
            {
                Location = new Point(fx, y0 - 2),
                Size = new Size(fw, 24),
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = new Font("Segoe UI", 9.5f)
            };
            cmbOfficialRole.Items.AddRange(new object[] { "(orice rol)", "ADMIN", "HR", "DIR. EC." });
            cmbOfficialRole.SelectedIndex = 0;
            Controls.Add(cmbOfficialRole);
            y0 += rowH;

            // ── Obligatorie (in linie cu campurile) ──
            AddLabel("Obligatorie:", lx, y0, lw);
            chkRequired = new CheckBox
            {
                Location = new Point(fx, y0),
                Size = new Size(20, 20),
                Checked = true,
                BackColor = Color.Transparent,
            };
            Controls.Add(chkRequired);
            y0 += rowH;
            int totalW = fx + fw;
            // ── Separator ──
            var sep = new Panel
            {
                Location = new Point(14, y0 - 4),
                Size = new Size(totalW + lx + 4 - 2 * lx, 1),
                BackColor = Color.FromArgb(210, 215, 230)
            };
            Controls.Add(sep);
            y0 += 10;

            // ── Butoane: Anuleaza | Adauga la lista | Semneaza Acum ──
            int btnY = y0;

            btnCancel = new Button
            {
                Text = "Anuleaza",
                Location = new Point(lx, btnY),
                Size = new Size(144, 34),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                BackColor = Color.FromArgb(200, 160, 30),
                ForeColor = Color.White,
                Cursor = Cursors.Hand,
                DialogResult = DialogResult.Cancel
            };
            btnCancel.FlatAppearance.BorderSize = 0;

            btnAdd = new Button
            {
                Text = "+ Adauga la lista",
                Location = new Point(lx + 144 + 8, btnY),
                Size = new Size(144, 34),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                BackColor = Color.FromArgb(30, 100, 200),
                ForeColor = Color.White,
                Cursor = Cursors.Hand
            };
            btnAdd.FlatAppearance.BorderSize = 0;
            btnAdd.Click += (s, e) =>
            {
                if (!ValidateInputs()) return;
                SignImmediately = false;
                CollectValues(nextId);
                DialogResult = DialogResult.OK;
            };

            btnSign = new Button
            {
                Text = "\u270e  Semneaza Acum",
                Location = new Point(lx + 144 + 8 + 144 + 8, btnY),
                Size = new Size(144, 34),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                BackColor = AppTheme.FreeForm.AccentBar,
                ForeColor = Color.White,
                Cursor = Cursors.Hand
            };
            btnSign.FlatAppearance.BorderSize = 0;
            btnSign.Click += (s, e) =>
            {
                if (!ValidateInputs()) return;
                SignImmediately = true;
                CollectValues(nextId);
                DialogResult = DialogResult.OK;
            };

            Controls.AddRange(new Control[] { btnCancel, btnAdd, btnSign });

            ClientSize = new Size(totalW + lx + 4, btnY + 44);

            AcceptButton = btnSign;
            CancelButton = btnCancel;

            UpdateOfficialRoleVisibility();
            Shown += (s, e) => txtSignerName.Focus();
        }

        private Label AddLabel(string text, int x, int y, int w)
        {
            var lbl = new Label
            {
                Text = text,
                Location = new Point(x, y + 3),
                Size = new Size(w, 20),
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                ForeColor = Color.FromArgb(50, 65, 100),
                BackColor = Color.Transparent
            };
            Controls.Add(lbl);
            return lbl;
        }

        private void UpdateOfficialRoleVisibility()
        {
            bool isOfficial = cmbParty.SelectedIndex == 1;
            lblOfficialRole.Visible = isOfficial;
            cmbOfficialRole.Visible = isOfficial;
        }

        private bool ValidateInputs()
        {
            if (string.IsNullOrWhiteSpace(txtSignerName.Text))
            {
                MessageBox.Show("Numele semnatarului nu poate fi gol.", "Validare",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                txtSignerName.Focus();
                return false;
            }
            if (string.IsNullOrWhiteSpace(txtReason.Text))
            {
                MessageBox.Show("Motivul nu poate fi gol.", "Validare",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                txtReason.Focus();
                return false;
            }
            return true;
        }

        private void CollectValues(int id)
        {
            SignatureId = id;
            SignerName = txtSignerName.Text.Trim();
            Reason = txtReason.Text.Trim();
            bool isOfficial = cmbParty.SelectedIndex == 1;
            Party = isOfficial ? "Official" : "Candidate";
            OfficialRole = isOfficial && cmbOfficialRole.SelectedIndex > 0
                ? cmbOfficialRole.SelectedItem.ToString()
                : string.Empty;
            Required = chkRequired.Checked;
        }
    }
}