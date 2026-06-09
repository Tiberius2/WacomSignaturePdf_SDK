using System;
using System.Drawing;
using System.Windows.Forms;
using WacomSignaturePdf.Theme;

namespace WacomSignaturePdf.Forms
{
    /// <summary>
    /// Dialog afisat la prima apasare a butonului "Adauga Semnatura Electronica".
    /// Explica utilizatorului cum sa deseneze zona de semnatura pe PDF.
    /// </summary>
    internal class DrawInstructionsDialog : Form
    {
        public bool DontShowAgain { get; private set; }

        public DrawInstructionsDialog()
        {
            BuildUI();
        }

        private void BuildUI()
        {
            Text = "Cum se adauga o semnatura";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(420, 280);
            MaximizeBox = false;
            MinimizeBox = false;
            BackColor = AppTheme.SidebarBg;
            Font = new Font("Segoe UI", 9.5f);

            // ── Icon + titlu ──
            var lblIcon = new Label
            {
                Text = "✏",
                Location = new Point(24, 24),
                Size = new Size(48, 48),
                Font = new Font("Segoe UI", 24f),
                ForeColor = AppTheme.AccentBlue,
                BackColor = Color.Transparent,
                TextAlign = ContentAlignment.MiddleCenter
            };

            var lblTitle = new Label
            {
                Text = "Adaugare Semnatura Electronica",
                Location = new Point(80, 28),
                Size = new Size(316, 26),
                Font = new Font("Segoe UI", 12f, FontStyle.Bold),
                ForeColor = Color.White,
                BackColor = Color.Transparent
            };

            // ── Instructiuni ──
            var lblInstr = new Label
            {
                Text = "1.  Documentul PDF este afisat in zona de previzualizare din dreapta.\r\n\r\n" +
                       "2.  Navigati la pagina unde doriti sa plasati semnatura.\r\n\r\n" +
                       "3.  Tineti apasat butonul stang al mouse-ului si desenati\r\n" +
                       "     un dreptunghi pe zona dorita.\r\n\r\n" +
                       "4.  Eliberati mouse-ul pentru a configura semnatura.",
                Location = new Point(24, 86),
                Size = new Size(372, 130),
                Font = new Font("Segoe UI", 9.5f),
                ForeColor = Color.FromArgb(200, 220, 255),
                BackColor = Color.Transparent
            };

            // ── Bifa "Nu mai afisa" ──
            var chkSkip = new CheckBox
            {
                Text = "Nu imi mai afisa aceasta informatie",
                Location = new Point(24, 226),
                AutoSize = true,
                Font = new Font("Segoe UI", 8.5f),
                ForeColor = Color.FromArgb(140, 170, 210),
                BackColor = Color.Transparent,
                Checked = false
            };

            // ── Buton OK ──
            var btnOk = new Button
            {
                Text = "Am inteles, continua",
                Location = new Point(ClientSize.Width - 180, 230),
                Size = new Size(156, 34),
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                FlatStyle = FlatStyle.Flat,
                BackColor = AppTheme.AccentBlue,
                ForeColor = Color.White,
                Cursor = Cursors.Hand
            };
            btnOk.FlatAppearance.BorderSize = 0;
            btnOk.Click += (s, e) =>
            {
                DontShowAgain = chkSkip.Checked;
                DialogResult = DialogResult.OK;
                Close();
            };

            Controls.Add(lblIcon);
            Controls.Add(lblTitle);
            Controls.Add(lblInstr);
            Controls.Add(chkSkip);
            Controls.Add(btnOk);

            AcceptButton = btnOk;
        }
    }
}