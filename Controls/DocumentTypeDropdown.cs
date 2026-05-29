using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using WacomSignaturePdf.Theme;

namespace WacomSignaturePdf.Controls
{
    // Owner-drawn ComboBox showing document status badges and a ▶ indicator for multi-doc templates.
    public class DocumentTypeDropdown : ComboBox
    {
        public static readonly Color ColorNotFound = Color.FromArgb(185, 100, 95);
        public static readonly Color ColorUnsigned = Color.FromArgb(80, 130, 195);
        public static readonly Color ColorPartialSigned = Color.FromArgb(155, 100, 180);
        public static readonly Color ColorSignedUnsealed = Color.FromArgb(190, 150, 60);
        public static readonly Color ColorSignedSealed = Color.FromArgb(70, 160, 105);

        private readonly List<Color> _statusColors = new List<Color>();
        private readonly List<bool> _isMultiDoc = new List<bool>();

        private const int BadgeWidth = 92;
        private const int ArrowAreaW = 20;

        private static readonly Dictionary<Color, string> _badgeLabels = new Dictionary<Color, string>();

        static DocumentTypeDropdown()
        {
            _badgeLabels[ColorSignedSealed] = "SIGILAT";
            _badgeLabels[ColorSignedUnsealed] = "SEMNAT";
            _badgeLabels[ColorPartialSigned] = "PARTIAL SEMNAT";
            _badgeLabels[ColorUnsigned] = "NESEMNAT";
            _badgeLabels[ColorNotFound] = "NEGASIT";
        }

        public DocumentTypeDropdown()
        {
            DrawMode = DrawMode.OwnerDrawFixed;
            DropDownStyle = ComboBoxStyle.DropDownList;
            ItemHeight = 36;
            Font = new Font("Segoe UI", 9f);
            FlatStyle = FlatStyle.Flat;
            BackColor = AppTheme.DropdownBgNormal;
            ForeColor = AppTheme.DropdownText;
        }

        public void SetStatusImages(List<Image> images, List<Color> colors)
        {
            _statusColors.Clear();
            if (colors != null) _statusColors.AddRange(colors);
            Invalidate();
        }

        public void SetMultiDocFlags(List<bool> flags)
        {
            _isMultiDoc.Clear();
            if (flags != null) _isMultiDoc.AddRange(flags);
            Invalidate();
        }

        // ── Drawing ───────────────────────────────────────────────────────────────

        protected override void OnDrawItem(DrawItemEventArgs e)
        {
            if (e.Index < 0) return;

            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            bool selected = (e.State & DrawItemState.Selected) != 0 || (e.State & DrawItemState.HotLight) != 0;

            using (var brush = new SolidBrush(selected ? AppTheme.DropdownBgSelected : AppTheme.DropdownBgNormal))
                g.FillRectangle(brush, e.Bounds);

            DrawBadge(g, e.Bounds.X, e.Bounds.Y, e.Bounds.Height, e.Index);

            using (var pen = new Pen(Color.FromArgb(180, 195, 215), 1f))
                g.DrawLine(pen, e.Bounds.X + BadgeWidth, e.Bounds.Y + 4, e.Bounds.X + BadgeWidth, e.Bounds.Bottom - 4);

            bool multiDoc = IsMultiDoc(e.Index);
            int nameRight = e.Bounds.Width - BadgeWidth - 14 - (multiDoc ? ArrowAreaW : 0);
            var nameRect = new Rectangle(e.Bounds.X + BadgeWidth + 10, e.Bounds.Y, nameRight, e.Bounds.Height);
            using (var f = new Font("Segoe UI", 9f, selected ? FontStyle.Bold : FontStyle.Regular))
                TextRenderer.DrawText(g, Items[e.Index].ToString(), f, nameRect, AppTheme.DropdownText,
                    TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis);

            if (multiDoc) DrawArrow(g, e.Bounds, selected ? AppTheme.AccentBlue : AppTheme.SidebarSub);

            if (e.Index < Items.Count - 1)
                using (var pen = new Pen(AppTheme.DropdownSeparator, 1f))
                    g.DrawLine(pen, e.Bounds.X + BadgeWidth, e.Bounds.Bottom - 1, e.Bounds.Right, e.Bounds.Bottom - 1);
        }

        protected override void WndProc(ref Message m)
        {
            base.WndProc(ref m);

            const int WM_PAINT = 0x000F;
            if (m.Msg != WM_PAINT || SelectedIndex < 0) return;

            using (var g = Graphics.FromHwnd(Handle))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

                int arrowW = SystemInformation.VerticalScrollBarWidth;
                int totalW = Width - arrowW - 2;

                using (var brush = new SolidBrush(AppTheme.DropdownBgNormal))
                    g.FillRectangle(brush, new Rectangle(0, 0, totalW, Height));

                DrawBadge(g, 0, 0, Height, SelectedIndex);

                using (var pen = new Pen(Color.FromArgb(180, 195, 215), 1f))
                    g.DrawLine(pen, BadgeWidth, 4, BadgeWidth, Height - 4);

                bool multiDoc = IsMultiDoc(SelectedIndex);
                int nameRight = totalW - BadgeWidth - 14 - (multiDoc ? ArrowAreaW : 0);
                using (var f = new Font("Segoe UI", 9f, FontStyle.Bold))
                    TextRenderer.DrawText(g, SelectedItem.ToString(), f,
                        new Rectangle(BadgeWidth + 10, 0, nameRight, Height),
                        AppTheme.DropdownText,
                        TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis);

                if (multiDoc) DrawArrow(g, new Rectangle(0, 0, totalW, Height), AppTheme.SidebarSub);

                using (var pen = new Pen(Enabled ? AppTheme.DropdownBorder : AppTheme.DropdownDisabled, 1f))
                    g.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
            }
        }

        // ── Private helpers ───────────────────────────────────────────────────────

        private void DrawBadge(Graphics g, int x, int y, int h, int index)
        {
            Color accent = AccentFor(index);
            var badgeRect = new Rectangle(x, y, BadgeWidth, h);
            using (var brush = new SolidBrush(accent))
                g.FillRectangle(brush, badgeRect);

            string label = _badgeLabels.TryGetValue(accent, out string lbl) ? lbl : "";
            using (var f = new Font("Segoe UI", 7f, FontStyle.Bold))
                TextRenderer.DrawText(g, label, f, badgeRect, Color.White,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }

        private void DrawArrow(Graphics g, Rectangle bounds, Color color)
        {
            var arrowRect = new Rectangle(bounds.Right - ArrowAreaW - 4, bounds.Y, ArrowAreaW, bounds.Height);
            using (var f = new Font("Segoe UI", 8f, FontStyle.Bold))
                TextRenderer.DrawText(g, "▶", f, arrowRect, color,
                    TextFormatFlags.VerticalCenter | TextFormatFlags.HorizontalCenter);
        }

        private Color AccentFor(int index) =>
            index >= 0 && index < _statusColors.Count
                ? _statusColors[index]
                : AppTheme.DropdownItemColors[index % AppTheme.DropdownItemColors.Length];

        private bool IsMultiDoc(int index) => index >= 0 && index < _isMultiDoc.Count && _isMultiDoc[index];
    }
}
