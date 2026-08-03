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
        public event Action<SignatureSlot> DeleteClicked;

        public SignatureSlot Slot { get; private set; }
        public bool Signed { get; private set; }
        public bool RoleRestricted { get; private set; }

        private Label lblSlotNumber;
        private Label lblReason;
        private Label lblPage;
        private Label lblSigner;
        private Label lblStatus;
        private Label lblRole;
        private Label lblRequired;
        private Button btnDelete;
        private PictureBox _pbIcon;

        private readonly bool _showDeleteButton;
        private readonly bool _isOfficial;

        private const int StatusW = 84;
        private const int StatusH = 22;
        private const int RoleBadgeH = 16;
        private const int RightMargin = 6;
        private const int LeftStart = 70;

        private Timer _animTimer;
        private float _hoverProgress = 0f;
        private bool _isHovered = false;
        private bool _isPressed = false;

        private static readonly Color RestrictedAccent = Color.FromArgb(155, 165, 182);
        private static readonly Color RestrictedBorder = Color.FromArgb(200, 208, 222);
        private static readonly Color RestrictedBackground = Color.FromArgb(240, 242, 246);
        private static readonly Color RestrictedStatusBg = Color.FromArgb(215, 220, 230);
        private static readonly Color RestrictedStatusFg = Color.FromArgb(120, 130, 150);
        private static readonly Color RestrictedText = Color.FromArgb(160, 168, 182);

        private static readonly Color CandidatAccentBar = Color.FromArgb(186, 117, 23);
        private static readonly Color CandidatBg = Color.FromArgb(255, 251, 240);
        private static readonly Color CandidatBorder = Color.FromArgb(232, 200, 64);
        private static readonly Color CandidatAccentHover = Color.FromArgb(220, 150, 0);
        private static readonly Color CandidatSlotFg = Color.FromArgb(160, 120, 0);
        private static readonly Color CandidatRoleBg = Color.FromArgb(250, 199, 117);
        private static readonly Color CandidatRoleFg = Color.FromArgb(65, 42, 0);

        private static readonly Color OfficialAccentBar = Color.FromArgb(83, 74, 183);
        private static readonly Color OfficialBg = Color.FromArgb(245, 243, 255);
        private static readonly Color OfficialBorder = Color.FromArgb(175, 169, 236);
        private static readonly Color OfficialAccentHover = Color.FromArgb(127, 119, 221);
        private static readonly Color OfficialSlotFg = Color.FromArgb(83, 74, 183);
        private static readonly Color OfficialRoleBg = Color.FromArgb(206, 203, 246);
        private static readonly Color OfficialRoleFg = Color.FromArgb(60, 52, 137);

        public SignatureCardPanel(SignatureSlot slot, bool showDeleteButton = false)
        {
            Slot = slot;
            _showDeleteButton = showDeleteButton;
            _isOfficial = slot.Party == "Official";
            Size = new Size(354, CardHeight(slot, showDeleteButton));
            BackColor = _isOfficial ? OfficialBg : CandidatBg;
            Cursor = Cursors.Hand;
            DoubleBuffered = true;

            BuildControls(slot);
            WireMouseEvents(this);

            _animTimer = new Timer { Interval = 10 };
            _animTimer.Tick += OnAnimTick;
        }

        private static int CardHeight(SignatureSlot slot, bool withDelete)
        {
            int h = slot.Required ? 76 : 62;
            return withDelete ? h + 22 : h;
        }

        private void BuildControls(SignatureSlot slot)
        {
            Color slotFg = _isOfficial ? OfficialSlotFg : CandidatSlotFg;
            int cardH = CardHeight(slot, _showDeleteButton);

            // Icon per tip
            _pbIcon = new PictureBox
            {
                Image = _isOfficial
                    ? Properties.Resources.verified
                    : Properties.Resources.candidat,
                Location = new Point(8, cardH / 2 - 12),
                Size = new Size(24, 24),
                SizeMode = PictureBoxSizeMode.Zoom,
                BackColor = Color.Transparent
            };
            Controls.Add(_pbIcon);
            WireMouseEvents(_pbIcon);

            lblSlotNumber = new Label
            {
                Text = $"#{slot.SignatureId}",
                Location = new Point(38, 6),
                Size = new Size(28, 20),
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                ForeColor = slotFg,
                BackColor = Color.Transparent
            };

            lblReason = new Label
            {
                Text = slot.Reason,
                Location = new Point(LeftStart, 6),
                Size = new Size(160, 18),
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                ForeColor = _isOfficial ? Color.FromArgb(38, 33, 92) : AppTheme.CardTitleText,
                BackColor = Color.Transparent,
                AutoEllipsis = true
            };

            lblPage = new Label
            {
                Text = $"PAGINA {slot.ResolvedPage}",
                Location = new Point(LeftStart, 26),
                Size = new Size(160, 14),
                Font = new Font("Segoe UI", 8f),
                ForeColor = _isOfficial ? Color.FromArgb(127, 119, 221) : Color.FromArgb(133, 101, 11),
                BackColor = Color.Transparent
            };

            lblSigner = new Label
            {
                Text = slot.Party == "Official" ? "" : (slot.ResolvedSignerName ?? slot.SignerName),
                Location = new Point(LeftStart, 42),
                Size = new Size(160, 14),
                Font = new Font("Segoe UI", 8f, FontStyle.Italic),
                ForeColor = AppTheme.CardSignerText,
                BackColor = Color.Transparent,
                AutoEllipsis = true
            };

            lblStatus = new Label
            {
                Text = "IN ASTEPTARE",
                Location = new Point(238, 6),
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
                    ForeColor = _isOfficial ? OfficialRoleFg : CandidatRoleFg,
                    BackColor = _isOfficial ? OfficialRoleBg : CandidatRoleBg,
                    TextAlign = ContentAlignment.MiddleCenter
                };
                Controls.Add(lblRole);
            }

            if (slot.Required)
            {
                lblRequired = new Label
                {
                    Text = "* Required",
                    Location = new Point(LeftStart, 58),
                    Size = new Size(80, 12),
                    Font = new Font("Segoe UI", 7f),
                    ForeColor = AppTheme.CardRequired,
                    BackColor = Color.Transparent
                };
                Controls.Add(lblRequired);
            }

            if (_showDeleteButton)
            {
                int btnY = (slot.Required ? 76 : 62) - 6;
                btnDelete = new Button
                {
                    Text = "Sterge",
                    Location = new Point(Width - StatusW - RightMargin, btnY),
                    Size = new Size(StatusW, 24),
                    Font = new Font("Segoe UI", 7.5f, FontStyle.Bold),
                    FlatStyle = FlatStyle.Flat,
                    BackColor = Color.FromArgb(186, 61, 30),
                    ForeColor = Color.White,
                    Cursor = Cursors.Hand,
                    Anchor = AnchorStyles.Right | AnchorStyles.Top,
                };
                btnDelete.FlatAppearance.BorderSize = 2;
                btnDelete.FlatAppearance.BorderColor = Color.FromArgb(196, 158, 153);
                btnDelete.Click += (s, e) => DeleteClicked?.Invoke(Slot);
                Controls.Add(btnDelete);
            }
        }

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

            if (btnDelete != null)
                btnDelete.Location = new Point(Width - StatusW - RightMargin, btnDelete.Top);
        }

        public void MarkSigned(string signerName = null)
        {
            Signed = true;
            RoleRestricted = false;
            _animTimer.Stop();
            _hoverProgress = 0f;
            if (btnDelete != null) btnDelete.Visible = false;

            if (!string.IsNullOrWhiteSpace(signerName))
                lblSigner.Text = signerName;

            lblStatus.Text = "SEMNAT";
            lblStatus.ForeColor = AppTheme.CardStatusSignFg;
            lblStatus.BackColor = AppTheme.CardStatusSignBg;
            BackColor = AppTheme.CardSigned;
            Cursor = Cursors.Default;

            // Restaureaza culorile normale la semnat
            lblReason.ForeColor = _isOfficial ? Color.FromArgb(38, 33, 92) : AppTheme.CardTitleText;
            lblPage.ForeColor = _isOfficial ? Color.FromArgb(127, 119, 221) : Color.FromArgb(133, 101, 11);
            lblSlotNumber.ForeColor = _isOfficial ? OfficialSlotFg : CandidatSlotFg;
            if (_pbIcon != null)
                _pbIcon.Image = _isOfficial ? Properties.Resources.verified : Properties.Resources.candidat;

            Invalidate();
        }

        public void SetRoleRestricted(bool restricted)
        {
            if (Signed || RoleRestricted == restricted) return;

            RoleRestricted = restricted;
            Cursor = restricted ? Cursors.No : Cursors.Hand;

            if (restricted)
            {
                BackColor = RestrictedBackground;
                lblStatus.Text = "ALT ROL";
                lblStatus.ForeColor = RestrictedStatusFg;
                lblStatus.BackColor = RestrictedStatusBg;

                // Grayed out complet
                lblReason.ForeColor = RestrictedText;
                lblPage.ForeColor = RestrictedText;
                lblSigner.ForeColor = RestrictedText;
                lblSlotNumber.ForeColor = RestrictedAccent;
                if (lblRole != null)
                {
                    lblRole.ForeColor = RestrictedStatusFg;
                    lblRole.BackColor = RestrictedStatusBg;
                }

                // Icon gri pentru Official
                if (_pbIcon != null && _isOfficial)
                    _pbIcon.Image = Properties.Resources.verified_gray;
            }
            else
            {
                BackColor = _isOfficial ? OfficialBg : CandidatBg;
                lblStatus.Text = "IN ASTEPTARE";
                lblStatus.ForeColor = AppTheme.CardStatusPendFg;
                lblStatus.BackColor = AppTheme.CardStatusPendBg;

                // Restaureaza culorile normale
                lblReason.ForeColor = _isOfficial ? Color.FromArgb(38, 33, 92) : AppTheme.CardTitleText;
                lblPage.ForeColor = _isOfficial ? Color.FromArgb(127, 119, 221) : Color.FromArgb(133, 101, 11);
                lblSigner.ForeColor = AppTheme.CardSignerText;
                lblSlotNumber.ForeColor = _isOfficial ? OfficialSlotFg : CandidatSlotFg;
                if (lblRole != null)
                {
                    lblRole.ForeColor = _isOfficial ? OfficialRoleFg : CandidatRoleFg;
                    lblRole.BackColor = _isOfficial ? OfficialRoleBg : CandidatRoleBg;
                }

                // Icon normal
                if (_pbIcon != null)
                    _pbIcon.Image = _isOfficial ? Properties.Resources.verified : Properties.Resources.candidat;
            }

            if (btnDelete != null)
            {
                btnDelete.Enabled = true;
                btnDelete.Visible = _showDeleteButton;
            }

            _isHovered = false;
            _hoverProgress = 0f;
            _animTimer.Stop();
            Invalidate();
        }

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
                Color baseBg = _isOfficial ? OfficialBg : CandidatBg;
                Color to = _isPressed ? AppTheme.CardPressed : AppTheme.CardHover;
                BackColor = Blend(baseBg, to, _hoverProgress);
            }

            Invalidate();
        }

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
                if (child != btnDelete)
                    WireMouseEvents(child);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            bool interactive = !Signed && !RoleRestricted;
            float p = _hoverProgress;

            int barW = interactive ? (int)(4 + p * 30) : 4;
            Color accent;
            if (Signed)
                accent = AppTheme.CardAccentSigned;
            else if (RoleRestricted)
                accent = RestrictedAccent;
            else if (_isOfficial)
                accent = Blend(OfficialAccentBar, OfficialAccentHover, p);
            else
                accent = Blend(CandidatAccentBar, CandidatAccentHover, p);

            using (var brush = new SolidBrush(accent))
                g.FillRectangle(brush, 0, 0, barW, Height);

            // Linie delimitatoare verticala inaintea numarului
            Color lineColor = _isOfficial
                ? Color.FromArgb(120, 83, 74, 183)
                : Color.FromArgb(120, 186, 117, 23);
            using (var pen = new Pen(lineColor, 1f))
                g.DrawLine(pen, 38, 4, 38, Height - 4);

            float borderW = interactive ? 1f + p * 1.5f : 1f;
            Color border;
            if (Signed)
                border = AppTheme.CardBorderSigned;
            else if (RoleRestricted)
                border = RestrictedBorder;
            else if (_isOfficial)
                border = Blend(OfficialBorder, OfficialAccentHover, p);
            else
                border = Blend(CandidatBorder, CandidatAccentHover, p);

            using (var pen = new Pen(border, borderW))
            {
                float half = borderW / 2f;
                g.DrawRectangle(pen, half, half, Width - borderW, Height - borderW);
            }

            if (interactive && p > 0.02f)
            {
                using (var brush = new SolidBrush(Color.FromArgb((int)(70 * p), 255, 255, 255)))
                    g.FillRectangle(brush, barW, 0, Width - barW, 2);

                using (var brush = new SolidBrush(Color.FromArgb((int)(35 * p), 30, 80, 200)))
                    g.FillRectangle(brush, barW, Height - 4, Width - barW, 4);
            }

            using (var pen = new Pen(AppTheme.CardSeparator, 1f))
                g.DrawLine(pen, 4, Height - 1, Width, Height - 1);
        }

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