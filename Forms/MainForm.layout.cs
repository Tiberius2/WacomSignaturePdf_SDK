using PdfiumViewer;
using System.Drawing;
using System.Windows.Forms;
using WacomSignaturePdf.Controls;
using WacomSignaturePdf.Theme;

namespace WacomSignaturePdf.Forms
{
    public partial class MainForm
    {
        // ── Control declarations ──────────────────────────────────────────────────
        private Panel panelSidebar;
        private Panel panelContent;
        private Splitter splitter;
        private Label lblAppTitle;
        private Label lblSectionCandidate;
        private Label lblCandidateIdCaption;
        private TextBox txtCandidateId;
        private Label lblCandidateName;
        private Label lblSectionDocument;
        private Label lblDocumentCaption;
        private DocumentTypeDropdown cmbTemplate;
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
        private RichTextBox txtLog;
        private DeviceStatusLabel deviceStatusLabel;
        private Panel previewHeader;
        private Label lblPreviewCaption;
        private Button btnMirror;
        private Button btnZoomIn;
        private Button btnZoomOut;
        private PdfViewer pdfViewer;
        private ToolTip toolTip;

        // ── Layout Y positions ────────────────────────────────────────────────────
        private const int YTitle = 0;
        private const int YCandidateSec = 64;
        private const int YIdRow = 84;
        private const int YCandName = 116;
        private const int YDocSec = 162;
        private const int YDocRow = 184;
        private const int YSigSec = 232;
        private const int YSigProgress = 250;
        private const int YPartyToggle = 272;
        private const int YCards = 308;
        private const int CardsHeight = 270;
        private const int YCancelLoad = YCards + CardsHeight + 4;
        private const int YSaveProgress = YCancelLoad + 36;
        private const int YFinish = YSaveProgress + 36;
        private const int YLogSec = YFinish + 50;
        private const int YLog = YLogSec + 18;

        private void BuildLayout()
        {
            BuildSidebarControls();
            BuildSidebar();
            BuildPreviewHeader();
            BuildContentPanel();
            BuildForm();
        }

        private void BuildSidebarControls()
        {
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

            lblSectionCandidate = MakeSectionLabel("CANDIDAT", new Point(16, YCandidateSec));

            lblCandidateIdCaption = new Label
            {
                Text = "ID",
                Location = new Point(16, YIdRow + 3),
                Size = new Size(24, 20),
                Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
                ForeColor = AppTheme.SidebarSub,
                BackColor = Color.Transparent
            };

            txtCandidateId = new TextBox
            {
                Location = new Point(44, YIdRow),
                Size = new Size(310, 26),
                Font = new Font("Segoe UI", 10f),
                BackColor = AppTheme.InputBg,
                ForeColor = AppTheme.InputText,
                BorderStyle = BorderStyle.FixedSingle
            };
            txtCandidateId.TextChanged += txtCandidateId_TextChanged;
            txtCandidateId.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) TryLoadDocument(); };

            lblCandidateName = new Label
            {
                Text = "Introduceti ID-ul candidatului",
                Location = new Point(16, YCandName),
                Size = new Size(338, 36),
                Font = new Font("Segoe UI", 8.5f, FontStyle.Italic),
                ForeColor = AppTheme.SidebarSub,
                BackColor = Color.Transparent,
                AutoEllipsis = true
            };

            lblSectionDocument = MakeSectionLabel("DOCUMENT", new Point(16, YDocSec));

            lblDocumentCaption = new Label
            {
                Text = "Tip",
                Location = new Point(16, YDocRow + 8),
                Size = new Size(26, 20),
                Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
                ForeColor = AppTheme.SidebarSub,
                BackColor = Color.Transparent
            };

            cmbTemplate = new DocumentTypeDropdown
            {
                Location = new Point(48, YDocRow),
                Size = new Size(234, 36),
                Enabled = false
            };

            btnLoad = new Button
            {
                Text = "Incarca",
                Location = new Point(288, YDocRow),
                Size = new Size(68, 42),
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
                Text = "Official",
                Location = new Point(144, YPartyToggle + 4),
                Size = new Size(56, 20),
                Font = new Font("Segoe UI", 9f),
                ForeColor = AppTheme.SidebarSub,
                BackColor = Color.Transparent
            };

            chkManualSigner = new CheckBox
            {
                Text = "Nume manual",
                Location = new Point(208, YPartyToggle + 5),
                Size = new Size(148, 18),
                Font = new Font("Segoe UI", 8.5f),
                ForeColor = AppTheme.SidebarSub,
                BackColor = Color.Transparent,
                Cursor = Cursors.Hand
            };

            cardsPanel = new Panel
            {
                Location = new Point(8, YCards),
                Size = new Size(346, CardsHeight),
                BackColor = AppTheme.SidebarCardsBg,
                AutoScroll = true,
                BorderStyle = BorderStyle.None
            };

            btnCancelLoad = new Button
            {
                Text = "✕  Inchidere document",
                Location = new Point(8, YCancelLoad),
                Size = new Size(346, 32),
                Font = new Font("Segoe UI", 9.0f),
                FlatStyle = FlatStyle.Flat,
                BackColor = AppTheme.CancelBg,
                ForeColor = AppTheme.CancelFg,
                Enabled = false,
                Visible = false,
                Cursor = Cursors.Hand
            };
            btnCancelLoad.FlatAppearance.BorderSize = 0;
            btnCancelLoad.FlatAppearance.BorderColor = AppTheme.CancelBorder;
            btnCancelLoad.Click += (s, e) => CancelCurrentDocument();

            btnSaveProgress = new Button
            {
                Text = "💾  Salvare Progres",
                Location = new Point(8, YSaveProgress),
                Size = new Size(346, 32),
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                FlatStyle = FlatStyle.Flat,
                BackColor = AppTheme.AccentBlue,
                ForeColor = Color.White,
                Enabled = false,
                Visible = false,
                Cursor = Cursors.Hand
            };
            btnSaveProgress.FlatAppearance.BorderSize = 0;
            btnSaveProgress.FlatAppearance.BorderColor = AppTheme.AccentBorderBlue;
            btnSaveProgress.Click += btnSaveProgress_Click;

            btnFinish = new Button
            {
                Text = "Finalizati si Deschideti in Adobe",
                Location = new Point(8, YFinish),
                Size = new Size(346, 35),
                Font = new Font("Segoe UI", 10f, FontStyle.Bold),
                FlatStyle = FlatStyle.Flat,
                BackColor = AppTheme.AccentGreen,
                ForeColor = Color.White,
                Enabled = false,
                Cursor = Cursors.Hand
            };
            btnFinish.FlatAppearance.BorderSize = 0;
            btnFinish.FlatAppearance.BorderColor = AppTheme.AccentGreenBorder;
            btnFinish.Click += btnFinish_Click;

            lblLogCaption = MakeSectionLabel("LOG", new Point(16, YLogSec));

            txtLog = new RichTextBox
            {
                Location = new Point(8, YLog),
                Size = new Size(346, 160),
                ReadOnly = true,
                BackColor = AppTheme.LogBg,
                ForeColor = AppTheme.LogText,
                Font = new Font("Consolas", 7.5f),
                ScrollBars = RichTextBoxScrollBars.Vertical,
                BorderStyle = BorderStyle.None
            };

            deviceStatusLabel = new DeviceStatusLabel();

            WireButtonBorder(btnLoad);
            WireButtonBorder(btnSaveProgress);
            WireButtonBorder(btnFinish);
            WireButtonBorder(btnCancelLoad);

            toolTip = new ToolTip();
            toolTip.SetToolTip(btnCancelLoad, "Anuleaza documentul curent si permite reselectionarea");
            toolTip.SetToolTip(btnSaveProgress, "Salveaza progresul si trimite documentul la urmatoarea persoana");
            toolTip.SetToolTip(toggleParty, "Comuta intre semnaturile candidatului si ale oficialilor");
            toolTip.SetToolTip(chkManualSigner, "Cand bifat, va fi cerut numele semnatarului la fiecare semnatura");
        }

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
            panelSidebar.Controls.Add(lblCandidateIdCaption);
            panelSidebar.Controls.Add(txtCandidateId);
            panelSidebar.Controls.Add(lblCandidateName);
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
            panelSidebar.Controls.Add(deviceStatusLabel);

            splitter = new Splitter
            {
                Dock = DockStyle.Left,
                Width = 3,
                BackColor = AppTheme.SplitterColor
            };
        }

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
                Text = "🔍+",
                Size = new Size(52, 28),
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                FlatStyle = FlatStyle.Flat,
                BackColor = AppTheme.HeaderBg,
                ForeColor = AppTheme.PreviewCaption,
                Cursor = Cursors.Hand,
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            btnZoomIn.FlatAppearance.BorderSize = 1;
            btnZoomIn.Click += (s, e) => pdfViewer.Renderer?.ZoomIn();

            btnZoomOut = new Button
            {
                Text = "🔍-",
                Size = new Size(52, 28),
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                FlatStyle = FlatStyle.Flat,
                BackColor = AppTheme.HeaderBg,
                ForeColor = AppTheme.PreviewCaption,
                Cursor = Cursors.Hand,
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            btnZoomOut.FlatAppearance.BorderSize = 1;
            btnZoomOut.Click += (s, e) => pdfViewer.Renderer?.ZoomOut();

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
            btn.EnabledChanged += (s, e) =>
                btn.FlatAppearance.BorderSize = btn.Enabled ? 2 : 0;
            btn.FlatAppearance.BorderSize = btn.Enabled ? 2 : 0;
        }
    }
}