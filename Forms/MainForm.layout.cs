using PdfiumViewer;
using System.Drawing;
using System.Resources;
using System.Windows.Forms;
using WacomSignaturePdf.Controls;
using WacomSignaturePdf.Theme;

namespace WacomSignaturePdf.Forms
{
    public partial class MainForm
    {
        #region Control Declarations

        private Panel panelSidebar;
        private Panel panelContent;
        private Splitter splitter;
        private Label lblAppTitle;
        private Label lblSectionCandidate;
        private Label lblSectionSigner;
        private Label lblCandidateIdCaption;
        private TextBox txtCandidateId;
        private Label lblCurrentSigner;
        private Label lblSectionDocument;
        private Label lblDocumentCaption;
        private DocumentTypeDropdown cmbTemplate;
        private CandidateFolderDropdown cmbCandidateFolder;
        private Button btnRefreshFolders;
        private Label lblFolderCaption;
        private Button btnLoad;
        private Button btnCancelLoad;
        private Label lblSectionSignatures;
        private Label lblProgress;
        private Label lblPartyCandidate;
        private ToggleSwitch toggleParty;
        private Label lblPartyOfficial;
        private CheckBox chkManualSigner;
        private Panel cardsPanel;
        private Button btnSaveProgress;
        private Button btnFinish;
        private Label lblLogCaption;
        private Button btnToggleLog;
        private RichTextBox txtLog;
        private DeviceStatusLabel deviceStatusLabel;
        private OneDriveStatusLabel oneDriveStatusLabel;
        private Label lblVersion;
        private Panel previewHeader;
        private Label lblPreviewCaption;
        private Button btnMirror;
        private Button btnZoomIn;
        private Button btnZoomOut;
        private PdfViewer pdfViewer;
        private ToolTip toolTip;

        #endregion

        #region Layout Constants

        private const int ButtonHeight = 42;
        private const int ButtonSpacing = 8;

        private const int YTitle = 0;
        private const int YCandidateSec = 64;
        private const int YIdRow = 84;
        private const int YFolderRow = 116;
        private const int YDocSec = 168;
        private const int YDocRow = 188;
        private const int YSigSec = 238;
        private const int YSigProgress = 256;
        private const int YPartyToggle = 278;
        private const int YCards = 314;
        private const int CardsHeight = 270;
        private const int YCancelLoad = YCards + CardsHeight + 4;
        private const int YSaveProgress = YCancelLoad + ButtonHeight + ButtonSpacing;
        private const int YFinish = YSaveProgress + ButtonHeight + ButtonSpacing;
        private const int YLogSec = YFinish + 50;
        private const int YLog = YLogSec + 18;

        // Shared horizontal layout
        private const int FieldX = 58;       // left edge of all three input fields (+10 vs before)
        private const int ButtonX = 288;     // left edge of Refresh / Incarca buttons
        private const int ButtonW = 68;      // button width
        private const int FieldW = ButtonX - FieldX - 6; // 224 — field width up to button with gap

        #endregion

        #region Layout Entry Point

        private void BuildLayout()
        {
            BuildSidebarControls();
            BuildSidebar();
            BuildPreviewHeader();
            BuildContentPanel();
            BuildForm();
        }

        #endregion

        #region Sidebar Controls

        private void BuildSidebarControls()
        {
            // ── Title ──
            lblAppTitle = new Label
            {
                Text = "PDF SIGNING",
                Location = new Point(0, YTitle),
                Size = new Size(360, 52),
                Font = new Font("Segoe UI", 13f, FontStyle.Bold),
                ForeColor = Color.White,
                BackColor = AppTheme.SidebarTitleBg,
                TextAlign = ContentAlignment.MiddleCenter
            };

            // ── Candidate section headers ──
            lblSectionCandidate = new Label
            {
                Text = "ID CANDIDAT",
                Location = new Point(16, YCandidateSec),
                Size = new Size(158, 16),
                Font = new Font("Segoe UI", 7.5f, FontStyle.Bold),
                ForeColor = AppTheme.SectionLabel,
                BackColor = Color.Transparent,
                TextAlign = ContentAlignment.MiddleCenter
            };

            lblSectionSigner = new Label
            {
                Text = "NUME SEMNATAR CURENT",
                Location = new Point(182, YCandidateSec),
                Size = new Size(ButtonX + ButtonW - 182, 16),  // right-aligns with Refresh button
                Font = new Font("Segoe UI", 7.5f, FontStyle.Bold),
                ForeColor = AppTheme.SectionLabel,
                BackColor = Color.Transparent,
                TextAlign = ContentAlignment.MiddleCenter
            };

            // ── ID field ──
            lblCandidateIdCaption = new Label
            {
                Text = "ID",
                Location = new Point(16, YIdRow + 3),
                Size = new Size(38, 20),
                Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
                ForeColor = AppTheme.SidebarSub,
                BackColor = Color.Transparent
            };

            txtCandidateId = new TextBox
            {
                Location = new Point(FieldX, YIdRow),
                Size = new Size(120, 26),
                Font = new Font("Segoe UI", 10f, FontStyle.Bold),
                BackColor = AppTheme.InputBg,
                ForeColor = AppTheme.InputText,
                BorderStyle = BorderStyle.Fixed3D,
            };
            txtCandidateId.TextChanged += txtCandidateId_TextChanged;
            txtCandidateId.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) TryLoadDocument(); };

            // right edge matches Refresh button right edge: ButtonX + ButtonW
            lblCurrentSigner = new Label
            {
                Text = "-",
                Location = new Point(182, YIdRow),
                Size = new Size(ButtonX + ButtonW - 182, 26),
                Font = new Font("Segoe UI", 10f, FontStyle.Bold),
                ForeColor = AppTheme.SplitterColor,
                BackColor = AppTheme.SidebarCardsBg,
                BorderStyle = BorderStyle.FixedSingle,
                AutoEllipsis = true,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(3, 0, 0, 0),
                Visible = true
            };

            // ── Folder picker ──
            lblFolderCaption = new Label
            {
                Text = "Dosar",
                Location = new Point(8, YFolderRow + 11),
                Size = new Size(46, 20),
                Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
                ForeColor = AppTheme.SidebarSub,
                BackColor = Color.Transparent
            };

            cmbCandidateFolder = new CandidateFolderDropdown
            {
                Location = new Point(FieldX, YFolderRow),
                Size = new Size(FieldW, 36),
                Enabled = true
            };
            cmbCandidateFolder.SelectedIndexChanged += (s, e) => OnCandidateFolderSelected();

            btnRefreshFolders = new Button
            {
                Text = "Refresh",
                Location = new Point(ButtonX, YFolderRow),
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                Size = new Size(ButtonW, ButtonHeight),
                FlatStyle = FlatStyle.Flat,
                BackColor = AppTheme.SidebarCardsBg,
                ForeColor = Color.GhostWhite,
                Cursor = Cursors.Hand,
                ImageAlign = ContentAlignment.MiddleCenter
            };
            btnRefreshFolders.FlatAppearance.BorderSize = 1;
            btnRefreshFolders.FlatAppearance.BorderColor = AppTheme.SidebarSub;
            btnRefreshFolders.Click += (s, e) => PopulateFolderDropdown();

            // ── Document section ──
            lblSectionDocument = MakeSectionLabel("DOCUMENT", new Point(16, YDocSec));

            lblDocumentCaption = new Label
            {
                Text = "Tip",
                Location = new Point(16, YDocRow + 11),
                Size = new Size(38, 20),
                Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
                ForeColor = AppTheme.SidebarSub,
                BackColor = Color.Transparent
            };

            cmbTemplate = new DocumentTypeDropdown
            {
                Location = new Point(FieldX, YDocRow),
                Size = new Size(FieldW, 36),
                Enabled = false
            };

            btnLoad = new Button
            {
                Text = "Incarca",
                Location = new Point(ButtonX, YDocRow),
                Size = new Size(ButtonW, ButtonHeight),
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                FlatStyle = FlatStyle.Flat,
                BackColor = AppTheme.AccentBlue,
                ForeColor = Color.White,
                Enabled = false,
                Cursor = Cursors.Hand
            };
            btnLoad.FlatAppearance.BorderSize = 0;
            btnLoad.FlatAppearance.BorderColor = AppTheme.AccentBorderBlue;
            btnLoad.Click += (s, e) => TryLoadDocument();

            // ── Signatures section ──
            lblSectionSignatures = MakeSectionLabel("SEMNATURI", new Point(16, YSigSec));

            lblProgress = new Label
            {
                Text = "",
                Location = new Point(16, YSigProgress),
                Size = new Size(338, 18),
                Font = new Font("Segoe UI", 8.5f),
                ForeColor = AppTheme.SidebarSub,
                BackColor = Color.Transparent,
                AutoEllipsis = true
            };

            lblPartyCandidate = new Label
            {
                Text = "Candidat",
                Location = new Point(8, YPartyToggle + 4),
                Size = new Size(68, 20),
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                ForeColor = AppTheme.AccentBlue,
                BackColor = Color.Transparent,
                TextAlign = ContentAlignment.MiddleRight
            };

            toggleParty = new ToggleSwitch
            {
                Location = new Point(82, YPartyToggle),
                IsOn = false
            };
            toggleParty.Toggled += (s, e) => OnPartyToggled();

            lblPartyOfficial = new Label
            {
                Text = "Oficial",
                Location = new Point(144, YPartyToggle + 4),
                Size = new Size(56, 20),
                Font = new Font("Segoe UI", 9f),
                ForeColor = AppTheme.SidebarSub,
                BackColor = Color.Transparent
            };

            chkManualSigner = new CheckBox
            {
                Text = "Imputernicire",
                Location = new Point(208, YPartyToggle + 5),
                Size = new Size(148, 18),
                Font = new Font("Segoe UI", 8.5f),
                ForeColor = AppTheme.SidebarSub,
                BackColor = Color.Transparent,
                Cursor = Cursors.Hand
            };
            chkManualSigner.CheckedChanged += (s, e) => UpdateCurrentSignerLabel();

            // ── Signature cards ──
            cardsPanel = new Panel
            {
                Location = new Point(8, YCards),
                Size = new Size(346, CardsHeight),
                BackColor = AppTheme.SidebarCardsBg,
                AutoScroll = true,
                BorderStyle = BorderStyle.None
            };

            // ── Action buttons ──
            btnCancelLoad = new Button
            {
                Text = "✕  Inchidere document",
                Location = new Point(8, YCancelLoad),
                Size = new Size(346, ButtonHeight),
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                FlatStyle = FlatStyle.Flat,
                BackColor = AppTheme.CancelBg,
                ForeColor = Color.White,
                Visible = false,
                Cursor = Cursors.Hand
            };
            btnCancelLoad.FlatAppearance.BorderSize = 0;
            btnCancelLoad.FlatAppearance.BorderColor = AppTheme.CancelBorder;
            btnCancelLoad.Click += (s, e) => CancelCurrentDocument();

            btnSaveProgress = new Button
            {
                Text = "💾  Salveaza progresul",
                Location = new Point(8, YSaveProgress),
                Size = new Size(346, ButtonHeight),
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                FlatStyle = FlatStyle.Flat,
                BackColor = AppTheme.AccentBlue,
                ForeColor = Color.White,
                Visible = false,
                Enabled = false,
                Cursor = Cursors.Hand
            };
            btnSaveProgress.FlatAppearance.BorderSize = 0;
            btnSaveProgress.FlatAppearance.BorderColor = AppTheme.AccentBorderBlue;
            btnSaveProgress.Click += btnSaveProgress_Click;

            btnFinish = new Button
            {
                Text = "Finalizati si Deschideti in Adobe",
                Location = new Point(8, YFinish),
                Size = new Size(346, ButtonHeight),
                Font = new Font("Segoe UI", 11f, FontStyle.Bold),
                FlatStyle = FlatStyle.Flat,
                BackColor = AppTheme.AccentGreen,
                ForeColor = Color.White,
                Enabled = false,
                Cursor = Cursors.Hand
            };
            btnFinish.FlatAppearance.BorderSize = 0;
            btnFinish.FlatAppearance.BorderColor = AppTheme.AccentBorderGreen;
            btnFinish.Click += btnFinish_Click;

            // ── Log ──
            lblLogCaption = MakeSectionLabel("LOG", new Point(16, YLogSec));

            txtLog = new RichTextBox
            {
                Location = new Point(8, YLog),
                Size = new Size(346, 80),
                ReadOnly = true,
                BackColor = AppTheme.LogBg,
                ForeColor = AppTheme.LogText,
                Font = new Font("Consolas", 7.5f),
                ScrollBars = RichTextBoxScrollBars.Vertical,
                BorderStyle = BorderStyle.None,
                Visible = false
            };

            lblLogCaption.Visible = false;

            deviceStatusLabel = new DeviceStatusLabel();
            oneDriveStatusLabel = new OneDriveStatusLabel();

            string version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "—";
            lblVersion = new Label
            {
                Text = $"v{version}",
                Dock = DockStyle.Bottom,
                Height = 18,
                Font = new Font("Segoe UI", 7f),
                ForeColor = Color.FromArgb(70, 95, 130),
                BackColor = AppTheme.SidebarTitleBg,
                TextAlign = ContentAlignment.MiddleCenter
            };

            // ── Button border wiring ──
            WireButtonBorder(btnLoad);
            WireButtonBorder(btnRefreshFolders);
            WireButtonBorder(btnSaveProgress);
            WireButtonBorder(btnFinish);
            WireButtonBorder(btnCancelLoad);

            HandleDisabledTextColor(btnCancelLoad);
            HandleDisabledTextColor(btnLoad);
            HandleDisabledTextColor(btnSaveProgress);
            HandleDisabledTextColor(btnFinish);
            HandleDisabledTextColor(btnRefreshFolders);

            // ── Tooltips ──
            toolTip = new ToolTip();
            toolTip.SetToolTip(btnCancelLoad, "Anuleaza documentul curent si permite reselectionarea");
            toolTip.SetToolTip(btnSaveProgress, "Salveaza progresul si trimite documentul la urmatoarea persoana");
            toolTip.SetToolTip(toggleParty, "Comuta intre semnaturile candidatului si ale oficialilor");
            toolTip.SetToolTip(chkManualSigner, "Cand bifat, va fi cerut numele semnatarului la fiecare semnatura");
        }

        #endregion

        #region Sidebar Assembly

        private void BuildSidebar()
        {
            panelSidebar = new Panel
            {
                Dock = DockStyle.Left,
                Width = 364,
                BackColor = AppTheme.SidebarBg
            };

            panelSidebar.Controls.Add(lblAppTitle);
            panelSidebar.Controls.Add(lblSectionCandidate);
            panelSidebar.Controls.Add(lblSectionSigner);
            panelSidebar.Controls.Add(lblCandidateIdCaption);
            panelSidebar.Controls.Add(txtCandidateId);
            panelSidebar.Controls.Add(lblCurrentSigner);
            panelSidebar.Controls.Add(cmbCandidateFolder);
            panelSidebar.Controls.Add(btnRefreshFolders);
            panelSidebar.Controls.Add(lblFolderCaption);
            panelSidebar.Controls.Add(lblSectionDocument);
            panelSidebar.Controls.Add(lblDocumentCaption);
            panelSidebar.Controls.Add(cmbTemplate);
            panelSidebar.Controls.Add(btnLoad);
            panelSidebar.Controls.Add(lblSectionSignatures);
            panelSidebar.Controls.Add(lblProgress);
            panelSidebar.Controls.Add(lblPartyCandidate);
            panelSidebar.Controls.Add(toggleParty);
            panelSidebar.Controls.Add(lblPartyOfficial);
            panelSidebar.Controls.Add(chkManualSigner);
            panelSidebar.Controls.Add(cardsPanel);
            panelSidebar.Controls.Add(btnCancelLoad);
            panelSidebar.Controls.Add(btnSaveProgress);
            panelSidebar.Controls.Add(btnFinish);
            panelSidebar.Controls.Add(lblLogCaption);
            panelSidebar.Controls.Add(txtLog);

            // ── Bottom bar ──
            btnToggleLog = new Button
            {
                Text = " LOG",
                Size = new Size(48, 32),
                Font = new Font("Segoe UI", 7.5f, FontStyle.Bold),
                FlatStyle = FlatStyle.Flat,
                BackColor = AppTheme.SidebarCardsBg,
                ForeColor = AppTheme.SidebarSub,
                Cursor = Cursors.Hand,
                Dock = DockStyle.Right
            };
            btnToggleLog.FlatAppearance.BorderSize = 1;
            btnToggleLog.FlatAppearance.BorderColor = AppTheme.SidebarSub;
            btnToggleLog.Click += (s, e) =>
            {
                bool show = !txtLog.Visible;
                txtLog.Visible = show;
                lblLogCaption.Visible = show;
                btnToggleLog.ForeColor = show ? AppTheme.CandidateFound : AppTheme.SidebarSub;
            };

            var panelBottom = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 32,
                BackColor = AppTheme.SidebarTitleBg
            };
            oneDriveStatusLabel.Dock = DockStyle.Right;
            oneDriveStatusLabel.Width = 140;
            deviceStatusLabel.Dock = DockStyle.Fill;
            panelBottom.Controls.Add(deviceStatusLabel);
            panelBottom.Controls.Add(oneDriveStatusLabel);
            panelBottom.Controls.Add(btnToggleLog);

            panelSidebar.Controls.Add(panelBottom);
            panelSidebar.Controls.Add(lblVersion);

            splitter = new Splitter
            {
                Dock = DockStyle.Left,
                Width = 3,
                BackColor = AppTheme.SplitterColor
            };
        }

        #endregion

        #region Preview Header

        private void BuildPreviewHeader()
        {
            previewHeader = new Panel
            {
                Dock = DockStyle.Top,
                Height = 44,
                BackColor = AppTheme.HeaderBg
            };

            lblPreviewCaption = new Label
            {
                Text = "Previzualizare Document PDF",
                Location = new Point(16, 0),
                Size = new Size(500, 44),
                Font = new Font("Segoe UI", 10f, FontStyle.Bold),
                ForeColor = AppTheme.PreviewCaption,
                BackColor = Color.Transparent,
                TextAlign = ContentAlignment.MiddleLeft
            };

            btnMirror = new Button
            {
                Text = "⊞  Oglindire pe Ecran",
                Size = new Size(180, 28),
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                FlatStyle = FlatStyle.Flat,
                BackColor = AppTheme.MirrorOn,
                ForeColor = Color.White,
                Cursor = Cursors.Hand,
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            btnMirror.FlatAppearance.BorderSize = 0;
            btnMirror.FlatAppearance.BorderColor = AppTheme.MirrorOnBorder;
            btnMirror.Click += btnMirror_Click;

            btnZoomIn = new Button
            {
                Size = new Size(52, 28),
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                FlatStyle = FlatStyle.Flat,
                BackColor = AppTheme.HeaderBg,
                ForeColor = AppTheme.PreviewCaption,
                Cursor = Cursors.Hand,
                Image = Image.FromStream(new System.IO.MemoryStream(Properties.Resources.zoom_in)),
                TextImageRelation = TextImageRelation.ImageBeforeText,
                ImageAlign = ContentAlignment.MiddleCenter,
                TextAlign = ContentAlignment.MiddleRight,
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Enabled = false
            };
            btnZoomIn.FlatAppearance.BorderSize = 1;
            btnZoomIn.Click += (s, e) => { if (pdfViewer.Document != null) pdfViewer.Renderer?.ZoomIn(); };

            btnZoomOut = new Button
            {
                Size = new Size(52, 28),
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                FlatStyle = FlatStyle.Flat,
                BackColor = AppTheme.HeaderBg,
                ForeColor = AppTheme.PreviewCaption,
                Cursor = Cursors.Hand,
                Image = Image.FromStream(new System.IO.MemoryStream(Properties.Resources.zoom_out)),
                TextImageRelation = TextImageRelation.ImageBeforeText,
                ImageAlign = ContentAlignment.MiddleCenter,
                TextAlign = ContentAlignment.MiddleRight,
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Enabled = false
            };
            btnZoomOut.FlatAppearance.BorderSize = 1;
            btnZoomOut.Click += (s, e) => { if (pdfViewer.Document != null) pdfViewer.Renderer?.ZoomOut(); };

            previewHeader.Controls.Add(lblPreviewCaption);
            previewHeader.Controls.Add(btnMirror);
            previewHeader.Controls.Add(btnZoomIn);
            previewHeader.Controls.Add(btnZoomOut);

            previewHeader.Resize += (s, e) =>
            {
                btnMirror.Location = new Point(previewHeader.Width - btnMirror.Width - 12, 8);
                btnZoomIn.Location = new Point(btnMirror.Left - btnZoomIn.Width - 8, 8);
                btnZoomOut.Location = new Point(btnZoomIn.Left - btnZoomOut.Width - 4, 8);
            };

            previewHeader.Paint += (s, e) =>
            {
                using (var pen = new Pen(AppTheme.HeaderBorder, 1f))
                    e.Graphics.DrawLine(pen, 0, previewHeader.Height - 1,
                        previewHeader.Width, previewHeader.Height - 1);
            };

            WireButtonBorder(btnMirror);
        }

        #endregion

        #region Content Panel

        private void BuildContentPanel()
        {
            pdfViewer = new PdfViewer
            {
                Dock = DockStyle.Fill,
                ShowToolbar = true,
                ShowBookmarks = false
            };

            panelContent = new Panel { Dock = DockStyle.Fill, BackColor = AppTheme.ContentBg };
            panelContent.Controls.Add(pdfViewer);
            panelContent.Controls.Add(previewHeader);
        }

        #endregion

        #region Form Setup

        private void BuildForm()
        {
            Text = "Wacom Signature — PDF Signing";
            ClientSize = new Size(1300, 800);
            MinimumSize = new Size(1000, 650);
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = AppTheme.ContentBg;
            Font = new Font("Segoe UI", 9f);
            FormBorderStyle = FormBorderStyle.Sizable;
            WindowState = FormWindowState.Maximized;

            Controls.Add(panelContent);
            Controls.Add(splitter);
            Controls.Add(panelSidebar);
        }

        #endregion

        #region Helpers

        private static Label MakeSectionLabel(string text, Point location) =>
            new Label
            {
                Text = text,
                Location = location,
                Size = new Size(338, 16),
                Font = new Font("Segoe UI", 7.5f, FontStyle.Bold),
                ForeColor = AppTheme.SectionLabel,
                BackColor = Color.Transparent
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

                    TextRenderer.DrawText(
                        e.Graphics,
                        btn.Text,
                        btn.Font,
                        btn.ClientRectangle,
                        Color.FromArgb(110, 110, 110),
                        TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter
                    );
                }
            };
        }

        #endregion
    }
}