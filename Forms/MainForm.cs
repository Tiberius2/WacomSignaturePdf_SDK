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
using WacomSignaturePdf.Theme;

namespace WacomSignaturePdf.Forms
{
    public partial class MainForm : Form
    {
        #region Fields

        private DocumentSession _session;
        private List<DocumentTemplate> _templates;
        private List<DocumentTemplate> _visibleTemplates = new List<DocumentTemplate>();
        private string _candidateFolder;
        private List<string> _allFolders = new List<string>();
        private List<SignatureCardPanel> _cards = new List<SignatureCardPanel>();

        private MirrorForm _mirrorForm;
        internal bool _mirrorActive;
        private Timer _syncTimer;
        private PointF _lastScrollRatio = PointF.Empty;
        private double _lastZoom = -1;
        private int _lastPage = -1;

        private string _prefillSignerName;
        private string _officialName;
        private string _officialRole;
        private string _candidateSignerName;

        private PdfDocument _currentPdfDoc;
        private string _currentViewerPath;

        private enum SigningParty { Candidate, Official }
        private SigningParty _currentParty = SigningParty.Candidate;

        private bool _captureInProgress;
        private bool _suppressToggleEvents;
        private FileSystemWatcher _folderWatcher;
        private ShellForm _embeddedShell;

        private bool FilterMyOnly => toggleFilter.IsOn;

        #endregion

        #region Constructors

        public MainForm()
        {
            DoubleBuffered = true;
            BuildLayout();
            cmbTemplate.SelectionChangeCommitted += (s, e) => OnTemplateSelectionCommitted();
            toggleFilter.Toggled += OnFilterToggled;

            if (string.IsNullOrWhiteSpace(AppConfig.WorkingRoot))
            {
                MessageBox.Show(
                    "Variabila de mediu 'RecruitmentDocsPath' nu este configurata pe aceasta masina.\n\n" +
                    "Contactati administratorul IT.",
                    "Configurare lipsa", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Load += (s, e) => Close();
                return;
            }

            if (S1.xSupp != null)
            {
                try
                {
                    int userId = S1.xSupp.ConnectionInfo.UserId;
                    var result = S1.xSupp.GetSQLDataSet($"SELECT NAME FROM USERS WHERE USERS.USERS = {userId}");
                    if (result?.Count > 0)
                        _officialName = _prefillSignerName = result[0, "NAME"]?.ToString() ?? string.Empty;
                    _officialRole = RoleHelper.GetRole(userId);
                }
                catch { }
            }

            LoadTemplates();
            PopulateFolderDropdown();
            InitFolderWatcher();
            InitSyncTimer();
            deviceStatusLabel.StartPolling();
            oneDriveStatusLabel.StartPolling();
            UpdateCurrentSignerLabel();
            OnFilterToggled(this, EventArgs.Empty);
        }

        // Full constructor used by ShellForm/Program
        public MainForm(string personId, string signerName, string officialName,
                        string officialRole = "", ShellForm embeddedShell = null) : this()
        {
            _prefillSignerName = signerName;
            _officialName = officialName;
            _officialRole = officialRole ?? "";
            _embeddedShell = embeddedShell;
            PopulateFolderDropdown();
            UpdateCurrentSignerLabel();
            OnFilterToggled(this, EventArgs.Empty);
            TryPreselectFolder(personId);
        }

        internal bool HasUnsavedWork => _session?.SignatureCount > 0;

        internal void SaveProgressNow()
        {
            if (_session?.SignatureCount > 0)
                btnSaveProgress_Click(this, EventArgs.Empty);
        }

        internal void UnloadCurrent(bool silent = false)
        {
            if (!silent && _session?.SignatureCount > 0)
                using (var dlg = new ResetOrUnloadDialog())
                    if (dlg.ShowDialog(this) == DialogResult.Cancel) return;
            ResetState();
        }

        // Detaches panelSidebar from MainForm into the ShellForm sidebar zone.
        // Lifts controls up by titleH, applies Template theme colors.
        internal void DetachSidebarInto(Control targetPanel)
        {
            if (panelSidebar == null) return;
            const int titleH = 72;

            if (lblAppTitle != null)
            {
                panelSidebar.Controls.Remove(lblAppTitle);
                lblAppTitle.Dispose();
                lblAppTitle = null;
            }

            foreach (Control c in panelSidebar.Controls)
                if (c.Dock == DockStyle.None)
                    c.Top = Math.Max(0, c.Top - titleH);

            var theme = AppTheme.Template;
            panelSidebar.BackColor = theme.SidebarBg;
            if (panelBottom != null) panelBottom.BackColor = theme.SidebarTitleBg;

            foreach (Control c in panelSidebar.Controls)
                if (c is Label lbl)
                {
                    if (lbl.Font.Bold && lbl.Height <= 20 && lbl.Width > 60)
                        lbl.ForeColor = theme.SectionLabel;
                    else if (!lbl.Font.Bold && lbl.ForeColor.B > 150)
                        lbl.ForeColor = theme.SidebarSub;
                }

            this.Controls.Remove(panelSidebar);
            panelSidebar.Dock = DockStyle.Fill;
            targetPanel.Controls.Add(panelSidebar);
            targetPanel.Layout += (s, e) => RecalcButtonPositions();
            RecalcButtonPositions();
        }

        #endregion

        #region Filter & Party Toggle

        private void OnFilterToggled(object sender, EventArgs e)
        {
            UpdateFilterUI();

            SigningParty targetParty = FilterMyOnly ? SigningParty.Official : SigningParty.Candidate;
            if (_currentParty != targetParty)
            {
                _suppressToggleEvents = true;
                toggleParty.IsOn = FilterMyOnly;
                _suppressToggleEvents = false;
                _currentParty = targetParty;
                UpdatePartyLabels();
                UpdateCurrentSignerLabel();
            }

            PopulateFolderDropdown();
            if (_candidateFolder != null) UpdateTemplateStatusIcons();
            ReflowCards();
            UpdateProgress();
        }

        private void OnPartyToggled()
        {
            if (_suppressToggleEvents) return;
            _currentParty = toggleParty.IsOn ? SigningParty.Official : SigningParty.Candidate;
            UpdatePartyLabels();
            UpdateCurrentSignerLabel();
            ReflowCards();
            UpdateProgress();
        }

        // Updates filter toggle labels and locks party toggle in FilterMyOnly mode.
        private void UpdateFilterUI()
        {
            bool myOnly = FilterMyOnly;

            lblFilterLeft.ForeColor = !myOnly ? AppTheme.AccentGreen : AppTheme.SidebarSub;
            lblFilterLeft.Font = new Font("Segoe UI", 9.5f, !myOnly ? FontStyle.Bold : FontStyle.Regular);
            lblFilterRight.ForeColor = myOnly ? AppTheme.AccentBlue : AppTheme.SidebarSub;
            lblFilterRight.Font = new Font("Segoe UI", 9.5f, myOnly ? FontStyle.Bold : FontStyle.Regular);

            toggleParty.Enabled = !myOnly;
            lblPartyCandidate.ForeColor = myOnly ? AppTheme.SidebarSub
                : (_currentParty == SigningParty.Candidate ? AppTheme.AccentBlue : AppTheme.SidebarSub);
            lblPartyOfficial.ForeColor = myOnly ? AppTheme.AccentGreen
                : (_currentParty == SigningParty.Official ? AppTheme.AccentGreen : AppTheme.SidebarSub);
        }

        private void UpdatePartyLabels()
        {
            bool isCandidate = _currentParty == SigningParty.Candidate;
            lblPartyCandidate.ForeColor = isCandidate ? AppTheme.AccentBlue : AppTheme.SidebarSub;
            lblPartyCandidate.Font = new Font("Segoe UI", 9.5f, isCandidate ? FontStyle.Bold : FontStyle.Regular);
            lblPartyOfficial.ForeColor = !isCandidate ? AppTheme.AccentGreen : AppTheme.SidebarSub;
            lblPartyOfficial.Font = new Font("Segoe UI", 9.5f, !isCandidate ? FontStyle.Bold : FontStyle.Regular);
        }

        private void UpdateCurrentSignerLabel()
        {
            if (chkManualSigner.Checked) { lblCurrentSigner.Text = "-"; return; }
            string name = _currentParty == SigningParty.Official ? _officialName : _candidateSignerName;
            lblCurrentSigner.Text = name ?? "";
        }

        #endregion

        #region Template Loading

        private void LoadTemplates()
        {
            try
            {
                _templates = TemplateService.LoadTemplates(AppConfig.TemplatesDir);
                _visibleTemplates = new List<DocumentTemplate>(_templates);
                cmbTemplate.Items.Clear();
                foreach (var t in _templates) cmbTemplate.Items.Add(t.TemplateName);
                if (cmbTemplate.Items.Count > 0) cmbTemplate.SelectedIndex = 0;
                cmbTemplate.SetMultiDocFlags(MultiDocFlags());
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Eroare Template", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        #endregion

        #region Folder Picker

        private void InitFolderWatcher()
        {
            try
            {
                if (!Directory.Exists(AppConfig.WorkingRoot)) return;
                _folderWatcher = new FileSystemWatcher(AppConfig.WorkingRoot)
                {
                    NotifyFilter = NotifyFilters.DirectoryName,
                    EnableRaisingEvents = true
                };
                _folderWatcher.Created += (s, e) => BeginInvoke(new Action(PopulateFolderDropdown));
                _folderWatcher.Deleted += (s, e) => BeginInvoke(new Action(PopulateFolderDropdown));
                _folderWatcher.Renamed += (s, e) => BeginInvoke(new Action(PopulateFolderDropdown));
            }
            catch { }
        }

        private void PopulateFolderDropdown()
        {
            if (!Directory.Exists(AppConfig.WorkingRoot)) return;

            var activeIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                var ds = S1.xSupp?.GetSQLDataSet("SELECT PRSN FROM PRSN WHERE ISACTIVE = 1");
                if (ds != null)
                    for (int i = 0; i < ds.Count; i++)
                        activeIds.Add(ds[i, "PRSN"]?.ToString() ?? string.Empty);
            }
            catch { }

            _allFolders = Directory.GetDirectories(AppConfig.WorkingRoot)
                .Select(System.IO.Path.GetFileName)
                .Where(name =>
                {
                    if (activeIds.Count == 0) return true;
                    int sep = name.IndexOf(" - ", StringComparison.Ordinal);
                    if (sep < 0) sep = name.IndexOf('-');
                    string folderId = sep >= 0 ? name.Substring(0, sep).Trim() : name;
                    return activeIds.Contains(folderId);
                })
                .OrderBy(n => n)
                .ToList();
        }

        private void OpenFolderPicker()
        {
            if (!Directory.Exists(AppConfig.WorkingRoot)) return;

            IEnumerable<string> folderNames = Directory.GetDirectories(AppConfig.WorkingRoot)
                .Select(Path.GetFileName)
                .OrderBy(n =>
                {
                    int sep = n.IndexOf(" - ", StringComparison.Ordinal);
                    return sep > 0 ? n.Substring(sep + 3) : n;
                });

            // Cand filtrul e activ, pastreaza doar folderele care au
            // cel putin un document cu sloturi pentru rolul curent
            if (FilterMyOnly && _templates != null && !string.IsNullOrEmpty(_officialRole))
            {
                folderNames = folderNames.Where(folderName =>
                {
                    string folderPath = Path.Combine(AppConfig.WorkingRoot, folderName);
                    return _templates.Any(t =>
                        t.Signatures.Any(s => s.Party == "Official" && s.OfficialRole == _officialRole)
                        && TemplateService.GetDocumentStatus(t, folderPath) != TemplateService.DocumentStatus.NotFound);
                });
            }

            using (var dlg = new CandidatPickerDialog(folderNames.ToList(), AppConfig.WorkingRoot))
            {
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                SelectFolder(dlg.SelectedFolderPath, dlg.SelectedFolderName);
            }
        }

        // Aplica selectia unui dosar candidat (din picker sau din preselectie automata).
        private void SelectFolder(string folderPath, string folderName)
        {
            int sep = folderName.IndexOf(" - ", StringComparison.Ordinal);
            string displayName = sep > 0 ? folderName.Substring(sep + 3).Trim() : folderName;

            _candidateFolder = folderPath;
            _candidateSignerName = _prefillSignerName = TemplateService.GetCandidateName(folderPath);

            lblSelectedFolderName.Text = displayName.ToUpper();
            lblSelectedFolderName.BackColor = Color.FromArgb(151, 222, 162);
            cmbTemplate.Enabled = btnLoad.Enabled = true;
            UpdateCurrentSignerLabel();
            UpdateTemplateStatusIcons();
        }

        // Preselecteaza dosarul candidatului cand formul e deschis direct din dosarul personal.
        private void TryPreselectFolder(string personId)
        {
            if (string.IsNullOrWhiteSpace(personId) || !Directory.Exists(AppConfig.WorkingRoot)) return;

            string match = Directory.GetDirectories(AppConfig.WorkingRoot)
                .Select(Path.GetFileName)
                .FirstOrDefault(name =>
                {
                    int sep = name.IndexOf(" - ", StringComparison.Ordinal);
                    if (sep < 0) sep = name.IndexOf('-');
                    string folderId = sep >= 0 ? name.Substring(0, sep).Trim() : name;
                    return folderId.Equals(personId, StringComparison.OrdinalIgnoreCase);
                });

            if (match == null) return;
            SelectFolder(Path.Combine(AppConfig.WorkingRoot, match), match);
        }



        #endregion

        #region Template Status Icons

        private void UpdateTemplateStatusIcons()
        {
            if (_templates == null) return;

            string currentTemplateId = _session?.Resolved?.Template?.TemplateId
                ?? (_visibleTemplates != null && cmbTemplate.SelectedIndex >= 0
                    && cmbTemplate.SelectedIndex < _visibleTemplates.Count
                    ? _visibleTemplates[cmbTemplate.SelectedIndex]?.TemplateId : null);

            _visibleTemplates = new List<DocumentTemplate>();
            var colors = new List<Color>();

            foreach (var template in _templates)
            {
                if (FilterMyOnly && !template.Signatures.Any(s =>
                    s.Party == "Official" && s.OfficialRole == _officialRole)) continue;

                var status = TemplateService.GetDocumentStatus(template, _candidateFolder);
                if (status == TemplateService.DocumentStatus.NotFound) continue;

                Color color;
                if (status == TemplateService.DocumentStatus.SignedSealed)
                    color = DocumentTypeDropdown.ColorSignedSealed;
                else if (status == TemplateService.DocumentStatus.SignedUnsealed)
                    color = DocumentTypeDropdown.ColorSignedUnsealed;
                else if (status == TemplateService.DocumentStatus.PartialSigned)
                    color = DocumentTypeDropdown.ColorPartialSigned;
                else
                    color = DocumentTypeDropdown.ColorUnsigned;

                _visibleTemplates.Add(template);
                colors.Add(color);
            }

            cmbTemplate.Items.Clear();
            foreach (var t in _visibleTemplates) cmbTemplate.Items.Add(t.TemplateName);

            int restoreIndex = 0;
            if (currentTemplateId != null)
            {
                int idx = _visibleTemplates.FindIndex(t => t.TemplateId == currentTemplateId);
                if (idx >= 0) restoreIndex = idx;
            }

            if (cmbTemplate.Items.Count > 0) cmbTemplate.SelectedIndex = restoreIndex;

            cmbTemplate.SetStatusImages(new List<Image>(new Image[_visibleTemplates.Count]), colors);
            cmbTemplate.SetMultiDocFlags(MultiDocFlags());
        }

        private List<bool> MultiDocFlags() =>
            _visibleTemplates.Select(t =>
                t.FileSystemBlock.IsMultiDocument &&
                (_candidateFolder == null || TemplateService.GetMatchingFiles(t, _candidateFolder).Count > 1)
            ).ToList();

        #endregion

        #region Load Document

        private void OnTemplateSelectionCommitted()
        {
            if (cmbTemplate.SelectedIndex < 0 || _candidateFolder == null) return;
            var template = _visibleTemplates[cmbTemplate.SelectedIndex];
            if (template.FileSystemBlock.IsMultiDocument) { ShowMultiDocumentFlyout(template); return; }
            if (_session != null && !CancelCurrentDocument()) return;
            string signerName = PromptSignerName(_prefillSignerName);
            if (signerName != null) LoadDocumentFromTemplate(template, signerName, null);
        }

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

            var template = _visibleTemplates[cmbTemplate.SelectedIndex];
            if (template.FileSystemBlock.IsMultiDocument) { ShowMultiDocumentFlyout(template); return; }
            if (_session != null && !CancelCurrentDocument()) return;
            string signerName = PromptSignerName(_prefillSignerName);
            if (signerName != null) LoadDocumentFromTemplate(template, signerName, null);
        }

        private void ShowMultiDocumentFlyout(DocumentTemplate template)
        {
            var files = TemplateService.GetMatchingFiles(template, _candidateFolder);
            if (files.Count == 0)
            {
                MessageBox.Show("Nu s-au gasit documente pentru acest template in dosarul candidatului.",
                    "Fara Documente", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (files.Count == 1)
            {
                if (_session != null && !CancelCurrentDocument()) return;
                string name = PromptSignerName(_prefillSignerName);
                if (name != null) LoadDocumentFromTemplate(template, name, files[0].FilePath);
                return;
            }

            var flyout = new MultiDocFlyout(files, btnLoad.Right - cmbTemplate.Left);
            flyout.FileSelected += filePath =>
            {
                if (_session != null && !CancelCurrentDocument()) return;
                string name = PromptSignerName(_prefillSignerName);
                if (name != null) LoadDocumentFromTemplate(template, name, filePath);
            };
            flyout.Location = cmbTemplate.PointToScreen(new Point(0, cmbTemplate.Height));
            flyout.Show(this);
        }

        private void LoadDocumentFromTemplate(DocumentTemplate template, string signerName, string specificFilePath)
        {
            try
            {
                ResetState();
                var resolved = TemplateService.Resolve(template, _candidateFolder, signerName, _officialName ?? "", specificFilePath);
                _session = new DocumentSession(resolved, new SignatureService(resolved.PdfPath, "", resolved.Slots));

                btnCancelLoad.Visible = btnCancelLoad.Enabled = true;
                btnSaveProgress.Visible = true;
                btnSaveProgress.Enabled = false;
                chkManualSigner.Checked = false;

                bool startOfficial = !resolved.Slots.Any(s => string.IsNullOrEmpty(s.Party) || s.Party == "Candidate")
                                     || FilterMyOnly;
                _suppressToggleEvents = true;
                toggleParty.IsOn = startOfficial;
                _suppressToggleEvents = false;
                _currentParty = startOfficial ? SigningParty.Official : SigningParty.Candidate;
                _candidateSignerName = signerName;
                UpdatePartyLabels();
                UpdateCurrentSignerLabel();

                lblPreviewCaption.Text = Path.GetFileName(resolved.PdfPath);
                BuildCards(resolved.Slots);
                RefreshPdfViewer(resolved.PdfPath);
                LoadSigningState();
                UpdateGhostSlots();
                UpdateProgress();
            }
            catch (DocumentAlreadyFinalizedException ex)
            {
                _session = null;
                ErrorDialog.Show(this, ex.Message, ErrorKind.DocumentFinalized);
            }
            catch (DocumentSignedNotSealedException ex)
            {
                _session = null;
                ErrorDialog.Show(this, ex.Message, ErrorKind.DocumentSignedNotSealed);
                try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName = ex.SemnatPath, UseShellExecute = true }); }
                catch { }
            }
            catch (Exception ex)
            {
                _session = null;
                ErrorDialog.Show(this, ex.Message, ex is FileNotFoundException ? ErrorKind.FileNotFound : ErrorKind.General);
            }
        }

        #endregion

        #region Signing State Restore

        private void LoadSigningState()
        {
            if (_session == null) return;
            var state = SignatureService.ReadSigningState(_session.Resolved.PdfPath);
            if (state?.Slots == null) return;

            foreach (var entry in state.Slots.Where(s => s.Signed))
            {
                var card = _cards.FirstOrDefault(c => c.Slot.SignatureId == entry.SignatureId);
                if (card == null) continue;
                card.MarkSigned(entry.ActualSignerName ?? entry.SignerName);
                _session.SignatureCount++;
            }

            UpdateProgress();
            CheckFinishEnabled();
        }

        #endregion

        #region Cancel / Unload Document

        private bool CancelCurrentDocument()
        {
            string pdfPath = _session?.Resolved?.PdfPath;
            string backupPath = pdfPath != null
                ? Path.Combine(Path.GetDirectoryName(pdfPath), "Originally Generated Documents", Path.GetFileName(pdfPath))
                : null;
            bool backupExists = backupPath != null && File.Exists(backupPath);

            UnloadAction action;
            if (!(_session?.Service.HasNewCaptures ?? false))
            {
                action = UnloadAction.DiscardSession;
            }
            else if (backupExists)
            {
                using (var dlg = new ResetOrUnloadDialog())
                {
                    if (dlg.ShowDialog(this) == DialogResult.Cancel) return false;
                    action = dlg.SelectedAction;
                }
            }
            else
            {
                if (MessageBox.Show("Exista semnaturi capturate. Sigur doriti sa descarcati documentul?",
                    "Confirmare", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
                    return false;
                action = UnloadAction.DiscardSession;
            }

            if (_mirrorActive) CloseMirror();

            try
            {
                switch (action)
                {
                    case UnloadAction.SaveAndClose:
                        ClearPdfViewer();
                        _session.Service.SaveProgress();
                        break;
                    case UnloadAction.ResetToOriginal:
                        _session?.Service.RestoreToSessionStart();
                        ClearPdfViewer();
                        if (backupExists) File.Copy(backupPath, pdfPath, overwrite: true);
                        break;
                    default:
                        _session?.Service.RestoreToSessionStart();
                        ClearPdfViewer();
                        break;
                }
            }
            catch (IOException ex)
            {
                MessageBox.Show(ex.Message, "Fisier blocat", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }

            ResetState();
            PopulateFolderDropdown();
            cmbTemplate.Enabled = btnLoad.Enabled = _candidateFolder != null;
            return true;
        }

        #endregion

        #region Save Progress

        private void btnSaveProgress_Click(object sender, EventArgs e)
        {
            if (_session?.SignatureCount == 0) return;
            try
            {
                _session.Service.SaveProgress();
                ClearPdfViewer();
                MessageBox.Show("Progresul a fost salvat si documentul a fost eliberat din aplicatie.",
                    "Salvat", MessageBoxButtons.OK, MessageBoxIcon.Information);
                ResetState();
                PopulateFolderDropdown();
                cmbTemplate.Enabled = btnLoad.Enabled = _candidateFolder != null;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Eroare", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        #endregion

        #region Signature Cards

        private void BuildCards(List<SignatureSlot> slots)
        {
            cardsPanel.Controls.Clear();
            _cards.Clear();
            foreach (var slot in slots)
            {
                var card = new SignatureCardPanel(slot) { Width = cardsPanel.Width - 12 };
                card.CardClicked += OnCardClicked;
                cardsPanel.Controls.Add(card);
                _cards.Add(card);
            }
            ReflowCards();
        }

        private void ReflowCards()
        {
            string party = _currentParty == SigningParty.Candidate ? "Candidate" : "Official";
            bool imputernicire = chkManualSigner.Checked;

            cardsPanel.SuspendLayout();
            int y = 6, visibleCount = 0;

            foreach (var card in _cards)
            {
                bool partyMatch = string.IsNullOrEmpty(card.Slot.Party) || card.Slot.Party == party;

                if (FilterMyOnly)
                {
                    bool isMatchingOfficial = card.Slot.Party == "Official"
                        && !string.IsNullOrEmpty(card.Slot.OfficialRole)
                        && card.Slot.OfficialRole == _officialRole;
                    card.Visible = isMatchingOfficial;
                    if (isMatchingOfficial)
                    {
                        if (!card.Signed) card.SetRoleRestricted(false);
                        card.Location = new Point(6, y);
                        y += card.Height + 6;
                        visibleCount++;
                    }
                    continue;
                }

                if (!partyMatch) { card.Visible = false; continue; }

                bool roleMatch = party != "Official"
                    || string.IsNullOrEmpty(card.Slot.OfficialRole)
                    || card.Slot.OfficialRole == _officialRole;

                card.Visible = true;
                if (!card.Signed) card.SetRoleRestricted(!imputernicire && !roleMatch);
                card.Location = new Point(6, y);
                y += card.Height + 6;
                visibleCount++;
            }

            if (visibleCount == 0)
                foreach (var card in _cards) card.Visible = false;

            cardsPanel.ResumeLayout();
        }

        private void SetCardsEnabled(bool enabled)
        {
            foreach (var c in _cards)
                if (!c.Signed && !c.RoleRestricted) c.Enabled = enabled;
        }

        #endregion

        #region Signature Capture

        private void OnCardClicked(SignatureSlot slot)
        {
            if (_session == null || _captureInProgress) return;

            var card = _cards.FirstOrDefault(c => c.Slot.SignatureId == slot.SignatureId);
            if (card == null || card.Signed || card.RoleRestricted) return;

            string prefill = _currentParty == SigningParty.Official ? _officialName : _candidateSignerName;
            string signerName = chkManualSigner.Checked || string.IsNullOrWhiteSpace(slot.ResolvedSignerName)
                ? PromptSignerName(prefill, slot.Reason)
                : slot.ResolvedSignerName;
            if (signerName == null) return;

            bool isImputernicire = chkManualSigner.Checked;

            if (SignatureService.IsFileLocked(_session.Resolved.PdfPath))
            {
                MessageBox.Show(
                    $"Fisierul '{Path.GetFileName(_session.Resolved.PdfPath)}' este deschis in alta aplicatie.\n" +
                    "Inchideti documentul si incercati din nou.",
                    "Fisier blocat", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            SetCardsEnabled(false);
            _captureInProgress = true;

            var thread = new System.Threading.Thread(() =>
            {
                Exception caughtEx = null;
                bool cancelled = false;
                try
                {
                    _session.Service.CaptureAndEmbed(
                        slot.SignatureId, slot.Party, signerName, slot.Reason,
                        slot.ResolvedPage, slot.Location.X, slot.Location.Y,
                        slot.Location.W, slot.Location.H, isImputernicire);
                }
                catch (OperationCanceledException) { cancelled = true; }
                catch (Exception ex) { caughtEx = ex; }

                Control invokeTarget = panelSidebar?.IsHandleCreated == true ? panelSidebar
                    : _embeddedShell?.IsHandleCreated == true ? (Control)_embeddedShell : this;

                invokeTarget.Invoke(new Action(() =>
                {
                    _captureInProgress = false;
                    SetCardsEnabled(true);
                    if (cancelled) return;

                    if (caughtEx != null)
                    {
                        bool isDeviceError = (caughtEx.Message + caughtEx.GetType().Name)
                            .IndexOfAny(new[] { 'S', 'C', 'D', 'F', 'L' }) >= 0 &&
                            new[] { "STU", "device", "pad", "DynCapt", "Florentis", "COMException", "Licensed" }
                            .Any(k => (caughtEx.Message + caughtEx.GetType().Name).Contains(k));

                        ErrorDialog.Show(this,
                            isDeviceError
                                ? "Dispozitivul de semnatura nu este conectat sau nu este disponibil.\n\nConectati tableta Wacom si reincercati."
                                : caughtEx.Message,
                            isDeviceError ? ErrorKind.DeviceNotConnected : ErrorKind.General);

                        if (_embeddedShell != null && _session?.Resolved?.PdfPath != null)
                            _embeddedShell.SharedOverlay.LoadDocument(_session.Resolved.PdfPath);
                        return;
                    }

                    _session.SignatureCount++;
                    card.MarkSigned(isImputernicire ? signerName + " *" : signerName);
                    btnSaveProgress.Enabled = true;
                    UpdateProgress();
                    UpdateGhostSlots();

                    Task.Run(() => _session.Service.SaveIntermediate())
                        .ContinueWith(_ =>
                        {
                            RefreshPdfViewer(_session.Resolved.PdfPath);
                            UpdateGhostSlots();
                            CheckFinishEnabled();
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
            if (_session?.SignatureCount == 0) return;

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

                string finalPath;
                if (_session.Service.HasNewCaptures)
                {
                    _session.Service.Finalize(openAfterSave: false);
                    finalPath = _session.Service.FinalizedPath;
                }
                else
                {
                    finalPath = _session.Service.FinalizeFromState();
                }

                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                { FileName = finalPath, UseShellExecute = true });

                ResetState();
                PopulateFolderDropdown();
            }
            catch (Exception ex)
            {
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

            var renderer = _embeddedShell?.SharedOverlay.Renderer ?? pdfViewer.Renderer;
            if (renderer == null) return;

            try
            {
                int page = renderer.Page;
                if (page != _lastPage) { _lastPage = page; _mirrorForm.SyncPage(page); }

                double zoom = renderer.Zoom;
                if (Math.Abs(zoom - _lastZoom) > 0.001) { _lastZoom = zoom; _mirrorForm.SyncZoom(zoom); }

                PointF ratio = GetScrollRatio(_embeddedShell?.SharedOverlay.Renderer ?? pdfViewer.Renderer);
                if (ratio != _lastScrollRatio) { _lastScrollRatio = ratio; _mirrorForm.SyncScrollRatio(ratio); }
            }
            catch { }
        }

        internal void btnMirror_Click(object sender, EventArgs e)
        {
            bool hasDoc = _embeddedShell != null
                ? _embeddedShell.SharedOverlay.HasDocument
                : _currentViewerPath != null;

            if (!_mirrorActive && !hasDoc)
            {
                MessageBox.Show("Nu exista niciun document incarcat pentru oglindire.",
                    "Fara Document", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (_mirrorActive) { CloseMirror(); return; }

            Screen targetScreen = Screen.AllScreens.FirstOrDefault(s => !s.Primary);
            if (targetScreen == null)
            {
                MessageBox.Show("Nu s-a detectat un al doilea monitor.\nConectati un display secundar si incercati din nou.",
                    "Fara Monitor Secundar", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (_mirrorForm == null) _mirrorForm = new MirrorForm();
            string mirrorPath = _embeddedShell != null ? _session?.Resolved?.PdfPath : _currentViewerPath;
            if (mirrorPath != null && File.Exists(mirrorPath)) _mirrorForm.LoadFromPath(mirrorPath);

            _mirrorForm.ShowOnScreen(targetScreen);
            _mirrorActive = true;
            btnMirror.Text = "✕  Inchide Oglindire";
            btnMirror.FlatAppearance.BorderColor = AppTheme.MirrorOffBorder;
            btnMirror.BackColor = AppTheme.MirrorOff;
            _lastScrollRatio = PointF.Empty;
            _lastZoom = _lastPage = -1;
            _syncTimer.Start();
        }

        private void CloseMirror()
        {
            _syncTimer.Stop();
            _mirrorForm?.Hide();
            _mirrorActive = false;
            btnMirror.Text = "⊞  Oglindire pe Ecran";
            btnMirror.BackColor = AppTheme.MirrorOn;
        }

        #endregion

        #region PDF Viewer

        private void RefreshPdfViewer(string pdfPath)
        {
            if (_embeddedShell != null)
            {
                try
                {
                    _embeddedShell.SharedOverlay.ReloadDocument(pdfPath);
                    _embeddedShell.SetZoomEnabled(true);
                    _embeddedShell.SetPreviewCaption(Path.GetFileName(pdfPath));
                }
                catch { }
                return;
            }

            try
            {
                int savedPage = pdfViewer.Renderer?.Page ?? 0;
                PointF savedRatio = GetScrollRatio(pdfViewer.Renderer);

                string copy = Path.Combine(Path.GetTempPath(),
                    $"wacom_viewer_{DateTime.Now:yyyyMMdd_HHmmss_fff}.pdf");

                ClearPdfViewer();
                File.Copy(pdfPath, copy, overwrite: false);
                _currentViewerPath = copy;
                _currentPdfDoc = PdfDocument.Load(copy);
                pdfViewer.Document = _currentPdfDoc;
                pdfViewer.Renderer.ZoomMode = PdfViewerZoomMode.FitBest;
                btnZoomIn.Enabled = btnZoomOut.Enabled = true;

                pdfViewer.Renderer.Page = savedPage;
                RestoreScrollRatio(savedRatio);

                if (_mirrorActive && _mirrorForm?.Visible == true)
                    _mirrorForm.LoadFromPath(copy);
            }
            catch { }
        }

        private void RestoreScrollRatio(PointF ratio)
        {
            if (pdfViewer.Renderer == null || ratio == PointF.Empty) return;
            pdfViewer.BeginInvoke(new Action(() =>
            {
                try
                {
                    var display = pdfViewer.Renderer.DisplayRectangle;
                    int scrollableX = display.Width - pdfViewer.Renderer.ClientSize.Width;
                    int scrollableY = display.Height - pdfViewer.Renderer.ClientSize.Height;
                    pdfViewer.Renderer.SetDisplayRectLocation(new Point(
                        scrollableX > 0 ? -(int)(ratio.X * scrollableX) : 0,
                        scrollableY > 0 ? -(int)(ratio.Y * scrollableY) : 0));
                }
                catch { }
            }));
        }

        private static PointF GetScrollRatio(PdfiumViewer.PdfRenderer renderer)
        {
            if (renderer == null) return PointF.Empty;
            try
            {
                var display = renderer.DisplayRectangle;
                int scrollableX = display.Width - renderer.ClientSize.Width;
                int scrollableY = display.Height - renderer.ClientSize.Height;
                return new PointF(
                    scrollableX > 0 ? (float)(-display.X) / scrollableX : 0f,
                    scrollableY > 0 ? (float)(-display.Y) / scrollableY : 0f);
            }
            catch { return PointF.Empty; }
        }

        private void ClearPdfViewer()
        {
            if (_embeddedShell != null)
            {
                _embeddedShell.SetZoomEnabled(false);
                _embeddedShell.SetPreviewCaption("Previzualizare — trage un PDF sau apasa Deschide");
                return;
            }

            _syncTimer.Stop();
            btnZoomIn.Enabled = btnZoomOut.Enabled = false;
            _currentPdfDoc?.Dispose();
            _currentPdfDoc = null;

            if (_currentViewerPath != null && File.Exists(_currentViewerPath))
                try { File.Delete(_currentViewerPath); } catch { }
            _currentViewerPath = null;
            _lastScrollRatio = PointF.Empty;
            _lastZoom = _lastPage = -1;

            panelContent.Controls.Remove(pdfViewer);
            pdfViewer.Dispose();
            pdfViewer = new PdfViewer { Dock = DockStyle.Fill, ShowToolbar = true, ShowBookmarks = false };
            panelContent.Controls.Add(pdfViewer);
            panelContent.Controls.SetChildIndex(pdfViewer, panelContent.Controls.Count - 1);

            if (_mirrorActive) _syncTimer.Start();
        }

        #endregion

        #region Helpers

        private void ResetState()
        {
            _session?.Dispose();
            _session = null;

            if (_embeddedShell != null)
            {
                _embeddedShell.SharedOverlay.ClearPreviewSlots();
                _embeddedShell.SharedOverlay.UnloadDocument();
            }

            cardsPanel.Controls.Clear();
            _cards.Clear();
            btnFinish.Enabled = false;
            btnSaveProgress.Visible = btnSaveProgress.Enabled = false;
            btnCancelLoad.Visible = btnCancelLoad.Enabled = false;
            cmbTemplate.Enabled = btnLoad.Enabled = _candidateFolder != null;
            lblProgress.Text = _candidateSignerName = lblCurrentSigner.Text = "";
            lblPreviewCaption.Text = "Previzualizare Document PDF";
        }

        private void UpdateProgress()
        {
            if (_session == null) return;
            var visible = _cards.Where(c => c.Visible).ToList();
            lblProgress.Text = $"{visible.Count(c => c.Signed)} din {visible.Count} semnaturi completate";
        }

        private void CheckFinishEnabled()
        {
            if (_cards.Where(c => c.Slot.Required).All(c => c.Signed))
                btnFinish.Enabled = true;
        }

        private void UpdateGhostSlots()
        {
            if (_embeddedShell == null || _session?.Resolved?.Slots == null) return;

            var rects = _session.Resolved.Slots.Select(s =>
            {
                bool accessible = s.Party != "Official"
                    || string.IsNullOrEmpty(s.OfficialRole)
                    || s.OfficialRole == _officialRole;

                return new DrawnRectangle
                {
                    Page = s.ResolvedPage,
                    X = s.Location?.X ?? 0,
                    Y = s.Location?.Y ?? 0,
                    W = s.Location?.W ?? 0,
                    H = s.Location?.H ?? 0,
                    RoleLabel = !string.IsNullOrEmpty(s.OfficialRole) ? s.OfficialRole
                        : s.Party == "Candidate" ? "Candidat / Angajat"
                        : s.SignerName,
                    IsAccessible = accessible,
                };
            }).ToArray();

            var signed = _session.Resolved.Slots.Select(s =>
                _cards.FirstOrDefault(c => c.Slot.SignatureId == s.SignatureId)?.Signed ?? false
            ).ToArray();

            _embeddedShell.SharedOverlay.SetPreviewSlots(rects, signed);
        }

        // Shows a signer name dialog. If prefill is provided, returns it directly without prompting.
        private string PromptSignerName(string prefill, string reason = null)
        {
            if (!string.IsNullOrWhiteSpace(prefill) && reason == null) return prefill;
            using (var dlg = new SignerNameDialog(reason, prefill))
                return dlg.ShowDialog() == DialogResult.OK ? dlg.SignerName : null;
        }

        #endregion

        #region Form Closing & Cleanup

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (_session != null && !CancelCurrentDocument()) { e.Cancel = true; return; }

            _syncTimer?.Stop();
            _folderWatcher?.Dispose();
            deviceStatusLabel?.StopPolling();
            oneDriveStatusLabel?.StopPolling();
            _mirrorForm?.Close();
            _session?.Dispose();
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