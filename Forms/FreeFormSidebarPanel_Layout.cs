using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using WacomSignaturePdf.Controls;
using WacomSignaturePdf.Theme;

namespace WacomSignaturePdf.Forms
{
    public partial class FreeFormSidebarPanel
    {
        #region Control Declarations

        private Label lblSectionDocument;
        private Button btnOpenFile;
        private Button btnOpenInProces;
        private Label lblLoadedFile;

        private Label lblSectionDraw;
        private Label lblDrawHint;
        private Button btnDrawZone;

        private Label lblSectionSignatures;
        private Label lblProgress;
        private CheckBox chkManualSigner;
        private Panel cardsPanel;

        private Button btnCancelLoad;
        private Button btnSaveAndClose;
        private Button btnFinish;

        private Panel panelBottom;
        private Label lblVersion;

        internal PdfDrawingOverlay pdfOverlay;   // wired to ShellForm.SharedOverlay

        private ToolTip toolTip;

        #endregion

        #region Layout Constants

        private const int BtnH = 38;
        private const int BtnSpacing = 10;
        private const int YDocSec = 12;
        private const int YOpenBtn = 32;
        private const int YFileLabel = 82;
        private const int YDrawSec = 112;
        private const int YDrawHint = 130;
        private const int YDrawBtn = 164;
        private const int YSigSec = 208;
        private const int YSigProg = 226;
        private const int YCards = 270;
        private const int CardsHeight = 290;

        private const int FieldX = 16;
        private const int ContentW = 428;
        private const int HalfBtnW = 209;

        #endregion

        private void BuildSidebarControls()
        {
            var theme = AppTheme.FreeForm;

            // ── DOCUMENT ──
            lblSectionDocument = MakeSectionLabel("DOCUMENT", new Point(FieldX, YDocSec));

            btnOpenFile = new Button
            {
                Text = "Incarca Document",
                Location = new Point(FieldX, YOpenBtn),
                Size = new Size(HalfBtnW, BtnH),
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                FlatStyle = FlatStyle.Flat,
                BackColor = AppTheme.FreeForm.AccentBar,
                ForeColor = Color.White,
                Cursor = Cursors.Hand,
                TextAlign = ContentAlignment.MiddleCenter,
                Image = System.Drawing.Image.FromStream(new System.IO.MemoryStream(Properties.Resources.file_browse)),
                ImageAlign = ContentAlignment.MiddleCenter,
                TextImageRelation = TextImageRelation.ImageBeforeText,
                Padding = new Padding(6, 0, 0, 0),
            };
            btnOpenFile.FlatAppearance.BorderSize = 0;
            btnOpenFile.FlatAppearance.BorderColor = AppTheme.FreeForm.ButtonBorder;
            btnOpenFile.Click += (s, e) => BrowseForFile();

            btnOpenInProces = new Button
            {
                Text = "Documente In Proces",
                Location = new Point(FieldX + HalfBtnW + 10, YOpenBtn),
                Size = new Size(HalfBtnW, BtnH),
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(52, 105, 120),
                ForeColor = Color.White,
                Cursor = Cursors.Hand,
                TextAlign = ContentAlignment.MiddleCenter,
                Image = System.Drawing.Image.FromStream(new System.IO.MemoryStream(Properties.Resources.document_in_progress)),
                ImageAlign = ContentAlignment.MiddleCenter,
                TextImageRelation = TextImageRelation.ImageBeforeText,
                Padding = new Padding(6, 0, 0, 0),
            };
            btnOpenInProces.FlatAppearance.BorderSize = 0;
            btnOpenInProces.FlatAppearance.BorderColor = AppTheme.FreeForm.ButtonBorderInProgress;
            btnOpenInProces.Click += (s, e) => BrowseInProces();

            lblLoadedFile = new Label
            {
                Text = "Niciun document incarcat.",
                Location = new Point(FieldX, YFileLabel),
                Size = new Size(ContentW, 22),
                Font = new Font("Segoe UI", 8.5f, FontStyle.Italic),
                ForeColor = theme.SidebarSub,
                BackColor = Color.Transparent,
                AutoEllipsis = true,
            };

            // ── ZONA SEMNATURA ──
            lblSectionDraw = MakeSectionLabel("ZONA SEMNATURA", new Point(FieldX, YDrawSec));

            lblDrawHint = new Label
            {
                Text = "Deseneaza un dreptunghi pe document pentru a plasa o semnatura.",
                Location = new Point(FieldX, YDrawHint),
                Size = new Size(ContentW, 32),
                Font = new Font("Segoe UI", 8.5f, FontStyle.Italic),
                ForeColor = theme.SidebarSub,
                BackColor = Color.Transparent,
            };
            string drawZoneText = "Adauga Semnatura Electronica";
            btnDrawZone = new Button
            {
                // Am șters Text = drawZoneText de aici pentru a opri duplicarea
                Location = new Point(FieldX, YDrawBtn),
                Size = new Size(ContentW, 34),
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                FlatStyle = FlatStyle.Flat,
                BackColor = AppTheme.FreeForm.AccentBar,
                ForeColor = Color.White,
                Cursor = Cursors.Hand,
            };
            btnDrawZone.FlatAppearance.BorderSize = 2;
            btnDrawZone.FlatAppearance.BorderColor = Color.WhiteSmoke;
            btnDrawZone.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

                using (var img = System.Drawing.Image.FromStream(new System.IO.MemoryStream(Properties.Resources.signature)))
                {
                    Size textSize = TextRenderer.MeasureText(e.Graphics, drawZoneText, btnDrawZone.Font);
                    int spacing = 6;

                    int totalWidth = img.Width + spacing + textSize.Width;
                    int startX = (btnDrawZone.Width - totalWidth) / 2;
                    int imgY = (btnDrawZone.Height - img.Height) / 2;
                    int textY = (btnDrawZone.Height - textSize.Height) / 2;

                    e.Graphics.DrawImage(img, new Point(startX, imgY));
                    TextRenderer.DrawText(e.Graphics, drawZoneText, btnDrawZone.Font,
                        new Point(startX + img.Width + spacing, textY), btnDrawZone.ForeColor);
                }
            };
            btnDrawZone.Click += (s, e) =>
            {
                if (!pdfOverlay.HasDocument)
                {
                    MessageBox.Show(
                        "Incarcati mai intai un document PDF.",
                        "Niciun document", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                ShowDrawInstructions();
            };

            // ── SEMNATURI ──
            lblSectionSignatures = MakeSectionLabel("SEMNATURI", new Point(FieldX, YSigSec));

            lblProgress = new Label
            {
                Text = "",
                Location = new Point(FieldX, YSigProg),
                Size = new Size(ContentW, 18),
                Font = new Font("Segoe UI", 8.5f),
                ForeColor = theme.SidebarSub,
                BackColor = Color.Transparent,
                AutoEllipsis = true,
            };

            chkManualSigner = new CheckBox
            {
                Text = "IMPUTERNICIRE",
                Location = new Point(ContentW + FieldX - 136, YSigProg + 22),
                Size = new Size(136, 18),
                Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
                ForeColor = Color.White,
                BackColor = Color.Transparent,
                CheckAlign = ContentAlignment.MiddleRight,
                TextAlign = ContentAlignment.MiddleRight,
                Cursor = Cursors.Hand,
                Enabled = false,
                UseVisualStyleBackColor = false,
            };
            chkManualSigner.Paint += (s, e) =>
            {
                e.Graphics.Clear(chkManualSigner.BackColor == Color.Transparent
                    ? AppTheme.FreeForm.SidebarBg
                    : chkManualSigner.BackColor);
                Color textColor = chkManualSigner.Enabled ? Color.White : Color.FromArgb(110, 150, 148);
                TextRenderer.DrawText(e.Graphics, chkManualSigner.Text, chkManualSigner.Font,
                    new Rectangle(0, 0, chkManualSigner.Width - 18, chkManualSigner.Height),
                    textColor,
                    TextFormatFlags.Right | TextFormatFlags.VerticalCenter);
                System.Windows.Forms.ControlPaint.DrawCheckBox(e.Graphics,
                    chkManualSigner.Width - 16, 1, 14, 14,
                    chkManualSigner.Checked
                        ? System.Windows.Forms.ButtonState.Checked
                        : System.Windows.Forms.ButtonState.Normal);
            };
            chkManualSigner.CheckedChanged += (s, e) => ReflowCards();

            cardsPanel = new Panel
            {
                Location = new Point(FieldX, YCards),
                Size = new Size(ContentW, CardsHeight),
                BackColor = AppTheme.FreeForm.SidebarCardsBg,
                AutoScroll = true,
                BorderStyle = BorderStyle.None,
            };

            // ── Action buttons ──
            btnCancelLoad = new Button
            {
                Text = "Inchide document",
                Location = new Point(8, YCards + CardsHeight + 4),
                Size = new Size(ContentW, BtnH),
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                FlatStyle = FlatStyle.Flat,
                BackColor = AppTheme.CancelBg,
                ForeColor = Color.White,
                Visible = false,
                Cursor = Cursors.Hand,
            };
            btnCancelLoad.FlatAppearance.BorderSize = 0;
            btnCancelLoad.FlatAppearance.BorderColor = AppTheme.CancelBorder;
            btnCancelLoad.Click += (s, e) =>
            {
                if (!HasUnsavedWork) { UnloadDocument(); return; }
                using (var dlg = new ResetOrUnloadDialog(CanResetToOriginal))
                {
                    if (dlg.ShowDialog() != DialogResult.OK) return;
                    switch (dlg.SelectedAction)
                    {
                        case UnloadAction.DiscardSession: UnloadDocument(); break;
                        case UnloadAction.SaveAndClose: btnSaveAndClose_Click(null, EventArgs.Empty); break;
                        case UnloadAction.ResetToOriginal: ResetToOriginal(); break;
                    }
                }
            };

            btnSaveAndClose = new Button
            {
                Text = "Salveaza si Inchide",
                Location = new Point(FieldX, 0),
                Size = new Size(ContentW, BtnH),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                BackColor = Color.FromArgb(30, 100, 160),
                ForeColor = Color.White,
                Cursor = Cursors.Hand,
                Enabled = false,
            };
            btnSaveAndClose.FlatAppearance.BorderSize = 0;
            btnSaveAndClose.FlatAppearance.BorderColor = AppTheme.AccentBorderBlue;
            btnSaveAndClose.Click += btnSaveAndClose_Click;

            btnFinish = new Button
            {
                Text = "Finalizati si Salvati",
                Location = new Point(8, YCards + CardsHeight + 4 + BtnH + BtnSpacing),
                Size = new Size(ContentW, BtnH),
                Font = new Font("Segoe UI", 11f, FontStyle.Bold),
                FlatStyle = FlatStyle.Flat,
                BackColor = AppTheme.AccentGreen,
                ForeColor = Color.White,
                Enabled = false,
                Cursor = Cursors.Hand,
            };
            btnFinish.FlatAppearance.BorderSize = 0;
            btnFinish.FlatAppearance.BorderColor = AppTheme.AccentBorderGreen;
            btnFinish.Click += btnFinish_Click;

            WireButtonBorder(btnOpenFile);
            WireButtonBorder(btnOpenInProces);
            WireButtonBorder(btnCancelLoad);
            WireButtonBorder(btnSaveAndClose);
            WireButtonBorder(btnFinish);
            HandleDisabledTextColor(btnFinish);
            HandleDisabledTextColor(btnSaveAndClose);
            HandleDisabledTextColor(btnCancelLoad);

            toolTip = new ToolTip();
            toolTip.SetToolTip(btnOpenFile, "Selecteaza un PDF sau trage fisierul in zona de vizualizare");
            toolTip.SetToolTip(btnOpenInProces, "Deschide un document din folderul Documente In Proces");
            toolTip.SetToolTip(btnDrawZone, "Deseneaza o zona dreptunghiulara pe PDF pentru semnatura");
            toolTip.SetToolTip(btnSaveAndClose, "Salveaza documentul in Documente In Proces si inchide");
            toolTip.SetToolTip(btnFinish, "Muta documentul in Documente Semnate Complet si inchide");
            toolTip.SetToolTip(chkManualSigner, "Cand bifat, numele semnatarului este cerut manual la fiecare semnatura");

            string ver = System.Reflection.Assembly.GetExecutingAssembly()
                             .GetName().Version?.ToString() ?? "—";
            lblVersion = new Label
            {
                Text = $"v{ver}",
                Dock = DockStyle.Bottom,
                Height = 16,
                Font = new Font("Segoe UI", 7f),
                ForeColor = Color.FromArgb(70, 130, 130),
                BackColor = theme.SidebarTitleBg,
                TextAlign = ContentAlignment.MiddleCenter,
            };

            this.Load += (s, e) => RecalcLayout();
            this.Resize += (s, e) => RecalcLayout();
        }

        private void RecalcLayout()
        {
            int bottomH = panelBottom?.Height ?? 48;
            int availH = this.ClientSize.Height - bottomH - 4;
            int btnsH = (BtnH * 3) + (BtnSpacing * 2);
            int y0 = availH - btnsH;
            btnCancelLoad.Location = new Point(FieldX, y0);
            btnSaveAndClose.Location = new Point(FieldX, y0 + BtnH + BtnSpacing);
            btnFinish.Location = new Point(FieldX, y0 + (BtnH + BtnSpacing) * 2);
            btnCancelLoad.Size = new Size(ContentW, BtnH);
            btnSaveAndClose.Size = new Size(ContentW, BtnH);
            btnFinish.Size = new Size(ContentW, BtnH);
            cardsPanel.Height = btnCancelLoad.Top - cardsPanel.Top - 8;
        }

        private void BuildSidebar()
        {
            pdfOverlay = _shell.SharedOverlay;
            pdfOverlay.RectangleDrawn += OnRectangleDrawn;
            pdfOverlay.DrawingAborted += ExitDrawingMode;

            this.BackColor = AppTheme.FreeForm.SidebarBg;

            this.Controls.Add(lblSectionDocument);
            this.Controls.Add(btnOpenFile);
            this.Controls.Add(btnOpenInProces);
            this.Controls.Add(lblLoadedFile);
            this.Controls.Add(lblSectionDraw);
            this.Controls.Add(lblDrawHint);
            this.Controls.Add(btnDrawZone);
            this.Controls.Add(lblSectionSignatures);
            this.Controls.Add(lblProgress);
            this.Controls.Add(chkManualSigner);
            this.Controls.Add(cardsPanel);
            this.Controls.Add(btnCancelLoad);
            this.Controls.Add(btnSaveAndClose);
            this.Controls.Add(btnFinish);

            panelBottom = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 48,
                BackColor = AppTheme.FreeForm.SidebarTitleBg,
            };
            lblVersion.Dock = DockStyle.Bottom;
            panelBottom.Controls.Add(lblVersion);
            this.Controls.Add(panelBottom);
        }

        #region Helpers

        private Label MakeSectionLabel(string text, Point location) =>
            new Label
            {
                Text = text,
                Location = location,
                Size = new Size(ContentW, 16),
                Font = new Font("Segoe UI", 8f, FontStyle.Bold),
                ForeColor = AppTheme.FreeForm.SectionLabel,
                BackColor = Color.Transparent,
            };

        private static void WireButtonBorder(Button btn)
        {
            void UpdateStyle()
            {
                btn.FlatAppearance.BorderSize = btn.Enabled ? 2 : 0;
                btn.ForeColor = btn.Enabled ? Color.White : Color.FromArgb(110, 110, 110);
            }
            btn.EnabledChanged += (s, e) => UpdateStyle();
            UpdateStyle();
        }

        private static void HandleDisabledTextColor(Button btn)
        {
            btn.FlatStyle = FlatStyle.Flat;
            btn.UseVisualStyleBackColor = false;
            btn.Paint += (s, e) =>
            {
                if (!btn.Enabled)
                {
                    e.Graphics.Clear(btn.BackColor);
                    TextRenderer.DrawText(e.Graphics, btn.Text, btn.Font,
                        btn.ClientRectangle, Color.FromArgb(110, 110, 110),
                        TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                }
            };
        }

        #endregion
    }
}