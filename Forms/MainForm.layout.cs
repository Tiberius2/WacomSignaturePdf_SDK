using PdfiumViewer;
using System.Drawing;
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
        private TextBox txtFolderSearch;
        private Label btnSearchClear;
        private Button btnRefreshFolders;
        private Label lblFolderCaption;
        private Button btnLoad;
        private Button btnCancelLoad;
        private Label lblSectionSignatures;
        private Label lblProgress;
        // ── Filter toggle (TOP row) ──
        private Label lblFilterLeft;       // "Toate semnaturile si documentele"
        private ToggleSwitch toggleFilter;
        private Label lblFilterRight;      // "Doar semnaturile mele"
        // ── Party toggle (BOTTOM row) ──
        private Label lblPartyCandidate;   // "Semnaturi candidat"
        private ToggleSwitch toggleParty;
        private Label lblPartyOfficial;    // "Semnaturi interne"
        // ── Imputernicire ──
        private CheckBox chkManualSigner;
        private Panel cardsPanel;
        private Button btnSaveProgress;
        private Button btnFinish;
        private DeviceStatusLabel deviceStatusLabel;
        private OneDriveStatusLabel oneDriveStatusLabel;
        private Panel panelBottom;
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

        private const int ButtonHeight = 38;    // bottom action buttons
        private const int RowButtonHeight = 40; // inline row buttons (Refresh, Load)
        private const int ButtonSpacing = 10;

        // ── Vertical positions ──────────────────────────────────────────────
        private const int YTitle = 0;
        private const int YCandidateSec = 72;  // ascuns
        private const int YIdRow = 94;          // ascuns
        private const int YFolderRow = 68;      // search box Dosar — sub titlu(52) + 16px gap
        private const int YDocRow = 150;        // Tip Doc. — sub folder dropdown(68+26+40=134) + 16px gap
        private const int YSigSec = 224;        // SEMNATURI label — sub doc row(150+40=190) + 34px gap
        private const int YSigProgress = 246;   // progress — sub sectiune + 22px
        private const int YFilterToggle = 270;  // toggle filter
        private const int YPartyToggle = YFilterToggle + 34;
        private const int YImputernicire = YPartyToggle + 34;
        private const int YCards = YImputernicire + 30;
        private const int CardsHeight = 100;  // ajustat dinamic de RecalcButtonPositions
        private const int YCancelLoad = YCards + CardsHeight + 4;
        private const int YSaveProgress = YCancelLoad + ButtonHeight + ButtonSpacing;
        private const int YFinish = YSaveProgress + ButtonHeight + ButtonSpacing;

        // ── Horizontal layout ───────────────────────────────────────────────
        private const int SidebarWidth = 460;
        private const int FieldX = 58;
        private const int ButtonW = 68;
        private const int ButtonX = SidebarWidth - 8 - ButtonW;  // 384
        private const int FieldW = ButtonX - FieldX - 6;        // 320
        private const int ContentW = SidebarWidth - 16;           // 444

        // ── Toggle centering (both toggles at the same X for visual alignment) ──
        private const int ToggleX = SidebarWidth / 2 - 28;   // 202 — toggle left edge
        private const int LabelLeft = 4;                         // close to sidebar edge
        private const int LabelWidth = ToggleX - LabelLeft - 4;  // 194 — left label width (4px gap to toggle)
        private const int LabelRightX = ToggleX + 56 + 4;         // 262 — right label left edge (4px gap from toggle)
        private const int LabelRightW = SidebarWidth - 8 - LabelRightX; // 190 — right label width

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
                Size = new Size(SidebarWidth, 52),
                Font = new Font("Segoe UI", 13f, FontStyle.Bold),
                ForeColor = Color.White,
                BackColor = AppTheme.Template.SidebarTitleBg,
                TextAlign = ContentAlignment.MiddleCenter
            };

            // ── Candidate / signer headers ──
            lblSectionCandidate = new Label
            {
                Visible = false,
                Text = "ID CANDIDAT",
                Location = new Point(16, YCandidateSec),
                Size = new Size(158, 16),
                Font = new Font("Segoe UI", 7.5f, FontStyle.Bold),
                ForeColor = AppTheme.Template.SectionLabel,
                BackColor = Color.Transparent,
                TextAlign = ContentAlignment.MiddleCenter
            };

            lblSectionSigner = new Label
            {
                Visible = false,
                Text = "NUME SEMNATAR CURENT",
                Location = new Point(182, YCandidateSec),
                Size = new Size(ButtonX + ButtonW - 182, 16),
                Font = new Font("Segoe UI", 7.5f, FontStyle.Bold),
                ForeColor = AppTheme.Template.SectionLabel,
                BackColor = Color.Transparent,
                TextAlign = ContentAlignment.MiddleCenter
            };

            // ── ID field ──
            lblCandidateIdCaption = new Label
            {
                Visible = false,
                Text = "ID",
                Location = new Point(16, YIdRow + 3),
                Size = new Size(38, 20),
                Font = new Font("Segoe UI", 8f, FontStyle.Bold),
                ForeColor = AppTheme.Template.SidebarSub,
                BackColor = Color.Transparent
            };

            txtCandidateId = new TextBox
            {
                Visible = false,
                Location = new Point(FieldX, YIdRow),
                Size = new Size(120, 26),
                Font = new Font("Segoe UI", 10f, FontStyle.Bold),
                BackColor = AppTheme.Template.SidebarCardsBg,
                ForeColor = AppTheme.SplitterColor,
                BorderStyle = BorderStyle.FixedSingle,
            };
            txtCandidateId.TextChanged += txtCandidateId_TextChanged;
            txtCandidateId.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) TryLoadDocument(); };

            lblCurrentSigner = new Label
            {
                Visible = false,
                Text = "-",
                Location = new Point(184, YIdRow),
                Size = new Size(ButtonX + ButtonW - 184, 26),
                Font = new Font("Segoe UI", 10f, FontStyle.Bold),
                ForeColor = AppTheme.SplitterColor,
                BackColor = AppTheme.Template.SidebarCardsBg,
                BorderStyle = BorderStyle.FixedSingle,
                AutoEllipsis = true,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(3, 0, 0, 0)
            };

            // ── Folder picker ──
            lblFolderCaption = new Label
            {
                Text = "Dosar",
                Location = new Point(8, YFolderRow - 16),
                Size = new Size(46, 20),
                Font = new Font("Segoe UI", 8f, FontStyle.Bold),
                ForeColor = AppTheme.Template.SidebarSub,
                BackColor = Color.Transparent
            };

            txtFolderSearch = new TextBox
            {
                Location = new Point(FieldX, YFolderRow),
                Size = new Size(FieldW, 26),
                Font = new Font("Segoe UI", 9f),
                BackColor = AppTheme.InputBg,
                ForeColor = AppTheme.Template.SidebarSub,
                BorderStyle = BorderStyle.Fixed3D,
                Text = "Cauta dosar candidat..."
            };
            txtFolderSearch.GotFocus += (s, e) =>
            {
                _folderSearchActive = true;
                if (txtFolderSearch.Text == "Cauta dosar candidat...")
                {
                    txtFolderSearch.Text = "";
                    txtFolderSearch.ForeColor = Color.Black;
                    txtFolderSearch.Font = new Font("Segoe UI", 9f, FontStyle.Bold);
                }
            };
            txtFolderSearch.LostFocus += (s, e) =>
            {
                if (string.IsNullOrWhiteSpace(txtFolderSearch.Text))
                {
                    txtFolderSearch.Text = "Cauta dosar candidat...";
                    txtFolderSearch.ForeColor = AppTheme.Template.SidebarSub;
                    txtFolderSearch.Font = new Font("Segoe UI", 9f, FontStyle.Regular);
                    btnSearchClear.Visible = false;
                }
            };
            txtFolderSearch.TextChanged += OnFolderSearchTextChanged;

            btnSearchClear = new Label
            {
                Text = "X",
                Location = new Point(ButtonX - 25, YFolderRow),
                Size = new Size(19, 21),
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                ForeColor = Color.FromArgb(200, 60, 60),
                BackColor = AppTheme.InputBg,
                TextAlign = ContentAlignment.MiddleCenter,
                Cursor = Cursors.Hand,
                Visible = false
            };
            btnSearchClear.Click += (s, e) => ClearFolderSearch();

            cmbCandidateFolder = new CandidateFolderDropdown
            {
                Location = new Point(FieldX, YFolderRow + 30),
                Size = new Size(FieldW, 40),
                Enabled = true
            };
            cmbCandidateFolder.SelectedIndexChanged += (s, e) => OnCandidateFolderSelected();

            btnRefreshFolders = new Button
            {
                Text = "Refresh",
                Location = new Point(ButtonX, YFolderRow + 30),
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                Size = new Size(ButtonW, RowButtonHeight),
                FlatStyle = FlatStyle.Flat,
                BackColor = AppTheme.Template.SidebarCardsBg,
                ForeColor = Color.GhostWhite,
                Cursor = Cursors.Hand,
                ImageAlign = ContentAlignment.MiddleCenter
            };
            btnRefreshFolders.FlatAppearance.BorderSize = 1;
            btnRefreshFolders.FlatAppearance.BorderColor = AppTheme.Template.SidebarSub;
            btnRefreshFolders.Click += (s, e) => PopulateFolderDropdown();

            // ── Document section ──
            lblDocumentCaption = new Label
            {
                Text = "Tip Doc.",
                Location = new Point(8, YDocRow + 8),
                Size = new Size(50, 20),
                Font = new Font("Segoe UI", 8f, FontStyle.Bold),
                ForeColor = AppTheme.Template.SidebarSub,
                BackColor = Color.Transparent
            };

            cmbTemplate = new DocumentTypeDropdown
            {
                Location = new Point(FieldX, YDocRow),
                Size = new Size(FieldW, 40),
                Enabled = false
            };

            btnLoad = new Button
            {
                Text = "Incarca",
                Location = new Point(ButtonX, YDocRow),
                Size = new Size(ButtonW, RowButtonHeight),
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
                Size = new Size(ContentW, 18),
                Font = new Font("Segoe UI", 8.5f),
                ForeColor = AppTheme.Template.SidebarSub,
                BackColor = Color.Transparent,
                AutoEllipsis = true
            };

            // ── Filter toggle row (TOP): "Toate semnaturile..." [toggle] "Doar semnaturile mele" ──
            // IsOn = false → "Toate" (showAll) — left label active
            // IsOn = true  → "Doar ale mele" (myOnly) — right label active
            lblFilterLeft = new Label
            {
                Text = "Toate semnaturile si docum...",
                Location = new Point(LabelLeft, YFilterToggle + 4),
                Size = new Size(LabelWidth, 20),
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                ForeColor = AppTheme.AccentGreen,   // "Toate" is the default active state
                BackColor = Color.Transparent,
                TextAlign = ContentAlignment.MiddleRight,
                AutoEllipsis = true
            };

            toggleFilter = new ToggleSwitch
            {
                Location = new Point(ToggleX, YFilterToggle),
                IsOn = false   // default: "Toate" (showAll)
            };
            // wired in MainForm constructor

            lblFilterRight = new Label
            {
                Text = "Doar semnaturile mele",
                Location = new Point(LabelRightX, YFilterToggle + 4),
                Size = new Size(LabelRightW, 20),
                Font = new Font("Segoe UI", 9.5f),
                ForeColor = AppTheme.Template.SidebarSub,
                BackColor = Color.Transparent,
                TextAlign = ContentAlignment.MiddleLeft,
                AutoEllipsis = true
            };

            // ── Party toggle row (BOTTOM): "Semnaturi candidat" [toggle] "Semnaturi interne" ──
            lblPartyCandidate = new Label
            {
                Text = "Semnaturi candidat",
                Location = new Point(LabelLeft, YPartyToggle + 4),
                Size = new Size(LabelWidth, 20),
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                ForeColor = AppTheme.AccentBlue,
                BackColor = Color.Transparent,
                TextAlign = ContentAlignment.MiddleRight,
                AutoEllipsis = true
            };

            toggleParty = new ToggleSwitch
            {
                Location = new Point(ToggleX, YPartyToggle),
                IsOn = false
            };
            toggleParty.Toggled += (s, e) => OnPartyToggled();

            lblPartyOfficial = new Label
            {
                Text = "Semnaturi interne",
                Location = new Point(LabelRightX, YPartyToggle + 4),
                Size = new Size(LabelRightW, 20),
                Font = new Font("Segoe UI", 9.5f),
                ForeColor = AppTheme.Template.SidebarSub,
                BackColor = Color.Transparent,
                TextAlign = ContentAlignment.MiddleLeft,
                AutoEllipsis = true
            };

            // ── Imputernicire — bottom right below both toggles ──
            chkManualSigner = new CheckBox
            {
                Text = "IMPUTERNICIRE",
                Location = new Point(SidebarWidth - 8 - 136, YImputernicire + 3),
                Size = new Size(136, 18),
                Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
                ForeColor = Color.White,
                BackColor = Color.Transparent,
                CheckAlign = ContentAlignment.MiddleRight,
                TextAlign = ContentAlignment.MiddleRight,
                Cursor = Cursors.Hand,
                UseVisualStyleBackColor = false,
            };
            chkManualSigner.Paint += (s, e) =>
            {
                e.Graphics.Clear(AppTheme.Template.SidebarBg);
                Color textColor = chkManualSigner.Enabled ? Color.White : Color.FromArgb(100, 130, 180);
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
            chkManualSigner.CheckedChanged += (s, e) => { ReflowCards(); UpdateCurrentSignerLabel(); };

            // ── Signature cards ──
            cardsPanel = new Panel
            {
                Location = new Point(8, YCards),
                Size = new Size(ContentW, CardsHeight),
                BackColor = AppTheme.Template.SidebarCardsBg,
                AutoScroll = true,
                BorderStyle = BorderStyle.None
            };

            // ── Action buttons ──
            btnCancelLoad = new Button
            {
                Text = "Inchidere document",
                Location = new Point(8, YCancelLoad),
                Size = new Size(ContentW, ButtonHeight),
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
                Text = "Salveaza progresul",
                Location = new Point(8, YSaveProgress),
                Size = new Size(ContentW, ButtonHeight),
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
                Size = new Size(ContentW, ButtonHeight),
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


            // ── Status ──
            deviceStatusLabel = new DeviceStatusLabel();
            oneDriveStatusLabel = new OneDriveStatusLabel();

            string version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "—";
            lblVersion = new Label
            {
                Text = $"v{version}",
                Dock = DockStyle.Bottom,
                Height = 18,
                Font = new Font("Segoe UI", 7f),
                ForeColor = AppTheme.Template.SidebarTitleBg,
                BackColor = AppTheme.Template.SidebarTitleBg,
                TextAlign = ContentAlignment.MiddleCenter
            };

            // ── Button wiring ──
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
            toolTip.SetToolTip(toggleParty, "Comuta intre semnaturile candidatilor si cele interne");
            toolTip.SetToolTip(toggleFilter, "Toate: afiseaza tot | Doar semnaturile mele: filtreaza documente si semnaturi dupa rolul tau");
            toolTip.SetToolTip(chkManualSigner, "Cand bifat, numele semnatarului este cerut manual la fiecare semnatura");

        }

        #endregion

        #region Button Positioning

        internal void RecalcButtonPositions()
        {
            if (panelSidebar == null || panelBottom == null) return;
            int availH = panelSidebar.ClientSize.Height - panelBottom.Height - 4;
            int btnsH = (ButtonHeight * 3) + (ButtonSpacing * 2) + 8;
            btnCancelLoad.Location = new Point(8, availH - btnsH);
            btnSaveProgress.Location = new Point(8, availH - btnsH + ButtonHeight + ButtonSpacing);
            btnFinish.Location = new Point(8, availH - btnsH + (ButtonHeight + ButtonSpacing) * 2);
            if (cardsPanel != null)
                cardsPanel.Height = btnCancelLoad.Top - cardsPanel.Top - 8;
        }

        #endregion

        #region Sidebar Assembly

        private void BuildSidebar()
        {
            panelSidebar = new Panel
            {
                Dock = DockStyle.Left,
                Width = SidebarWidth,
                BackColor = AppTheme.Template.SidebarBg
            };
            panelSidebar.Resize += (s, e) => RecalcButtonPositions();

            panelSidebar.Controls.Add(lblAppTitle);
            panelSidebar.Controls.Add(lblSectionCandidate);
            panelSidebar.Controls.Add(lblSectionSigner);
            panelSidebar.Controls.Add(lblCandidateIdCaption);
            panelSidebar.Controls.Add(txtCandidateId);
            panelSidebar.Controls.Add(lblCurrentSigner);

            panelSidebar.Controls.Add(txtFolderSearch);
            panelSidebar.Controls.Add(btnSearchClear);
            btnSearchClear.BringToFront();
            panelSidebar.Controls.Add(cmbCandidateFolder);
            panelSidebar.Controls.Add(btnRefreshFolders);
            panelSidebar.Controls.Add(lblFolderCaption);
            panelSidebar.Controls.Add(lblDocumentCaption);
            panelSidebar.Controls.Add(cmbTemplate);
            panelSidebar.Controls.Add(btnLoad);
            panelSidebar.Controls.Add(lblSectionSignatures);
            panelSidebar.Controls.Add(lblProgress);
            panelSidebar.Controls.Add(lblFilterLeft);
            panelSidebar.Controls.Add(toggleFilter);
            panelSidebar.Controls.Add(lblFilterRight);
            panelSidebar.Controls.Add(lblPartyCandidate);
            panelSidebar.Controls.Add(toggleParty);
            panelSidebar.Controls.Add(lblPartyOfficial);
            panelSidebar.Controls.Add(chkManualSigner);
            panelSidebar.Controls.Add(cardsPanel);
            panelSidebar.Controls.Add(btnCancelLoad);
            panelSidebar.Controls.Add(btnSaveProgress);
            panelSidebar.Controls.Add(btnFinish);

            // ── Bottom bar ──
            panelBottom = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 48,  // 32 status + 16 version
                BackColor = Color.FromArgb(238, 240, 245)
            };
            lblVersion.Dock = DockStyle.Bottom;
            oneDriveStatusLabel.Dock = DockStyle.Right;
            oneDriveStatusLabel.Width = 140;
            deviceStatusLabel.Dock = DockStyle.Fill;
            panelBottom.Controls.Add(deviceStatusLabel);
            panelBottom.Controls.Add(oneDriveStatusLabel);
            panelBottom.Controls.Add(lblVersion);

            panelSidebar.Controls.Add(panelBottom);

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
            MinimumSize = new Size(1100, 650);
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
                Size = new Size(ContentW, 16),
                Font = new Font("Segoe UI", 7.5f, FontStyle.Bold),
                ForeColor = AppTheme.Template.SectionLabel,
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
                    TextRenderer.DrawText(e.Graphics, btn.Text, btn.Font, btn.ClientRectangle,
                        Color.FromArgb(110, 110, 110),
                        TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                }
            };
        }

        #endregion
    }
}