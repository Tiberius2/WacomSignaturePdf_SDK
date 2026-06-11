using System;
using System.Windows.Forms;
using WacomSignaturePdf.Theme;

namespace WacomSignaturePdf.Forms
{
    // Hosts the Template signing mode. Wraps MainForm (headless) and exposes ISidebarPanel.
    public class TemplateSidebarPanel : UserControl, ISidebarPanel
    {
        private readonly ShellForm _shell;
        private readonly MainForm _inner;

        public TemplateSidebarPanel(ShellForm shell)
        {
            _shell = shell;
            Dock = DockStyle.Fill;

            _inner = new MainForm(
                personId: shell.InitPersonId,
                signerName: shell.InitSignerName,
                officialName: shell.InitOfficialName,
                officialRole: shell.InitOfficialRole,
                embeddedShell: shell);

            // Preluam sidebar-ul din MainForm si il montam in acest panel
            _inner.DetachSidebarInto(this);

            BackColor = AppTheme.Template.SidebarBg;
        }

        // ── ISidebarPanel ─────────────────────────────────────────────────────────
        public bool HasUnsavedWork => _inner.HasUnsavedWork;
        public bool CanResetToOriginal => false; // Template nu are reset la original
        public void ResetToOriginal() { }        // no-op
        public void SaveWork() => _inner.SaveProgressNow();
        public void Unload() => _inner.UnloadCurrent(silent: true);
        public void OnFileDrop(string path) { /* Template nu accepta drop direct */ }

        internal void ToggleMirror() => _inner.btnMirror_Click(this, EventArgs.Empty);
        internal bool MirrorActive => _inner._mirrorActive;

        protected override void Dispose(bool disposing)
        {
            if (disposing) _inner?.Dispose();
            base.Dispose(disposing);
        }
    }
}