using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using WacomSignaturePdf.Models;
using WacomSignaturePdf.Theme;

namespace WacomSignaturePdf.Controls
{
    public class SignatureCardPanel : Panel
    {
        public event Action<SignatureSlot> CardClicked;

        public SignatureSlot Slot { get; private set; }
        public bool Signed { get; private set; }
        public bool RoleRestricted { get; private set; }

        private Label lblSlotNumber;
        private Label lblReason;
        private Label lblPage;
        private Label lblSigner;
        private Label lblStatus;
        private Label lblRole;      // OfficialRole badge — only shown when role is set
        private Label lblRequired;

        // Layout constants
        private const int StatusW = 84;
        private const int StatusH = 22;
        private const int RoleBadgeH = 16;
        private const int RightMargin = 6;
        private const int LeftStart = 70;

        // Animation
        private Timer _animTimer;
        private float _hoverProgress = 0f;
        private bool _isHovered = false;
        private bool _isPressed = false;

        // Role-restricted colours
        private static readonly Color RestrictedAccent = Color.FromArgb(155, 165, 182);
        private static readonly Color RestrictedBorder = Color.FromArgb(200, 208, 222);
        private static readonly Color RestrictedBackground = Color.FromArgb(240, 242, 246);
        private static readonly Color RestrictedStatusBg = Color.FromArgb(215, 220, 230);
        private static readonly Color RestrictedStatusFg = Color.FromArgb(120, 130, 150);

        public SignatureCardPanel(SignatureSlot slot)
        {
            Slot = slot;
            Size = new Size(354, slot.Required ? 86 : 72);
            BackColor = AppTheme.CardBase;
            Cursor = Cursors.Hand;
            DoubleBuffered = true;

            BuildControls(slot);
            WireMouseEvents(this);

            _animTimer = new Timer { Interval = 10 };
            _animTimer.Tick += OnAnimTick;
        }

        // ── Controls ──────────────────────────────────────────────────────────────

        private void BuildControls(SignatureSlot slot)
        {
            lblSlotNumber = new Label
            {
                Text = $"#{slot.SignatureId}",
                Location = new Point(38, 8),
                Size = new Size(28, 22),
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                ForeColor = AppTheme.CardAccentPend,
                BackColor = Color.Transparent
            };

            lblReason = new Label
            {
                Text = slot.Reason,
                Location = new Point(LeftStart, 8),
                Size = new Size(160, 20),   // width fixed in LayoutControls
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                ForeColor = AppTheme.CardTitleText,
                BackColor = Color.Transparent,
                AutoEllipsis = true
            };

            lblPage = new Label
            {
                Text = $"PAGINA {slot.ResolvedPage}",
                Location = new Point(LeftStart, 30),
                Size = new Size(160, 16),
                Font = new Font("Segoe UI", 8f),
                ForeColor = AppTheme.CardPageText,
                BackColor = Color.Transparent
            };

            lblSigner = new Label
            {
                // Official slots: name shown only after signing (set via MarkSigned)
                Text = slot.Party == "Official" ? "" : (slot.ResolvedSignerName ?? slot.SignerName),
                Location = new Point(LeftStart, 48),
                Size = new Size(160, 16),
                Font = new Font("Segoe UI", 8f, FontStyle.Italic),
                ForeColor = AppTheme.CardSignerText,
                BackColor = Color.Transparent,
                AutoEllipsis = true
            };

            lblStatus = new Label
            {
                Text = "IN ASTEPTARE",
                Location = new Point(238, 8),     // repositioned in LayoutControls
                Size = new Size(StatusW, StatusH),
                Font = new Font("Segoe UI", 7.5f, FontStyle.Bold),
                ForeColor = AppTheme.CardStatusPendFg,
                BackColor = AppTheme.CardStatusPendBg,
                TextAlign = ContentAlignment.MiddleCenter
            };

            Controls.Add(lblSlotNumber);
            Controls.Add(lblReason);
            Controls.Add(lblPage);
            Controls.Add(lblSigner);
            Controls.Add(lblStatus);

            // Role badge — only created when role is non-empty
            string roleText = !string.IsNullOrEmpty(slot.OfficialRole)
                                ? slot.OfficialRole
                                : (slot.Party == "Candidate" || string.IsNullOrEmpty(slot.Party)) ? "Candidat" : null;
            if (roleText != null)
            {
                lblRole = new Label
                {
                    Text = roleText,
                    Location = new Point(238, 34),
                    Size = new Size(StatusW, RoleBadgeH),
                    Font = new Font("Segoe UI", 7f, FontStyle.Bold),
                    ForeColor = Color.FromArgb(100, 120, 160),
                    BackColor = Color.FromArgb(225, 232, 245),
                    TextAlign = ContentAlignment.MiddleCenter
                };
                Controls.Add(lblRole);
            }

            if (slot.Required)
            {
                lblRequired = new Label
                {
                    Text = "★ Required",
                    Location = new Point(LeftStart, 66),
                    Size = new Size(80, 14),
                    Font = new Font("Segoe UI", 7f),
                    ForeColor = AppTheme.CardRequired,
                    BackColor = Color.Transparent
                };
                Controls.Add(lblRequired);
            }
        }

        // ── Dynamic layout — called on every resize so status stays right-anchored ──

        protected override void OnLayout(LayoutEventArgs levent)
        {
            base.OnLayout(levent);
            LayoutControls();
        }

        private void LayoutControls()
        {
            if (lblStatus == null) return;

            int statusX = Width - StatusW - RightMargin;
            int reasonW = statusX - LeftStart - 6;

            lblReason.Width = Math.Max(40, reasonW);
            lblPage.Width = Math.Max(40, reasonW);
            lblSigner.Width = Math.Max(40, reasonW);

            lblStatus.Location = new Point(statusX, 8);

            if (lblRole != null)
                lblRole.Location = new Point(statusX, 34);
        }

        // ── Public state changes ───────────────────────────────────────────────────

        public void MarkSigned(string signerName = null)
        {
            Signed = true;
            RoleRestricted = false;
            _animTimer.Stop();
            _hoverProgress = 0f;

            if (!string.IsNullOrWhiteSpace(signerName))
                lblSigner.Text = signerName;

            lblStatus.Text = "SEMNAT ✓";
            lblStatus.ForeColor = AppTheme.CardStatusSignFg;
            lblStatus.BackColor = AppTheme.CardStatusSignBg;
            BackColor = AppTheme.CardSigned;
            Cursor = Cursors.Default;
            Invalidate();
        }

        public void SetRoleRestricted(bool restricted)
        {
            if (Signed || RoleRestricted == restricted) return;

            RoleRestricted = restricted;
            Enabled = !restricted;
            Cursor = restricted ? Cursors.No : Cursors.Hand;
            BackColor = restricted ? RestrictedBackground : AppTheme.CardBase;

            if (restricted)
            {
                lblStatus.Text = "ALT ROL";
                lblStatus.ForeColor = RestrictedStatusFg;
                lblStatus.BackColor = RestrictedStatusBg;
            }
            else
            {
                lblStatus.Text = "IN ASTEPTARE";
                lblStatus.ForeColor = AppTheme.CardStatusPendFg;
                lblStatus.BackColor = AppTheme.CardStatusPendBg;
            }

            _isHovered = false;
            _hoverProgress = 0f;
            _animTimer.Stop();
            Invalidate();
        }

        // ── Animation ─────────────────────────────────────────────────────────────

        private void OnAnimTick(object sender, EventArgs e)
        {
            float target = _isHovered ? 1f : 0f;
            _hoverProgress += (target - _hoverProgress) * 0.20f;

            if (Math.Abs(_hoverProgress - target) < 0.01f)
            {
                _hoverProgress = target;
                _animTimer.Stop();
            }

            if (!Signed && !RoleRestricted)
            {
                Color to = _isPressed ? AppTheme.CardPressed : AppTheme.CardHover;
                BackColor = Blend(AppTheme.CardBase, to, _hoverProgress);
            }

            Invalidate();
        }

        // ── Mouse ─────────────────────────────────────────────────────────────────

        private void WireMouseEvents(Control c)
        {
            c.MouseEnter += (s, e) =>
            {
                if (!Signed && !RoleRestricted) { _isHovered = true; _animTimer.Start(); }
            };
            c.MouseLeave += (s, e) =>
            {
                if (ClientRectangle.Contains(PointToClient(Cursor.Position))) return;
                _isHovered = false;
                _isPressed = false;
                if (!RoleRestricted) _animTimer.Start();
            };
            c.MouseDown += (s, e) =>
            {
                if (Signed || RoleRestricted || e.Button != MouseButtons.Left) return;
                _isPressed = true;
                BackColor = AppTheme.CardPressed;
                _animTimer.Stop();
                Invalidate();
            };
            c.MouseUp += (s, e) =>
            {
                if (!Signed && !RoleRestricted) { _isPressed = false; _animTimer.Start(); }
            };
            c.MouseClick += (s, e) =>
            {
                if (!Signed && !RoleRestricted && e.Button == MouseButtons.Left)
                    CardClicked?.Invoke(Slot);
            };

            foreach (Control child in c.Controls)
                WireMouseEvents(child);
        }

        // ── Paint ─────────────────────────────────────────────────────────────────

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            bool interactive = !Signed && !RoleRestricted;
            float p = _hoverProgress;

            // Left accent bar — widens on hover (4px → 7px)
            int barW = interactive ? (int)(4 + p * 10) : 4;
            Color accent = Signed ? AppTheme.CardAccentSigned
                         : RoleRestricted ? RestrictedAccent
                         : Blend(AppTheme.CardAccentPend, Color.FromArgb(60, 120, 230), p);

            using (var brush = new SolidBrush(accent))
                g.FillRectangle(brush, 0, 0, barW, Height);

            // Border — thickens and brightens on hover (1px → 2.5px)
            float borderW = interactive ? 1f + p * 1.5f : 1f;
            Color border = Signed ? AppTheme.CardBorderSigned
                          : RoleRestricted ? RestrictedBorder
                          : Blend(AppTheme.CardBorderNormal, Color.FromArgb(70, 130, 230), p);

            using (var pen = new Pen(border, borderW))
            {
                float half = borderW / 2f;
                g.DrawRectangle(pen, half, half, Width - borderW, Height - borderW);
            }

            if (interactive && p > 0.02f)
            {
                // Top shine — white highlight simulating light hitting a lifted surface
                using (var brush = new SolidBrush(Color.FromArgb((int)(70 * p), 255, 255, 255)))
                    g.FillRectangle(brush, barW, 0, Width - barW, 2);

                // Bottom inner shadow — blue tint simulating depth below the card
                using (var brush = new SolidBrush(Color.FromArgb((int)(35 * p), 30, 80, 200)))
                    g.FillRectangle(brush, barW, Height - 4, Width - barW, 4);
            }

            // Bottom separator
            using (var pen = new Pen(AppTheme.CardSeparator, 1f))
                g.DrawLine(pen, 4, Height - 1, Width, Height - 1);
        }

        // ── Helpers ───────────────────────────────────────────────────────────────

        private static Color Blend(Color from, Color to, float t)
        {
            t = Math.Max(0f, Math.Min(1f, t));
            return Color.FromArgb(
                (int)(from.A + (to.A - from.A) * t),
                (int)(from.R + (to.R - from.R) * t),
                (int)(from.G + (to.G - from.G) * t),
                (int)(from.B + (to.B - from.B) * t));
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) _animTimer?.Dispose();
            base.Dispose(disposing);
        }
    }
}