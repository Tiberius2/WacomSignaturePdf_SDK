using PdfiumViewer;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
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
        // ── State ─────────────────────────────────────────────────────────────────
        private SignatureService _service;
        private ResolvedTemplate _resolved;
        private List<DocumentTemplate> _templates;
        private string _candidateFolder;
        private int _signatureCount;
        private PdfDocument _currentPdfDoc;
        private string _currentViewerPath;
        private List<SignatureCardPanel> _cards = new List<SignatureCardPanel>();

        // ── Mirror state ──────────────────────────────────────────────────────────
        private MirrorForm _mirrorForm;
        private bool _mirrorActive;
        private Timer _syncTimer;
        private PointF _lastScrollRatio = PointF.Empty;
        private double _lastZoom = -1;
        private int _lastPage = -1;

        // ── Softone prefill ───────────────────────────────────────────────────────
        private string _prefillSignerName;

        // ── Constructors ──────────────────────────────────────────────────────────

        public MainForm()
        {
            DoubleBuffered = true;
            BuildLayout();
            LoadTemplates();
            InitSyncTimer();
        }

        /// <summary>
        /// Softone entry point: prefills candidate ID and skips the signer name dialog.
        /// </summary>
        public MainForm(string personId, string signerName) : this()
        {
            txtCandidateId.Text = personId;   // fires TextChanged → resolves candidate folder
            _prefillSignerName = signerName;
        }

        // ── Sync timer ────────────────────────────────────────────────────────────

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

        // ── Template loading ──────────────────────────────────────────────────────

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

        // ── Candidate ID text changed ─────────────────────────────────────────────

        private void txtCandidateId_TextChanged(object sender, EventArgs e)
        {
            string id = txtCandidateId.Text.Trim();

            if (string.IsNullOrEmpty(id))
            {
                lblCandidateName.Text = "Introduceti ID-ul candidatului";
                lblCandidateName.ForeColor = AppTheme.SidebarSub;
                _candidateFolder = null;
                cmbTemplate.Enabled = false;
                btnLoad.Enabled = false;
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
            }
            catch
            {
                lblCandidateName.Text = "Nu s-a gasit candidatul cu acest ID";
                lblCandidateName.ForeColor = AppTheme.CandidateError;
                _candidateFolder = null;
                cmbTemplate.Enabled = false;
                btnLoad.Enabled = false;
            }
        }

        // ── Load document ─────────────────────────────────────────────────────────

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
                _resolved = TemplateService.Resolve(template, _candidateFolder, signerName);
                _service = new SignatureService(_resolved.InputPath, _resolved.ArtifactsPath);

                cmbTemplate.Enabled = false;
                btnLoad.Enabled = false;
                btnCancelLoad.Visible = true;
                btnCancelLoad.Enabled = true;

                BuildCards(_resolved.Slots);
                RefreshPdfViewer(_resolved.InputPath);

                Log($"Document : {_resolved.Template.TemplateName}");
                Log($"Input    : {_resolved.InputPath}");
                Log($"Sloturi  : {_resolved.Slots.Count}");
                Log("Apasati pe un card pentru a semna.");
                UpdateProgress();
            }
            catch (Exception ex)
            {
                _resolved = null;
                Log($"EROARE: {ex.Message}");
                MessageBox.Show(ex.Message, "Eroare Incarcare", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // ── Cancel document ───────────────────────────────────────────────────────

        private void CancelCurrentDocument()
        {
            if (_signatureCount > 0)
            {
                var confirm = MessageBox.Show(
                    "Exista semnaturi capturate. Sigur doriti sa anulati documentul curent?",
                    "Confirmare", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                if (confirm != DialogResult.Yes) return;
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

            cmbTemplate.Enabled = _candidateFolder != null;
            btnLoad.Enabled = _candidateFolder != null;
            Log("Document descarcat. Selectati un alt document.");
        }

        // ── Build signature cards ─────────────────────────────────────────────────

        private void BuildCards(List<SignatureSlot> slots)
        {
            cardsPanel.Controls.Clear();
            _cards.Clear();

            int y = 6;
            foreach (var slot in slots)
            {
                var card = new SignatureCardPanel(slot);
                card.Location = new Point(6, y);
                card.CardClicked += OnCardClicked;
                cardsPanel.Controls.Add(card);
                _cards.Add(card);
                y += card.Height + 6;
            }
        }

        // ── Card clicked → capture signature ─────────────────────────────────────

        private void OnCardClicked(SignatureSlot slot)
        {
            if (_service == null || _resolved == null) return;

            var card = _cards.FirstOrDefault(c => c.Slot.SignatureId == slot.SignatureId);
            if (card == null || card.Signed) return;

            Log($"Semnatura #{slot.SignatureId} — {slot.Reason} (Pagina {slot.ResolvedPage})");

            try
            {
                _service.CaptureAndEmbed(
                    slot.ResolvedSignerName,
                    slot.Reason,
                    slot.ResolvedPage,
                    slot.Location.X, slot.Location.Y,
                    slot.Location.W, slot.Location.H);

                _signatureCount++;
                card.MarkSigned();
                Log($"  Semnat: {slot.ResolvedSignerName}  |  {slot.Reason}");

                _service.SaveIntermediate(_resolved.OutputPath);
                RefreshPdfViewer(_resolved.OutputPath);
                UpdateProgress();

                bool allRequired = _cards.Where(c => c.Slot.Required).All(c => c.Signed);
                if (allRequired)
                {
                    btnFinish.Enabled = true;
                    Log("Toate semnaturile obligatorii au fost completate. Apasati Finalizati.");
                }
            }
            catch (OperationCanceledException)
            {
                Log($"Slot #{slot.SignatureId} anulat.");
            }
            catch (Exception ex)
            {
                Log($"EROARE #: {ex}");
                MessageBox.Show(ex.ToString(), "Eroare Captura", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // ── Finish ────────────────────────────────────────────────────────────────

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
                ClearPdfViewer();
                if (_mirrorForm != null)
                {
                    _mirrorForm.Hide();
                    _mirrorForm.ClearDocument();
                }
                System.Threading.Thread.Sleep(50);
                var captures = _service.Finalize(_resolved.OutputPath, openAfterSave: true);

                Log($"Salvat: {_resolved.OutputPath}");
                Log("Deschis in Adobe.");
                foreach (var c in captures)
                    Log($"  [{c.SignerName}] Hash={c.DocumentHash.Substring(0, 16)}... " +
                        $"TSA={(c.TrustedAt.HasValue ? c.TrustedAt.Value.ToString("u") : "indisponibil")}");

                btnFinish.Enabled = false;
                btnCancelLoad.Visible = false;
                btnCancelLoad.Enabled = false;
                cmbTemplate.Enabled = true;
                btnLoad.Enabled = true;
                Log("Gata pentru urmatorul document.");
            }
            catch (Exception ex)
            {
                Log($"EROARE: {ex.Message}");
                MessageBox.Show(ex.Message, "Eroare Salvare", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // ── Mirror ────────────────────────────────────────────────────────────────

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

        // ── PDF Viewer ────────────────────────────────────────────────────────────

        private void RefreshPdfViewer(string pdfPath)
        {
            try
            {
                string copy = Path.Combine(Path.GetTempPath(),
                    $"wacom_viewer_{DateTime.Now:yyyyMMdd_HHmmss_fff}.pdf");

                ClearPdfViewer();
                System.Threading.Thread.Sleep(50);

                File.Copy(pdfPath, copy, overwrite: false);
                _currentViewerPath = copy;
                _currentPdfDoc = PdfDocument.Load(copy);
                pdfViewer.Document = _currentPdfDoc;
                pdfViewer.Renderer.ZoomMode = PdfViewerZoomMode.FitWidth;
                //pdfViewer.Renderer.Zoom = 1.5;

                if (_mirrorActive && _mirrorForm != null && _mirrorForm.Visible)
                    _mirrorForm.LoadFromPath(copy);
            }
            catch (Exception ex)
            {
                Log($"Eroare previzualizare: {ex.Message}");
            }
        }

        private void ClearPdfViewer()
        {
            _syncTimer.Stop();

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

        // ── Helpers ───────────────────────────────────────────────────────────────

        private void ResetState()
        {
            _service?.Dispose();
            _service = null;
            _resolved = null;
            _signatureCount = 0;

            cardsPanel.Controls.Clear();
            _cards.Clear();

            btnFinish.Enabled = false;
            btnCancelLoad.Visible = false;
            btnCancelLoad.Enabled = false;
            cmbTemplate.Enabled = _candidateFolder != null;
            btnLoad.Enabled = _candidateFolder != null;
            lblProgress.Text = "";
        }

        private void UpdateProgress()
        {
            if (_resolved == null) return;
            int signed = _cards.Count(c => c.Signed);
            lblProgress.Text = $"{signed} din {_cards.Count} semnaturi completate";
        }

        private string PromptSignerName()
        {
            if (!string.IsNullOrWhiteSpace(_prefillSignerName))
                return _prefillSignerName;

            using (var dlg = new SignerNameDialog())
                return dlg.ShowDialog() == DialogResult.OK ? dlg.SignerName : null;
        }

        private void Log(string msg) =>
            txtLog.AppendText($"[{DateTime.Now:HH:mm:ss}] {msg}{Environment.NewLine}");

        // ── Form closing ──────────────────────────────────────────────────────────

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            _syncTimer?.Stop();
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
    }
}