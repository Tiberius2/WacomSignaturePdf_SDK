using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using WacomSignaturePdf.Controls;
using WacomSignaturePdf.Theme;

namespace WacomSignaturePdf.Forms
{
    public partial class ShellForm
    {
        // ── Controls ──────────────────────────────────────────────────────────────
        private Panel panelTitleBar;
        private Label lblTitle;
        private Panel pillContainer;
        private Button btnPillTemplate;
        private Button btnPillFreeForm;
        private Panel panelAccentBar;
        private Label lblAccentText;
        private Panel lineAccent;
        private Panel panelSidebar;       // swap zone for UserControl panels
        private Panel _panelSidebarOuter; // full left column incl title+accent
        private Splitter splitter;
        private Panel panelPreviewHeader;
        private Label lblPreviewCaption;
        private Button btnZoomIn;
        private Button btnZoomOut;
        private Panel panelContent;

        private const int SidebarW = 460;
        private const int TitleH = 56;
        private const int AccentH = 26;
        private const int HeaderH = 44;

        private void BuildLayout(AppMode initialMode)
        {
            BuildTitleBar();
            BuildAccentBar();
            BuildSidebarShell();
            BuildPreviewHeader();
            BuildContentPanel();
            BuildForm();
        }

        // ─────────────────────────────────────────────────────────────────────────
        //  TITLE BAR
        // ─────────────────────────────────────────────────────────────────────────
        private void BuildTitleBar()
        {
            panelTitleBar = new Panel
            {
                Location = new Point(0, 0),
                Size = new Size(SidebarW, TitleH),
                BackColor = AppTheme.Template.TitleBg,
            };

            lblTitle = new Label
            {
                Text = "PDF SIGNING",
                AutoSize = false,
                Location = new Point(14, 0),
                Size = new Size(150, TitleH),
                Font = new Font("Segoe UI", 11f, FontStyle.Bold),
                ForeColor = Color.White,
                BackColor = Color.Transparent,
                TextAlign = ContentAlignment.MiddleLeft,
            };

            // ── Pill: 220×34, dreapta in title bar ──
            const int PillW = 130; // latime per buton (destul pentru "Semnatura Libera")
            const int PillH = 28;
            const int PillRadius = 8; // border radius moderat

            pillContainer = new Panel
            {
                Size = new Size(PillW * 2 + 4, PillH + 6),
                BackColor = Color.Transparent,
            };

            btnPillTemplate = new Button
            {
                Text = "",  // text desenat manual in Paint
                Size = new Size(PillW, PillH),
                Location = new Point(2, 3),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
                Cursor = Cursors.Hand,
                BackColor = Color.Transparent,
                ForeColor = Color.White,
                UseVisualStyleBackColor = false,
                TabStop = false,
            };
            btnPillTemplate.FlatAppearance.BorderSize = 0;
            btnPillTemplate.FlatAppearance.MouseOverBackColor = Color.Transparent;
            btnPillTemplate.FlatAppearance.MouseDownBackColor = Color.Transparent;
            btnPillTemplate.Click += BtnPillTemplate_Click;

            btnPillFreeForm = new Button
            {
                Text = "",  // text desenat manual in Paint
                Size = new Size(PillW, PillH),
                Location = new Point(PillW + 2, 3),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 8.5f, FontStyle.Regular),
                Cursor = Cursors.Hand,
                BackColor = Color.Transparent,
                ForeColor = Color.FromArgb(170, 255, 255, 255),
                UseVisualStyleBackColor = false,
                TabStop = false,
            };
            btnPillFreeForm.FlatAppearance.BorderSize = 0;
            btnPillFreeForm.FlatAppearance.MouseOverBackColor = Color.Transparent;
            btnPillFreeForm.FlatAppearance.MouseDownBackColor = Color.Transparent;
            btnPillFreeForm.Click += BtnPillFreeForm_Click;

            // Custom paint: background track + buton activ rotunjit
            Action<bool> paintPill = (templateActive) =>
            {
                pillContainer.Invalidate();
            };

            pillContainer.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                // Track background
                var track = new Rectangle(0, 0, pillContainer.Width - 1, pillContainer.Height - 1);
                using (var br = new SolidBrush(Color.FromArgb(55, 255, 255, 255)))
                using (var path = MakeRoundRect(track, PillRadius + 2))
                    e.Graphics.FillPath(br, path);

                // Buton activ (alb rotunjit)
                bool isTpl = _currentMode == AppMode.Template;
                var activeRect = isTpl
                    ? new Rectangle(2, 2, PillW - 2, PillH - 2)
                    : new Rectangle(PillW + 2, 2, PillW - 2, PillH - 2);
                using (var br = new SolidBrush(Color.White))
                using (var path = MakeRoundRect(activeRect, PillRadius))
                    e.Graphics.FillPath(br, path);
            };

            // Hover custom paint pe butoane inactive
            btnPillTemplate.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                if (_currentMode != AppMode.Template)
                {
                    bool hov = btnPillTemplate.ClientRectangle.Contains(btnPillTemplate.PointToClient(Control.MousePosition));
                    if (hov)
                    {
                        var rc2 = new Rectangle(0, 0, btnPillTemplate.Width - 1, btnPillTemplate.Height - 1);
                        using (var br = new SolidBrush(Color.FromArgb(40, 255, 255, 255)))
                        using (var path = MakeRoundRect(rc2, PillRadius))
                            e.Graphics.FillPath(br, path);
                    }
                }
                var flags = TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine;
                TextRenderer.DrawText(e.Graphics, "Sablon", btnPillTemplate.Font, btnPillTemplate.ClientRectangle, btnPillTemplate.ForeColor, flags);
            };

            btnPillFreeForm.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                if (_currentMode != AppMode.FreeForm)
                {
                    bool hov = btnPillFreeForm.ClientRectangle.Contains(btnPillFreeForm.PointToClient(Control.MousePosition));
                    if (hov)
                    {
                        var rc2 = new Rectangle(0, 0, btnPillFreeForm.Width - 1, btnPillFreeForm.Height - 1);
                        using (var br = new SolidBrush(Color.FromArgb(40, 255, 255, 255)))
                        using (var path = MakeRoundRect(rc2, PillRadius))
                            e.Graphics.FillPath(br, path);
                    }
                }
                var flags = TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine;
                TextRenderer.DrawText(e.Graphics, "Semnatura Libera", btnPillFreeForm.Font, btnPillFreeForm.ClientRectangle, btnPillFreeForm.ForeColor, flags);
            };

            // Invalidate on hover pentru repaint
            btnPillTemplate.MouseEnter += (s, e) => pillContainer.Invalidate();
            btnPillTemplate.MouseLeave += (s, e) => pillContainer.Invalidate();
            btnPillFreeForm.MouseEnter += (s, e) => pillContainer.Invalidate();
            btnPillFreeForm.MouseLeave += (s, e) => pillContainer.Invalidate();

            pillContainer.Controls.Add(btnPillTemplate);
            pillContainer.Controls.Add(btnPillFreeForm);
            panelTitleBar.Controls.Add(lblTitle);
            panelTitleBar.Controls.Add(pillContainer);

            // Pozitionare initiala
            pillContainer.Location = new Point(SidebarW - pillContainer.Width - 8, (TitleH - pillContainer.Height) / 2);

            panelTitleBar.Resize += (s, e) =>
            {
                pillContainer.Location = new Point(
                    panelTitleBar.Width - pillContainer.Width - 8,
                    (TitleH - pillContainer.Height) / 2);
            };
        }

        // ─────────────────────────────────────────────────────────────────────────
        //  ACCENT BAR
        // ─────────────────────────────────────────────────────────────────────────
        private void BuildAccentBar()
        {
            panelAccentBar = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = AccentH,
                BackColor = AppTheme.Template.TitleBgDark,
            };

            lineAccent = new Panel
            {
                Dock = DockStyle.Top,
                Height = 2,
                BackColor = AppTheme.Template.AccentBar,
            };

            lblAccentText = new Label
            {
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 7.5f),
                ForeColor = AppTheme.Template.AccentBarColor,
                BackColor = Color.Transparent,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(14, 0, 0, 0),
                Text = "MOD TEMPLATE — sabloane predefinite cu roluri si semnatari",
            };

            panelAccentBar.Controls.Add(lblAccentText);
            panelAccentBar.Controls.Add(lineAccent);
        }

        // ─────────────────────────────────────────────────────────────────────────
        //  SIDEBAR SHELL
        // ─────────────────────────────────────────────────────────────────────────
        private void BuildSidebarShell()
        {
            _panelSidebarOuter = new Panel
            {
                Dock = DockStyle.Left,
                Width = SidebarW,
                BackColor = AppTheme.Template.TitleBg,
            };

            panelSidebar = new Panel
            {
                BackColor = AppTheme.Template.SidebarBg,
                Location = new Point(0, TitleH),
            };

            _panelSidebarOuter.Controls.Add(panelAccentBar); // Dock=Bottom - primul adaugat
            _panelSidebarOuter.Controls.Add(panelSidebar);
            _panelSidebarOuter.Controls.Add(panelTitleBar);

            _panelSidebarOuter.Resize += (s, e) =>
            {
                int hdrH = TitleH;
                panelSidebar.Size = new Size(
                    _panelSidebarOuter.ClientSize.Width,
                    Math.Max(0, _panelSidebarOuter.ClientSize.Height - hdrH - AccentH));
                panelTitleBar.Width = _panelSidebarOuter.ClientSize.Width;
            };

            splitter = new Splitter
            {
                Dock = DockStyle.Left,
                Width = 3,
                BackColor = Color.FromArgb(80, 100, 140),
                Enabled = false,   // dezactivat - sidebar nu e redimensionabil
                MinSize = 460,     // blocheaza resize-ul
                MinExtra = 400,
            };
        }

        // ─────────────────────────────────────────────────────────────────────────
        //  PREVIEW HEADER
        // ─────────────────────────────────────────────────────────────────────────
        private Button btnMirror;
        private Button btnCancelDraw;

        private void BuildPreviewHeader()
        {
            panelPreviewHeader = new Panel
            {
                Dock = DockStyle.Top,
                Height = HeaderH,
                BackColor = AppTheme.HeaderBg,
            };

            lblPreviewCaption = new Label
            {
                Text = "Previzualizare — trage un PDF sau apasa Deschide",
                Location = new Point(16, 0),
                Size = new Size(700, HeaderH),
                Font = new Font("Segoe UI", 10f, FontStyle.Bold),
                ForeColor = AppTheme.PreviewCaption,
                BackColor = Color.Transparent,
                TextAlign = ContentAlignment.MiddleLeft,
            };

            btnZoomIn = new Button
            {
                Size = new Size(52, 28),
                FlatStyle = FlatStyle.Flat,
                BackColor = AppTheme.HeaderBg,
                ForeColor = AppTheme.PreviewCaption,
                Cursor = Cursors.Hand,
                Image = System.Drawing.Image.FromStream(
                    new System.IO.MemoryStream(Properties.Resources.zoom_in)),
                ImageAlign = ContentAlignment.MiddleCenter,
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
            };
            btnZoomIn.FlatAppearance.BorderSize = 1;
            btnZoomIn.Click += (s, e) => SharedOverlay?.ZoomIn();

            btnZoomOut = new Button
            {
                Size = new Size(52, 28),
                FlatStyle = FlatStyle.Flat,
                BackColor = AppTheme.HeaderBg,
                ForeColor = AppTheme.PreviewCaption,
                Cursor = Cursors.Hand,
                Image = System.Drawing.Image.FromStream(
                    new System.IO.MemoryStream(Properties.Resources.zoom_out)),
                ImageAlign = ContentAlignment.MiddleCenter,
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
            };
            btnZoomOut.FlatAppearance.BorderSize = 1;
            btnZoomOut.Click += (s, e) => SharedOverlay?.ZoomOut();

            btnMirror = new Button
            {
                Text = "Oglindire",
                Size = new Size(110, 28),
                FlatStyle = FlatStyle.Flat,
                BackColor = AppTheme.MirrorOn,
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
                Cursor = Cursors.Hand,
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
            };
            btnMirror.FlatAppearance.BorderSize = 0;
            btnMirror.Click += BtnMirror_Click;

            btnCancelDraw = new Button
            {
                Text = "✕  Anulare desenare",
                Size = new Size(180, 26),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(160, 50, 50),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
                Cursor = Cursors.Hand,
                Visible = false,
            };
            btnCancelDraw.FlatAppearance.BorderSize = 0;
            btnCancelDraw.Click += (s, e) =>
            {
                if (_currentPanel is FreeFormSidebarPanel ff)
                    ff.CancelDrawing();
            };

            panelPreviewHeader.Controls.Add(lblPreviewCaption);
            panelPreviewHeader.Controls.Add(btnMirror);
            panelPreviewHeader.Controls.Add(btnZoomIn);
            panelPreviewHeader.Controls.Add(btnZoomOut);
            panelPreviewHeader.Controls.Add(btnCancelDraw); // last = on top

            panelPreviewHeader.Resize += (s, e) =>
            {
                btnZoomIn.Location = new Point(panelPreviewHeader.Width - 64, 8);
                btnZoomOut.Location = new Point(panelPreviewHeader.Width - 120, 8);
                btnMirror.Location = new Point(panelPreviewHeader.Width - 120 - btnMirror.Width - 8, 8);
                if (btnCancelDraw.Visible)
                    btnCancelDraw.Location = new Point(
                        (panelPreviewHeader.Width - btnCancelDraw.Width) / 2,
                        (HeaderH - btnCancelDraw.Height) / 2);
            };

            panelPreviewHeader.Paint += (s, e) =>
            {
                using (var p = new Pen(AppTheme.HeaderBorder, 1f))
                    e.Graphics.DrawLine(p, 0, HeaderH - 1, panelPreviewHeader.Width, HeaderH - 1);
            };
        }

        // ─────────────────────────────────────────────────────────────────────────
        //  CONTENT PANEL
        // ─────────────────────────────────────────────────────────────────────────
        private void BuildContentPanel()
        {
            SharedOverlay = new PdfDrawingOverlay { Dock = DockStyle.Fill };
            SharedOverlay.AllowDrop = true;
            SharedOverlay.DragEnter += (s, e) =>
            {
                if (e.Data.GetDataPresent(DataFormats.FileDrop))
                    e.Effect = DragDropEffects.Copy;
            };
            SharedOverlay.DragDrop += (s, e) =>
            {
                var files = e.Data.GetData(DataFormats.FileDrop) as string[];
                if (files?.Length > 0 &&
                    System.IO.Path.GetExtension(files[0]).ToLowerInvariant() == ".pdf")
                    _currentPanel?.OnFileDrop(files[0]);
            };

            panelContent = new Panel { Dock = DockStyle.Fill, BackColor = AppTheme.ContentBg };
            panelContent.Controls.Add(SharedOverlay);
            panelContent.Controls.Add(panelPreviewHeader);
        }

        // ─────────────────────────────────────────────────────────────────────────
        //  FORM
        // ─────────────────────────────────────────────────────────────────────────
        private void BuildForm()
        {
            Text = "Wacom Signature — PDF Signing";
            ClientSize = new Size(1400, 860);
            MinimumSize = new Size(1100, 650);
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = AppTheme.ContentBg;
            Font = new Font("Segoe UI", 9f);
            FormBorderStyle = FormBorderStyle.Sizable;
            WindowState = FormWindowState.Maximized;

            Controls.Add(panelContent);
            Controls.Add(splitter);
            Controls.Add(_panelSidebarOuter);

            AllowDrop = true;
            DragEnter += (s, e) =>
            {
                if (e.Data.GetDataPresent(DataFormats.FileDrop))
                    e.Effect = DragDropEffects.Copy;
            };
            DragDrop += (s, e) =>
            {
                var files = e.Data.GetData(DataFormats.FileDrop) as string[];
                if (files?.Length > 0 &&
                    System.IO.Path.GetExtension(files[0]).ToLowerInvariant() == ".pdf")
                    _currentPanel?.OnFileDrop(files[0]);
            };
        }

        // ─────────────────────────────────────────────────────────────────────────
        //  THEME APPLICATION (called from ShellForm.cs ApplyTheme)
        // ─────────────────────────────────────────────────────────────────────────
        internal void ApplyThemeColors(AppMode mode)
        {
            var t = mode == AppMode.Template ? AppTheme.Template : AppTheme.FreeForm;
            bool isTpl = mode == AppMode.Template;

            panelTitleBar.BackColor = t.TitleBg;
            panelAccentBar.BackColor = t.TitleBgDark;
            lineAccent.BackColor = t.AccentBar;
            lblAccentText.ForeColor = t.AccentBarColor;
            _panelSidebarOuter.BackColor = t.TitleBg;
            panelSidebar.BackColor = t.SidebarBg;

            lblAccentText.Text = isTpl
                ? "MOD TEMPLATE — sabloane predefinite cu roluri si semnatari"
                : "MOD LIBER — semnare libera pe orice document PDF";

            // Pill active - culorile sunt gestionate prin custom paint in pillContainer.Paint
            btnPillTemplate.ForeColor = isTpl ? t.PillActiveFg : Color.FromArgb(180, 255, 255, 255);
            btnPillTemplate.Font = new Font("Segoe UI", 8.5f,
                isTpl ? FontStyle.Bold : FontStyle.Regular);

            btnPillFreeForm.ForeColor = isTpl ? Color.FromArgb(180, 255, 255, 255) : AppTheme.FreeForm.PillActiveFg;
            btnPillFreeForm.Font = new Font("Segoe UI", 8.5f,
                isTpl ? FontStyle.Regular : FontStyle.Bold);

            // Repaint pill container pentru a reflecta noul mod activ
            pillContainer.Invalidate();
            btnPillTemplate.Invalidate();
            btnPillFreeForm.Invalidate();
        }

        // ─────────────────────────────────────────────────────────────────────────
        //  WIN32 + HELPERS
        // ─────────────────────────────────────────────────────────────────────────
        [System.Runtime.InteropServices.DllImport("Gdi32.dll")]
        private static extern IntPtr CreateRoundRectRgn(
            int x1, int y1, int x2, int y2, int cx, int cy);

        private static GraphicsPath MakeRoundRect(Rectangle r, int radius)
        {
            var path = new GraphicsPath();
            path.AddArc(r.X, r.Y, radius * 2, radius * 2, 180, 90);
            path.AddArc(r.Right - radius * 2, r.Y, radius * 2, radius * 2, 270, 90);
            path.AddArc(r.Right - radius * 2, r.Bottom - radius * 2, radius * 2, radius * 2, 0, 90);
            path.AddArc(r.X, r.Bottom - radius * 2, radius * 2, radius * 2, 90, 90);
            path.CloseFigure();
            return path;
        }
    }
}