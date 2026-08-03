using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace WacomSignaturePdf.Forms
{
    public class CandidatPickerDialog : Form
    {
        public string SelectedFolderPath { get; private set; }
        public string SelectedFolderName { get; private set; }

        private readonly List<FolderEntry> _allEntries;
        private List<FolderEntry> _filteredByDigital;
        private readonly HashSet<string> _digitalIds;

        // Persista starea bifei pe durata sesiunii aplicatiei
        private static bool _lastDigitalOnlyState = true;

        private TextBox txtSearch;
        private ListView listView;
        private Label lblCount;
        private Button btnSelect;
        private Button btnCancel;
        private CheckBox chkDigitalOnly;

        private readonly Color BgColor;
        private readonly Color HeaderBg;
        private readonly Color InputBg;
        private readonly Color InputFg;
        private readonly Color ListBg;
        private readonly Color ListAltBg;
        private readonly Color ListSelBg;
        private readonly Color ListFg;
        private readonly Color ListSubFg;
        private readonly Color HeaderFg;
        private readonly Color BorderColor;
        private readonly Color AccentBlue;
        private readonly Color PanelBg;
        private readonly Color CancelBtnBg;

        private class FolderEntry
        {
            public string Name { get; set; }
            public string Id { get; set; }
            public string FolderName { get; set; }
            public string FullPath { get; set; }
            public bool IsDigital { get; set; }
        }

        public CandidatPickerDialog(IEnumerable<string> folderNames, string basePath,
            HashSet<string> digitalIds = null, bool lightTheme = false)
        {
            _digitalIds = digitalIds ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (lightTheme)
            {
                BgColor = Color.FromArgb(240, 243, 248);
                HeaderBg = Color.FromArgb(42, 90, 165);
                InputBg = Color.White;
                InputFg = Color.FromArgb(40, 55, 80);
                ListBg = Color.White;
                ListAltBg = Color.FromArgb(238, 242, 248);
                ListSelBg = Color.FromArgb(63, 125, 210);
                ListFg = Color.FromArgb(40, 55, 80);
                ListSubFg = Color.FromArgb(90, 110, 140);
                HeaderFg = Color.FromArgb(50, 90, 150);
                BorderColor = Color.FromArgb(190, 205, 225);
                AccentBlue = Color.FromArgb(50, 115, 195);
                PanelBg = Color.FromArgb(225, 231, 240);
                CancelBtnBg = Color.FromArgb(120, 135, 160);
            }
            else
            {
                BgColor = Color.FromArgb(22, 34, 64);
                HeaderBg = Color.FromArgb(14, 24, 48);
                InputBg = Color.FromArgb(32, 50, 88);
                InputFg = Color.FromArgb(180, 205, 240);
                ListBg = Color.FromArgb(28, 44, 78);
                ListAltBg = Color.FromArgb(32, 50, 88);
                ListSelBg = Color.FromArgb(63, 110, 185);
                ListFg = Color.FromArgb(205, 220, 242);
                ListSubFg = Color.FromArgb(130, 165, 215);
                HeaderFg = Color.FromArgb(105, 170, 230);
                BorderColor = Color.FromArgb(52, 96, 168);
                AccentBlue = Color.FromArgb(63, 125, 210);
                PanelBg = Color.FromArgb(12, 22, 46);
                CancelBtnBg = Color.FromArgb(34, 54, 90);
            }

            _allEntries = folderNames
                .Select(name => ParseFolder(name, basePath))
                .OrderBy(e => e.Name)
                .ToList();

            BuildUI();
            // Aplica starea salvata dupa BuildUI (chkDigitalOnly exista deja)
            ApplyDigitalFilter();
            FilterList("");
        }

        private FolderEntry ParseFolder(string folderName, string basePath)
        {
            int sep = folderName.IndexOf(" - ", StringComparison.Ordinal);
            string id = sep > 0 ? folderName.Substring(0, sep).Trim() : "";
            return new FolderEntry
            {
                FolderName = folderName,
                Id = id,
                Name = sep > 0 ? folderName.Substring(sep + 3).Trim() : folderName,
                FullPath = Path.Combine(basePath, folderName),
                IsDigital = _digitalIds.Contains(id)
            };
        }

        private void BuildUI()
        {
            Text = "Selectare Dosar Candidat";
            ClientSize = new Size(620, 540);
            MinimumSize = new Size(480, 380);
            StartPosition = FormStartPosition.CenterParent;
            BackColor = BgColor;
            Font = new Font("Segoe UI", 9f);
            FormBorderStyle = FormBorderStyle.Sizable;
            ShowIcon = false;
            ShowInTaskbar = false;

            // Header
            var header = new Panel { Dock = DockStyle.Top, Height = 56, BackColor = HeaderBg };
            header.Controls.Add(new Label
            {
                Text = "Selectare Dosar Candidat",
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 12f, FontStyle.Bold),
                ForeColor = Color.White,
                BackColor = Color.Transparent,
                TextAlign = ContentAlignment.MiddleCenter,
            });
            header.Controls.Add(new Panel { Dock = DockStyle.Bottom, Height = 2, BackColor = AccentBlue });

            // Search
            var panelSearch = new Panel { Dock = DockStyle.Top, Height = 44, BackColor = BgColor, Padding = new Padding(12, 8, 12, 4) };
            txtSearch = new TextBox
            {
                Dock = DockStyle.Fill,
                BackColor = InputBg,
                ForeColor = InputFg,
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Segoe UI", 10f),
            };
            SetPlaceholder(txtSearch, "Caută după nume sau ID...");
            txtSearch.TextChanged += (s, e) => FilterList(txtSearch.Text);
            txtSearch.KeyDown += (s, e) => { if (e.KeyCode == Keys.Down && listView.Items.Count > 0) { listView.Focus(); listView.Items[0].Selected = true; } };
            panelSearch.Controls.Add(txtSearch);

            // ListView
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
                Font = new Font("Segoe UI", 9.5f),
                HeaderStyle = ColumnHeaderStyle.Nonclickable,
                OwnerDraw = true,
            };
            listView.Columns.Add("Nume", 400);
            listView.Columns.Add("ID", 100);

            listView.Resize += (s, e) =>
            {
                int idW = 90;
                int nameW = listView.ClientSize.Width - idW;
                if (nameW > 80) listView.Columns[0].Width = nameW;
                listView.Columns[1].Width = idW;
            };

            listView.DrawColumnHeader += (s, e) =>
            {
                e.Graphics.FillRectangle(new SolidBrush(PanelBg), e.Bounds);
                TextRenderer.DrawText(e.Graphics, e.Header.Text,
                    new Font("Segoe UI", 8.5f, FontStyle.Bold),
                    new Rectangle(e.Bounds.X + 10, e.Bounds.Y, e.Bounds.Width, e.Bounds.Height),
                    HeaderFg, TextFormatFlags.VerticalCenter | TextFormatFlags.Left);
            };

            listView.DrawSubItem += (s, e) =>
            {
                bool selected = e.Item.Selected;
                var bg = selected ? ListSelBg : (e.ItemIndex % 2 == 0 ? ListBg : ListAltBg);
                e.Graphics.FillRectangle(new SolidBrush(bg), e.Bounds);
                var fg = selected ? Color.White : (e.ColumnIndex == 1 ? ListSubFg : ListFg);
                int pad = e.ColumnIndex == 0 ? 10 : 6;
                TextRenderer.DrawText(e.Graphics, e.SubItem.Text,
                    e.Item.Font ?? listView.Font,
                    new Rectangle(e.Bounds.X + pad, e.Bounds.Y, e.Bounds.Width - pad, e.Bounds.Height),
                    fg, TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis);
            };

            listView.DrawItem += (s, e) => { };
            listView.SelectedIndexChanged += (s, e) => btnSelect.Enabled = listView.SelectedItems.Count > 0;
            listView.MouseDoubleClick += (s, e) => { if (listView.SelectedItems.Count > 0) AcceptSelection(); };
            listView.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter && listView.SelectedItems.Count > 0) AcceptSelection(); };

            // Bottom panel
            var panelBottom = new Panel { Dock = DockStyle.Bottom, Height = 52, BackColor = PanelBg };

            lblCount = new Label
            {
                Location = new Point(14, 0),
                Size = new Size(160, 52),
                ForeColor = ListSubFg,
                BackColor = Color.Transparent,
                Font = new Font("Segoe UI", 8.5f, FontStyle.Italic),
                TextAlign = ContentAlignment.MiddleLeft,
            };

            // Bifa cu starea persistata pe sesiune
            chkDigitalOnly = new CheckBox
            {
                Text = "Doar proceduri digitale",
                Checked = _lastDigitalOnlyState,
                AutoSize = true,
                ForeColor = ListSubFg,
                BackColor = Color.Transparent,
                Font = new Font("Segoe UI", 8.5f),
                Cursor = Cursors.Hand,
            };
            chkDigitalOnly.CheckedChanged += (s, e) =>
            {
                _lastDigitalOnlyState = chkDigitalOnly.Checked;
                ApplyDigitalFilter();
                FilterList(txtSearch.Text);
            };

            btnCancel = new Button
            {
                Text = "Anulează",
                Size = new Size(100, 32),
                FlatStyle = FlatStyle.Flat,
                BackColor = CancelBtnBg,
                ForeColor = Color.White,
                Cursor = Cursors.Hand,
                DialogResult = DialogResult.Cancel,
                Anchor = AnchorStyles.Right | AnchorStyles.Top,
            };
            btnCancel.FlatAppearance.BorderColor = BorderColor;
            btnCancel.FlatAppearance.BorderSize = 1;

            btnSelect = new Button
            {
                Text = "Selectează  →",
                Size = new Size(130, 32),
                FlatStyle = FlatStyle.Flat,
                BackColor = AccentBlue,
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                Cursor = Cursors.Hand,
                Enabled = false,
                Anchor = AnchorStyles.Right | AnchorStyles.Top,
            };
            btnSelect.FlatAppearance.BorderSize = 0;
            btnSelect.Click += (s, e) => AcceptSelection();

            void PositionBottomControls()
            {
                int y = (panelBottom.Height - btnSelect.Height) / 2;
                btnCancel.Location = new Point(panelBottom.Width - btnCancel.Width - 12, y);
                btnSelect.Location = new Point(btnCancel.Left - btnSelect.Width - 8, y);
                chkDigitalOnly.Location = new Point(lblCount.Right + 8, (panelBottom.Height - chkDigitalOnly.Height) / 2);
            }
            panelBottom.Resize += (s, e) => PositionBottomControls();
            panelBottom.Controls.Add(lblCount);
            panelBottom.Controls.Add(chkDigitalOnly);
            panelBottom.Controls.Add(btnSelect);
            panelBottom.Controls.Add(btnCancel);

            Controls.Add(listView);
            Controls.Add(panelSearch);
            Controls.Add(header);
            Controls.Add(new Panel { Dock = DockStyle.Bottom, Height = 1, BackColor = BorderColor });
            Controls.Add(panelBottom);

            AcceptButton = btnSelect;
            CancelButton = btnCancel;
            Shown += (s, e) =>
            {
                PositionBottomControls();
                txtSearch.Focus();
            };
        }

        private void ApplyDigitalFilter()
        {
            if (chkDigitalOnly == null) return;
            if (chkDigitalOnly.Checked)
                _filteredByDigital = _allEntries.Where(e => e.IsDigital).ToList();
            else
                _filteredByDigital = _allEntries;
        }

        private void FilterList(string query)
        {
            listView.BeginUpdate();
            listView.Items.Clear();

            var source = _filteredByDigital ?? _allEntries;
            var filtered = string.IsNullOrWhiteSpace(query)
                ? source
                : source.Where(e =>
                    e.Name.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    e.Id.StartsWith(query, StringComparison.OrdinalIgnoreCase)).ToList();

            foreach (var e in filtered)
            {
                var item = new ListViewItem(e.Name);
                item.SubItems.Add(e.Id);
                item.Tag = e;
                listView.Items.Add(item);
            }

            listView.EndUpdate();

            int totalShown = source.Count;
            lblCount.Text = filtered.Count == totalShown
                ? $"{totalShown} candidați"
                : $"{filtered.Count} din {totalShown} candidați";
            btnSelect.Enabled = false;
        }

        private void AcceptSelection()
        {
            if (listView.SelectedItems.Count == 0) return;
            var entry = listView.SelectedItems[0].Tag as FolderEntry;
            if (entry == null) return;
            SelectedFolderPath = entry.FullPath;
            SelectedFolderName = entry.FolderName;
            DialogResult = DialogResult.OK;
            Close();
        }

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wp, string lp);
        private const int EM_SETCUEBANNER = 0x1501;
        private static void SetPlaceholder(TextBox tb, string text) =>
            tb.HandleCreated += (s, e) => SendMessage(tb.Handle, EM_SETCUEBANNER, (IntPtr)1, text);
    }
}