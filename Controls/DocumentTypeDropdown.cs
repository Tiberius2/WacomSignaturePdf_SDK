using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using WacomSignaturePdf.Theme;

namespace WacomSignaturePdf.Controls
{
    public class DocumentTypeDropdown : ComboBox
    {
        private readonly List<Color> _statusColors = new List<Color>();

        // Status accent colors (badge background)
        public static readonly Color ColorNotFound = Color.FromArgb(185, 100, 95);
        public static readonly Color ColorUnsigned = Color.FromArgb(80, 130, 195);
        public static readonly Color ColorPartialSigned = Color.FromArgb(155, 100, 180);
        public static readonly Color ColorSignedUnsealed = Color.FromArgb(190, 150, 60);
        public static readonly Color ColorSignedSealed = Color.FromArgb(70, 160, 105);

        private const int BadgeWidth = 92;

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

        private Color GetAccentColor(int index) =>
            index >= 0 && index < _statusColors.Count
                ? _statusColors[index]
                : AppTheme.DropdownItemColors[index % AppTheme.DropdownItemColors.Length];

        private static string GetStatusLabel(Color c)
        {
            if (c == ColorSignedSealed) return "SIGILAT";
            if (c == ColorSignedUnsealed) return "SEMNAT";
            if (c == ColorPartialSigned) return "PARTIAL SEMNAT";
            if (c == ColorUnsigned) return "NESEMNAT";
            if (c == ColorNotFound) return "NEGASIT";
            return "";
        }

        protected override void OnDrawItem(DrawItemEventArgs e)
        {
            if (e.Index < 0) return;

            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            bool isSelected = (e.State & DrawItemState.Selected) != 0
                           || (e.State & DrawItemState.HotLight) != 0;

            using (var brush = new SolidBrush(isSelected ? AppTheme.DropdownBgSelected : AppTheme.DropdownBgNormal))
                g.FillRectangle(brush, e.Bounds);

            Color accent = GetAccentColor(e.Index);
            var badgeRect = new Rectangle(e.Bounds.X, e.Bounds.Y, BadgeWidth, e.Bounds.Height);
            using (var brush = new SolidBrush(accent))
                g.FillRectangle(brush, badgeRect);

            string statusLabel = GetStatusLabel(accent);
            using (var badgeFont = new Font("Segoe UI", 7f, FontStyle.Bold))
                TextRenderer.DrawText(g, statusLabel, badgeFont, badgeRect, Color.White,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);

            using (var pen = new Pen(Color.FromArgb(180, 195, 215), 1f))
                g.DrawLine(pen, e.Bounds.X + BadgeWidth, e.Bounds.Y + 4,
                    e.Bounds.X + BadgeWidth, e.Bounds.Bottom - 4);

            using (var nameFont = new Font("Segoe UI", 9f, isSelected ? FontStyle.Bold : FontStyle.Regular))
            {
                var nameRect = new Rectangle(
                    e.Bounds.X + BadgeWidth + 10, e.Bounds.Y,
                    e.Bounds.Width - BadgeWidth - 14, e.Bounds.Height);
                TextRenderer.DrawText(g, Items[e.Index].ToString(), nameFont, nameRect,
                    AppTheme.DropdownText,
                    TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis);
            }

            if (e.Index < Items.Count - 1)
                using (var pen = new Pen(AppTheme.DropdownSeparator, 1f))
                    g.DrawLine(pen, e.Bounds.X + BadgeWidth, e.Bounds.Bottom - 1,
                        e.Bounds.Right, e.Bounds.Bottom - 1);
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

                Color accent = GetAccentColor(SelectedIndex);
                int arrowW = SystemInformation.VerticalScrollBarWidth;
                int totalW = Width - arrowW - 2;

                using (var brush = new SolidBrush(AppTheme.DropdownBgNormal))
                    g.FillRectangle(brush, new Rectangle(0, 0, totalW, Height));

                var badgeRect = new Rectangle(0, 0, BadgeWidth, Height);
                using (var brush = new SolidBrush(accent))
                    g.FillRectangle(brush, badgeRect);

                string statusLabel = GetStatusLabel(accent);
                using (var badgeFont = new Font("Segoe UI", 7f, FontStyle.Bold))
                    TextRenderer.DrawText(g, statusLabel, badgeFont, badgeRect, Color.White,
                        TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);

                using (var pen = new Pen(Color.FromArgb(180, 195, 215), 1f))
                    g.DrawLine(pen, BadgeWidth, 4, BadgeWidth, Height - 4);

                using (var nameFont = new Font("Segoe UI", 9f, FontStyle.Bold))
                    TextRenderer.DrawText(g, SelectedItem.ToString(), nameFont,
                        new Rectangle(BadgeWidth + 10, 0, totalW - BadgeWidth - 14, Height),
                        AppTheme.DropdownText,
                        TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis);

                using (var pen = new Pen(Enabled ? AppTheme.DropdownBorder : AppTheme.DropdownDisabled, 1f))
                    g.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
            }
        }
    }
}