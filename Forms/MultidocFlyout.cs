using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Windows.Forms;
using WacomSignaturePdf.Controls;
using WacomSignaturePdf.Services;
using WacomSignaturePdf.Theme;

namespace WacomSignaturePdf.Forms
{
    // Dropdown popup listing files for a multi-document template.
    // Anchors below the template dropdown. Closes on deactivation or Escape.
    internal class MultiDocFlyout : Form
    {
        public event Action<string> FileSelected;

        private readonly List<(string FilePath, TemplateService.DocumentStatus Status)> _files;
        private int _hoveredIndex = -1;

        private const int ItemHeight = 36;
        private const int BadgeWidth = 92;

        public MultiDocFlyout(List<(string, TemplateService.DocumentStatus)> files, int width)
        {
            _files = files;
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            TopMost = true;
            StartPosition = FormStartPosition.Manual;
            BackColor = AppTheme.DropdownBgNormal;
            DoubleBuffered = true;
            KeyPreview = true;
            ClientSize = new Size(width, files.Count * ItemHeight + 2);

            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer, true);
        }

        // ── Paint ─────────────────────────────────────────────────────────────────

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            for (int i = 0; i < _files.Count; i++)
            {
                var (filePath, status) = _files[i];
                var itemRect = new Rectangle(0, 1 + i * ItemHeight, Width, ItemHeight);
                bool hovered = i == _hoveredIndex;

                using (var brush = new SolidBrush(hovered ? AppTheme.DropdownBgSelected : AppTheme.DropdownBgNormal))
                    g.FillRectangle(brush, itemRect);

                Color badgeColor = BadgeColor(status);
                var badgeRect = new Rectangle(itemRect.X, itemRect.Y, BadgeWidth, ItemHeight);
                using (var brush = new SolidBrush(badgeColor))
                    g.FillRectangle(brush, badgeRect);

                using (var f = new Font("Segoe UI", 7f, FontStyle.Bold))
                    TextRenderer.DrawText(g, BadgeLabel(status), f, badgeRect, Color.White,
                        TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);

                using (var pen = new Pen(Color.FromArgb(180, 195, 215), 1f))
                    g.DrawLine(pen, BadgeWidth, itemRect.Y + 4, BadgeWidth, itemRect.Bottom - 4);

                var nameRect = new Rectangle(BadgeWidth + 10, itemRect.Y, Width - BadgeWidth - 14, ItemHeight);
                using (var f = new Font("Segoe UI", 9f, hovered ? FontStyle.Bold : FontStyle.Regular))
                    TextRenderer.DrawText(g, Path.GetFileNameWithoutExtension(filePath), f, nameRect,
                        AppTheme.DropdownText,
                        TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis);

                if (i < _files.Count - 1)
                    using (var pen = new Pen(AppTheme.DropdownSeparator, 1f))
                        g.DrawLine(pen, BadgeWidth, itemRect.Bottom - 1, Width, itemRect.Bottom - 1);
            }

            using (var pen = new Pen(AppTheme.DropdownBorder, 1.5f))
                g.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
        }

        // ── Mouse ─────────────────────────────────────────────────────────────────

        protected override void OnMouseMove(MouseEventArgs e)
        {
            int idx = (e.Y - 1) / ItemHeight;
            if (idx < 0 || idx >= _files.Count) idx = -1;
            if (idx != _hoveredIndex) { _hoveredIndex = idx; Invalidate(); }
            base.OnMouseMove(e);
        }

        protected override void OnMouseLeave(EventArgs e) { _hoveredIndex = -1; Invalidate(); base.OnMouseLeave(e); }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;
            int idx = (e.Y - 1) / ItemHeight;
            if (idx >= 0 && idx < _files.Count) { Close(); FileSelected?.Invoke(_files[idx].FilePath); }
            base.OnMouseClick(e);
        }

        protected override void OnDeactivate(EventArgs e) { Close(); base.OnDeactivate(e); }
        protected override void OnKeyDown(KeyEventArgs e) { if (e.KeyCode == Keys.Escape) Close(); base.OnKeyDown(e); }

        // ── Helpers ───────────────────────────────────────────────────────────────

        private static Color BadgeColor(TemplateService.DocumentStatus s)
        {
            switch (s)
            {
                case TemplateService.DocumentStatus.SignedSealed: return DocumentTypeDropdown.ColorSignedSealed;
                case TemplateService.DocumentStatus.SignedUnsealed: return DocumentTypeDropdown.ColorSignedUnsealed;
                case TemplateService.DocumentStatus.PartialSigned: return DocumentTypeDropdown.ColorPartialSigned;
                default: return DocumentTypeDropdown.ColorUnsigned;
            }
        }

        private static string BadgeLabel(TemplateService.DocumentStatus s)
        {
            switch (s)
            {
                case TemplateService.DocumentStatus.SignedSealed: return "SIGILAT";
                case TemplateService.DocumentStatus.SignedUnsealed: return "SEMNAT";
                case TemplateService.DocumentStatus.PartialSigned: return "PARTIAL SEMNAT";
                default: return "NESEMNAT";
            }
        }
    }
}
