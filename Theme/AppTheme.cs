using System.Drawing;

namespace WacomSignaturePdf.Theme
{
    public static class AppTheme
    {
        // ── Sidebar ───────────────────────────────────────────────────────────────
        public static readonly Color SidebarBg = Color.FromArgb(28, 48, 80);
        public static readonly Color SidebarTitleBg = Color.FromArgb(20, 36, 64);
        public static readonly Color SidebarSub = Color.FromArgb(130, 155, 185);
        public static readonly Color SidebarCardsBg = Color.FromArgb(36, 58, 94);
        public static readonly Color SectionLabel = Color.FromArgb(100, 130, 170);
        public static readonly Color SplitterColor = Color.FromArgb(180, 200, 230);

        // ── Content area ──────────────────────────────────────────────────────────
        public static readonly Color ContentBg = Color.FromArgb(245, 247, 250);
        public static readonly Color HeaderBg = Color.White;
        public static readonly Color HeaderBorder = Color.FromArgb(210, 218, 230);
        public static readonly Color PreviewCaption = Color.FromArgb(40, 60, 100);

        // ── Inputs ────────────────────────────────────────────────────────────────
        public static readonly Color InputBg = Color.FromArgb(245, 248, 255);
        public static readonly Color InputText = Color.FromArgb(30, 40, 60);

        // ── Accent buttons ────────────────────────────────────────────────────────
        public static readonly Color AccentBlue = Color.FromArgb(30, 100, 200);
        public static readonly Color AccentBorderBlue = Color.FromArgb(130, 165, 210);
        public static readonly Color AccentGreen = Color.FromArgb(30, 160, 90);
        public static readonly Color AccentGreenBorder = Color.FromArgb(110, 200, 150);
        public static readonly Color MirrorOn = Color.FromArgb(40, 70, 130);
        public static readonly Color MirrorOnBorder = Color.FromArgb(105, 120, 145);
        public static readonly Color MirrorOff = Color.FromArgb(160, 60, 40);
        public static readonly Color MirrorOffBorder = Color.FromArgb(163, 107, 96);

        // ── Cancel button ─────────────────────────────────────────────────────────
        public static readonly Color CancelBg = Color.FromArgb(186, 61, 30);
        public static readonly Color CancelFg = Color.FromArgb(247, 216, 208);
        public static readonly Color CancelBorder = Color.FromArgb(184, 130, 118);

        // ── Log ───────────────────────────────────────────────────────────────────
        public static readonly Color LogBg = Color.FromArgb(20, 36, 64);
        public static readonly Color LogText = Color.FromArgb(140, 200, 140);

        // ── Candidate status labels ───────────────────────────────────────────────
        public static readonly Color CandidateFound = Color.FromArgb(100, 220, 140);
        public static readonly Color CandidateError = Color.FromArgb(240, 100, 80);

        // ── Signature cards ───────────────────────────────────────────────────────
        public static readonly Color CardBase = Color.White;
        public static readonly Color CardHover = Color.FromArgb(230, 240, 255);
        public static readonly Color CardPressed = Color.FromArgb(210, 228, 255);
        public static readonly Color CardSigned = Color.FromArgb(238, 252, 242);
        public static readonly Color CardAccentPend = Color.FromArgb(30, 100, 200);
        public static readonly Color CardAccentSigned = Color.FromArgb(50, 180, 100);
        public static readonly Color CardBorderNormal = Color.FromArgb(210, 220, 235);
        public static readonly Color CardBorderHover = Color.FromArgb(100, 150, 230);
        public static readonly Color CardBorderSigned = Color.FromArgb(80, 190, 120);
        public static readonly Color CardSeparator = Color.FromArgb(220, 226, 236);
        public static readonly Color CardStatusPendBg = Color.FromArgb(255, 248, 220);
        public static readonly Color CardStatusPendFg = Color.FromArgb(160, 130, 30);
        public static readonly Color CardStatusSignBg = Color.FromArgb(220, 248, 230);
        public static readonly Color CardStatusSignFg = Color.FromArgb(30, 140, 70);
        public static readonly Color CardRequired = Color.FromArgb(200, 100, 30);
        public static readonly Color CardPageText = Color.FromArgb(120, 130, 150);
        public static readonly Color CardSignerText = Color.FromArgb(80, 110, 160);
        public static readonly Color CardTitleText = Color.FromArgb(30, 40, 60);

        // ── Dropdown ──────────────────────────────────────────────────────────────
        public static readonly Color DropdownBgNormal = Color.FromArgb(245, 248, 255);
        public static readonly Color DropdownBgSelected = Color.FromArgb(215, 232, 255);
        public static readonly Color DropdownText = Color.FromArgb(30, 40, 60);
        public static readonly Color DropdownBorder = Color.FromArgb(150, 180, 230);
        public static readonly Color DropdownSeparator = Color.FromArgb(220, 228, 240);
        public static readonly Color DropdownDisabled = Color.FromArgb(200, 210, 225);

        /// <summary>
        /// Cycles through these 6 colors for dropdown item accents.
        /// Use: ItemColors[index % ItemColors.Length]
        /// </summary>
        public static readonly Color[] DropdownItemColors =
        {
            Color.FromArgb( 30, 100, 200),  // blue
            Color.FromArgb(160,  60, 180),  // purple
            Color.FromArgb(200, 100,  30),  // orange
            Color.FromArgb( 30, 160,  90),  // green
            Color.FromArgb(180,  40,  60),  // red
            Color.FromArgb( 20, 140, 160),  // teal
        };

        // ── Mirror form ───────────────────────────────────────────────────────────
        public static readonly Color MirrorBg = Color.FromArgb(30, 30, 30);
        public static readonly Color MirrorFooterBg = Color.FromArgb(20, 36, 64);
        public static readonly Color MirrorFooterFg = Color.FromArgb(160, 180, 220);

        // ── Error Dialog ──
        public static readonly Color FileNotFoundHeaderColor = Color.FromArgb(180, 60, 40);
        public static readonly Color DeviceNotConnectedHeaderColor = Color.FromArgb(180, 110, 20);
        public static readonly Color DocumentFinalizedHeaderColor = Color.FromArgb(30, 140, 70);
        public static readonly Color DocumentSignedNotSealedHeaderColor = Color.FromArgb(200, 140, 20);
        public static readonly Color DefaultHeaderColor = Color.FromArgb(28, 48, 80);

        // ── Toggle Switch ──
        public static readonly Color SwitchOn = Color.FromArgb(33, 150, 243);
        public static readonly Color SwitchOff = Color.FromArgb(80, 80, 80);
    }
}