using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using WacomSignaturePdf.Theme;

namespace WacomSignaturePdf.Controls
{
    /// <summary>
    /// Owner-drawn ComboBox with colored accent bars and document icons per item.
    /// </summary>
    public class DocumentTypeDropdown : ComboBox
    {
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
        /// <summary>
        /// Just two overrides to implement a custom-drawn dropdown list and selected item display. 
        /// The dropdown items show a colored accent bar, a document icon, and the item text.
        /// The selected item area also shows the icon and text with the accent color. 
        /// Theme colors are used for backgrounds, text, and accents to match the overall app design.
        /// </summary>
        protected override void OnDrawItem(DrawItemEventArgs e)
        {
            if (e.Index < 0) return;

            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            bool isSelected = (e.State & DrawItemState.Selected) != 0
                           || (e.State & DrawItemState.HotLight) != 0;

            using (var brush = new SolidBrush(isSelected ? AppTheme.DropdownBgSelected : AppTheme.DropdownBgNormal))
                g.FillRectangle(brush, e.Bounds);

            Color accent = AppTheme.DropdownItemColors[e.Index % AppTheme.DropdownItemColors.Length];

            using (var brush = new SolidBrush(accent))
                g.FillRectangle(brush, e.Bounds.X, e.Bounds.Y, 4, e.Bounds.Height);

            using (var iconFont = new Font("Segoe UI", 11f))
            {
                var iconRect = new Rectangle(e.Bounds.X + 10, e.Bounds.Y, 26, e.Bounds.Height);
                TextRenderer.DrawText(g, "📄", iconFont, iconRect, accent,
                    TextFormatFlags.VerticalCenter | TextFormatFlags.Left);
            }

            using (var nameFont = new Font("Segoe UI", 9f, isSelected ? FontStyle.Bold : FontStyle.Regular))
            {
                var nameRect = new Rectangle(e.Bounds.X + 40, e.Bounds.Y, e.Bounds.Width - 48, e.Bounds.Height);
                TextRenderer.DrawText(g, Items[e.Index].ToString(), nameFont, nameRect, AppTheme.DropdownText,
                    TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis);
            }

            if (e.Index < Items.Count - 1)
                using (var pen = new Pen(AppTheme.DropdownSeparator, 1f))
                    g.DrawLine(pen, e.Bounds.X + 4, e.Bounds.Bottom - 1, e.Bounds.Right, e.Bounds.Bottom - 1);
        }

        // ── Draw the selected item in the collapsed button area ───────────────────
        protected override void WndProc(ref Message m)
        {
            base.WndProc(ref m);

            const int WM_PAINT = 0x000F;
            if (m.Msg != WM_PAINT || SelectedIndex < 0) return;

            using (var g = Graphics.FromHwnd(Handle))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;

                Color accent = AppTheme.DropdownItemColors[SelectedIndex % AppTheme.DropdownItemColors.Length];
                int arrowW = SystemInformation.VerticalScrollBarWidth;
                var bgRect = new Rectangle(0, 0, Width - arrowW - 2, Height);

                using (var brush = new SolidBrush(AppTheme.DropdownBgNormal))
                    g.FillRectangle(brush, bgRect);

                using (var brush = new SolidBrush(accent))
                    g.FillRectangle(brush, 0, 0, 4, Height);

                using (var iconFont = new Font("Segoe UI", 11f))
                    TextRenderer.DrawText(g, "📄", iconFont, new Rectangle(8, 0, 26, Height),
                        accent, TextFormatFlags.VerticalCenter | TextFormatFlags.Left);

                using (var nameFont = new Font("Segoe UI", 9f, FontStyle.Bold))
                    TextRenderer.DrawText(g, SelectedItem.ToString(), nameFont,
                        new Rectangle(38, 0, Width - arrowW - 46, Height),
                        AppTheme.DropdownText,
                        TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis);

                using (var pen = new Pen(Enabled ? AppTheme.DropdownBorder : AppTheme.DropdownDisabled, 1f))
                    g.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
            }
        }
    }
}