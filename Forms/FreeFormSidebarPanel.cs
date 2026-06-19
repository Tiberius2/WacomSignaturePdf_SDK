using Newtonsoft.Json;
using PdfiumViewer;
using PdfSharp.Drawing;
using PdfSharp.Pdf;
using PdfSharp.Pdf.Advanced;
using PdfSharp.Pdf.IO;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using WacomSignaturePdf.Config;
using WacomSignaturePdf.Controls;
using WacomSignaturePdf.Models;
using WacomSignaturePdf.Services;
using WacomSignaturePdf.Theme;

namespace WacomSignaturePdf.Forms
{
    /// <summary>
    /// Free-form signing form: load any PDF, draw signature zones, configure and sign.
    /// Independent of template system. Persists slot configuration as JSON in PDF attachment.
    /// Output saved to: RecruitmentDocsPath/Documente Semnate Electronic - Semnatura Libera/
    /// </summary>
    public partial class FreeFormSidebarPanel : UserControl, ISidebarPanel
    {
        #region Fields

        private string _loadedPdfPath;      // path of the currently loaded PDF (working copy)
        private string _originalPdfPath;    // original file chosen by user (never modified)

        // Configured slots (from dialog + JSON attachment)
        private List<FreeFormSlot> _slots = new List<FreeFormSlot>();
        private List<SignatureCardPanel> _cards = new List<SignatureCardPanel>();

        // SignatureService for actual Wacom capture + PDF embedding
        private SignatureService _signatureService;

        private bool _captureInProgress;

        // Layout helper — set in BuildContentPanel, used in BuildForm

        // Output folder names inside FreeFormDocumentsPath
        private const string FolderSemnatComplet = "Documente Semnate Complet";
        private const string FolderInProces = "Documente In Proces";
        private const string FolderInOriginal = "Documente In Original";

        // Flag: originalul a fost deja copiat in FolderInOriginal
        private bool _originalBackedUp;
        private string _originalDocHash; // SHA-256 al documentului original curat

        // Path-ul documentului in "Documente In Proces" (dupa primul MoveToInProces)
        private string _inProcesPath;
        // Numele fișierului original (fără sufix _InProces) — pentru ResetToOriginal
        private string _originalFileName;

        #endregion

        #region Constructor

        private readonly ShellForm _shell;
        private string _officialRole => _shell.InitOfficialRole ?? "";

        public FreeFormSidebarPanel(ShellForm shell)
        {
            _shell = shell;
            DoubleBuffered = true;
            BuildSidebarControls();
            BuildSidebar();
        }

        // ── ISidebarPanel ─────────────────────────────────────────────────────────
        public bool HasUnsavedWork =>
            _loadedPdfPath != null && _slots != null && _slots.Count > 0;

        public bool HasDocumentLoaded => _loadedPdfPath != null;

        public bool CanResetToOriginal => false;

        public void SaveWork()
        {
            if (_loadedPdfPath == null) return;
            try { SaveToInProces(); }
            catch { }
            UnloadDocument();
        }

        public void ResetToOriginal() { /* Nu este disponibila in FreeForm */ }


        public void Unload()
        {
            UnloadDocument();
        }

        public void OnFileDrop(string path) => LoadPdf(path);

        #endregion

        #region File Loading



        // ── Default mode (persistent in AppData) ─────────────────────────────────
        // ── Draw instructions dialog ──────────────────────────────────────────────

        private static string DrawHintSkipFile =>
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "WacomSignaturePdf", "skip_draw_hint.txt");

        private void ShowDrawInstructions()
        {
            bool skip = File.Exists(DrawHintSkipFile);

            if (!skip)
            {
                using (var dlg = new DrawInstructionsDialog())
                {
                    dlg.ShowDialog(_shell);
                    if (dlg.DontShowAgain)
                    {
                        try
                        {
                            var dir = Path.GetDirectoryName(DrawHintSkipFile);
                            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                            File.WriteAllText(DrawHintSkipFile, "1");
                        }
                        catch { }
                    }
                }
            }

            pdfOverlay.EnableDrawing(true);
            _shell.SetDrawingMode(true);
            SetSidebarButtonsEnabled(false);
        }

        private void ExitDrawingMode()
        {
            pdfOverlay.EnableDrawing(false);
            _shell.SetDrawingMode(false);
            SetSidebarButtonsEnabled(true);
        }

        private void SetSidebarButtonsEnabled(bool enabled)
        {
            btnOpenFile.Enabled = enabled;
            btnOpenInProces.Enabled = enabled;
            btnOpenFolder.Enabled = enabled;
            btnDrawZone.Enabled = enabled;
            btnCancelLoad.Enabled = enabled && btnCancelLoad.Visible;
            btnSaveAndClose.Enabled = enabled && _slots?.Count > 0;
            btnFinish.Enabled = enabled && (_cards?.Any(c => c.Signed) ?? false);
            chkManualSigner.Enabled = enabled && _slots?.Count > 0;
        }

        public void CancelDrawing()
        {
            ExitDrawingMode();
        }

        // ── Mirror (FreeForm) ─────────────────────────────────────────────────────
        private MirrorForm _mirrorForm;
        private System.Windows.Forms.Timer _mirrorSyncTimer;
        private int _mirrorLastPage = -1;
        private double _mirrorLastZoom = -1;

        public bool MirrorActive => _mirrorForm != null && !_mirrorForm.IsDisposed && _mirrorForm.Visible;

        public void ToggleMirror()
        {
            if (MirrorActive)
            {
                _mirrorSyncTimer?.Stop();
                _mirrorForm.Close();
                _mirrorForm = null;
                return;
            }

            // Alege ecranul secundar
            var screens = System.Windows.Forms.Screen.AllScreens;
            var primary = System.Windows.Forms.Screen.PrimaryScreen;
            var target = screens.Length > 1
                ? System.Array.Find(screens, s => !s.Primary) ?? primary
                : primary;

            if (_mirrorForm == null || _mirrorForm.IsDisposed)
                _mirrorForm = new MirrorForm();

            _mirrorForm.FormClosed += (s, e) =>
            {
                _mirrorSyncTimer?.Stop();
                _mirrorForm = null;
            };

            // Incarca documentul curent
            if (_loadedPdfPath != null && File.Exists(_loadedPdfPath))
                _mirrorForm.LoadFromPath(_loadedPdfPath);

            _mirrorForm.ShowOnScreen(target);

            // Sincronizare pagina/zoom/scroll
            if (_mirrorSyncTimer == null)
            {
                _mirrorSyncTimer = new System.Windows.Forms.Timer { Interval = 150 };
                _mirrorSyncTimer.Tick += MirrorSyncTick;
            }
            _mirrorSyncTimer.Start();
        }

        private void MirrorSyncTick(object sender, EventArgs e)
        {
            if (!MirrorActive) { _mirrorSyncTimer?.Stop(); return; }
            var renderer = pdfOverlay?.Renderer;
            if (renderer == null) return;

            try
            {
                int page = renderer.Page;
                double zoom = renderer.Zoom;
                if (page != _mirrorLastPage) { _mirrorLastPage = page; _mirrorForm.SyncPage(page); }
                if (Math.Abs(zoom - _mirrorLastZoom) > 0.001) { _mirrorLastZoom = zoom; _mirrorForm.SyncZoom(zoom); }
            }
            catch { }
        }

        private void BrowseForFile()
        {
            using (var dlg = new OpenFileDialog
            {
                Title = "Selecteaza un document PDF",
                Filter = "PDF Files (*.pdf)|*.pdf",
                CheckFileExists = true
            })
            {
                if (dlg.ShowDialog(_shell) == DialogResult.OK)
                    LoadPdf(dlg.FileName);
            }
        }

        private void OpenFreeFormFolder()
        {
            string path = AppConfig.FreeFormDocumentsPath;
            if (string.IsNullOrEmpty(path) || !Directory.Exists(path))
            {
                MessageBox.Show("Dosarul FreeFormDocumentsPath nu este configurat sau nu există.",
                    "Dosar negăsit", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            System.Diagnostics.Process.Start("explorer.exe", path);
        }

        private void BrowseInProces()
        {
            string root = AppConfig.FreeFormDocumentsPath;
            if (string.IsNullOrEmpty(root))
            {
                MessageBox.Show(
                    "Variabila de sistem 'FreeFormDocumentsPath' nu este configurata pe aceasta masina.\n\n" +
                    "Contactati administratorul IT.",
                    "Configurare lipsa", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            string inProcesDir = Path.Combine(root, FolderInProces);
            if (!Directory.Exists(inProcesDir))
            {
                MessageBox.Show(
                    $"Folderul 'Documente In Proces' nu exista inca:\n{inProcesDir}",
                    "Folder negasit", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            using (var dlg = new InProcesPickerDialog(inProcesDir))
            {
                if (dlg.ShowDialog(_shell) == DialogResult.OK && dlg.SelectedPath != null)
                    LoadPdf(dlg.SelectedPath);
            }
        }

        private void LoadPdf(string path)
        {
            try
            {
                // Verifica FreeFormDocumentsPath configurat
                if (string.IsNullOrEmpty(AppConfig.FreeFormDocumentsPath))
                {
                    MessageBox.Show(
                        "Variabila de sistem 'FreeFormDocumentsPath' nu este configurata pe aceasta masina.\n\n" +
                        "Contactati administratorul IT pentru a configura calea catre folderul de documente FreeForm.",
                        "Configurare lipsa", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                // Verifica daca documentul e finalizat
                var existingState = SignatureService.ReadSigningState(path);
                if (existingState?.Finalized == true)
                {
                    MessageBox.Show(
                        "Acest document a fost deja finalizat si exportat.\n\n" +
                        "Documentele finalizate nu pot fi redeschise pentru semnare.",
                        "Document Finalizat",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                    return;
                }

                // Verifica daca documentul e de tip Template — blocat in FreeForm
                if (existingState?.Source == "Template")
                {
                    MessageBox.Show(
                        "Acest document a fost creat prin fluxul Sablon si nu poate fi deschis in modul Semnatura Libera.\n\n" +
                        "Comutati pe modul Sablon pentru a continua semnarea acestui document.",
                        "Document Sablon",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                    return;
                }

                // If a document is already loaded, unload it first
                if (_loadedPdfPath != null) UnloadDocument();

                // Detectam daca documentul vine din InProces
                string inProcesDir = Path.Combine(AppConfig.FreeFormDocumentsPath, FolderInProces);
                bool isFromInProces = Directory.Exists(inProcesDir) &&
                    Path.GetFullPath(Path.GetDirectoryName(path) ?? "")
                        .Equals(Path.GetFullPath(inProcesDir), StringComparison.OrdinalIgnoreCase);

                string workingPath;
                if (isFromInProces)
                {
                    // Lucram direct pe fisierul din InProces — nu copiem in temp
                    workingPath = path;
                }
                else
                {
                    // Document nou — copie temp pana la primul slot (cand e mutat in InProces)
                    workingPath = Path.Combine(
                        Path.GetTempPath(),
                        $"freeform_{DateTime.Now:yyyyMMdd_HHmmss_fff}_{Path.GetFileName(path)}");
                    File.Copy(path, workingPath, overwrite: true);
                }

                _originalPdfPath = path;
                _loadedPdfPath = workingPath;
                _originalBackedUp = false;
                _originalDocHash = null;
                _inProcesPath = null;
                _originalFileName = Path.GetFileNameWithoutExtension(path);

                if (isFromInProces)
                {
                    _inProcesPath = path;
                    _originalBackedUp = true;
                    var state = SignatureService.ReadSigningState(path);
                    _originalDocHash = state?.OriginalDocumentHash;
                    // Recuperam numele original din signing state (fara sufix _InProces)
                    if (!string.IsNullOrEmpty(state?.OriginalFileName))
                        _originalFileName = state.OriginalFileName;
                }

                // Load in overlay viewer
                pdfOverlay.LoadDocument(workingPath, fitPage: true);
                _shell.SetZoomEnabled(true);


                // Update sidebar
                lblLoadedFile.Text = Path.GetFileName(path);
                lblLoadedFile.ForeColor = Color.FromArgb(180, 230, 180);
                _shell.SetPreviewCaption(Path.GetFileName(path));
                btnCancelLoad.Visible = true;
                btnSaveAndClose.Enabled = true;
                chkManualSigner.Checked = false;
                chkManualSigner.Enabled = false;

                // Initialize SignatureService with an empty slot list (will be populated from config)
                _slots.Clear();
                _cards.Clear();
                cardsPanel.Controls.Clear();
                UpdateProgress();

                // Read existing slot config from the ORIGINAL file (not temp copy)
                // so we pick up any previously saved configuration
                var existing = ReadSlotConfig(path);
                if (existing != null && existing.Count > 0)
                {
                    _slots = existing;
                    RebuildAllCards();
                    lblDrawHint.Text = $"{existing.Count} semnatura(ri) incarcate din document.";
                }
                else
                {
                    lblDrawHint.Text = "Deseneaza un dreptunghi pe document pentru a plasa o semnatura.";
                }

                // Restore signed state din JSON attachment (fara SignatureService)
                RestoreSignedState();
                RefreshPreviewSlots();

                lblProgress.Text = $"Document incarcat. {_slots.Count} sloturi configurate.";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Eroare la incarcarea documentului:\n{ex.Message}",
                    "Eroare", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void UnloadDocument()
        {
            _signatureService?.Dispose();
            _signatureService = null;

            pdfOverlay.UnloadDocument();
            pdfOverlay.ClearPreviewSlots();
            _shell.SetZoomEnabled(false);


            // Clean up temp copy (nu stergem daca e fisierul din InProces)
            if (_loadedPdfPath != null && _inProcesPath == null && File.Exists(_loadedPdfPath))
                try { File.Delete(_loadedPdfPath); } catch { }

            _loadedPdfPath = null;
            _originalPdfPath = null;
            _inProcesPath = null;
            _originalFileName = null;

            _slots.Clear();
            _cards.Clear();
            cardsPanel.Controls.Clear();
            UpdateProgress();

            lblLoadedFile.Text = "Niciun document incarcat.";
            lblLoadedFile.ForeColor = AppTheme.SidebarSub;
            _shell.SetPreviewCaption("Previzualizare — trage un PDF sau apasa Deschide");
            lblDrawHint.Text = "Deseneaza un dreptunghi pe document pentru a plasa o semnatura.";
            lblProgress.Text = "";
            btnCancelLoad.Visible = false;
            btnSaveAndClose.Enabled = false;
            btnFinish.Enabled = false;
            chkManualSigner.Checked = false;
            chkManualSigner.Enabled = false;
            ExitDrawingMode();
        }

        #endregion

        #region Original Backup + InProces

        /// <summary>
        /// La primul slot configurat:
        /// 1. Copiaza originalul curat in FolderInOriginal
        /// 2. Muta copia de lucru in FolderInProces (devine noul _loadedPdfPath)
        /// </summary>
        private void BackupOriginalOnce()
        {
            if (_originalBackedUp || _originalPdfPath == null) return;
            try
            {
                string root = AppConfig.FreeFormDocumentsPath;
                if (string.IsNullOrEmpty(root)) return;

                // Daca documentul are deja signing-state FreeForm, a mai trecut prin flux
                var existingState = SignatureService.ReadSigningState(_originalPdfPath);
                if (existingState?.Source == "FreeForm")
                {
                    _originalDocHash = existingState.OriginalDocumentHash;
                    _originalBackedUp = true;
                    // Asiguram ca e in InProces
                    if (_inProcesPath == null) MoveToInProces();
                    return;
                }

                // 1. Backup original curat → FolderInOriginal (comprimat cu Ghostscript)
                string origDir = Path.Combine(root, FolderInOriginal);
                Directory.CreateDirectory(origDir);
                string baseName = Path.GetFileNameWithoutExtension(_originalPdfPath);
                _originalFileName = baseName;
                string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                string origDest = Path.Combine(origDir, $"{baseName}_orig_{stamp}.pdf");

                // Incearca compresie Ghostscript; fallback la copie simpla daca nu e disponibil
                if (!CompressPdfWithGhostscript(_originalPdfPath, origDest))
                    File.Copy(_originalPdfPath, origDest, overwrite: false);

                // Calculeaza SHA-256
                using (var sha = System.Security.Cryptography.SHA256.Create())
                using (var fs = File.OpenRead(_originalPdfPath))
                {
                    var hashBytes = sha.ComputeHash(fs);
                    _originalDocHash = BitConverter.ToString(hashBytes).Replace("-", "").ToLower();
                }

                _originalBackedUp = true;

                // 2. Muta copia de lucru in FolderInProces
                MoveToInProces();
            }
            catch { /* non-fatal */ }
        }

        /// <summary>
        /// Copiaza/muta _loadedPdfPath in FolderInProces si actualizeaza _loadedPdfPath + _inProcesPath.
        /// </summary>
        private void MoveToInProces()
        {
            if (_loadedPdfPath == null) return;
            try
            {
                string root = AppConfig.FreeFormDocumentsPath;
                if (string.IsNullOrEmpty(root)) return;

                string inProcesDir = Path.Combine(root, FolderInProces);
                Directory.CreateDirectory(inProcesDir);

                string baseName = Path.GetFileNameWithoutExtension(_originalPdfPath ?? _loadedPdfPath);
                string destPath = Path.Combine(inProcesDir, $"{baseName}_InProces.pdf");

                // Genereaza nume unic daca exista deja
                if (File.Exists(destPath))
                {
                    string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss_fff");
                    destPath = Path.Combine(inProcesDir, $"{baseName}_InProces_{stamp}.pdf");
                }

                File.Copy(_loadedPdfPath, destPath, overwrite: false);

                // Sterge temp-ul anterior daca nu era deja in InProces
                if (_inProcesPath == null && File.Exists(_loadedPdfPath))
                    try { File.Delete(_loadedPdfPath); } catch { }

                _loadedPdfPath = destPath;
                _inProcesPath = destPath;

                // Reincarca viewer din noua locatie
                pdfOverlay.LoadDocumentRestoring(destPath);
            }
            catch { /* non-fatal */ }
        }

        /// <summary>
        /// Salveaza starea curenta in fisierul din InProces (fara finalizare).
        /// </summary>
        private void SaveToInProces()
        {
            if (_loadedPdfPath == null) return;
            if (_inProcesPath == null) return; // nu a trecut inca prin BackupOriginalOnce

            // _loadedPdfPath == _inProcesPath deja, signing state e actualizat in timp real
            // Nimic de facut suplimentar — fisierul e deja scris la fiecare semnatura/slot
        }

        private static bool CompressPdfWithGhostscript(string sourcePath, string destPath)
        {
            try
            {
                var psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "gswin32c.exe",
                    Arguments = $"-sDEVICE=pdfwrite -dCompatibilityLevel=1.4 -dPDFSETTINGS=/ebook " +
                                $"-dNOPAUSE -dQUIET -dBATCH " +
                                $"-sOutputFile=\"{destPath}\" \"{sourcePath}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardError = true
                };
                using (var proc = System.Diagnostics.Process.Start(psi))
                {
                    proc.WaitForExit();
                    return proc.ExitCode == 0 && File.Exists(destPath);
                }
            }
            catch { return false; }
        }

        #endregion

        #region Rectangle Drawn → Slot Config Dialog

        private void OnRectangleDrawn(DrawnRectangle rect)
        {
            if (_loadedPdfPath == null) return;

            // Iese din drawing mode inainte de dialog
            ExitDrawingMode();

            int nextId = _slots.Count == 0 ? 1 : _slots.Max(s => s.SignatureId) + 1;

            using (var dlg = new FreeFormSlotDialog(nextId, rect.X, rect.Y, rect.W, rect.H, rect.Page))
            {
                if (dlg.ShowDialog(_shell) != DialogResult.OK) return;

                // Guard: SignatureId must be unique
                if (_slots.Any(s => s.SignatureId == dlg.SignatureId))
                {
                    MessageBox.Show(
                        $"Exista deja o semnatura cu ID #{dlg.SignatureId}. Alegeti un ID diferit.",
                        "ID duplicat", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                var slot = new FreeFormSlot
                {
                    SignatureId = dlg.SignatureId,
                    SignerName = dlg.SignerName,
                    Reason = dlg.Reason,
                    Party = dlg.Party,
                    OfficialRole = dlg.OfficialRole,
                    Required = dlg.Required,
                    Biometric = dlg.Biometric,
                    Page = rect.Page,
                    X = rect.X,
                    Y = rect.Y,
                    W = rect.W,
                    H = rect.H
                };

                _slots.Add(slot);

                // Persist slot config into working copy (temp pana la SaveAndClose)
                SaveSlotConfigToPdf(_loadedPdfPath);

                // Reincarca viewer
                pdfOverlay.LoadDocumentRestoring(_loadedPdfPath);
                RefreshPreviewSlots();

                // Add card (unsigned)
                AddCard(slot, signed: false, actualSignerName: null);
                chkManualSigner.Enabled = true;
                UpdateProgress();

                if (dlg.SignImmediately)
                {
                    if (!string.IsNullOrEmpty(slot.OfficialRole)
                        && slot.Party == "Official"
                        && slot.OfficialRole != _officialRole
                        && !chkManualSigner.Checked)
                    {
                        MessageBox.Show(
                            $"Aceasta semnatura este asignata rolului \"{slot.OfficialRole}\".\n" +
                            $"Rolul dvs. curent este \"{(string.IsNullOrEmpty(_officialRole) ? "nespecificat" : _officialRole)}\".\n\n" +
                            "Slotul a fost adaugat la lista dar nu puteti semna personal.",
                            "Rol diferit", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    }
                    else
                    {
                        CaptureForSlot(slot);
                    }
                }
            }
        }

        #endregion

        #region Signature Cards

        private void RebuildAllCards()
        {
            cardsPanel.Controls.Clear();
            _cards.Clear();
            foreach (var slot in _slots)
                AddCard(slot, signed: false, actualSignerName: null);
            chkManualSigner.Enabled = _cards.Count > 0;
            UpdateProgress();
        }

        private void AddCard(FreeFormSlot slot, bool signed, string actualSignerName)
        {
            var sigSlot = SlotToSignatureSlot(slot);
            var card = new SignatureCardPanel(sigSlot, showDeleteButton: true);
            card.Width = cardsPanel.Width - 12;

            if (signed && actualSignerName != null)
                card.MarkSigned(actualSignerName);
            else
                ApplyRoleRestriction(card, slot);

            card.CardClicked += s => OnCardClicked(slot);
            card.DeleteClicked += s => OnDeleteSlot(slot);
            cardsPanel.Controls.Add(card);
            _cards.Add(card);

            int y = 6;
            foreach (SignatureCardPanel c in cardsPanel.Controls)
            {
                c.Location = new Point(6, y);
                y += c.Height + 6;
            }
        }

        private void ApplyRoleRestriction(SignatureCardPanel card, FreeFormSlot slot)
        {
            bool imputernicire = chkManualSigner.Checked;
            // Sloturi Official cu rol specific — restricted daca userul nu are rolul si nu e imputernicire
            bool roleMatch = slot.Party != "Official"
                          || string.IsNullOrEmpty(slot.OfficialRole)
                          || slot.OfficialRole == _officialRole;
            card.SetRoleRestricted(!imputernicire && !roleMatch);
        }

        private void OnDeleteSlot(FreeFormSlot slot)
        {
            var card = FindCard(slot.SignatureId);
            if (card == null || card.Signed) return;

            var result = MessageBox.Show(
                $"Stergi semnatura \"{slot.Reason}\" (pagina {slot.Page})?\nAceasta actiune nu poate fi anulata.",
                "Confirma stergere", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
            if (result != DialogResult.Yes) return;

            // Scoate din liste
            _slots.Remove(slot);
            _cards.Remove(card);
            cardsPanel.Controls.Remove(card);
            card.Dispose();

            // Reflow pozitii
            int y = 6;
            foreach (SignatureCardPanel c in cardsPanel.Controls)
            {
                c.Location = new Point(6, y);
                y += c.Height + 6;
            }

            // Salveaza starea actualizata in PDF
            if (_loadedPdfPath != null)
            {
                try
                {
                    using (var ms = new System.IO.MemoryStream(File.ReadAllBytes(_loadedPdfPath)))
                    using (var doc = PdfSharp.Pdf.IO.PdfReader.Open(ms, PdfSharp.Pdf.IO.PdfDocumentOpenMode.Modify))
                    {
                        WriteSigningStateToPdfDoc(doc, justSignedSlot: null, finalized: false);
                        doc.Save(_loadedPdfPath);
                    }
                }
                catch { }
            }

            chkManualSigner.Enabled = _cards.Count > 0;
            UpdateProgress();
            CheckFinishEnabled();
            RefreshPreviewSlots();
        }

        private void OnCardClicked(FreeFormSlot slot)
        {
            if (_captureInProgress) return;
            var card = FindCard(slot.SignatureId);
            if (card == null || card.Signed || card.RoleRestricted) return;

            // Verifica rolul daca slotul are un rol oficial specificat
            if (!string.IsNullOrEmpty(slot.OfficialRole)
                && slot.Party == "Official"
                && slot.OfficialRole != _officialRole
                && !chkManualSigner.Checked)
            {
                var result = MessageBox.Show(
                    $"Aceasta semnatura este asignata rolului \"{slot.OfficialRole}\".\n" +
                    $"Rolul dvs. curent este \"{(string.IsNullOrEmpty(_officialRole) ? "nespecificat" : _officialRole)}\".\n\n" +
                    "Doriti sa continuati totusi?",
                    "Rol diferit", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                if (result != DialogResult.Yes) return;
            }

            CaptureForSlot(slot);
        }

        private SignatureCardPanel FindCard(int signatureId)
        {
            for (int i = 0; i < _cards.Count; i++)
                if (_cards[i].Slot.SignatureId == signatureId)
                    return _cards[i];
            return null;
        }

        #endregion

        #region Signature Capture

        private void CaptureForSlot(FreeFormSlot slot)
        {
            if (_loadedPdfPath == null) return;
            if (_captureInProgress) return;

            try
            {
                _signatureService?.Dispose();
                _signatureService = null;
                var sigSlots = _slots.Select(SlotToSignatureSlot).ToList();
                _signatureService = new SignatureService(
                    _loadedPdfPath, artifactsRootDir: "", allSlots: sigSlots);
            }
            catch (IOException ex)
            {
                // Reload viewer inainte de a arata eroarea
                pdfOverlay.LoadDocumentRestoring(_loadedPdfPath);
                MessageBox.Show(ex.Message, "Fisier blocat", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            SetCardsEnabled(false);
            _captureInProgress = true;

            bool isImputernicire = chkManualSigner.Checked;
            string signerName = slot.SignerName;

            if (isImputernicire)
            {
                using (var dlg = new SignerNameDialog(slot.Reason, slot.SignerName))
                {
                    if (dlg.ShowDialog(_shell) != DialogResult.OK)
                    {
                        _captureInProgress = false;
                        SetCardsEnabled(true);
                        return;
                    }
                    signerName = dlg.SignerName;
                }
            }

            var thread = new Thread(() =>
            {
                Exception caughtEx = null;
                bool cancelled = false;

                try
                {
                    _signatureService.CaptureAndEmbed(
                        slot.SignatureId, slot.Party,
                        signerName, slot.Reason,
                        slot.Page,
                        slot.X, slot.Y, slot.W, slot.H,
                        isImputernicire: isImputernicire);
                }
                catch (OperationCanceledException) { cancelled = true; }
                catch (Exception ex) { caughtEx = ex; }

                Invoke(new Action(() =>
                {
                    _captureInProgress = false;
                    SetCardsEnabled(true);

                    if (cancelled)
                    {
                        // Wacom cancel — reload viewer (fusese descarcat pentru scriere)
                        pdfOverlay.LoadDocumentRestoring(_loadedPdfPath);
                        return;
                    }

                    if (caughtEx != null)
                    {
                        pdfOverlay.LoadDocumentRestoring(_loadedPdfPath);
                        if (IsWacomMissingError(caughtEx))
                        {
                            MessageBox.Show(
                                "Dispozitivul de semnatura nu este conectat sau nu este disponibil.\n\n" +
                                "Conectati tableta Wacom si reincercati.",
                                "Dispozitiv Neconectat",
                                MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        }
                        else
                        {
                            MessageBox.Show(caughtEx.Message, "Eroare Captura",
                                MessageBoxButtons.OK, MessageBoxIcon.Error);
                        }
                        return;
                    }

                    // Success
                    var card = FindCard(slot.SignatureId);
                    string displayName = isImputernicire ? signerName + " *" : signerName;
                    card?.MarkSigned(displayName);
                    UpdateProgress();
                    CheckFinishEnabled();
                    RefreshPreviewSlots();

                    // 1. Citim signing state INAINTE ca SaveIntermediateNoState sa-l stearga
                    var stateBeforeWrite = SignatureService.ReadSigningState(_loadedPdfPath);
                    // 2. Scrie semnatura biometrica in PDF (sterge signing state intern)
                    _signatureService.SaveIntermediateNoState();
                    // 3. Rescrie signing state FreeForm corect (cu semnatura noua + cele anterioare)
                    AttachSigningStateToFile(slot, stateBeforeWrite);
                    pdfOverlay.LoadDocumentRestoring(_loadedPdfPath);
                }));
            });

            thread.SetApartmentState(ApartmentState.STA);
            thread.IsBackground = true;
            thread.Start();
        }

        private static bool IsWacomMissingError(Exception ex)
        {
            string msg = ex.Message + ex.GetType().Name;
            return msg.Contains("STU") || msg.Contains("pad") || msg.Contains("Pad")
                || msg.Contains("device") || msg.Contains("DynCaptPadError")
                || msg.Contains("DynCaptNotLicensed")
                || msg.Contains("COMException") || msg.Contains("Florentis");
        }




        private void LoadDocumentWithRestore(string path)
        {
            try { pdfOverlay.LoadDocumentRestoring(path); }
            catch { pdfOverlay.LoadDocument(path, fitPage: true); }
        }

        private void AttachSigningStateAfterWrite(FreeFormSlot slot)
        {
            try
            {
                byte[] pdfBytes = File.ReadAllBytes(_loadedPdfPath);
                string tempOut = _loadedPdfPath + ".tmp";
                using (var ms = new MemoryStream(pdfBytes))
                using (var doc = PdfSharp.Pdf.IO.PdfReader.Open(ms, PdfSharp.Pdf.IO.PdfDocumentOpenMode.Modify))
                {
                    RemoveStateAttachment(doc);
                    AttachSigningStateDirect(doc, slot);
                    doc.Save(tempOut);
                }
                File.Delete(_loadedPdfPath);
                File.Move(tempOut, _loadedPdfPath);
            }
            catch { }
        }

        #endregion

        #region Signing State (freeform-specific — parallel to SignatureService.AttachSigningState)

        /// <summary>
        /// Reads signing-state.json from the PDF to restore which slots were already signed.
        /// </summary>
        private void RestoreSignedState()
        {
            if (_loadedPdfPath == null) return;

            var state = SignatureService.ReadSigningState(_loadedPdfPath);
            if (state?.Slots == null) return;

            foreach (var entry in state.Slots.Where(s => s.Signed))
            {
                var card = FindCard(entry.SignatureId);
                card?.MarkSigned(entry.ActualSignerName ?? entry.SignerName);
            }

            // Re-aplica role restriction pe cardurile nesemnate
            ReflowCards();

            UpdateProgress();
            CheckFinishEnabled();
        }

        /// <summary>
        /// Used by placeholder path: directly updates signing-state in the open PdfDocument.
        /// Merges with existing state so prior-session signatures are not lost.
        /// </summary>
        /// <summary>
        /// Scrie signing state in _loadedPdfPath dupa o semnatura reusita.
        /// Inlocuieste SaveIntermediate (care nu stia de Source=FreeForm).
        /// </summary>
        private void AttachSigningStateToFile(FreeFormSlot justSigned, SigningState previousState = null)
        {
            try
            {
                string tempOut = _loadedPdfPath + ".tmp";
                byte[] pdfBytes = File.ReadAllBytes(_loadedPdfPath);
                using (var ms = new MemoryStream(pdfBytes))
                using (var doc = PdfSharp.Pdf.IO.PdfReader.Open(ms, PdfSharp.Pdf.IO.PdfDocumentOpenMode.Modify))
                {
                    WriteSigningStateToPdfDoc(doc, justSigned, previousState);
                    doc.Save(tempOut);
                }
                File.Delete(_loadedPdfPath);
                File.Move(tempOut, _loadedPdfPath);
            }
            catch { /* non-fatal */ }
        }

        /// <summary>
        /// Construieste SigningState complet si il scrie in documentul PdfSharp deschis.
        /// justSignedSlot = null inseamna doar salvare configuratie (fara a marca ca semnat).
        /// </summary>
        private void WriteSigningStateToPdfDoc(PdfSharp.Pdf.PdfDocument document, FreeFormSlot justSignedSlot, SigningState existingState = null, bool finalized = false)
        {
            // Daca nu e furnizat explicit, citim din PDF (pentru cazul SaveSlotConfig fara semnare)
            if (existingState == null)
                existingState = SignatureService.ReadSigningState(_loadedPdfPath);
            var previousSigned = existingState?.Slots?
                .Where(e => e.Signed && (justSignedSlot == null || e.SignatureId != justSignedSlot.SignatureId))
                .ToDictionary(e => e.SignatureId)
                ?? new Dictionary<int, SigningStateEntry>();

            var stateObj = new SigningState
            {
                OriginalDocumentHash = _originalDocHash
                    ?? existingState?.OriginalDocumentHash
                    ?? string.Empty,
                OriginalFileName = _originalFileName
                    ?? existingState?.OriginalFileName
                    ?? string.Empty,
                Source = "FreeForm",
                Finalized = finalized || (existingState?.Finalized ?? false),
                Slots = _slots.Select(s =>
                {
                    var geo = new FreeFormSlotGeometry
                    {
                        Page = s.Page,
                        X = s.X,
                        Y = s.Y,
                        W = s.W,
                        H = s.H,
                        OfficialRole = s.OfficialRole,
                        Required = s.Required,
                        Biometric = s.Biometric,
                    };

                    if (justSignedSlot != null && s.SignatureId == justSignedSlot.SignatureId)
                        return new SigningStateEntry
                        {
                            SignatureId = s.SignatureId,
                            Party = s.Party,
                            SignerName = s.SignerName,
                            ActualSignerName = s.SignerName,
                            Reason = s.Reason,
                            Signed = true,
                            SignedAt = DateTime.Now,
                            MachineName = Environment.MachineName,
                            FreeForm = geo,
                        };

                    if (previousSigned.TryGetValue(s.SignatureId, out var prev))
                        return new SigningStateEntry
                        {
                            SignatureId = prev.SignatureId,
                            Party = prev.Party,
                            SignerName = prev.SignerName,
                            ActualSignerName = prev.ActualSignerName ?? prev.SignerName,
                            Reason = prev.Reason,
                            Signed = true,
                            SignedAt = prev.SignedAt,
                            MachineName = prev.MachineName,
                            FreeForm = prev.FreeForm ?? geo,
                        };

                    return new SigningStateEntry
                    {
                        SignatureId = s.SignatureId,
                        Party = s.Party,
                        SignerName = s.SignerName,
                        Reason = s.Reason,
                        Signed = false,
                        FreeForm = geo,
                    };
                }).ToList()
            };

            RemoveStateAttachment(document);
            PdfAttachmentHelper.AttachFile(document, "signing-state.json",
                "Document Signing State",
                Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(stateObj, Formatting.Indented)));
        }

        private void AttachSigningStateDirect(PdfSharp.Pdf.PdfDocument document, FreeFormSlot justSigned)
            => WriteSigningStateToPdfDoc(document, justSigned);

        private static void RemoveStateAttachment(PdfSharp.Pdf.PdfDocument document)
        {
            var nameArray = PdfAttachmentHelper.GetEmbeddedFilesArray(document);
            if (nameArray == null) return;

            for (int i = 0; i + 1 < nameArray.Elements.Count; i += 2)
            {
                var key = nameArray.Elements[i] as PdfString;
                if (key?.Value == "signing-state.json")
                {
                    nameArray.Elements.RemoveAt(i + 1);
                    nameArray.Elements.RemoveAt(i);
                    return;
                }
            }
        }

        #endregion

        private void MarkAsFinalized()
        {
            try
            {
                string tempOut = _loadedPdfPath + ".tmp";
                byte[] pdfBytes = File.ReadAllBytes(_loadedPdfPath);
                using (var ms = new System.IO.MemoryStream(pdfBytes))
                using (var doc = PdfSharp.Pdf.IO.PdfReader.Open(ms, PdfSharp.Pdf.IO.PdfDocumentOpenMode.Modify))
                {
                    WriteSigningStateToPdfDoc(doc, justSignedSlot: null, finalized: true);
                    doc.Save(tempOut);
                }
                File.Delete(_loadedPdfPath);
                File.Move(tempOut, _loadedPdfPath);
            }
            catch { }
        }

        #region Slot Config (prin signing-state.json)

        /// <summary>
        /// Salveaza configuratia sloturilor in signing-state.json (fara a marca ca semnate).
        /// Inlocuieste freeform-slots.json — un singur attachment per document.
        /// </summary>
        private void SaveSlotConfigToPdf(string pdfPath)
        {
            try
            {
                string tempOut = pdfPath + ".tmp";
                byte[] pdfBytes = File.ReadAllBytes(pdfPath);
                using (var ms = new MemoryStream(pdfBytes))
                using (var doc = PdfSharp.Pdf.IO.PdfReader.Open(ms, PdfSharp.Pdf.IO.PdfDocumentOpenMode.Modify))
                {
                    WriteSigningStateToPdfDoc(doc, justSignedSlot: null);
                    doc.Save(tempOut);
                }
                File.Delete(pdfPath);
                File.Move(tempOut, pdfPath);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Eroare la salvarea configuratiei:\n{ex.Message}",
                    "Eroare", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        /// <summary>
        /// Citeste sloturile din signing-state.json (campul FreeForm per entry).
        /// Inlocuieste ReadSlotConfig (freeform-slots.json).
        /// </summary>
        private static List<FreeFormSlot> ReadSlotConfig(string pdfPath)
        {
            try
            {
                var state = SignatureService.ReadSigningState(pdfPath);
                if (state?.Source != "FreeForm" || state.Slots == null) return null;
                return state.Slots
                    .Where(e => e.FreeForm != null)
                    .Select(e => new FreeFormSlot
                    {
                        SignatureId = e.SignatureId,
                        SignerName = e.SignerName,
                        Reason = e.Reason,
                        Party = e.Party,
                        OfficialRole = e.FreeForm.OfficialRole,
                        Required = e.FreeForm.Required,
                        Biometric = e.FreeForm.Biometric,
                        Page = e.FreeForm.Page,
                        X = e.FreeForm.X,
                        Y = e.FreeForm.Y,
                        W = e.FreeForm.W,
                        H = e.FreeForm.H,
                    }).ToList();
            }
            catch { }
            return null;
        }

        #endregion

        #region SignatureService Management

        private void RebuildSignatureService()
        {
            _signatureService?.Dispose();
            _signatureService = null;

            if (_loadedPdfPath == null) return;

            // Convert FreeFormSlots to SignatureSlots for SignatureService
            var sigSlots = _slots.Select(SlotToSignatureSlot).ToList();

            _signatureService = new SignatureService(
                _loadedPdfPath,
                artifactsRootDir: "",
                allSlots: sigSlots);
        }

        private static SignatureSlot SlotToSignatureSlot(FreeFormSlot s) =>
            new SignatureSlot
            {
                SignatureId = s.SignatureId,
                SignerName = s.SignerName,
                ResolvedSignerName = s.SignerName,
                Reason = s.Reason,
                Page = s.Page.ToString(),
                ResolvedPage = s.Page,
                Party = s.Party,
                OfficialRole = s.OfficialRole,
                Required = s.Required,
                Biometric = s.Biometric,
                Location = new SignatureLocation
                {
                    X = s.X,
                    Y = s.Y,
                    W = s.W,
                    H = s.H
                }
            };

        #endregion

        #region Finalize

        private void btnSaveAndClose_Click(object sender, EventArgs e)
        {
            if (_loadedPdfPath == null) return;
            try
            {
                if (_slots.Count == 0)
                {
                    UnloadDocument();
                    return;
                }

                // Acum facem backup original si mutam in InProces
                BackupOriginalOnce();

                MessageBox.Show(
                    $"Document salvat in Documente In Proces:\n{Path.GetFileName(_inProcesPath)}",
                    "Salvat", MessageBoxButtons.OK, MessageBoxIcon.Information);

                UnloadDocument();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Eroare la salvare:\n{ex.Message}",
                    "Eroare", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void btnFinish_Click(object sender, EventArgs e)
        {
            if (_loadedPdfPath == null) return;

            // Toate semnaturile trebuie semnate
            int unsigned = _cards.Count(c => !c.Signed);
            if (unsigned > 0)
            {
                MessageBox.Show(
                    $"Exista {unsigned} semnatura(ri) nesemnate.\n\n" +
                    "Toate semnaturile trebuie completate inainte de finalizare.",
                    "Semnaturi incomplete", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                // Asiguram ca documentul e in InProces (daca nu s-a salvat anterior)
                if (_inProcesPath == null)
                    BackupOriginalOnce();

                string root = AppConfig.FreeFormDocumentsPath;
                string outputDir = Path.Combine(root, FolderSemnatComplet);
                Directory.CreateDirectory(outputDir);

                string baseName = Path.GetFileNameWithoutExtension(_originalPdfPath ?? _loadedPdfPath);
                string dest = Path.Combine(outputDir, $"{baseName}_Semnat.pdf");
                if (File.Exists(dest))
                {
                    string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                    dest = Path.Combine(outputDir, $"{baseName}_Semnat_{stamp}.pdf");
                }

                // Marcheaza finalizat
                MarkAsFinalized();

                // Copiaza in Semnate Complet
                File.Copy(_loadedPdfPath, dest, overwrite: false);

                // Sterge din InProces
                if (_inProcesPath != null && File.Exists(_inProcesPath))
                    try { File.Delete(_inProcesPath); } catch { }

                MessageBox.Show(
                    $"Document finalizat:\n{dest}",
                    "Finalizat", MessageBoxButtons.OK, MessageBoxIcon.Information);

                _inProcesPath = null; // marcat ca sters, UnloadDocument nu va incerca sa-l stearga din nou
                UnloadDocument();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Eroare la finalizare:\n{ex.Message}",
                    "Eroare", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        #endregion

        #region Helpers

        private void RefreshPreviewSlots()
        {
            if (_slots == null || _slots.Count == 0)
            {
                pdfOverlay.ClearPreviewSlots();
                return;
            }

            var rects = new DrawnRectangle[_slots.Count];
            var signed = new bool[_slots.Count];
            for (int i = 0; i < _slots.Count; i++)
            {
                var s = _slots[i];
                bool accessible = s.Party != "Official"
                    || string.IsNullOrEmpty(s.OfficialRole)
                    || s.OfficialRole == _officialRole;

                rects[i] = new DrawnRectangle
                {
                    Page = s.Page,
                    X = s.X,
                    Y = s.Y,
                    W = s.W,
                    H = s.H,
                    RoleLabel = !string.IsNullOrEmpty(s.OfficialRole) ? s.OfficialRole
                        : s.Party == "Candidate" ? "Candidat / Angajat"
                        : s.Party,
                    IsAccessible = accessible,
                };
                var card = FindCard(s.SignatureId);
                signed[i] = card?.Signed ?? false;
            }
            pdfOverlay.SetPreviewSlots(rects, signed);
        }

        private void ReflowCards()
        {
            for (int i = 0; i < _slots.Count; i++)
            {
                var card = FindCard(_slots[i].SignatureId);
                if (card == null || card.Signed) continue;
                ApplyRoleRestriction(card, _slots[i]);
            }
        }

        private void SetCardsEnabled(bool enabled)
        {
            foreach (var c in _cards)
                if (!c.Signed && !c.RoleRestricted)
                    c.Enabled = enabled;
        }

        private void UpdateProgress()
        {
            int signed = _cards.Count(c => c.Signed);
            int total = _cards.Count;
            lblProgress.Text = total == 0
                ? "Nicio semnatura configurata."
                : $"{signed} din {total} semnaturi completate";
        }

        private void CheckFinishEnabled()
        {
            bool allSigned = _cards.Count > 0 && _cards.All(c => c.Signed);
            btnFinish.Enabled = _loadedPdfPath != null && _slots.Count > 0 && allSigned;
        }

        #endregion

        #region Cleanup

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _mirrorSyncTimer?.Stop();
                _mirrorSyncTimer?.Dispose();
                _mirrorForm?.Close();
                _signatureService?.Dispose();
                // pdfOverlay este al ShellForm — nu il dispose-uim noi
                if (_loadedPdfPath != null && File.Exists(_loadedPdfPath))
                    try { File.Delete(_loadedPdfPath); } catch { }

                // Detasam event handler ca sa nu ramana reference
                if (pdfOverlay != null)
                {
                    pdfOverlay.RectangleDrawn -= OnRectangleDrawn;
                    pdfOverlay.DrawingAborted -= ExitDrawingMode;
                }
            }
            base.Dispose(disposing);
        }

        #endregion
    }

    // ── Data model ────────────────────────────────────────────────────────────────

    /// <summary>
    /// A free-form signature slot — everything needed to place and capture a signature,
    /// stored as JSON attachment in the PDF.
    /// </summary>
    public class FreeFormSlot
    {
        [JsonProperty("SignatureId")] public int SignatureId { get; set; }
        [JsonProperty("SignerName")] public string SignerName { get; set; }
        [JsonProperty("Reason")] public string Reason { get; set; }
        [JsonProperty("Party")] public string Party { get; set; }
        [JsonProperty("OfficialRole")] public string OfficialRole { get; set; }
        [JsonProperty("Required")] public bool Required { get; set; }
        [JsonProperty("Biometric")] public bool Biometric { get; set; }
        [JsonProperty("Page")] public int Page { get; set; }
        [JsonProperty("X")] public float X { get; set; }
        [JsonProperty("Y")] public float Y { get; set; }
        [JsonProperty("W")] public float W { get; set; }
        [JsonProperty("H")] public float H { get; set; }
    }
}