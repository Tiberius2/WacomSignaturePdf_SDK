using PdfiumViewer;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using WacomSignaturePdf.Config;
using WacomSignaturePdf.Controls;
using WacomSignaturePdf.Models;
using WacomSignaturePdf.Services;

namespace WacomSignaturePdf.Forms
{
    // Form lightweight pentru semnarea Fiselor de Evaluare Proba Practica.
    // Filtreaza automat dosarele si documentele dupa pattern-ul "Evaluare_Proba_Practica_*.pdf".
    // Constructor fara parametri — invocat direct din Softone (Tip operatie: Dll Form).
    public partial class EvaluareProbaForm : Form
    {
        private const string DocumentPattern = "Evaluare_Proba_Practica_*.pdf";
        private const string TemplateId = "FisaExaminare_V1";

        private string _officialName;
        private string _officialRole;
        private string _selectedPdfPath;
        private string _selectedFolderPath;
        private string _selectedFolderName;
        private DocumentTemplate _template;

        private List<string> _allFolders = new List<string>();
        private List<string> _docPaths = new List<string>();
        private List<SignatureCardPanel> _cards = new List<SignatureCardPanel>();
        private SignatureService _sigService;
        private string _tempViewerPath;
        private bool _captureInProgress;
        private MirrorForm _mirrorForm;
        private bool _mirrorActive;
        private Timer _syncTimer;

        // ── Constructor ───────────────────────────────────────────────────────────
        public EvaluareProbaForm()
        {
            BuildLayout();

            try
            {
                if (S1.xSupp != null)
                {
                    int userId = S1.xSupp.ConnectionInfo.UserId;
                    var ds = S1.xSupp.GetSQLDataSet($"SELECT NAME FROM USERS WHERE USERS.USERS = {userId}");
                    if (ds?.Count > 0) _officialName = ds[0, "NAME"]?.ToString() ?? string.Empty;
                    _officialRole = RoleHelper.GetRole(userId);
                }
            }
            catch { }

            try
            {
                _template = TemplateService.LoadTemplates(AppConfig.TemplatesDir)
                    .FirstOrDefault(t => t.TemplateId == TemplateId);
            }
            catch { }

            _syncTimer = new Timer { Interval = 33 };
            _syncTimer.Tick += SyncMirror;


            deviceStatusLabel.StartPolling();
            oneDriveStatusLabel.StartPolling();
        }

        // ── Folder picker ─────────────────────────────────────────────────────────
        private void OpenFolderPicker()
        {
            if (!Directory.Exists(AppConfig.WorkingRoot)) return;

            var folderNames = Directory.GetDirectories(AppConfig.WorkingRoot)
                .Select(Path.GetFileName)
                .Where(name => Directory.GetFiles(
                    Path.Combine(AppConfig.WorkingRoot, name), DocumentPattern).Length > 0)
                .OrderBy(n => { int s = n.IndexOf(" - ", StringComparison.Ordinal); return s > 0 ? n.Substring(s + 3) : n; })
                .ToList();

            using (var dlg = new CandidatPickerDialog(folderNames, AppConfig.WorkingRoot, lightTheme: true))
            {
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                _selectedFolderPath = dlg.SelectedFolderPath;
                _selectedFolderName = dlg.SelectedFolderName;

                int sep = _selectedFolderName.IndexOf(" - ", StringComparison.Ordinal);
                string displayName = sep > 0 ? _selectedFolderName.Substring(sep + 3).Trim() : _selectedFolderName;

                btnSelectFolder.Text = displayName;
                btnSelectFolder.ForeColor = Color.FromArgb(30, 50, 80);
                btnSelectFolder.Font = new Font("Segoe UI", 9f, FontStyle.Bold);

                LoadDocumentsForFolder(_selectedFolderPath);
            }
        }

        private void OnDocumentSelected()
        {
            if (cmbDocument.SelectedIndex < 0 || cmbDocument.SelectedIndex >= _docPaths.Count) return;
            LoadDocument(_docPaths[cmbDocument.SelectedIndex]);
        }

        private void LoadDocumentsForFolder(string folderPath)
        {
            cmbDocument.Items.Clear();
            _docPaths.Clear();
            ClearPdfViewer();
            ClearCards();

            bool showSigned = toggleSigned.IsOn;
            _docPaths = GetEvaluarePdfs(folderPath)
                .Where(p => showSigned ? IsFullySigned(p) : !IsFullySigned(p))
                .ToList();

            foreach (var p in _docPaths)
                cmbDocument.Items.Add(Path.GetFileName(p));

            if (_docPaths.Count == 0)
            {
                lblSelectedFolder.ForeColor = Color.FromArgb(220, 130, 80);
                lblSelectedFolder.Text = toggleSigned.IsOn
                    ? "Niciun document semnat+sigilat gasit."
                    : "Niciun document Evaluare Proba Practica gasit.";
            }
            else
            {
                lblSelectedFolder.ForeColor = Color.FromArgb(100, 165, 225);
                lblSelectedFolder.Text = $"{_docPaths.Count} document(e) gasite";
                cmbDocument.SelectedIndex = 0;
            }
        }

        private void OnToggleChanged()
        {
            bool isSigned = toggleSigned.IsOn;
            lblToggleLeft.ForeColor = !isSigned ? Color.FromArgb(60, 130, 210) : Color.FromArgb(150, 185, 220);
            lblToggleLeft.Font = new Font("Segoe UI", 8f, !isSigned ? FontStyle.Bold : FontStyle.Regular);
            lblToggleRight.ForeColor = isSigned ? Color.FromArgb(60, 130, 210) : Color.FromArgb(150, 185, 220);
            lblToggleRight.Font = new Font("Segoe UI", 8f, isSigned ? FontStyle.Bold : FontStyle.Regular);
            if (_selectedFolderPath != null) LoadDocumentsForFolder(_selectedFolderPath);
        }

        // ── Document loading ──────────────────────────────────────────────────────
        private void LoadDocument(string pdfPath)
        {
            ClearPdfViewer();
            ClearCards();

            try
            {
                _selectedPdfPath = pdfPath;
                var resolvedSlots = ResolveTemplateSlots();
                _sigService = new SignatureService(pdfPath, _officialName ?? "", resolvedSlots);

                var state = SignatureService.ReadSigningState(pdfPath);
                BuildCards(resolvedSlots, state?.Slots);

                RefreshPdfViewer(pdfPath);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Eroare", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // Rezolva sloturile din FisaExaminare_V1.json (placeholder {{OfficialName}} inlocuit).
        private List<SignatureSlot> ResolveTemplateSlots()
        {
            if (_template?.Signatures == null) return new List<SignatureSlot>();

            return _template.Signatures.Select(s => new SignatureSlot
            {
                SignatureId = s.SignatureId,
                SignerName = s.SignerName,
                ResolvedSignerName = (s.SignerName ?? "").Replace("{{OfficialName}}", _officialName ?? ""),
                Reason = s.Reason,
                Page = s.Page,
                ResolvedPage = int.TryParse(s.Page, out int p) ? p : 1,
                Party = s.Party,
                OfficialRole = s.OfficialRole,
                Location = s.Location,
                Required = s.Required,
                Biometric = s.Biometric,
            }).ToList();
        }

        // ── Signature cards ───────────────────────────────────────────────────────
        // templateSlots = sloturile fixe din FisaExaminare_V1.json.
        // signedEntries = signing-state.json existent in PDF (poate fi null pentru un document nou).
        private void BuildCards(List<SignatureSlot> templateSlots, List<SigningStateEntry> signedEntries)
        {
            cardsPanel.Controls.Clear();
            _cards.Clear();

            var signedById = signedEntries?
                .Where(e => e.Signed)
                .ToDictionary(e => e.SignatureId)
                ?? new Dictionary<int, SigningStateEntry>();

            foreach (var slot in templateSlots)
            {
                slot.SignerName = slot.ResolvedSignerName;

                var card = new SignatureCardPanel(slot) { Width = cardsPanel.Width - 8 };
                if (signedById.TryGetValue(slot.SignatureId, out var signedEntry))
                    card.MarkSigned(signedEntry.ActualSignerName ?? signedEntry.SignerName);

                card.CardClicked += OnCardClicked;
                cardsPanel.Controls.Add(card);
                _cards.Add(card);
            }

            ReflowCards();
            UpdateCardCount();
        }

        private void ReflowCards()
        {
            int y = 6;
            foreach (SignatureCardPanel card in cardsPanel.Controls)
            {
                card.Location = new Point(4, y);
                y += card.Height + 6;
            }
        }

        private void UpdateCardCount()
        {
            int signed = _cards.Count(c => c.Signed);
            lblCardCount.Text = _cards.Count > 0
                ? $"{signed} din {_cards.Count} semnaturi completate"
                : "";
        }

        // ── Card click / capture ──────────────────────────────────────────────────
        private void OnCardClicked(SignatureSlot slot)
        {
            if (_captureInProgress || _selectedPdfPath == null) return;
            var card = _cards.FirstOrDefault(c => c.Slot.SignatureId == slot.SignatureId);
            if (card == null || card.Signed) return;

            if (!string.IsNullOrEmpty(slot.OfficialRole)
                && slot.Party == "Official"
                && slot.OfficialRole != _officialRole)
            {
                var ans = MessageBox.Show(
                    $"Aceasta semnatura este asignata rolului \"{slot.OfficialRole}\".\n" +
                    $"Rolul dvs. curent este \"{(string.IsNullOrEmpty(_officialRole) ? "nespecificat" : _officialRole)}\".\n\nContinuati?",
                    "Rol diferit", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                if (ans != DialogResult.Yes) return;
            }

            _captureInProgress = true;
            foreach (SignatureCardPanel c in cardsPanel.Controls)
                if (!c.Signed) c.Enabled = false;

            var thread = new System.Threading.Thread(() =>
            {
                Exception caughtEx = null;
                bool cancelled = false;
                try
                {
                    _sigService.CaptureAndEmbed(
                        slot.SignatureId, slot.Party, slot.SignerName ?? _officialName,
                        slot.Reason, slot.ResolvedPage,
                        slot.Location.X, slot.Location.Y,
                        slot.Location.W, slot.Location.H, false);
                }
                catch (OperationCanceledException) { cancelled = true; }
                catch (Exception ex) { caughtEx = ex; }

                this.Invoke(new Action(() =>
                {
                    _captureInProgress = false;
                    foreach (SignatureCardPanel c in cardsPanel.Controls)
                        if (!c.Signed) c.Enabled = true;

                    if (cancelled) return;

                    if (caughtEx != null)
                    {
                        MessageBox.Show(caughtEx.Message, "Eroare", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }

                    card.MarkSigned(_officialName);
                    UpdateCardCount();

                    Task.Run(() => _sigService.SaveIntermediate())
                        .ContinueWith(_ => RefreshPdfViewer(_selectedPdfPath),
                            TaskScheduler.FromCurrentSynchronizationContext());
                }));
            });
            thread.SetApartmentState(System.Threading.ApartmentState.STA);
            thread.IsBackground = true;
            thread.Start();
        }

        // ── PDF Viewer ────────────────────────────────────────────────────────────
        private void RefreshPdfViewer(string pdfPath)
        {
            try
            {
                string copy = Path.Combine(Path.GetTempPath(),
                    $"ep_viewer_{DateTime.Now:yyyyMMddHHmmssfff}.pdf");
                File.Copy(pdfPath, copy, overwrite: true);
                if (_tempViewerPath != null && _tempViewerPath != copy && File.Exists(_tempViewerPath))
                    try { File.Delete(_tempViewerPath); } catch { }
                _tempViewerPath = copy;

                pdfOverlay.LoadDocument(copy, fitPage: true);
                btnZoomIn.Enabled = btnZoomOut.Enabled = true;

                RefreshPreviewSlots();
            }
            catch { }
        }

        // Construieste ghost slots (verde=semnat, galben=nesemnat accesibil, rosu=restrictionat)
        // pe baza sloturilor din template si a cardurilor curente.
        private void RefreshPreviewSlots()
        {
            if (_cards.Count == 0)
            {
                pdfOverlay.ClearPreviewSlots();
                return;
            }

            var rects = new DrawnRectangle[_cards.Count];
            var signed = new bool[_cards.Count];
            for (int i = 0; i < _cards.Count; i++)
            {
                var slot = _cards[i].Slot;
                bool accessible = slot.Party != "Official"
                    || string.IsNullOrEmpty(slot.OfficialRole)
                    || slot.OfficialRole == _officialRole;

                rects[i] = new DrawnRectangle
                {
                    Page = slot.ResolvedPage,
                    X = slot.Location.X,
                    Y = slot.Location.Y,
                    W = slot.Location.W,
                    H = slot.Location.H,
                    RoleLabel = !string.IsNullOrEmpty(slot.OfficialRole) ? slot.OfficialRole : slot.Party,
                    IsAccessible = accessible,
                };
                signed[i] = _cards[i].Signed;
            }
            pdfOverlay.SetPreviewSlots(rects, signed);
        }

        private void ClearPdfViewer()
        {
            _syncTimer.Stop();
            btnZoomIn.Enabled = btnZoomOut.Enabled = false;
            pdfOverlay.UnloadDocument();

            if (_tempViewerPath != null && File.Exists(_tempViewerPath))
                try { File.Delete(_tempViewerPath); } catch { }
            _tempViewerPath = null;

            if (_mirrorActive) _syncTimer.Start();
        }

        private void ClearCards()
        {
            cardsPanel.Controls.Clear();
            _cards.Clear();
            _sigService = null;
            lblCardCount.Text = "";
        }

        // ── Mirror ────────────────────────────────────────────────────────────────
        private void btnMirror_Click(object sender, EventArgs e)
        {
            if (!_mirrorActive && !pdfOverlay.HasDocument)
            {
                MessageBox.Show("Nu exista niciun document incarcat.", "Fara Document",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (_mirrorActive) { CloseMirror(); return; }

            var screen = Screen.AllScreens.FirstOrDefault(s => !s.Primary);
            if (screen == null)
            {
                MessageBox.Show("Nu s-a detectat un al doilea monitor.", "Fara Monitor Secundar",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (_mirrorForm == null) _mirrorForm = new MirrorForm();
            if (_tempViewerPath != null) _mirrorForm.LoadFromPath(_tempViewerPath);
            _mirrorForm.ShowOnScreen(screen);
            _mirrorActive = true;
            btnMirror.Text = "✕  Inchide Oglindire";
            btnMirror.BackColor = Color.FromArgb(160, 40, 40);
            _syncTimer.Start();
        }

        private void CloseMirror()
        {
            _syncTimer.Stop();
            _mirrorForm?.Hide();
            _mirrorActive = false;
            btnMirror.Text = "⊞  Oglindire";
            btnMirror.BackColor = Color.FromArgb(40, 70, 130);
        }

        private void SyncMirror(object sender, EventArgs e)
        {
            if (!_mirrorActive || _mirrorForm == null || pdfOverlay?.Renderer == null) return;
            try
            {
                _mirrorForm.SyncPage(pdfOverlay.Renderer.Page);
                _mirrorForm.SyncZoom(pdfOverlay.Renderer.Zoom);
            }
            catch { }
        }

        // ── Helpers ───────────────────────────────────────────────────────────────
        private static List<string> GetEvaluarePdfs(string folderPath)
        {
            if (!Directory.Exists(folderPath)) return new List<string>();
            return Directory.GetFiles(folderPath, DocumentPattern, SearchOption.TopDirectoryOnly)
                .OrderBy(f => f)
                .ToList();
        }

        private static bool IsFullySigned(string pdfPath)
        {
            try
            {
                var state = SignatureService.ReadSigningState(pdfPath);
                return state?.Finalized == true
                    || (state?.Slots != null && state.Slots.Count > 0 && state.Slots.All(s => s.Signed));
            }
            catch { return false; }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            _syncTimer?.Stop();
            _mirrorForm?.Close();
            pdfOverlay?.Dispose();
            deviceStatusLabel?.StopPolling();
            oneDriveStatusLabel?.StopPolling();
            if (_tempViewerPath != null && File.Exists(_tempViewerPath))
                try { File.Delete(_tempViewerPath); } catch { }
            base.OnFormClosing(e);
        }
    }
}