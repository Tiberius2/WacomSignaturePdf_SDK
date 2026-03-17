using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using WacomSignaturePdf.Models;
using WacomSignaturePdf.Theme;

namespace WacomSignaturePdf.Controls
{
    /// <summary>
    /// This is a custom user control representing a signature slot as a card in the UI. 
    /// It displays the slot number, reason, page, signer name, and status (pending or signed).
    /// It also has hover and click interactions to indicate interactivity 
    /// and allow the user to select a slot for signing. 
    /// The card's appearance changes based on its state (pending vs signed) and user interactions (hover, press). 
    /// The control raises a CardClicked event when clicked, 
    /// passing the associated SignatureSlot for handling in the parent form.
    /// </summary>
    public class SignatureCardPanel : Panel
    {
        public event Action<SignatureSlot> CardClicked; // Raised when the card is clicked, passing the associated SignatureSlot.

        public SignatureSlot Slot { get; private set; }
        public bool Signed { get; private set; }

        private Label lblSlotNumber;
        private Label lblReason;
        private Label lblPage;
        private Label lblSigner;
        private Label lblStatus;
        private Label lblRequired;

        // ── Animation state ──
        private Timer _animTimer;
        private float _hoverProgress = 0f;
        private bool _isHovered = false;
        private bool _isPressed = false;

        public SignatureCardPanel(SignatureSlot slot)
        {
            Slot = slot;
            Size = new Size(354, slot.Required ? 86 : 72);
            BackColor = AppTheme.CardBase;
            Cursor = Cursors.Hand;
            DoubleBuffered = true;

            BuildControls(slot);
            WireMouseEvents(this);

            _animTimer = new Timer { Interval = 16 }; // ~60 fps
            _animTimer.Tick += OnAnimTick;
        }

        // ── Control UI creation ──
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
                Location = new Point(70, 8),
                Size = new Size(160, 20),
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                ForeColor = AppTheme.CardTitleText,
                BackColor = Color.Transparent,
                AutoEllipsis = true
            };

            lblPage = new Label
            {
                Text = $"PAGINA {slot.ResolvedPage}",
                Location = new Point(70, 30),
                Size = new Size(160, 16),
                Font = new Font("Segoe UI", 8f),
                ForeColor = AppTheme.CardPageText,
                BackColor = Color.Transparent
            };

            lblSigner = new Label
            {
                Text = slot.ResolvedSignerName ?? slot.SignerName,
                Location = new Point(70, 48),
                Size = new Size(160, 16),
                Font = new Font("Segoe UI", 8f, FontStyle.Italic),
                ForeColor = AppTheme.CardSignerText,
                BackColor = Color.Transparent,
                AutoEllipsis = true
            };

            lblStatus = new Label
            {
                Text = "IN ASTEPTARE",
                Location = new Point(238, 8),
                Size = new Size(76, 22),
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

            if (slot.Required)
            {
                lblRequired = new Label
                {
                    Text = "★ Required",
                    Location = new Point(70, 66),
                    Size = new Size(80, 14),
                    Font = new Font("Segoe UI", 7f),
                    ForeColor = AppTheme.CardRequired,
                    BackColor = Color.Transparent
                };
                Controls.Add(lblRequired);
            }
        }

        // ── Public methods ──
        /// <param name="signerName">
        /// The name actually embedded in the signature. When provided, updates
        /// the label so manual-entry names are reflected in the card UI.
        /// </param>
        public void MarkSigned(string signerName = null)
        {
            Signed = true;
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

        // ── "Animation" ──

        private void OnAnimTick(object sender, EventArgs e)
        {
            float target = _isHovered ? 1f : 0f;
            _hoverProgress += (target - _hoverProgress) * 0.12f;

            if (Math.Abs(_hoverProgress - target) < 0.01f)
            {
                _hoverProgress = target;
                _animTimer.Stop();
            }

            if (!Signed)
            {
                Color to = _isPressed ? AppTheme.CardPressed : AppTheme.CardHover;
                BackColor = Blend(AppTheme.CardBase, to, _hoverProgress);
            }

            Invalidate();
        }

        // ── Mouse wiring , just ui stuff ──

        private void WireMouseEvents(Control c)
        {
            c.MouseEnter += (s, e) => { if (!Signed) { _isHovered = true; _animTimer.Start(); } };
            c.MouseLeave += (s, e) =>
            {
                if (ClientRectangle.Contains(PointToClient(Cursor.Position))) return;
                _isHovered = false;
                _isPressed = false;
                _animTimer.Start();
            };
            c.MouseDown += (s, e) =>
            {
                if (Signed || e.Button != MouseButtons.Left) return;
                _isPressed = true;
                BackColor = AppTheme.CardPressed;
                _animTimer.Stop();
                Invalidate();
            };
            c.MouseUp += (s, e) => { if (!Signed) { _isPressed = false; _animTimer.Start(); } };
            c.MouseClick += (s, e) => { if (!Signed && e.Button == MouseButtons.Left) CardClicked?.Invoke(Slot); };

            foreach (Control child in c.Controls)
                WireMouseEvents(child);
        }

        // ── Paint ──
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            // Left accent bar
            Color accent = Signed
                ? AppTheme.CardAccentSigned
                : Blend(AppTheme.CardAccentPend, Color.FromArgb(80, 130, 220), _hoverProgress);

            using (var brush = new SolidBrush(accent))
                g.FillRectangle(brush, 0, 0, 4, Height);

            // Border
            Color border = Signed
                ? AppTheme.CardBorderSigned
                : Blend(AppTheme.CardBorderNormal, AppTheme.CardBorderHover, _hoverProgress);

            using (var pen = new Pen(border, 1f))
                g.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);

            // Bottom separator
            using (var pen = new Pen(AppTheme.CardSeparator, 1f))
                g.DrawLine(pen, 4, Height - 1, Width, Height - 1);
        }

        // ── Helpers ──
        private static Color Blend(Color from, Color to, float t)
        {
            t = Math.Max(0f, Math.Min(1f, t));
            return Color.FromArgb(
                (int)(from.A + (to.A - from.A) * t),
                (int)(from.R + (to.R - from.R) * t),
                (int)(from.G + (to.G - from.G) * t),
                (int)(from.B + (to.B - from.B) * t));
        }

        // We call this when the form is closing to stop timers and avoid cross-thread issues
        protected override void Dispose(bool disposing)
        {
            if (disposing) _animTimer?.Dispose();
            base.Dispose(disposing);
        }
    }
}