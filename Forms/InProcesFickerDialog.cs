using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using WacomSignaturePdf.Services;
using WacomSignaturePdf.Theme;

namespace WacomSignaturePdf.Forms
{
    public class InProcesPickerDialog : Form
    {
        public string SelectedPath { get; private set; }

        private readonly string _folder;
        private List<PdfFileInfo> _allFiles;

        private TextBox txtSearch;
        private ListView listView;
        private Label lblCount;
        private Button btnOpen;
        private Button btnCancel;

        private static readonly Color BgColor = Color.FromArgb(18, 60, 60);
        private static readonly Color PanelColor = Color.FromArgb(13, 46, 46);
        private static readonly Color InputBg = Color.FromArgb(26, 76, 76);
        private static readonly Color InputFg = Color.FromArgb(180, 230, 220);
        private static readonly Color ListBg = Color.FromArgb(22, 70, 68);
        private static readonly Color ListAltBg = Color.FromArgb(24, 74, 72);
        private static readonly Color ListSelBg = Color.FromArgb(38, 168, 168);
        private static readonly Color ListSelFg = Color.White;
        private static readonly Color ListFg = Color.FromArgb(200, 235, 230);
        private static readonly Color ListSubFg = Color.FromArgb(110, 175, 170);
        private static readonly Color HeaderFg = Color.FromArgb(60, 195, 195);
        private static readonly Color BorderColor = Color.FromArgb(38, 110, 110);

        private static readonly Color ColorSigned = Color.FromArgb(100, 220, 140);
        private static readonly Color ColorPartial = Color.FromArgb(255, 185, 80);
        private static readonly Color ColorUnsigned = Color.FromArgb(160, 190, 185);

        // Starea de semnare per fisier, citita o singura data la load
        private class PdfFileInfo
        {
            public FileInfo File { get; set; }
            public string StatusText { get; set; }
            public string ProgressText { get; set; }
            public Color StatusColor { get; set; }
        }

        public InProcesPickerDialog(string folder)
        {
            _folder = folder;
            BuildUI();
            LoadFiles();
        }

        private void BuildUI()
        {
            Text = "Documente In Proces";
            ClientSize = new Size(740, 460);
            MinimumSize = new Size(560, 340);
            StartPosition = FormStartPosition.CenterParent;
            BackColor = BgColor;
            Font = new Font("Segoe UI", 9f);
            FormBorderStyle = FormBorderStyle.Sizable;
            ShowIcon = false;
            ShowInTaskbar = false;

            var lblTitle = new Label
            {
                Text = "Selecteaza document",
                Dock = DockStyle.Top,
                Height = 42,
                Font = new Font("Segoe UI", 11f, FontStyle.Bold),
                ForeColor = Color.White,
                BackColor = PanelColor,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(16, 0, 0, 0),
            };

            var panelSearch = new Panel
            {
                Dock = DockStyle.Top,
                Height = 44,
                BackColor = BgColor,
                Padding = new Padding(12, 8, 12, 4),
            };
            txtSearch = new TextBox
            {
                Dock = DockStyle.Fill,
                BackColor = InputBg,
                ForeColor = InputFg,
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Segoe UI", 9.5f),
            };
            SetPlaceholder(txtSearch, "Cauta dupa nume...");
            txtSearch.TextChanged += (s, e) => FilterList(txtSearch.Text);
            panelSearch.Controls.Add(txtSearch);

            listView = new ListView
            {
                Dock = DockStyle.Fill,
                View = View.Details,
                FullRowSelect = true,
                GridLines = false,
                MultiSelect = false,
                BackColor = ListBg,
                ForeColor = ListFg,
                BorderStyle = BorderStyle.None,
                Font = new Font("Segoe UI", 9f),
                HeaderStyle = ColumnHeaderStyle.Nonclickable,
                OwnerDraw = true,
            };

            const int ColStatus = 110;
            const int ColProgress = 80;
            const int ColDate = 130;

            listView.Columns.Add("Nume document", 300);
            listView.Columns.Add("Status", ColStatus);
            listView.Columns.Add("Semnaturi", ColProgress);
            listView.Columns.Add("Modificat", ColDate);

            listView.Resize += (s, e) =>
            {
                int fixed_ = ColStatus + ColProgress + ColDate;
                int nameW = listView.ClientSize.Width - fixed_;
                if (nameW > 80) listView.Columns[0].Width = nameW;
            };

            listView.DrawColumnHeader += (s, e) =>
            {
                e.Graphics.FillRectangle(new SolidBrush(PanelColor), e.Bounds);
                TextRenderer.DrawText(e.Graphics, e.Header.Text,
                    new Font("Segoe UI", 8.5f, FontStyle.Bold),
                    new Rectangle(e.Bounds.X + 8, e.Bounds.Y, e.Bounds.Width - 8, e.Bounds.Height),
                    HeaderFg, TextFormatFlags.VerticalCenter | TextFormatFlags.Left);
            };

            listView.DrawSubItem += (s, e) =>
            {
                bool selected = e.Item.Selected;
                var bg = selected ? ListSelBg : (e.ItemIndex % 2 == 0 ? ListBg : ListAltBg);
                e.Graphics.FillRectangle(new SolidBrush(bg), e.Bounds);

                Color fg;
                if (selected)
                    fg = ListSelFg;
                else if (e.ColumnIndex == 1)
                    fg = e.Item.Tag is PdfFileInfo info ? info.StatusColor : ListSubFg;
                else if (e.ColumnIndex == 2 || e.ColumnIndex == 3)
                    fg = ListSubFg;
                else
                    fg = ListFg;

                int pad = e.ColumnIndex == 0 ? 10 : 6;
                var flags = TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis;
                TextRenderer.DrawText(e.Graphics, e.SubItem.Text,
                    e.Item.Font ?? listView.Font,
                    new Rectangle(e.Bounds.X + pad, e.Bounds.Y, e.Bounds.Width - pad, e.Bounds.Height),
                    fg, flags);
            };

            listView.DrawItem += (s, e) => { };

            listView.SelectedIndexChanged += (s, e) =>
                btnOpen.Enabled = listView.SelectedItems.Count > 0;
            listView.MouseDoubleClick += (s, e) =>
            { if (listView.SelectedItems.Count > 0) AcceptSelection(); };
            listView.KeyDown += (s, e) =>
            { if (e.KeyCode == Keys.Enter && listView.SelectedItems.Count > 0) AcceptSelection(); };

            var panelBottom = new Panel { Dock = DockStyle.Bottom, Height = 52, BackColor = PanelColor };
            lblCount = new Label
            {
                Text = "",
                AutoSize = false,
                Location = new Point(14, 0),
                Size = new Size(220, 52),
                ForeColor = ListSubFg,
                BackColor = Color.Transparent,
                Font = new Font("Segoe UI", 8.5f, FontStyle.Italic),
                TextAlign = ContentAlignment.MiddleLeft,
            };
            btnCancel = new Button
            {
                Text = "Anuleaza",
                Size = new Size(100, 32),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(40, 90, 90),
                ForeColor = Color.White,
                Cursor = Cursors.Hand,
                DialogResult = DialogResult.Cancel,
                Anchor = AnchorStyles.Right | AnchorStyles.Top,
            };
            btnCancel.FlatAppearance.BorderColor = BorderColor;
            btnCancel.FlatAppearance.BorderSize = 1;

            btnOpen = new Button
            {
                Text = "Deschide",
                Size = new Size(110, 32),
                FlatStyle = FlatStyle.Flat,
                BackColor = AppTheme.FreeForm.AccentBar,
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                Cursor = Cursors.Hand,
                Enabled = false,
                Anchor = AnchorStyles.Right | AnchorStyles.Top,
            };
            btnOpen.FlatAppearance.BorderSize = 0;
            btnOpen.Click += (s, e) => AcceptSelection();

            void PositionButtons()
            {
                int y = (panelBottom.Height - btnOpen.Height) / 2;
                btnCancel.Location = new Point(panelBottom.Width - btnCancel.Width - 12, y);
                btnOpen.Location = new Point(btnCancel.Left - btnOpen.Width - 8, y);
            }
            panelBottom.Resize += (s, e) => PositionButtons();
            panelBottom.Controls.Add(lblCount);
            panelBottom.Controls.Add(btnOpen);
            panelBottom.Controls.Add(btnCancel);

            Controls.Add(listView);
            Controls.Add(panelSearch);
            Controls.Add(lblTitle);
            Controls.Add(new Panel { Dock = DockStyle.Bottom, Height = 1, BackColor = BorderColor });
            Controls.Add(panelBottom);

            AcceptButton = btnOpen;
            CancelButton = btnCancel;
            Shown += (s, e) => txtSearch.Focus();
        }

        private void LoadFiles()
        {
            _allFiles = new List<PdfFileInfo>();
            try
            {
                var files = Directory.GetFiles(_folder, "*.pdf", SearchOption.TopDirectoryOnly)
                    .Select(f => new FileInfo(f))
                    .OrderByDescending(f => f.LastWriteTime);

                foreach (var fi in files)
                {
                    var pfi = new PdfFileInfo { File = fi };
                    ResolveSigningInfo(fi.FullName, pfi);
                    _allFiles.Add(pfi);
                }
            }
            catch { }

            FilterList("");
        }

        private static void ResolveSigningInfo(string pdfPath, PdfFileInfo pfi)
        {
            try
            {
                var state = SignatureService.ReadSigningState(pdfPath);
                if (state?.Slots == null || state.Slots.Count == 0)
                {
                    pfi.StatusText = "Nesemnat";
                    pfi.ProgressText = "-";
                    pfi.StatusColor = ColorUnsigned;
                    return;
                }

                int total = state.Slots.Count;
                int signed = state.Slots.Count(s => s.Signed);

                pfi.ProgressText = $"{signed} din {total}";

                if (signed == 0)
                {
                    pfi.StatusText = "Nesemnat";
                    pfi.StatusColor = ColorUnsigned;
                }
                else if (signed < total)
                {
                    pfi.StatusText = "Partial semnat";
                    pfi.StatusColor = ColorPartial;
                }
                else
                {
                    pfi.StatusText = "Semnat complet";
                    pfi.StatusColor = ColorSigned;
                }
            }
            catch
            {
                pfi.StatusText = "-";
                pfi.ProgressText = "-";
                pfi.StatusColor = ColorUnsigned;
            }
        }

        private void FilterList(string query)
        {
            listView.BeginUpdate();
            listView.Items.Clear();

            var filtered = string.IsNullOrWhiteSpace(query)
                ? _allFiles
                : _allFiles.Where(f =>
                    f.File.Name.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0).ToList();

            foreach (var pfi in filtered)
            {
                var item = new ListViewItem(pfi.File.Name);
                item.SubItems.Add(pfi.StatusText);
                item.SubItems.Add(pfi.ProgressText);
                item.SubItems.Add(pfi.File.LastWriteTime.ToString("dd.MM.yyyy  HH:mm"));
                item.Tag = pfi;
                listView.Items.Add(item);
            }

            listView.EndUpdate();

            int total = _allFiles.Count;
            int shown = filtered.Count;
            lblCount.Text = shown == total
                ? $"{total} document{(total != 1 ? "e" : "")}"
                : $"{shown} din {total} documente";

            btnOpen.Enabled = false;
        }

        private void AcceptSelection()
        {
            if (listView.SelectedItems.Count == 0) return;
            SelectedPath = (listView.SelectedItems[0].Tag as PdfFileInfo)?.File.FullName;
            DialogResult = DialogResult.OK;
            Close();
        }

        [System.Runtime.InteropServices.DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wp, string lp);
        private const int EM_SETCUEBANNER = 0x1501;

        private static void SetPlaceholder(TextBox tb, string text)
        {
            tb.HandleCreated += (s, e) =>
                SendMessage(tb.Handle, EM_SETCUEBANNER, (IntPtr)1, text);
        }
    }
}