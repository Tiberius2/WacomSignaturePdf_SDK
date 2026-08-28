using PdfiumViewer;
using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using WacomSignaturePdf.Config;
using WacomSignaturePdf.Controls;
using WacomSignaturePdf.Theme;

namespace WacomSignaturePdf.Forms
{
    /// <summary>
    /// Form unic care gazduieste ambele moduri (Template / FreeForm).
    /// Sidebar-ul e swap-uit la schimbarea modului.
    /// PdfViewer + previewHeader raman comune — nu se reincarca la switch.
    /// </summary>
    public partial class ShellForm : Form
    {
        // ── Mode ──────────────────────────────────────────────────────────────────
        public enum AppMode { Template, FreeForm }

        private AppMode _currentMode;

        // ── Shared viewer ─────────────────────────────────────────────────────────
        internal PdfViewer SharedPdfViewer { get; private set; }
        internal PdfDrawingOverlay SharedOverlay { get; private set; }

        // ── Current sidebar panel ─────────────────────────────────────────────────
        private ISidebarPanel _currentPanel;

        // ── Init params (passed to TemplateSidebarPanel) ──────────────────────────
        internal string InitPersonId { get; private set; }
        internal string InitSignerName { get; private set; }
        internal string InitOfficialName { get; private set; }
        internal string InitOfficialRole { get; private set; }

        // ── Singleton guard (same as original MainForm) ───────────────────────────
        private static ShellForm _instance;
        public static ShellForm Instance => _instance;

        // Constructor pentru apelul direct din Softone (Tip operatie: Dll Form).
        // Rezolva singur numele si rolul oficialului din sesiunea curenta.
        public ShellForm() : this(
            personId: string.Empty,
            signerName: string.Empty,
            officialName: ResolveOfficialName(),
            officialRole: ResolveOfficialRole(),
            initialMode: LoadLastMode())
        { }

        // Constructor complet — folosit de Program.cs si StandaloneProgram
        public ShellForm(string personId, string signerName,
                         string officialName, string officialRole,
                         AppMode initialMode)
        {
            _instance = this;
            InitPersonId = personId;
            InitSignerName = signerName;
            InitOfficialName = officialName;
            InitOfficialRole = officialRole;
            BuildLayout(initialMode);
            SwitchMode(initialMode, force: true);
        }

        private static string ResolveOfficialName()
        {
            try
            {
                if (S1.xSupp == null) return string.Empty;
                int userId = S1.xSupp.ConnectionInfo.UserId;
                var result = S1.xSupp.GetSQLDataSet(
                    $"SELECT NAME FROM USERS WHERE USERS.USERS = {userId}");
                return result?.Count > 0 ? result[0, "NAME"]?.ToString() ?? string.Empty : string.Empty;
            }
            catch { return string.Empty; }
        }

        private static string ResolveOfficialRole()
        {
            try { return S1.xSupp != null ? RoleHelper.GetRole(S1.xSupp.ConnectionInfo.UserId) : string.Empty; }
            catch { return string.Empty; }
        }

        // ── Mode persistence ──────────────────────────────────────────────────────
        private static string LastModeFile => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "WacomSignaturePdf", "last_mode.txt");

        public static AppMode LoadLastMode()
        {
            try
            {
                if (File.Exists(LastModeFile))
                    return File.ReadAllText(LastModeFile).Trim() == "FreeForm"
                        ? AppMode.FreeForm : AppMode.Template;
            }
            catch { }
            return AppMode.Template;
        }

        public static void SaveLastMode(AppMode mode)
        {
            try
            {
                var dir = Path.GetDirectoryName(LastModeFile);
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                File.WriteAllText(LastModeFile, mode.ToString());
            }
            catch { }
        }

        // ── Switch mode ───────────────────────────────────────────────────────────
        internal void SwitchMode(AppMode newMode, bool force = false)
        {
            if (!force && newMode == _currentMode) return;

            // 1. Verifica daca panelul curent are lucrari nesalvate
            if (!force && _currentPanel != null && _currentPanel.HasUnsavedWork)
            {
                using (var dlg = new ResetOrUnloadDialog(_currentPanel.CanResetToOriginal))
                {
                    var result = dlg.ShowDialog(this);
                    if (result == DialogResult.Cancel) return;
                    if (result == DialogResult.OK)
                    {
                        switch (dlg.SelectedAction)
                        {
                            case UnloadAction.SaveAndClose:
                                _currentPanel.SaveWork();
                                break;
                            case UnloadAction.ResetToOriginal:
                                _currentPanel.ResetToOriginal();
                                return; // ResetToOriginal nu face switch de mod
                            case UnloadAction.DiscardSession:
                            default:
                                break;
                        }
                    }
                }
            }

            // 2. Unload panelul curent
            if (_currentPanel != null)
            {
                _currentPanel.Unload();
                var oldCtrl = _currentPanel as Control;
                if (oldCtrl != null) panelSidebar.Controls.Remove(oldCtrl);
            }

            // 3. Creeaza noul panel
            ISidebarPanel newPanel;
            if (newMode == AppMode.Template)
                newPanel = new TemplateSidebarPanel(this);
            else
                newPanel = new FreeFormSidebarPanel(this);

            // 4. Monteaza in sidebar
            var newCtrl = (Control)newPanel;
            newCtrl.Dock = DockStyle.Fill;
            panelSidebar.Controls.Add(newCtrl);
            newCtrl.BringToFront();

            _currentPanel = newPanel;
            _currentMode = newMode;

            // 5. Aplica tema vizuala
            ApplyThemeColors(newMode);

            // 6. Caption previzualizare specific modului
            SetPreviewCaption(newMode == AppMode.Template
                ? "Previzualizare \u2014 alege documentul din dosarul candidatului"
                : "Previzualizare \u2014 trage un PDF sau apasa Deschide");

            // 7. Salveaza preferinta
            SaveLastMode(newMode);
        }

        // ── Helpers for sidebar panels ────────────────────────────────────────────
        internal void SetZoomEnabled(bool enabled)
        {
            if (btnZoomIn != null) btnZoomIn.Enabled = enabled;
            if (btnZoomOut != null) btnZoomOut.Enabled = enabled;
        }

        internal void ResetMirrorButton()
        {
            if (btnMirror == null) return;
            btnMirror.Text = "Oglindire";
            btnMirror.BackColor = AppTheme.MirrorOn;
            btnMirror.FlatAppearance.BorderColor = AppTheme.MirrorOnBorder;
        }

        // ── Mirror ────────────────────────────────────────────────────────────────
        private void BtnMirror_Click(object sender, EventArgs e)
        {
            if (_currentPanel is TemplateSidebarPanel tp)
            {
                tp.ToggleMirror();
                bool active = tp.MirrorActive;
                btnMirror.Text = active ? "Inchide Oglindire" : "Oglindire";
                btnMirror.BackColor = active ? AppTheme.MirrorOff : AppTheme.MirrorOn;
            }
            else if (_currentPanel is FreeFormSidebarPanel ff)
            {
                if (!ff.HasDocumentLoaded)
                {
                    MessageBox.Show(
                        "Incarcati un document inainte de a activa oglindirea.",
                        "Niciun document", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                ff.ToggleMirror();
                bool active = ff.MirrorActive;
                btnMirror.Text = active ? "Inchide Oglindire" : "Oglindire";
                btnMirror.BackColor = active ? AppTheme.MirrorOff : AppTheme.MirrorOn;
            }
        }

        /// <summary>
        /// Apelat de FreeFormSidebarPanel cand intra/iese din drawing mode.
        /// Actualizeaza header-ul si butonul de oglindire.
        /// </summary>
        internal void SetDrawingMode(bool active)
        {
            btnMirror.Enabled = !active;
            btnCancelDraw.Visible = active;
            panelPreviewHeader.Height = HeaderH;
            lblPreviewCaption.Text = active
                ? "MOD DESENARE — deseneaza o zona dreptunghiulara pe document"
                : (_currentPreviewCaption ?? "Previzualizare");
            panelPreviewHeader.BackColor = active
                ? Color.FromArgb(100, 40, 40)
                : AppTheme.HeaderBg;
            lblPreviewCaption.ForeColor = active ? Color.FromArgb(255, 200, 200) : AppTheme.PreviewCaption;
            if (active)
            {
                btnCancelDraw.Location = new Point(
                    (panelPreviewHeader.Width - btnCancelDraw.Width) / 2,
                    (HeaderH - btnCancelDraw.Height) / 2);
                btnCancelDraw.BringToFront();
            }
        }

        private string _currentPreviewCaption;

        internal new void SetPreviewCaption(string text)
        {
            _currentPreviewCaption = text;
            if (lblPreviewCaption != null && (panelPreviewHeader.BackColor == AppTheme.HeaderBg))
                lblPreviewCaption.Text = text;
        }
        private void BtnPillTemplate_Click(object sender, EventArgs e)
            => SwitchMode(AppMode.Template);

        private void BtnPillFreeForm_Click(object sender, EventArgs e)
            => SwitchMode(AppMode.FreeForm);

        // ── Form close ────────────────────────────────────────────────────────────
        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (_currentPanel != null && _currentPanel.HasUnsavedWork)
            {
                using (var dlg = new ResetOrUnloadDialog(_currentPanel.CanResetToOriginal))
                {
                    var result = dlg.ShowDialog(this);
                    if (result == DialogResult.Cancel) { e.Cancel = true; return; }
                    if (result == DialogResult.OK)
                    {
                        switch (dlg.SelectedAction)
                        {
                            case UnloadAction.SaveAndClose: _currentPanel.SaveWork(); break;
                            case UnloadAction.ResetToOriginal: _currentPanel.ResetToOriginal(); break;
                        }
                    }
                }
            }
            _currentPanel?.Unload();
            base.OnFormClosing(e);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                SharedPdfViewer?.Dispose();
                SharedOverlay?.Dispose();
            }
            base.Dispose(disposing);
        }
    }

    // ── ISidebarPanel contract ────────────────────────────────────────────────────
    public interface ISidebarPanel
    {
        bool HasUnsavedWork { get; }
        bool CanResetToOriginal { get; }
        void ResetToOriginal();
        void SaveWork();
        void Unload();
        void OnFileDrop(string path);
    }
}