using PdfiumViewer;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using WacomSignaturePdf.Controls;
using WacomSignaturePdf.Models;
using WacomSignaturePdf.Services;
using WacomSignaturePdf.Theme;
using WacomSignaturePdf.Config;

namespace WacomSignaturePdf.Forms
{
    public partial class MainForm : Form
    {
        #region Fields

        // ── State ──
        private SignatureService _service;
        private ResolvedTemplate _resolved;
        private List<DocumentTemplate> _templates;
        private string _candidateFolder;
        private int _signatureCount;
        private PdfDocument _currentPdfDoc;
        private string _currentViewerPath;
        private List<SignatureCardPanel> _cards = new List<SignatureCardPanel>();

        // ── Mirror state ──
        private MirrorForm _mirrorForm;
        private bool _mirrorActive;
        private Timer _syncTimer;
        private PointF _lastScrollRatio = PointF.Empty;
        private double _lastZoom = -1;
        private int _lastPage = -1;

        // ── Softone prefill ──
        private string _prefillSignerName;
        private string _officialName;
        private string _candidateSignerName;

        // ── Party toggle ──
        private enum SigningParty { Candidate, Official }
        private SigningParty _currentParty = SigningParty.Candidate;

        // ── Capture guard ──
        private bool _captureInProgress = false;

        #endregion

        #region Constructors

        public MainForm()
        {
            DoubleBuffered = true;
            BuildLayout();
            LoadTemplates();
            PopulateFolderDropdown();
            InitSyncTimer();
            deviceStatusLabel.StartPolling();
            oneDriveStatusLabel.StartPolling();
        }

        public MainForm(string personId, string signerName) : this()
        {
            _prefillSignerName = signerName;
            txtCandidateId.Text = personId;
            PopulateFolderDropdown();
        }

        public MainForm(string personId, string signerName, string officialName)
            : this(personId, signerName)
        {
            _officialName = officialName;
            UpdateCurrentSignerLabel();
        }

        #endregion

        #region Template Loading

        private void LoadTemplates()
        {
            try
            {
                _templates = TemplateService.LoadTemplates(AppConfig.TemplatesDir);
                cmbTemplate.Items.Clear();
                foreach (var t in _templates)
                    cmbTemplate.Items.Add(t.TemplateName);
                if (cmbTemplate.Items.Count > 0)
                    cmbTemplate.SelectedIndex = 0;
                Log($"S-au incarcat {_templates.Count} template-uri.");
            }
            catch (Exception ex)
            {
                Log($"EROARE la incarcare template-uri: {ex.Message}");
                MessageBox.Show(ex.Message, "Eroare Template", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

            Log($"WorkingRoot  : {AppConfig.WorkingRoot}");
            Log($"TemplatesDir : {AppConfig.TemplatesDir}");
            Log($"BaseDirectory: {AppDomain.CurrentDomain.BaseDirectory}");
        }

        #endregion

        #region Folder Picker

        private void PopulateFolderDropdown()
        {
            cmbCandidateFolder.Items.Clear();
            try
            {
                if (!System.IO.Directory.Exists(AppConfig.WorkingRoot)) return;

                var folders = System.IO.Directory.GetDirectories(AppConfig.WorkingRoot)
                    .Select(System.IO.Path.GetFileName)
                    .OrderBy(n => n)
                    .ToList();

                foreach (var f in folders)
                    cmbCandidateFolder.Items.Add(f);

                // Auto-select the folder matching the current candidate ID
                string currentId = txtCandidateId.Text.Trim();
                if (!string.IsNullOrWhiteSpace(currentId))
                {
                    for (int i = 0; i < cmbCandidateFolder.Items.Count; i++)
                    {
                        string item = cmbCandidateFolder.Items[i].ToString();
                        if (item.StartsWith(currentId + " - ", StringComparison.OrdinalIgnoreCase) ||
                            item.StartsWith(currentId + "-", StringComparison.OrdinalIgnoreCase))
                        {
                            cmbCandidateFolder.SelectedIndex = i;
                            cmbCandidateFolder.Invalidate();
                            cmbCandidateFolder.Refresh();
                            break;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log($"EROARE incarcare foldere: {ex.Message}");
            }
        }

        private void OnCandidateFolderSelected()
        {
            if (cmbCandidateFolder.SelectedIndex < 0) return;

            string folderName = cmbCandidateFolder.SelectedItem.ToString();
            string fullPath = System.IO.Path.Combine(AppConfig.WorkingRoot, folderName);

            try
            {
                _candidateFolder = fullPath;

                // Extract ID from folder name (everything before first " - " or "-")
                string id = folderName;
                int dash = folderName.IndexOf(" - ", StringComparison.Ordinal);
                if (dash < 0) dash = folderName.IndexOf('-');
                if (dash > 0) id = folderName.Substring(0, dash).Trim();

                // Update the ID field without triggering the TextChanged folder search
                txtCandidateId.TextChanged -= txtCandidateId_TextChanged;
                txtCandidateId.Text = id;
                txtCandidateId.TextChanged += txtCandidateId_TextChanged;

                string name = TemplateService.GetCandidateName(fullPath);
                _candidateSignerName = name;
                _prefillSignerName = name;
                lblCandidateName.Text = name + "\n" + fullPath;
                lblCandidateName.ForeColor = AppTheme.CandidateFound;
                cmbTemplate.Enabled = true;
                btnLoad.Enabled = true;
                _candidateSignerName = null;
                UpdateCurrentSignerLabel();
            }
            catch (Exception ex)
            {
                lblCandidateName.Text = "Eroare la selectarea folderului";
                lblCandidateName.ForeColor = AppTheme.CandidateError;
                _candidateFolder = null;
                cmbTemplate.Enabled = false;
                btnLoad.Enabled = false;
                Log($"EROARE folder: {ex.Message}");
            }
        }

        #endregion

        #region Candidate ID

        private void txtCandidateId_TextChanged(object sender, EventArgs e)
        {
            string id = txtCandidateId.Text.Trim();

            if (string.IsNullOrEmpty(id))
            {
                lblCandidateName.Text = "Introduceti ID-ul candidatului";
                lblCandidateName.ForeColor = AppTheme.SidebarSub;
                _candidateFolder = null;
                _candidateSignerName = null;
                cmbTemplate.Enabled = false;
                btnLoad.Enabled = false;
                UpdateCurrentSignerLabel();
                return;
            }

            try
            {
                _candidateFolder = TemplateService.FindCandidateFolder(AppConfig.WorkingRoot, id);
                string name = TemplateService.GetCandidateName(_candidateFolder);
                lblCandidateName.Text = name + "\n" + _candidateFolder;
                lblCandidateName.ForeColor = AppTheme.CandidateFound;
                cmbTemplate.Enabled = true;
                btnLoad.Enabled = true;

                // Show prefilled signer name immediately if available
                if (!string.IsNullOrWhiteSpace(_prefillSignerName))
                {
                    _candidateSignerName = _prefillSignerName;
                    UpdateCurrentSignerLabel();
                }
            }
            catch
            {
                lblCandidateName.Text = "Nu s-a gasit candidatul cu acest ID";
                lblCandidateName.ForeColor = AppTheme.CandidateError;
                _candidateFolder = null;
                _candidateSignerName = null;
                cmbTemplate.Enabled = false;
                btnLoad.Enabled = false;
                UpdateCurrentSignerLabel();
            }
        }

        #endregion

        #region Load Document

        private void TryLoadDocument()
        {
            if (_candidateFolder == null)
            {
                MessageBox.Show("Nu s-a gasit folderul candidatului.", "Negasit",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (cmbTemplate.SelectedIndex < 0)
            {
                MessageBox.Show("Selectati tipul de document.", "Fara Template",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string signerName = PromptSignerName();
            if (signerName == null) return;

            try
            {
                ResetState();

                var template = _templates[cmbTemplate.SelectedIndex];
                _resolved = TemplateService.Resolve(
                    template, _candidateFolder, signerName, _officialName ?? string.Empty);

                // Pass all slots so SignatureService can write a complete state
                _service = new SignatureService(
                    _resolved.PdfPath,
                    _resolved.ArtifactsPath,
                    _resolved.Slots);

                cmbTemplate.Enabled = false;
                btnLoad.Enabled = false;
                btnCancelLoad.Visible = true;
                btnCancelLoad.Enabled = true;
                btnSaveProgress.Visible = true;
                btnSaveProgress.Enabled = false; // enabled after first signature

                toggleParty.IsOn = false;
                _currentParty = SigningParty.Candidate;
                _candidateSignerName = signerName;
                UpdatePartyLabels();
                UpdateCurrentSignerLabel();

                BuildCards(_resolved.Slots);
                RefreshPdfViewer(_resolved.PdfPath);
                LoadSigningState();

                Log($"Document : {_resolved.Template.TemplateName}");
                Log($"PDF      : {_resolved.PdfPath}");
                Log($"Sloturi  : {_resolved.Slots.Count}");
                Log("Apasati pe un card pentru a semna.");
                UpdateProgress();
            }
            catch (DocumentAlreadyFinalizedException ex)
            {
                _resolved = null;
                Log($"Document deja finalizat si sigilat.");
                ErrorDialog.Show(this, ex.Message, ErrorKind.DocumentFinalized);
            }
            catch (DocumentSignedNotSealedException ex)
            {
                _resolved = null;
                Log($"Document semnat dar nesigilat in Adobe: {ex.SemnatPath}");

                ErrorDialog.Show(this, ex.Message, ErrorKind.DocumentSignedNotSealed);

                Log("Se deschide in Adobe pentru sigilare...");
                try
                {
                    System.Diagnostics.Process.Start(
                        new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = ex.SemnatPath,
                            UseShellExecute = true
                        });
                }
                catch (Exception openEx)
                {
                    Log($"EROARE la deschidere: {openEx.Message}");
                }
            }
            catch (Exception ex)
            {
                _resolved = null;
                Log($"EROARE: {ex.Message}");
                var kind = ex is System.IO.FileNotFoundException
                    ? ErrorKind.FileNotFound
                    : ErrorKind.General;
                ErrorDialog.Show(this, ex.Message, kind);
            }
        }

        #endregion

        #region Signing State Restore

        private void LoadSigningState()
        {
            if (_resolved == null) return;

            var state = SignatureService.ReadSigningState(_resolved.PdfPath);
            if (state == null || state.Slots == null) return;

            foreach (var entry in state.Slots.Where(s => s.Signed))
            {
                var card = _cards.FirstOrDefault(c => c.Slot.SignatureId == entry.SignatureId);
                if (card == null) continue;

                card.MarkSigned(entry.SignerName);
                _signatureCount++;
                Log($"  [Restaurat] #{entry.SignatureId} {entry.SignerName} — " +
                    $"{entry.SignedAt:g} pe {entry.MachineName}");
            }

            UpdateProgress();

            bool allRequired = _cards.Where(c => c.Slot.Required).All(c => c.Signed);
            if (allRequired)
            {
                btnFinish.Enabled = true;
                Log("Toate semnaturile obligatorii sunt completate. Apasati Finalizati.");
            }
        }

        #endregion

        #region Cancel / Unload Document

        /// <summary>
        /// Runs the document unload flow (reset/unload dialog if applicable).
        /// Returns true if the document was unloaded, false if the user cancelled.
        /// </summary>
        private bool CancelCurrentDocument()
        {
            // Grab paths before ResetState nulls _resolved
            string pdfPath = _resolved?.PdfPath;
            string backupPath = pdfPath != null
                ? Path.Combine(Path.GetDirectoryName(pdfPath),
                      "Originally Generated Documents",
                      Path.GetFileName(pdfPath))
                : null;
            bool backupExists = backupPath != null && File.Exists(backupPath);

            bool resetToOriginal = false;
            if (backupExists)
            {
                using (var dlg = new ResetOrUnloadDialog())
                {
                    var result = dlg.ShowDialog(this);
                    if (result == DialogResult.Cancel) return false;
                    resetToOriginal = dlg.ResetToOriginal;
                }
            }
            else if (_signatureCount > 0)
            {
                var confirm = MessageBox.Show(
                    "Exista semnaturi capturate. Sigur doriti sa descarcati documentul?",
                    "Confirmare", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                if (confirm != DialogResult.Yes) return false;
            }

            if (_mirrorActive)
            {
                _syncTimer.Stop();
                _mirrorForm?.Hide();
                _mirrorActive = false;
                btnMirror.Text = "⊞  Oglindire pe Ecran";
                btnMirror.BackColor = AppTheme.MirrorOn;
            }
            _mirrorForm?.ClearDocument();

            ResetState();
            ClearPdfViewer();

            // Restore the original clean PDF so the document can be loaded again
            if (resetToOriginal && backupExists)
            {
                try
                {
                    File.Copy(backupPath, pdfPath, overwrite: true);
                    Log("Document resetat la originalul nesemnat.");
                }
                catch (Exception ex)
                {
                    Log($"AVERTISMENT: Nu s-a putut reseta documentul: {ex.Message}");
                }
            }

            cmbTemplate.Enabled = _candidateFolder != null;
            btnLoad.Enabled = _candidateFolder != null;
            Log("Document descarcat. Selectati un alt document.");

            return true;
        }

        #endregion

        #region Save Progress

        private void btnSaveProgress_Click(object sender, EventArgs e)
        {
            if (_service == null || _signatureCount == 0) return;

            try
            {
                ClearPdfViewer();

                _service.SaveProgress();

                RefreshPdfViewer(_resolved.PdfPath);
                Log("Progres salvat. Documentul poate fi trimis la urmatoarea persoana.");
                MessageBox.Show(
                    "Progresul a fost salvat in document.\nTrimiteti fisierul la urmatoarea persoana.",
                    "Salvat", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                Log($"EROARE salvare progres: {ex.Message}");
                MessageBox.Show(ex.Message, "Eroare", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        #endregion

        #region Party Toggle & Signer Label

        private void OnPartyToggled()
        {
            _currentParty = toggleParty.IsOn ? SigningParty.Official : SigningParty.Candidate;
            UpdatePartyLabels();
            UpdateCurrentSignerLabel();
            ReflowCards();
            UpdateProgress();
        }

        private void UpdatePartyLabels()
        {
            lblPartyCandidate.ForeColor = _currentParty == SigningParty.Candidate
                ? AppTheme.AccentBlue : AppTheme.SidebarSub;
            lblPartyOfficial.ForeColor = _currentParty == SigningParty.Official
                ? AppTheme.AccentGreen : AppTheme.SidebarSub;
        }

        private void UpdateCurrentSignerLabel()
        {
            if (chkManualSigner.Checked)
            {
                lblCurrentSigner.Text = "-";
                return;
            }

            string name = _currentParty == SigningParty.Official
                ? _officialName
                : _candidateSignerName;

            lblCurrentSigner.Text = string.IsNullOrWhiteSpace(name) ? "" : name;
        }

        #endregion

        #region Signature Cards

        private void BuildCards(List<SignatureSlot> slots)
        {
            cardsPanel.Controls.Clear();
            _cards.Clear();

            foreach (var slot in slots)
            {
                var card = new SignatureCardPanel(slot);
                card.Width = cardsPanel.Width - 12;
                card.CardClicked += OnCardClicked;
                cardsPanel.Controls.Add(card);
                _cards.Add(card);
            }

            ReflowCards();
        }

        private void ReflowCards()
        {
            string party = _currentParty == SigningParty.Candidate ? "Candidate" : "Official";

            cardsPanel.SuspendLayout();
            int y = 6;
            int visibleCount = 0;
            foreach (var card in _cards)
            {
                bool visible = string.IsNullOrEmpty(card.Slot.Party) || card.Slot.Party == party;
                card.Visible = visible;
                if (visible)
                {
                    card.Location = new Point(6, y);
                    y += card.Height + 6;
                    visibleCount++;
                }
            }
            // No cards for this party - hide all to clear the panel
            if (visibleCount == 0)
                foreach (var card in _cards)
                    card.Visible = false;
            cardsPanel.ResumeLayout();
        }

        private void SetCardsEnabled(bool enabled)
        {
            foreach (var c in _cards)
                if (!c.Signed) c.Enabled = enabled;
        }

        #endregion

        #region Signature Capture

        private void OnCardClicked(SignatureSlot slot)
        {
            if (_service == null || _resolved == null) return;
            if (_captureInProgress) return;

            var card = _cards.FirstOrDefault(c => c.Slot.SignatureId == slot.SignatureId);
            if (card == null || card.Signed) return;

            Log($"Semnatura #{slot.SignatureId} — {slot.Reason} (Pagina {slot.ResolvedPage})");

            // Resolve signer name on the UI thread before handing off
            string prefill = _currentParty == SigningParty.Official ? _officialName : _candidateSignerName;
            string signerName = chkManualSigner.Checked
                ? PromptSignerNameForSlot(slot.Reason, prefill)
                : slot.ResolvedSignerName;
            if (signerName == null) return;

            bool isImputernicire = chkManualSigner.Checked;

            // Disable cards so a second click cannot start a parallel capture
            SetCardsEnabled(false);
            _captureInProgress = true;

            // CaptureAndEmbed uses STA COM objects (SigCtl, DynamicCapture).
            // Task.Run uses MTA pool threads, so we spin a dedicated STA thread instead.
            var thread = new System.Threading.Thread(() =>
            {
                Exception caughtEx = null;
                bool cancelled = false;

                try
                {
                    _service.CaptureAndEmbed(
                        slot.SignatureId,
                        slot.Party,
                        signerName,
                        slot.Reason,
                        slot.ResolvedPage,
                        slot.Location.X, slot.Location.Y,
                        slot.Location.W, slot.Location.H,
                        isImputernicire);
                }
                catch (OperationCanceledException) { cancelled = true; }
                catch (Exception ex) { caughtEx = ex; }

                // All UI updates must happen back on the UI thread
                Invoke(new Action(() =>
                {
                    _captureInProgress = false;
                    SetCardsEnabled(true);

                    if (cancelled)
                    {
                        Log($"Slot #{slot.SignatureId} anulat.");
                        return;
                    }

                    if (caughtEx != null)
                    {
                        Log($"EROARE: {caughtEx.Message}");
                        var kind = caughtEx.Message.Contains("STU") || caughtEx.Message.Contains("device")
                                       || caughtEx.Message.Contains("pad") || caughtEx.Message.Contains("Pad")
                            ? ErrorKind.DeviceNotConnected
                            : ErrorKind.General;
                        ErrorDialog.Show(this, caughtEx.Message, kind);
                        return;
                    }

                    _signatureCount++;
                    card.MarkSigned(signerName);
                    Log($"  Semnat: {signerName}  |  {slot.Reason}");
                    btnSaveProgress.Enabled = true;
                    UpdateProgress();

                    // Save intermediate + refresh viewer off the UI thread so the
                    // interface stays responsive while the PDF is being rewritten.
                    Task.Run(() =>
                    {
                        _service.SaveIntermediate();
                    }).ContinueWith(_ =>
                    {
                        RefreshPdfViewer(_resolved.PdfPath);

                        bool allRequired = _cards.Where(c => c.Slot.Required).All(c => c.Signed);
                        if (allRequired)
                        {
                            btnFinish.Enabled = true;
                            Log("Toate semnaturile obligatorii au fost completate. Apasati Finalizati.");
                        }
                    }, TaskScheduler.FromCurrentSynchronizationContext());
                }));
            });

            thread.SetApartmentState(System.Threading.ApartmentState.STA);
            thread.IsBackground = true;
            thread.Start();
        }

        #endregion

        #region Finalize

        private void btnFinish_Click(object sender, EventArgs e)
        {
            if (_service == null || _signatureCount == 0) return;

            int missing = _cards.Count(c => c.Slot.Required && !c.Signed);
            if (missing > 0)
            {
                MessageBox.Show($"{missing} semnaturi obligatorii sunt in asteptare.",
                    "Semnaturi Obligatorii", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                _mirrorForm?.Hide();
                _mirrorForm?.ClearDocument();
                ClearPdfViewer();

                // If no new captures this session, signatures are already embedded.
                // Just strip the signing-state attachment and rename.
                string finalPath;
                if (_service.HasNewCaptures)
                {
                    var captures = _service.Finalize(openAfterSave: false);
                    Log($"Salvat: {_resolved.PdfPath}");
                    foreach (var cap in captures)
                        Log($"  [{cap.SignerName}] Hash={cap.DocumentHash.Substring(0, 16)}... " +
                            $"TSA={(cap.TrustedAt.HasValue ? cap.TrustedAt.Value.ToString("u") : "indisponibil")}");
                    finalPath = _service.FinalizedPath;
                }
                else
                {
                    finalPath = _service.FinalizeFromState();
                    Log($"Finalizat din stare salvata: {finalPath}");
                }

                Log("Deschis in Adobe.");
                System.Diagnostics.Process.Start(
                    new System.Diagnostics.ProcessStartInfo { FileName = finalPath, UseShellExecute = true });

                btnFinish.Enabled = false;
                btnSaveProgress.Visible = false;
                btnSaveProgress.Enabled = false;
                btnCancelLoad.Visible = false;
                btnCancelLoad.Enabled = false;
                cmbTemplate.Enabled = true;
                btnLoad.Enabled = true;
                Log("Gata pentru urmatorul document.");
            }
            catch (Exception ex)
            {
                Log($"EROARE: {ex.Message}");
                ErrorDialog.Show(this, ex.Message, ErrorKind.General);
            }
        }

        #endregion

        #region Mirror

        private void InitSyncTimer()
        {
            _syncTimer = new Timer { Interval = 33 };
            _syncTimer.Tick += SyncMirror;
        }

        private void SyncMirror(object sender, EventArgs e)
        {
            if (!_mirrorActive || _mirrorForm == null || !_mirrorForm.Visible) return;
            if (pdfViewer.Renderer == null) return;

            try
            {
                int page = pdfViewer.Renderer.Page;
                if (page != _lastPage)
                {
                    _lastPage = page;
                    _mirrorForm.SyncPage(page);
                }

                double zoom = pdfViewer.Renderer.Zoom;
                if (Math.Abs(zoom - _lastZoom) > 0.001)
                {
                    _lastZoom = zoom;
                    _mirrorForm.SyncZoom(zoom);
                }

                PointF ratio = GetViewerScrollRatio(pdfViewer);
                if (ratio != _lastScrollRatio)
                {
                    _lastScrollRatio = ratio;
                    _mirrorForm.SyncScrollRatio(ratio);
                }
            }
            catch { }
        }

        private void btnMirror_Click(object sender, EventArgs e)
        {
            if (!_mirrorActive && _currentViewerPath == null)
            {
                MessageBox.Show("Nu exista niciun document incarcat pentru oglindire.",
                    "Fara Document", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (_mirrorActive)
            {
                _syncTimer.Stop();
                _mirrorForm?.Hide();
                _mirrorActive = false;
                btnMirror.Text = "⊞  Oglindire pe Ecran";
                btnMirror.BackColor = AppTheme.MirrorOn;
                Log("Oglindire inchisa.");
                return;
            }

            Screen targetScreen = GetSecondScreen();
            if (targetScreen == null)
            {
                MessageBox.Show(
                    "Nu s-a detectat un al doilea monitor.\nConectati un display secundar si incercati din nou.",
                    "Fara Monitor Secundar", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (_mirrorForm == null)
                _mirrorForm = new MirrorForm();

            if (_currentViewerPath != null && File.Exists(_currentViewerPath))
                _mirrorForm.LoadFromPath(_currentViewerPath);

            _mirrorForm.ShowOnScreen(targetScreen);
            _mirrorActive = true;
            btnMirror.Text = "✕  Inchide Oglindire";
            btnMirror.FlatAppearance.BorderColor = AppTheme.MirrorOffBorder;
            btnMirror.BackColor = AppTheme.MirrorOff;

            _lastScrollRatio = PointF.Empty;
            _lastZoom = -1;
            _lastPage = -1;

            _syncTimer.Start();
            Log($"Oglindire activa pe: {targetScreen.DeviceName}");
        }

        private static Screen GetSecondScreen()
        {
            foreach (Screen s in Screen.AllScreens)
                if (!s.Primary) return s;
            return null;
        }

        #endregion

        #region PDF Viewer

        private void RefreshPdfViewer(string pdfPath)
        {
            try
            {
                // Snapshot position before destroying the viewer
                int savedPage = pdfViewer.Renderer != null ? pdfViewer.Renderer.Page : 0;
                PointF savedRatio = GetViewerScrollRatio(pdfViewer);

                string copy = Path.Combine(Path.GetTempPath(),
                    $"wacom_viewer_{DateTime.Now:yyyyMMdd_HHmmss_fff}.pdf");

                ClearPdfViewer();

                File.Copy(pdfPath, copy, overwrite: false);
                _currentViewerPath = copy;
                _currentPdfDoc = PdfDocument.Load(copy);
                pdfViewer.Document = _currentPdfDoc;
                pdfViewer.Renderer.ZoomMode = PdfViewerZoomMode.FitWidth;

                btnZoomIn.Enabled = true;
                btnZoomOut.Enabled = true;

                // Restore position after the viewer has rendered
                pdfViewer.Renderer.Page = savedPage;
                RestoreScrollRatio(pdfViewer, savedRatio);

                if (_mirrorActive && _mirrorForm != null && _mirrorForm.Visible)
                    _mirrorForm.LoadFromPath(copy);
            }
            catch (Exception ex)
            {
                Log($"Eroare previzualizare: {ex.Message}");
            }
        }

        private static void RestoreScrollRatio(PdfViewer viewer, PointF ratio)
        {
            if (viewer.Renderer == null) return;
            if (ratio == PointF.Empty) return;

            // The renderer needs a layout pass before DisplayRectangle has real dimensions.
            // Use BeginInvoke so it runs after the current paint cycle completes.
            viewer.BeginInvoke(new Action(() =>
            {
                try
                {
                    var display = viewer.Renderer.DisplayRectangle;
                    int scrollableX = display.Width - viewer.Renderer.ClientSize.Width;
                    int scrollableY = display.Height - viewer.Renderer.ClientSize.Height;

                    int x = scrollableX > 0 ? (int)(ratio.X * scrollableX) : 0;
                    int y = scrollableY > 0 ? (int)(ratio.Y * scrollableY) : 0;

                    viewer.Renderer.SetDisplayRectLocation(new System.Drawing.Point(-x, -y));
                }
                catch { }
            }));
        }

        private static PointF GetViewerScrollRatio(PdfViewer viewer)
        {
            try
            {
                if (viewer.Renderer == null) return PointF.Empty;
                var display = viewer.Renderer.DisplayRectangle;
                int scrollableY = display.Height - viewer.Renderer.ClientSize.Height;
                int scrollableX = display.Width - viewer.Renderer.ClientSize.Width;
                float ratioX = scrollableX > 0 ? (float)(-display.X) / scrollableX : 0f;
                float ratioY = scrollableY > 0 ? (float)(-display.Y) / scrollableY : 0f;
                return new PointF(ratioX, ratioY);
            }
            catch { }
            return PointF.Empty;
        }

        private void ClearPdfViewer()
        {
            _syncTimer.Stop();

            btnZoomIn.Enabled = false;
            btnZoomOut.Enabled = false;

            _currentPdfDoc?.Dispose();
            _currentPdfDoc = null;

            if (_currentViewerPath != null && File.Exists(_currentViewerPath))
                try { File.Delete(_currentViewerPath); } catch { }
            _currentViewerPath = null;

            _lastScrollRatio = PointF.Empty;
            _lastZoom = -1;
            _lastPage = -1;

            panelContent.Controls.Remove(pdfViewer);
            pdfViewer.Dispose();

            pdfViewer = new PdfViewer
            {
                Dock = DockStyle.Fill,
                ShowToolbar = true,
                ShowBookmarks = false
            };

            panelContent.Controls.Add(pdfViewer);
            panelContent.Controls.SetChildIndex(pdfViewer, panelContent.Controls.Count - 1);

            if (_mirrorActive)
                _syncTimer.Start();
        }

        #endregion

        #region Helpers

        private void ResetState()
        {
            _service?.Dispose();
            _service = null;
            _resolved = null;
            _signatureCount = 0;

            cardsPanel.Controls.Clear();
            _cards.Clear();

            btnFinish.Enabled = false;
            btnSaveProgress.Visible = false;
            btnSaveProgress.Enabled = false;
            btnCancelLoad.Visible = false;
            btnCancelLoad.Enabled = false;
            cmbTemplate.Enabled = _candidateFolder != null;
            btnLoad.Enabled = _candidateFolder != null;
            lblProgress.Text = "";
            _candidateSignerName = null;
            lblCurrentSigner.Text = "";
        }

        private void UpdateProgress()
        {
            if (_resolved == null) return;
            var visible = _cards.Where(c => c.Visible).ToList();
            int signed = visible.Count(c => c.Signed);
            lblProgress.Text = $"{signed} din {visible.Count} semnaturi completate";
        }

        private string PromptSignerName()
        {
            if (!string.IsNullOrWhiteSpace(_prefillSignerName))
                return _prefillSignerName;

            using (var dlg = new SignerNameDialog())
                return dlg.ShowDialog() == DialogResult.OK ? dlg.SignerName : null;
        }

        /// <summary>
        /// Prompts for a signer name with the slot reason shown as context.
        /// Used when manual signer mode is active.
        /// </summary>
        private string PromptSignerNameForSlot(string reason, string prefillName = null)
        {
            using (var dlg = new SignerNameDialog(reason, prefillName))
                return dlg.ShowDialog() == DialogResult.OK ? dlg.SignerName : null;
        }

        private void Log(string msg) =>
            txtLog.AppendText($"[{DateTime.Now:HH:mm:ss}] {msg}{Environment.NewLine}");

        #endregion

        #region Form Closing & Cleanup

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            // If a document is loaded, run the unload flow first (reset/unload dialog).
            // If the user cancels that dialog, abort the close entirely.
            if (_resolved != null)
            {
                if (!CancelCurrentDocument())
                {
                    e.Cancel = true;
                    return;
                }
            }

            _syncTimer?.Stop();
            deviceStatusLabel?.StopPolling();
            _mirrorForm?.Close();
            _service?.Dispose();
            pdfViewer.Document = null;
            _currentPdfDoc?.Dispose();
            CleanupTempFiles();
            base.OnFormClosing(e);
        }

        private void CleanupTempFiles()
        {
            try
            {
                string temp = Path.GetTempPath();
                foreach (string f in Directory.GetFiles(temp, "wacom_viewer_*.pdf"))
                    try { File.Delete(f); } catch { }
                foreach (string f in Directory.GetFiles(temp, "WacomSig_*"))
                    try { File.Delete(f); } catch { }
            }
            catch { }
        }

        #endregion
    }
}