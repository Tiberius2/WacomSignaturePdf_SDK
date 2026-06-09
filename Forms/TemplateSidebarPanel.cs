using System;
using System.Windows.Forms;
using WacomSignaturePdf.Theme;

namespace WacomSignaturePdf.Forms
{
    /// <summary>
    /// UserControl care gazduieste intreaga logica si UI din MainForm (modul Template).
    /// Implementeaza ISidebarPanel pentru integrare cu ShellForm.
    /// MainForm este instantiat intern ca un UserControl headless — UI-ul sau
    /// este injectat direct in acest panel.
    /// </summary>
    public class TemplateSidebarPanel : UserControl, ISidebarPanel
    {
        private readonly ShellForm _shell;
        private readonly MainForm _inner;

        public TemplateSidebarPanel(ShellForm shell)
        {
            _shell = shell;
            Dock = DockStyle.Fill;

            // Instantiem MainForm in modul embedded — fara sa o afisam ca fereastra
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