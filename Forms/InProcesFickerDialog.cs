using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using WacomSignaturePdf.Theme;

namespace WacomSignaturePdf.Forms
{
    /// <summary>
    /// Dialog custom pentru selectarea unui document din folderul "Documente In Proces".
    /// Afiseaza lista de PDF-uri cu data modificarii si permite cautare rapida.
    /// </summary>
    public class InProcesPickerDialog : Form
    {
        public string SelectedPath { get; private set; }

        private readonly string _folder;
        private List<FileInfo> _allFiles;

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
        private static readonly Color ListSelBg = Color.FromArgb(38, 168, 168);
        private static readonly Color ListSelFg = Color.White;
        private static readonly Color ListFg = Color.FromArgb(200, 235, 230);
        private static readonly Color ListSubFg = Color.FromArgb(110, 175, 170);
        private static readonly Color HeaderFg = Color.FromArgb(60, 195, 195);
        private static readonly Color BorderColor = Color.FromArgb(38, 110, 110);

        public InProcesPickerDialog(string folder)
        {
            _folder = folder;
            BuildUI();
            LoadFiles();
        }

        private void BuildUI()
        {
            Text = "Documente In Proces";
            ClientSize = new Size(560, 460);
            MinimumSize = new Size(420, 340);
            StartPosition = FormStartPosition.CenterParent;
            BackColor = BgColor;
            Font = new Font("Segoe UI", 9f);
            FormBorderStyle = FormBorderStyle.Sizable;
            ShowIcon = false;
            ShowInTaskbar = false;

            // ── Header ──
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

            // ── Search ──
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

            // ── ListView ──
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
            listView.Columns.Add("Nume document", 340);
            listView.Columns.Add("Modificat", 140);

            // Redimensioneaza coloana 1 sa umple spatiul ramas (elimina coloana goala)
            listView.Resize += (s, e) =>
            {
                int col2W = 140;
                int col1W = listView.ClientSize.Width - col2W;
                if (col1W > 80) listView.Columns[0].Width = col1W;
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
                var bg = selected ? ListSelBg : (e.ItemIndex % 2 == 0 ? ListBg : Color.FromArgb(24, 74, 72));
                e.Graphics.FillRectangle(new SolidBrush(bg), e.Bounds);

                var fg = selected ? ListSelFg : (e.ColumnIndex == 1 ? ListSubFg : ListFg);
                int pad = e.ColumnIndex == 0 ? 10 : 6;
                TextRenderer.DrawText(e.Graphics, e.SubItem.Text,
                    e.Item.Font ?? listView.Font,
                    new Rectangle(e.Bounds.X + pad, e.Bounds.Y, e.Bounds.Width - pad, e.Bounds.Height),
                    fg, TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis);
            };

            listView.DrawItem += (s, e) => { }; // required for OwnerDraw

            listView.SelectedIndexChanged += (s, e) =>
                btnOpen.Enabled = listView.SelectedItems.Count > 0;

            listView.MouseDoubleClick += (s, e) =>
            {
                if (listView.SelectedItems.Count > 0) AcceptSelection();
            };

            listView.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Enter && listView.SelectedItems.Count > 0)
                    AcceptSelection();
            };

            // ── Bottom bar ──
            var panelBottom = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 52,
                BackColor = PanelColor,
            };

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

            // ── Separator line ──
            var line = new Panel { Dock = DockStyle.Bottom, Height = 1, BackColor = BorderColor };

            // ── Assemble ──
            Controls.Add(listView);
            Controls.Add(panelSearch);
            Controls.Add(lblTitle);
            Controls.Add(line);
            Controls.Add(panelBottom);

            AcceptButton = btnOpen;
            CancelButton = btnCancel;

            // Focus search on open
            Shown += (s, e) => txtSearch.Focus();
        }

        private void LoadFiles()
        {
            try
            {
                _allFiles = Directory.GetFiles(_folder, "*.pdf", SearchOption.TopDirectoryOnly)
                    .Select(f => new FileInfo(f))
                    .OrderByDescending(f => f.LastWriteTime)
                    .ToList();
            }
            catch
            {
                _allFiles = new List<FileInfo>();
            }
            FilterList("");
        }

        private void FilterList(string query)
        {
            listView.BeginUpdate();
            listView.Items.Clear();

            var filtered = string.IsNullOrWhiteSpace(query)
                ? _allFiles
                : _allFiles.Where(f =>
                    f.Name.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0).ToList();

            foreach (var fi in filtered)
            {
                var item = new ListViewItem(fi.Name);
                item.SubItems.Add(fi.LastWriteTime.ToString("dd.MM.yyyy  HH:mm"));
                item.Tag = fi.FullName;
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
            SelectedPath = listView.SelectedItems[0].Tag as string;
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