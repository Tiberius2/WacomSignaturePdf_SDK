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
        private Label lblCurrentSigner;
        private Label lblSectionDocument;
        private Label lblDocumentCaption;
        private DocumentTypeDropdown cmbTemplate;
        private Label lblSelectedFolderName;
        private Button btnSelectFolder;
        private Label lblFolderCaption;
        private Button btnLoad;
        private Button btnCancelLoad;
        private Label lblSectionSignatures;
        private Label lblProgress;
        private Label lblFiltre;
        // ── Filter pill (TOP row) ──
        private PillSwitcher pillFilter;
        // ── Party pill (BOTTOM row) ──
        private PillSwitcher3 pillParty;
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

        // Labels pentru UpdateFilterUI / UpdatePartyLabels — alias-uri goale
        private Label lblFilterLeft;
        private Label lblFilterRight;
        private Label lblPartyCandidate;
        private Label lblPartyOfficial;

        #endregion

        #region Layout Constants

        private const int ButtonHeight = 38;
        private const int RowButtonHeight = 40;
        private const int ButtonSpacing = 10;

        private const int YTitle = 0;
        private const int YCandidateSec = 72;
        private const int YIdRow = 94;
        private const int YFolderRow = 68;
        private const int YDocRow = 123;   // YFolderRow(68) + folderH(40) + 15px gap
        private const int PillH = 36;

        // Layout cascadat de sus in jos — fara spatiu intre DOSAR si TIP DOC
        private const int YFilterLabel = YDocRow + 44 + 8;      // direct sub dropdown, fara gap
        private const int YFilterToggle = YFilterLabel + 28;    // 5px sub label
        private const int YPartyToggle = YFilterToggle + PillH + 4;
        private const int YImputernicire = YPartyToggle + PillH + 6;
        private const int YCards = YImputernicire + 26;
        private const int YSigProgress = YCards - 20;           // deasupra cardurilor
        private const int CardsHeight = 100;
        private const int YCancelLoad = YCards + CardsHeight + 4;
        private const int YSaveProgress = YCancelLoad + ButtonHeight + ButtonSpacing;
        private const int YFinish = YSaveProgress + ButtonHeight + ButtonSpacing;

        // Pastrate pentru compatibilitate
        private const int YSigSec = 224;
        private const int SidebarWidth = 460;
        private const int FieldX = 58;
        private const int ButtonW = 68;
        private const int ButtonX = SidebarWidth - 8 - ButtonW;
        private const int FieldW = ButtonX - FieldX - 6;
        private const int ContentW = SidebarWidth - 16;
        private const int ToggleX = SidebarWidth / 2 - 28;
        private const int LabelLeft = 4;
        private const int LabelWidth = ToggleX - LabelLeft - 4;
        private const int LabelRightX = ToggleX + 56 + 4;
        private const int LabelRightW = SidebarWidth - 8 - LabelRightX;

        #endregion

        #region Layout Entry Point

        private void BuildLayout()
        {
            BuildSidebarControls();
            BuildSidebar();
            BuildPreviewHeader();
            BuildContentPanel();
            BuildForm();
            InitTooltips();
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
                ForeColor = System.Drawing.Color.White,
                BackColor = AppTheme.Template.SidebarTitleBg,
                TextAlign = ContentAlignment.MiddleCenter
            };

            lblSectionCandidate = new Label
            {
                Visible = false,
                Text = "ID CANDIDAT",
                Location = new Point(16, YCandidateSec),
                Size = new Size(158, 16),
                Font = new Font("Segoe UI", 7.5f, FontStyle.Bold),
                ForeColor = AppTheme.Template.SectionLabel,
                BackColor = System.Drawing.Color.Transparent,
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
                BackColor = System.Drawing.Color.Transparent,
                TextAlign = ContentAlignment.MiddleCenter
            };

            lblCandidateIdCaption = new Label
            {
                Visible = false,
                Text = "ID",
                Location = new Point(16, YIdRow + 3),
                Size = new Size(38, 20),
                Font = new Font("Segoe UI", 8f, FontStyle.Bold),
                ForeColor = AppTheme.Template.SidebarSub,
                BackColor = System.Drawing.Color.Transparent
            };

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
                Text = "DOSAR",
                Location = new Point(8, YFolderRow),
                Size = new Size(50, 40),
                Font = new Font("Segoe UI", 8f, FontStyle.Bold),
                ForeColor = AppTheme.Template.SidebarSub,
                BackColor = System.Drawing.Color.Transparent,
                TextAlign = ContentAlignment.MiddleLeft,
            };

            lblSelectedFolderName = new Label
            {
                Text = "Niciun dosar selectat",
                Location = new Point(FieldX, YFolderRow),
                Size = new Size(FieldW, 40),
                Font = new Font("Segoe UI", 10f, FontStyle.Bold),
                ForeColor = System.Drawing.Color.FromArgb(30, 40, 60),
                BackColor = System.Drawing.Color.LightGoldenrodYellow,
                TextAlign = ContentAlignment.MiddleCenter,
                Padding = new Padding(8, 0, 0, 0),
                AutoEllipsis = true,
            };
            lblSelectedFolderName.Paint += (s, e) =>
            {
                using (var pen = new System.Drawing.Pen(System.Drawing.Color.FromArgb(180, 190, 210), 2))
                    e.Graphics.DrawRectangle(pen, 1, 1, lblSelectedFolderName.Width - 3, lblSelectedFolderName.Height - 3);
            };

            btnSelectFolder = new Button
            {
                Text = "Alege Dosar",
                Location = new Point(ButtonX, YFolderRow),
                Size = new Size(ButtonW, RowButtonHeight),
                Font = new Font("Segoe UI", 8f, FontStyle.Bold),
                FlatStyle = FlatStyle.Flat,
                BackColor = AppTheme.AccentBlue,
                ForeColor = System.Drawing.Color.White,
                Cursor = Cursors.Hand,
            };
            btnSelectFolder.FlatAppearance.BorderSize = 0;
            btnSelectFolder.FlatAppearance.BorderColor = AppTheme.AccentBorderBlue;
            btnSelectFolder.Click += (s, e) => OpenFolderPicker();

            // ── Document section ──
            lblDocumentCaption = new Label
            {
                Text = "TIP DOC.",
                Location = new Point(8, YDocRow),
                Size = new Size(50, 40),
                Font = new Font("Segoe UI", 8f, FontStyle.Bold),
                ForeColor = AppTheme.Template.SidebarSub,
                BackColor = System.Drawing.Color.Transparent,
                TextAlign = ContentAlignment.MiddleLeft,
            };

            cmbTemplate = new DocumentTypeDropdown
            {
                Location = new Point(FieldX, YDocRow),
                Size = new Size(FieldW, 40),
                Enabled = false,
            };

            btnLoad = new Button
            {
                Text = "Incarca",
                Location = new Point(ButtonX, YDocRow),
                Size = new Size(ButtonW, RowButtonHeight),
                Font = new Font("Segoe UI", 8f, FontStyle.Bold),
                FlatStyle = FlatStyle.Flat,
                BackColor = AppTheme.AccentBlue,
                ForeColor = System.Drawing.Color.White,
                Enabled = false,
                Cursor = Cursors.Hand
            };
            btnLoad.FlatAppearance.BorderSize = 0;
            btnLoad.FlatAppearance.BorderColor = AppTheme.AccentBorderBlue;
            btnLoad.Click += (s, e) => TryLoadDocument();

            // ── Progress ──
            lblSectionSignatures = new Label { Visible = false }; // pastrat pentru compatibilitate
            lblProgress = new Label
            {
                Text = "",
                Location = new Point(8, YImputernicire + 3),
                Size = new Size(ContentW / 2, 18),
                Font = new Font("Segoe UI", 8.5f),
                ForeColor = AppTheme.Template.SidebarSub,
                BackColor = System.Drawing.Color.Transparent,
                AutoEllipsis = true
            };

            // ── Label filtre ──
            lblFiltre = new Label
            {
                Text = "FILTRE SEMNATURI SI DOCUMENTE",
                Location = new Point(8, YFilterToggle - 26),
                Size = new Size(ContentW, 28),
                Font = new Font("Segoe UI", 10f, FontStyle.Bold),
                ForeColor = AppTheme.Template.SectionLabel,
                BackColor = System.Drawing.Color.Transparent,
                TextAlign = ContentAlignment.MiddleCenter
            };

            // ── Filter pill ──
            pillFilter = new PillSwitcher("Fara Filtru", "Necesita Semnatura Mea")
            {
                Location = new Point(8, YFilterToggle),
                Size = new Size(ContentW, PillH),
                ActiveBg = System.Drawing.Color.FromArgb(126, 232, 192),
                ActiveFg = System.Drawing.Color.FromArgb(6, 61, 40),
                InactiveFg = System.Drawing.Color.FromArgb(190, 210, 245),
                IsOn = false,
            };
            pillFilter.Toggled += OnFilterToggled;

            // ── Party pill ──
            pillParty = new PillSwitcher3("Toate Semnaturile", "Semn. Angajat", "Semn. Interne")
            {
                Location = new Point(8, YPartyToggle),
                Size = new Size(ContentW, PillH),
                ActiveBg = System.Drawing.Color.FromArgb(255, 230, 128),
                ActiveFg = System.Drawing.Color.FromArgb(74, 48, 0),
                InactiveFg = System.Drawing.Color.FromArgb(190, 210, 245),
            };
            pillParty.SelectionChanged += (s, e) => OnPartyToggled();

            // Alias-uri labels invizibile
            lblFilterLeft = new Label { Visible = false };
            lblFilterRight = new Label { Visible = false };
            lblPartyCandidate = new Label { Visible = false };
            lblPartyOfficial = new Label { Visible = false };

            // ── Imputernicire ──
            chkManualSigner = new System.Windows.Forms.CheckBox
            {
                Text = "IMPUTERNICIRE",
                Location = new Point(SidebarWidth - 8 - 136, YImputernicire + 3),
                Size = new Size(136, 18),
                Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
                ForeColor = System.Drawing.Color.White,
                BackColor = System.Drawing.Color.Transparent,
                CheckAlign = ContentAlignment.MiddleRight,
                TextAlign = ContentAlignment.MiddleRight,
                Cursor = Cursors.Hand,
                UseVisualStyleBackColor = false,
            };
            chkManualSigner.Paint += (s, e) =>
            {
                e.Graphics.Clear(AppTheme.Template.SidebarBg);
                System.Drawing.Color textColor = chkManualSigner.Enabled
                    ? System.Drawing.Color.White
                    : System.Drawing.Color.FromArgb(100, 130, 180);
                TextRenderer.DrawText(e.Graphics, chkManualSigner.Text, chkManualSigner.Font,
                    new System.Drawing.Rectangle(0, 0, chkManualSigner.Width - 18, chkManualSigner.Height),
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
                BorderStyle = BorderStyle.FixedSingle
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
                ForeColor = System.Drawing.Color.White,
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
                ForeColor = System.Drawing.Color.White,
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
                ForeColor = System.Drawing.Color.White,
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
            WireButtonBorder(btnSelectFolder);
            WireButtonBorder(btnLoad);
            WireButtonBorder(btnSaveProgress);
            WireButtonBorder(btnFinish);
            WireButtonBorder(btnCancelLoad);

            HandleDisabledTextColor(btnCancelLoad);
            HandleDisabledTextColor(btnLoad);
            HandleDisabledTextColor(btnSaveProgress);
            HandleDisabledTextColor(btnFinish);

            // ── Tooltips ──
            toolTip = new ToolTip { AutoPopDelay = 8000, InitialDelay = 500, ReshowDelay = 300 };

            // Butoane principale
            toolTip.SetToolTip(btnSelectFolder, "Deschide lista dosarelor candidatilor / angajatilor");
            toolTip.SetToolTip(btnLoad, "Incarca documentul selectat pentru semnare");
            toolTip.SetToolTip(btnCancelLoad, "Inchide documentul curent (cu optiune de salvare a progresului)");
            toolTip.SetToolTip(btnSaveProgress, "Salveaza semnaturile capturate pana acum si elibereaza documentul");
            toolTip.SetToolTip(btnFinish, "Finalizeaza documentul, sigileaza PDF-ul si il deschide in Adobe");

            // Filtre
            toolTip.SetToolTip(pillFilter,
                "Fara Filtru: afiseaza toate dosarele, documentele si semnaturile, indiferent de rol" +
                "Necesita Semnatura Mea: filtreaza lista de dosare, tipurile de documente si semnaturile dupa rolul tau curent");
            toolTip.SetToolTip(pillParty,
                "Toate Semnaturile: afiseaza semnaturile tuturor partilor (angajat + interne)" +
                "Semn. Angajat: afiseaza doar semnaturile care apartin angajatului / candidatului" +
                "Semn. Interne: afiseaza doar semnaturile interne ale companiei (HR, Director, Admin etc.)");

            // Alte controale
            toolTip.SetToolTip(chkManualSigner,
                "Imputernicire: permite semnarea in numele altei persoane" +
                "Cand este bifat, numele semnatarului va fi cerut manual inainte de fiecare semnatura");
            toolTip.SetToolTip(cmbTemplate, "Selecteaza tipul de document pe care doresti sa il semnezi");
            toolTip.SetToolTip(lblSelectedFolderName, "Dosarul candidatului / angajatului selectat curent");
        }

        #endregion

        #region Tooltips

        private void InitTooltips()
        {
            toolTip = new ToolTip { AutoPopDelay = 8000, InitialDelay = 500, ReshowDelay = 300, ShowAlways = true };

            // Butoane principale
            toolTip.SetToolTip(btnSelectFolder, "Deschide lista dosarelor candidatilor / angajatilor");
            toolTip.SetToolTip(btnLoad, "Incarca documentul selectat pentru semnare");
            toolTip.SetToolTip(btnCancelLoad, "Inchide documentul curent (cu optiune de salvare a progresului)");
            toolTip.SetToolTip(btnSaveProgress, "Salveaza semnaturile capturate pana acum si elibereaza documentul");
            toolTip.SetToolTip(btnFinish, "Finalizeaza documentul, sigileaza PDF-ul si il deschide in Adobe");

            // Filtre
            pillFilter.SetTooltip(toolTip,
                "Fara Filtru: afiseaza toate dosarele, documentele si semnaturile, indiferent de rol\n" +
                "Necesita Semnatura Mea: filtreaza lista de dosare, tipurile de documente si semnaturile dupa rolul tau curent");
            pillParty.SetTooltip(toolTip,
                "Toate Semnaturile: afiseaza semnaturile tuturor partilor (angajat + interne)\n" +
                "Semn. Angajat: afiseaza doar semnaturile care apartin angajatului / candidatului\n" +
                "Semn. Interne: afiseaza doar semnaturile interne ale companiei (HR, Director, Admin etc.)");

            // Alte controale
            toolTip.SetToolTip(chkManualSigner,
                "Imputernicire: permite semnarea in numele altei persoane\n" +
                "Cand este bifat, numele semnatarului va fi cerut manual inainte de fiecare semnatura");
            toolTip.SetToolTip(cmbTemplate, "Selecteaza tipul de document pe care doresti sa il semnezi");
            toolTip.SetToolTip(lblSelectedFolderName, "Dosarul candidatului / angajatului selectat curent");
        }

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
            panelSidebar.Controls.Add(lblFolderCaption);
            panelSidebar.Controls.Add(lblSelectedFolderName);
            panelSidebar.Controls.Add(btnSelectFolder);
            panelSidebar.Controls.Add(lblCurrentSigner);
            panelSidebar.Controls.Add(lblDocumentCaption);
            panelSidebar.Controls.Add(cmbTemplate);
            panelSidebar.Controls.Add(btnLoad);
            panelSidebar.Controls.Add(lblProgress);
            panelSidebar.Controls.Add(lblFiltre);
            panelSidebar.Controls.Add(pillFilter);
            panelSidebar.Controls.Add(pillParty);
            panelSidebar.Controls.Add(chkManualSigner);
            panelSidebar.Controls.Add(cardsPanel);
            panelSidebar.Controls.Add(btnCancelLoad);
            panelSidebar.Controls.Add(btnSaveProgress);
            panelSidebar.Controls.Add(btnFinish);

            // ── Bottom bar ──
            panelBottom = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 48,
                BackColor = System.Drawing.Color.FromArgb(238, 240, 245)
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
                BackColor = System.Drawing.Color.Transparent,
                TextAlign = ContentAlignment.MiddleLeft
            };

            btnMirror = new Button
            {
                Text = "⊞  Oglindire pe Ecran",
                Size = new Size(180, 28),
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                FlatStyle = FlatStyle.Flat,
                BackColor = AppTheme.MirrorOn,
                ForeColor = System.Drawing.Color.White,
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
                Image = System.Drawing.Image.FromStream(new System.IO.MemoryStream(Properties.Resources.zoom_in)),
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
                Image = System.Drawing.Image.FromStream(new System.IO.MemoryStream(Properties.Resources.zoom_out)),
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
                using (var pen = new System.Drawing.Pen(AppTheme.HeaderBorder, 1f))
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
                ShowToolbar = false,
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
                BackColor = System.Drawing.Color.Transparent
            };

        private static void WireButtonBorder(Button btn)
        {
            void UpdateStyle()
            {
                btn.FlatAppearance.BorderSize = btn.Enabled ? 2 : 0;
                btn.ForeColor = btn.Enabled ? System.Drawing.Color.White : System.Drawing.Color.FromArgb(110, 110, 110);
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
                        System.Drawing.Color.FromArgb(110, 110, 110),
                        TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                }
            };
        }

        #endregion
    }
}

        #endregion