using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using WacomSignaturePdf.Theme;

namespace WacomSignaturePdf.Controls
{
    /// <summary>
    /// Owner-drawn ComboBox listing candidate folders from WorkingRoot.
    /// Same style as DocumentTypeDropdown but with a single teal accent and folder icon.
    /// </summary>
    public class CandidateFolderDropdown : ComboBox
    {
        private static readonly Color AccentColor = Color.FromArgb(20, 140, 160);

        public CandidateFolderDropdown()
        {
            DrawMode = DrawMode.OwnerDrawFixed;
            DropDownStyle = ComboBoxStyle.DropDownList;
            ItemHeight = 36;
            Font = new Font("Segoe UI", 9f);
            FlatStyle = FlatStyle.Flat;
            BackColor = AppTheme.DropdownBgNormal;
            ForeColor = AppTheme.DropdownText;
        }

        protected override void OnDrawItem(DrawItemEventArgs e)
        {
            if (e.Index < 0) return;

            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            bool isSelected = (e.State & DrawItemState.Selected) != 0
                           || (e.State & DrawItemState.HotLight) != 0;

            using (var brush = new SolidBrush(isSelected ? AppTheme.DropdownBgSelected : AppTheme.DropdownBgNormal))
                g.FillRectangle(brush, e.Bounds);

            using (var brush = new SolidBrush(AccentColor))
                g.FillRectangle(brush, e.Bounds.X, e.Bounds.Y, 4, e.Bounds.Height);

            using (var iconFont = new Font("Segoe UI", 11f))
            {
                var iconRect = new Rectangle(e.Bounds.X + 10, e.Bounds.Y, 26, e.Bounds.Height);
                TextRenderer.DrawText(g, "📁", iconFont, iconRect, AccentColor,
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

        protected override void WndProc(ref Message m)
        {
            base.WndProc(ref m);

            const int WM_PAINT = 0x000F;
            if (m.Msg != WM_PAINT) return;

            using (var g = Graphics.FromHwnd(Handle))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;

                int arrowW = SystemInformation.VerticalScrollBarWidth;

                if (SelectedIndex < 0)
                {
                    // Placeholder
                    var bgRect = new Rectangle(0, 0, Width - arrowW - 2, Height);
                    using (var brush = new SolidBrush(AppTheme.DropdownBgNormal))
                        g.FillRectangle(brush, bgRect);

                    using (var brush = new SolidBrush(AccentColor))
                        g.FillRectangle(brush, 0, 0, 4, Height);

                    using (var font = new Font("Segoe UI", 9f, FontStyle.Italic))
                    using (var brush = new SolidBrush(AppTheme.SidebarSub))
                        TextRenderer.DrawText(g, "Selectati dosarul candidatului", font,
                            new Rectangle(12, 0, Width - arrowW - 16, Height),
                            AppTheme.DropdownText,
                            TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis);

                    using (var pen = new Pen(AppTheme.DropdownBorder, 1f))
                        g.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);

                    return;
                }

                var bgRect2 = new Rectangle(0, 0, Width - arrowW - 2, Height);
                using (var brush = new SolidBrush(AppTheme.DropdownBgNormal))
                    g.FillRectangle(brush, bgRect2);

                using (var brush = new SolidBrush(AccentColor))
                    g.FillRectangle(brush, 0, 0, 4, Height);

                using (var iconFont = new Font("Segoe UI", 11f))
                    TextRenderer.DrawText(g, "📁", iconFont, new Rectangle(8, 0, 26, Height),
                        AccentColor, TextFormatFlags.VerticalCenter | TextFormatFlags.Left);

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