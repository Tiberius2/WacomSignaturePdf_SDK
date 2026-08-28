using PdfiumViewer;
using System;
using System.Drawing;
using System.Windows.Forms;
using WacomSignaturePdf.Controls;
using WacomSignaturePdf.Theme;

namespace WacomSignaturePdf.Forms
{
    public partial class EvaluareProbaForm
    {
        #region Layout Constants
        private const int SidebarW = 370;
        private const int ToolbarH = 36;
        private const int FieldX = 14;
        private const int ContentW = 342;  // SidebarW - FieldX*2

        private static readonly Color BgSidebar = Color.FromArgb(196, 208, 226);
        private static readonly Color BgTitle = Color.FromArgb(42, 90, 165);
        private static readonly Color BgContent = Color.FromArgb(232, 236, 242);
        private static readonly Color AccentBlue = Color.FromArgb(50, 115, 195);
        private static readonly Color AccentBorder = Color.FromArgb(150, 180, 210);
        private static readonly Color SectionFg = Color.FromArgb(55, 95, 150);
        private static readonly Color SubFg = Color.FromArgb(85, 105, 135);
        private static readonly Color CardsBg = Color.FromArgb(250, 251, 253);
        private static readonly Color StatusBg = Color.FromArgb(200, 210, 225);
        #endregion

        #region Controls
        private Button btnSelectFolder;
        private Label lblSelectedFolder;
        private ComboBox cmbDocument;
        private ToggleSwitch toggleSigned;
        private Label lblToggleLeft;
        private Label lblToggleRight;
        private Panel cardsPanel;
        private Button btnMirror;
        private Button btnZoomIn;
        private Button btnZoomOut;
        private PdfDrawingOverlay pdfOverlay;
        private Panel panelSidebar;
        private Panel panelContent;
        private Panel panelToolbar;
        private Panel panelStatus;
        internal DeviceStatusLabel deviceStatusLabel;
        internal OneDriveStatusLabel oneDriveStatusLabel;
        private Label lblCardCount;
        private Label lblFolderSection;
        private Label lblDocSection;
        private Label lblCardsSection;
        #endregion

        private void BuildLayout()
        {
            this.Text = "Evaluare Proba Practica";
            this.ClientSize = new Size(1100, 700);
            this.MinimumSize = new Size(900, 580);
            this.BackColor = BgContent;
            this.DoubleBuffered = true;
            this.Font = new Font("Segoe UI", 9f);

            BuildSidebar();
            BuildContentArea();

            this.Controls.Add(panelContent);
            this.Controls.Add(panelSidebar);
        }

        // ── SIDEBAR ───────────────────────────────────────────────────────────────
        private void BuildSidebar()
        {
            panelSidebar = new Panel
            {
                Dock = DockStyle.Left,
                Width = SidebarW,
                BackColor = BgSidebar,
            };
            panelSidebar.Controls.Add(new Panel
            {
                Dock = DockStyle.Right,
                Width = 2,
                BackColor = Color.FromArgb(190, 200, 215),
            });

            // Title
            var panelTitle = new Panel
            {
                Dock = DockStyle.Top,
                Height = 50,
                BackColor = BgTitle,
            };
            panelTitle.Controls.Add(new Label
            {
                Text = "EVALUARE PROBĂ PRACTICĂ",
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 10f, FontStyle.Bold),
                ForeColor = Color.White,
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = Color.Transparent,
            });
            panelTitle.Controls.Add(new Panel { Dock = DockStyle.Bottom, Height = 2, BackColor = AccentBlue });

            // ── Folder ──
            lblFolderSection = MakeSectionLabel("DOSAR CANDIDAT", new Point(FieldX, 62));

            btnSelectFolder = new Button
            {
                Text = "Selectează dosar candidat...",
                Location = new Point(FieldX, 80),
                Size = new Size(ContentW, 32),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.White,
                ForeColor = Color.FromArgb(60, 90, 130),
                Font = new Font("Segoe UI", 9f),
                TextAlign = ContentAlignment.MiddleLeft,
                Cursor = Cursors.Hand,
                Padding = new Padding(8, 0, 0, 0),
            };
            btnSelectFolder.FlatAppearance.BorderSize = 2;
            btnSelectFolder.FlatAppearance.BorderColor = Color.FromArgb(180, 200, 225);
            btnSelectFolder.Click += (s, e) => OpenFolderPicker();

            // ── Document ──
            lblDocSection = MakeSectionLabel("DOCUMENT", new Point(FieldX, 144));

            cmbDocument = new ComboBox
            {
                Location = new Point(FieldX, 162),
                Size = new Size(ContentW, 26),
                DropDownStyle = ComboBoxStyle.DropDownList,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.White,
                ForeColor = Color.FromArgb(50, 60, 80),
            };
            cmbDocument.SelectedIndexChanged += (s, e) => OnDocumentSelected();

            lblSelectedFolder = new Label
            {
                Text = "",
                Location = new Point(FieldX, 116),
                Size = new Size(ContentW, 16),
                Font = new Font("Segoe UI", 8f, FontStyle.Italic),
                ForeColor = Color.FromArgb(80, 130, 185),
                BackColor = Color.Transparent,
                AutoEllipsis = true,
            };

            // ── Toggle filtru ──
            var panelToggle = new Panel
            {
                Location = new Point(FieldX, 200),
                Size = new Size(ContentW, 28),
                BackColor = Color.Transparent,
            };

            lblToggleLeft = new Label
            {
                Text = "Nesemnate / Partial Semnate",
                Location = new Point(0, 0),
                Size = new Size(130, 28),
                Font = new Font("Segoe UI", 8f, FontStyle.Bold),
                ForeColor = AccentBlue,
                TextAlign = ContentAlignment.MiddleLeft,
            };

            toggleSigned = new ToggleSwitch
            {
                Location = new Point(134, 4),
                Size = new Size(46, 20),
                IsOn = false,
                BackColor = BgSidebar,
            };
            toggleSigned.Toggled += (s, e) => OnToggleChanged();

            lblToggleRight = new Label
            {
                Text = "Semnate + Sigilate",
                Location = new Point(184, 0),
                Size = new Size(130, 28),
                Font = new Font("Segoe UI", 8f),
                ForeColor = SubFg,
                TextAlign = ContentAlignment.MiddleLeft,
            };

            panelToggle.Controls.Add(lblToggleLeft);
            panelToggle.Controls.Add(toggleSigned);
            panelToggle.Controls.Add(lblToggleRight);

            // ── Cards ──
            lblCardsSection = MakeSectionLabel("SEMNATURI", new Point(FieldX, 240));

            lblCardCount = new Label
            {
                Location = new Point(FieldX, 258),
                Size = new Size(ContentW, 16),
                Font = new Font("Segoe UI", 8f),
                ForeColor = SubFg,
                BackColor = Color.Transparent,
                Text = "",
            };

            cardsPanel = new Panel
            {
                Location = new Point(FieldX, 278),
                Size = new Size(ContentW, 360),
                BackColor = CardsBg,
                AutoScroll = true,
                BorderStyle = BorderStyle.FixedSingle,
            };

            // ── Status bar ──
            panelStatus = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 28,
                BackColor = AppTheme.SidebarTitleBg,
            };

            deviceStatusLabel = new DeviceStatusLabel
            {
                Width = 160,
                Height = 28,
            };
            oneDriveStatusLabel = new OneDriveStatusLabel
            {
                Location = new Point(180, 0),
                Width = 160,
                Height = 28,
            };
            panelStatus.Controls.Add(oneDriveStatusLabel);
            panelStatus.Controls.Add(deviceStatusLabel);

            panelSidebar.Controls.Add(panelTitle);
            panelSidebar.Controls.Add(lblFolderSection);
            panelSidebar.Controls.Add(btnSelectFolder);
            panelSidebar.Controls.Add(lblSelectedFolder);
            panelSidebar.Controls.Add(lblDocSection);
            panelSidebar.Controls.Add(cmbDocument);
            panelSidebar.Controls.Add(panelToggle);
            panelSidebar.Controls.Add(lblCardsSection);
            panelSidebar.Controls.Add(lblCardCount);
            panelSidebar.Controls.Add(cardsPanel);
            panelSidebar.Controls.Add(panelStatus);

            panelSidebar.Resize += (s, e) => RecalcSidebarLayout();
        }

        private void RecalcSidebarLayout()
        {
            int statusH = panelStatus?.Height ?? 28;
            int bottom = panelSidebar.ClientSize.Height - statusH - 4;
            cardsPanel.Height = bottom - cardsPanel.Top;
            cardsPanel.Width = panelSidebar.ClientSize.Width - FieldX * 2;
        }

        // ── CONTENT ───────────────────────────────────────────────────────────────
        private void BuildContentArea()
        {
            panelContent = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = BgContent,
            };

            panelToolbar = new Panel
            {
                Dock = DockStyle.Top,
                Height = ToolbarH,
                BackColor = Color.White,
                Padding = new Padding(6, 4, 6, 4),
            };
            panelToolbar.Controls.Add(new Panel { Dock = DockStyle.Bottom, Height = 1, BackColor = Color.FromArgb(210, 218, 230) });

            const int BtnH = 28;
            const int MirrorW = 120;
            const int ZoomW = 36;
            int btnTop = (ToolbarH - BtnH) / 2;

            btnMirror = new Button
            {
                Text = "Oglindire",
                Size = new Size(MirrorW, BtnH),
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(40, 70, 130),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
                Cursor = Cursors.Hand,
            };
            btnMirror.FlatAppearance.BorderSize = 1;
            btnMirror.FlatAppearance.BorderColor = Color.FromArgb(90, 120, 180);
            btnMirror.Click += btnMirror_Click;

            btnZoomIn = MakeToolbarButton(Properties.Resources.zoom_in, "Zoom in");
            btnZoomIn.Dock = DockStyle.None;
            btnZoomIn.Size = new Size(ZoomW, BtnH);
            btnZoomIn.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btnZoomIn.FlatStyle = FlatStyle.Popup;
            btnZoomIn.Click += (s, e) => pdfOverlay?.ZoomIn();
            btnZoomIn.Enabled = false;

            btnZoomOut = MakeToolbarButton(Properties.Resources.zoom_out, "Zoom out");
            btnZoomOut.Dock = DockStyle.None;
            btnZoomOut.Size = new Size(ZoomW, BtnH);
            btnZoomOut.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btnZoomOut.FlatStyle = FlatStyle.Popup;
            btnZoomOut.Enabled = false;
            btnZoomOut.Click += (s, e) => pdfOverlay?.ZoomOut();

            void PositionToolbarButtons()
            {
                int rightEdge = panelToolbar.ClientSize.Width - 6;
                btnMirror.Location = new Point(rightEdge - MirrorW, btnTop);
                btnZoomOut.Location = new Point(btnMirror.Left - ZoomW - 4, btnTop);
                btnZoomIn.Location = new Point(btnZoomOut.Left - ZoomW - 2, btnTop);
            }
            panelToolbar.Resize += (s, e) => PositionToolbarButtons();

            panelToolbar.Controls.Add(btnMirror);
            panelToolbar.Controls.Add(btnZoomOut);
            panelToolbar.Controls.Add(btnZoomIn);
            PositionToolbarButtons();

            pdfOverlay = new PdfDrawingOverlay
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(240, 242, 245),
            };

            panelContent.Controls.Add(pdfOverlay);
            panelContent.Controls.Add(panelToolbar);
        }

        #region Helpers
        private Label MakeSectionLabel(string text, Point location) => new Label
        {
            Text = text,
            Location = location,
            Size = new Size(ContentW, 14),
            Font = new Font("Segoe UI", 7.5f, FontStyle.Bold),
            ForeColor = SectionFg,
            BackColor = Color.Transparent,
        };

        private Button MakeToolbarButton(byte[] iconBytes, string tooltip)
        {
            var btn = new Button
            {
                Text = "",
                Dock = DockStyle.Left,
                Width = 36,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(244, 246, 250),
                ForeColor = Color.FromArgb(60, 80, 120),
                Cursor = Cursors.Hand,
                Image = System.Drawing.Image.FromStream(new System.IO.MemoryStream(iconBytes)),
                ImageAlign = ContentAlignment.MiddleCenter,
            };
            btn.FlatAppearance.BorderSize = 1;
            btn.FlatAppearance.BorderColor = Color.FromArgb(195, 205, 222);
            btn.FlatAppearance.MouseOverBackColor = Color.FromArgb(225, 232, 245);
            new ToolTip().SetToolTip(btn, tooltip);
            return btn;
        }
        #endregion
    }
}