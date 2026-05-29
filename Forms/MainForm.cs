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
using Softone;
using System.Diagnostics;


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
        private bool _folderSearchActive = false;
        private List<SignatureCardPanel> _cards = new List<SignatureCardPanel>();

        private MirrorForm _mirrorForm;
        private bool _mirrorActive;
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
        // private XSupport _xSupport;
        private FileSystemWatcher _folderWatcher;

        // Standard users (no role) are treated as a valid role — they can use this filter too.
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

            // Populate official info from the logged-in Softone user.
            // Works both when launched from PRSNIN and from a menu DLL Form entry.
            try
            {
                int userId = S1.xSupp.ConnectionInfo.UserId;
                var result = S1.xSupp.GetSQLDataSet(
                    $"SELECT NAME FROM USERS WHERE USERS.USERS = {userId}");
                if (result != null && result.Count > 0)
                {
                    string fullName = result[0, "NAME"]?.ToString() ?? string.Empty;
                    _officialName = fullName;
                    _prefillSignerName = fullName;
                }
                _officialRole = RoleHelper.GetRole(userId);
            }
            catch { }

            LoadTemplates();
            PopulateFolderDropdown();
            InitFolderWatcher();
            InitSyncTimer();
            deviceStatusLabel.StartPolling();
            oneDriveStatusLabel.StartPolling();

            UpdateCurrentSignerLabel();
            OnFilterToggled(this, EventArgs.Empty);
        }
        public MainForm(string personId, string signerName) : this()
        {
            _prefillSignerName = signerName;
            txtCandidateId.Text = personId;
            PopulateFolderDropdown();
        }

        public MainForm(string personId, string signerName, string officialName, string officialRole = "")
            : this(personId, signerName)
        {
            _officialName = officialName;
            _officialRole = officialRole ?? "";
            UpdateCurrentSignerLabel();
            OnFilterToggled(this, EventArgs.Empty);
        }

        #endregion

        #region Filter Toggle

        private void OnFilterToggled(object sender, EventArgs e)
        {
            UpdateFilterLabels();
            UpdatePartyToggleForFilterMode();

            if (FilterMyOnly)
            {
                // Lock party to Official — candidate slots are irrelevant in this mode
                if (_currentParty != SigningParty.Official)
                {
                    _suppressToggleEvents = true;
                    toggleParty.IsOn = true;
                    _suppressToggleEvents = false;
                    _currentParty = SigningParty.Official;
                    UpdatePartyLabels();
                    UpdateCurrentSignerLabel();
                }
            }
            else
            {
                if (_currentParty != SigningParty.Candidate)
                {
                    _suppressToggleEvents = true;
                    toggleParty.IsOn = false;
                    _suppressToggleEvents = false;
                    _currentParty = SigningParty.Candidate;
                    UpdatePartyLabels();
                    UpdateCurrentSignerLabel();
                }
            }

            PopulateFolderDropdown();

            if (_candidateFolder != null)
                UpdateTemplateStatusIcons();

            ReflowCards();
            UpdateProgress();
        }

        // In FilterMyOnly mode: party toggle is locked to Official and visually dimmed.
        private void UpdatePartyToggleForFilterMode()
        {
            bool myOnly = FilterMyOnly;
            toggleParty.Enabled = !myOnly;
            lblPartyCandidate.ForeColor = myOnly ? AppTheme.SidebarSub
                : (_currentParty == SigningParty.Candidate ? AppTheme.AccentBlue : AppTheme.SidebarSub);
            lblPartyOfficial.ForeColor = myOnly ? AppTheme.AccentGreen
                : (_currentParty == SigningParty.Official ? AppTheme.AccentGreen : AppTheme.SidebarSub);
        }

        private void UpdateFilterLabels()
        {
            bool myOnly = toggleFilter.IsOn;
            lblFilterLeft.ForeColor = !myOnly ? AppTheme.AccentGreen : AppTheme.SidebarSub;
            lblFilterLeft.Font = new Font("Segoe UI", 9.5f, !myOnly ? FontStyle.Bold : FontStyle.Regular);
            lblFilterRight.ForeColor = myOnly ? AppTheme.AccentBlue : AppTheme.SidebarSub;
            lblFilterRight.Font = new Font("Segoe UI", 9.5f, myOnly ? FontStyle.Bold : FontStyle.Regular);
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
                foreach (var t in _templates)
                    cmbTemplate.Items.Add(t.TemplateName);

                if (cmbTemplate.Items.Count > 0)
                    cmbTemplate.SelectedIndex = 0;

                cmbTemplate.SetMultiDocFlags(
                    _visibleTemplates.Select(t =>
                    t.FileSystemBlock.IsMultiDocument &&
                    (_candidateFolder == null || TemplateService.GetMatchingFiles(t, _candidateFolder).Count > 1)
                ).ToList());

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

        private void InitFolderWatcher()
        {
            try
            {
                if (!Directory.Exists(AppConfig.WorkingRoot)) return;

                _folderWatcher = new FileSystemWatcher(AppConfig.WorkingRoot)
                {
                    NotifyFilter = NotifyFilters.DirectoryName,
                    IncludeSubdirectories = false,
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
            cmbCandidateFolder.Items.Clear();
            try
            {
                if (!Directory.Exists(AppConfig.WorkingRoot)) return;

                // Fetch active candidate IDs in one query
                var activeIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                try
                {
                    var ds = S1.xSupp.GetSQLDataSet("SELECT PRSN FROM PRSN WHERE ISACTIVE = 1");
                    if (ds != null)
                        for (int i = 0; i < ds.Count; i++)
                            activeIds.Add(ds[i, "PRSN"]?.ToString() ?? string.Empty);
                }
                catch { /* if query fails, show all folders */ }

                _allFolders = Directory.GetDirectories(AppConfig.WorkingRoot)
                    .Select(Path.GetFileName)
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

                var allFolders = _allFolders;

                List<string> folders;
                if (FilterMyOnly && _templates != null && _templates.Count > 0)
                {
                    Log("Filtrare dosare dupa rol...");
                    folders = allFolders
                        .Where(name => TemplateService.FolderHasPendingForRole(
                            Path.Combine(AppConfig.WorkingRoot, name), _templates, _officialRole))
                        .ToList();
                    Log($"Dosare cu semnaturi in asteptare: {folders.Count} din {allFolders.Count}");
                }
                else
                {
                    folders = allFolders;
                }

                foreach (var f in folders)
                    cmbCandidateFolder.Items.Add(f);

                string currentId = txtCandidateId.Text.Trim();
                if (!string.IsNullOrWhiteSpace(currentId))
                {
                    bool found = false;
                    for (int i = 0; i < cmbCandidateFolder.Items.Count; i++)
                    {
                        string item = cmbCandidateFolder.Items[i].ToString();
                        if (item.StartsWith(currentId + " - ", StringComparison.OrdinalIgnoreCase) ||
                            item.StartsWith(currentId + "-", StringComparison.OrdinalIgnoreCase))
                        {
                            cmbCandidateFolder.SelectedIndex = i;
                            cmbCandidateFolder.Invalidate();
                            cmbCandidateFolder.Refresh();
                            found = true;
                            break;
                        }
                    }
                    if (!found)
                        Log($"ATENTIE: Nu s-a gasit un dosar pentru ID '{currentId}'.");
                }
            }
            catch (Exception ex)
            {
                Log($"EROARE incarcare foldere: {ex.Message}");
            }
        }

        private void ClearFolderSearch()
        {
            txtFolderSearch.Text = "Cauta dosar candidat...";
            txtFolderSearch.ForeColor = AppTheme.SidebarSub;
            txtFolderSearch.Font = new System.Drawing.Font("Segoe UI", 9f, System.Drawing.FontStyle.Regular);
            btnSearchClear.Visible = false;
            this.ActiveControl = null;
            string current = cmbCandidateFolder.SelectedItem?.ToString();
            cmbCandidateFolder.BeginUpdate();
            cmbCandidateFolder.Items.Clear();
            foreach (var f in _allFolders) cmbCandidateFolder.Items.Add(f);
            if (current != null) cmbCandidateFolder.SelectedItem = current;
            cmbCandidateFolder.EndUpdate();
        }

        private void OnFolderSearchTextChanged(object sender, EventArgs e)
        {
            string query = txtFolderSearch.Text.Trim();
            if (query == "Cauta dosar candidat...") query = "";

            btnSearchClear.Visible = !string.IsNullOrWhiteSpace(query);

            var filtered = string.IsNullOrEmpty(query)
                ? _allFolders
                : _allFolders.Where(f => f.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0).ToList();

            string currentSelection = cmbCandidateFolder.SelectedItem?.ToString();
            cmbCandidateFolder.BeginUpdate();
            cmbCandidateFolder.Items.Clear();
            foreach (var f in filtered)
                cmbCandidateFolder.Items.Add(f);
            if (currentSelection != null && filtered.Contains(currentSelection))
                cmbCandidateFolder.SelectedItem = currentSelection;
            else
                cmbCandidateFolder.SelectedIndex = -1;
            cmbCandidateFolder.EndUpdate();
        }

        private void OnCandidateFolderSelected()
        {
            if (cmbCandidateFolder.SelectedIndex < 0) return;

            string folderName = cmbCandidateFolder.SelectedItem.ToString();
            string fullPath = Path.Combine(AppConfig.WorkingRoot, folderName);

            try
            {
                _candidateFolder = fullPath;

                // Extract ID from folder name (format: "ID - Name" or "ID-Name")
                string id = folderName;
                int dash = folderName.IndexOf(" - ", StringComparison.Ordinal);
                if (dash < 0) dash = folderName.IndexOf('-');
                if (dash > 0) id = folderName.Substring(0, dash).Trim();

                txtCandidateId.TextChanged -= txtCandidateId_TextChanged;
                txtCandidateId.Text = id;
                txtCandidateId.TextChanged += txtCandidateId_TextChanged;

                string name = TemplateService.GetCandidateName(fullPath);
                _candidateSignerName = name;
                _prefillSignerName = name;

                Log($"Candidat: {name}");
                Log($"Dosar   : {fullPath}");

                cmbTemplate.Enabled = true;
                btnLoad.Enabled = true;
                UpdateCurrentSignerLabel();
                UpdateTemplateStatusIcons();
            }
            catch (Exception ex)
            {
                Log($"EROARE folder: {ex.Message}");
                _candidateFolder = null;
                cmbTemplate.Enabled = false;
                btnLoad.Enabled = false;
            }
        }

        #endregion

        #region Candidate ID

        private void txtCandidateId_TextChanged(object sender, EventArgs e)
        {
            string id = txtCandidateId.Text.Trim();

            if (string.IsNullOrEmpty(id))
            {
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

                Log($"Candidat: {name}");
                Log($"Dosar   : {_candidateFolder}");

                cmbTemplate.Enabled = true;
                btnLoad.Enabled = true;

                if (!string.IsNullOrWhiteSpace(_prefillSignerName))
                {
                    _candidateSignerName = _prefillSignerName;
                    UpdateCurrentSignerLabel();
                }
                UpdateTemplateStatusIcons();
            }
            catch
            {
                Log($"Nu s-a gasit candidatul cu ID '{id}'.");
                _candidateFolder = null;
                _candidateSignerName = null;
                cmbTemplate.Enabled = false;
                btnLoad.Enabled = false;
                UpdateCurrentSignerLabel();
            }
        }

        #endregion

        #region Template Status Icons

        private void UpdateTemplateStatusIcons()
        {
            if (_templates == null) return;

            // Preserve current selection across rebuild — prefer open document, fall back to dropdown
            string currentTemplateId = _session?.Resolved?.Template?.TemplateId
                ?? (_visibleTemplates != null && cmbTemplate.SelectedIndex >= 0
                        && cmbTemplate.SelectedIndex < _visibleTemplates.Count
                    ? _visibleTemplates[cmbTemplate.SelectedIndex]?.TemplateId
                    : null);

            _visibleTemplates = new List<DocumentTemplate>();
            var colors = new List<Color>();

            foreach (var template in _templates)
            {
                if (FilterMyOnly)
                {
                    // Exact match: Standard (no-role) users see only slots with no role.
                    // Named-role users see only slots matching their role.
                    bool hasMySlot = template.Signatures.Any(s =>
                        s.Party == "Official" && s.OfficialRole == _officialRole);
                    if (!hasMySlot) continue;
                }

                var status = TemplateService.GetDocumentStatus(template, _candidateFolder);
                if (status == TemplateService.DocumentStatus.NotFound) continue;

                Color color;
                switch (status)
                {
                    case TemplateService.DocumentStatus.SignedSealed: color = DocumentTypeDropdown.ColorSignedSealed; break;
                    case TemplateService.DocumentStatus.SignedUnsealed: color = DocumentTypeDropdown.ColorSignedUnsealed; break;
                    case TemplateService.DocumentStatus.PartialSigned: color = DocumentTypeDropdown.ColorPartialSigned; break;
                    default: color = DocumentTypeDropdown.ColorUnsigned; break;
                }

                _visibleTemplates.Add(template);
                colors.Add(color);
            }

            cmbTemplate.Items.Clear();
            foreach (var t in _visibleTemplates)
                cmbTemplate.Items.Add(t.TemplateName);

            int restoreIndex = 0;
            if (currentTemplateId != null)
            {
                int idx = _visibleTemplates.FindIndex(t => t.TemplateId == currentTemplateId);
                if (idx >= 0) restoreIndex = idx;
            }

            if (cmbTemplate.Items.Count > 0)
                cmbTemplate.SelectedIndex = restoreIndex;

            cmbTemplate.SetStatusImages(new List<Image>(new Image[_visibleTemplates.Count]), colors);
            cmbTemplate.SetMultiDocFlags(_visibleTemplates.Select(t =>
                    t.FileSystemBlock.IsMultiDocument &&
                    (_candidateFolder == null || TemplateService.GetMatchingFiles(t, _candidateFolder).Count > 1)
                ).ToList());
        }

        #endregion

        #region Load Document

        private void OnTemplateSelectionCommitted()
        {
            if (cmbTemplate.SelectedIndex < 0 || _candidateFolder == null) return;
            var template = _visibleTemplates[cmbTemplate.SelectedIndex];

            if (template.FileSystemBlock.IsMultiDocument)
            {
                ShowMultiDocumentFlyout(template);
                return;
            }

            if (_session != null && !CancelCurrentDocument()) return;

            string signerName = PromptSignerName();
            if (signerName == null) return;
            LoadDocumentFromTemplate(template, signerName, null);
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

            if (template.FileSystemBlock.IsMultiDocument)
            {
                ShowMultiDocumentFlyout(template);
                return;
            }

            if (_session != null && !CancelCurrentDocument()) return;

            string signerName = PromptSignerName();
            if (signerName == null) return;
            LoadDocumentFromTemplate(template, signerName, null);
        }

        private void ShowMultiDocumentFlyout(DocumentTemplate template)
        {
            var files = TemplateService.GetMatchingFiles(template, _candidateFolder);

            if (files.Count == 0)
            {
                MessageBox.Show(
                    "Nu s-au gasit documente pentru acest template in dosarul candidatului.",
                    "Fara Documente", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            // Single file -- load directly without showing the flyout
            if (files.Count == 1)
            {
                if (_session != null && !CancelCurrentDocument()) return;
                string signerNameDirect = PromptSignerName();
                if (signerNameDirect == null) return;
                LoadDocumentFromTemplate(template, signerNameDirect, files[0].FilePath);
                return;
            }

            int flyoutWidth = btnLoad.Right - cmbTemplate.Left;
            var flyout = new MultiDocFlyout(files, flyoutWidth);

            flyout.FileSelected += filePath =>
            {
                if (_session != null && !CancelCurrentDocument()) return;
                string signerName = PromptSignerName();
                if (signerName == null) return;
                LoadDocumentFromTemplate(template, signerName, filePath);
            };

            flyout.Location = cmbTemplate.PointToScreen(new Point(0, cmbTemplate.Height));
            flyout.Show(this);
        }

        private void LoadDocumentFromTemplate(DocumentTemplate template, string signerName, string specificFilePath)
        {
            try
            {
                ResetState();

                var resolved = TemplateService.Resolve(
                    template, _candidateFolder, signerName, _officialName ?? "", specificFilePath);
                _session = new DocumentSession(resolved,
                    new SignatureService(resolved.PdfPath, "", resolved.Slots));

                btnCancelLoad.Visible = true;
                btnCancelLoad.Enabled = true;
                btnSaveProgress.Visible = true;
                btnSaveProgress.Enabled = false;

                // Always reset Imputernicire per document
                chkManualSigner.Checked = false;

                // Start on Official when no candidate slots exist, or when filter is set to my-only
                bool hasCandidate = resolved.Slots.Any(s => string.IsNullOrEmpty(s.Party) || s.Party == "Candidate");
                bool startOfficial = !hasCandidate || FilterMyOnly;

                _suppressToggleEvents = true;
                toggleParty.IsOn = startOfficial;
                _suppressToggleEvents = false;
                _currentParty = startOfficial ? SigningParty.Official : SigningParty.Candidate;
                _candidateSignerName = signerName;
                UpdatePartyLabels();
                UpdateCurrentSignerLabel();

                lblPreviewCaption.Text = System.IO.Path.GetFileName(resolved.PdfPath);

                BuildCards(resolved.Slots);
                RefreshPdfViewer(resolved.PdfPath);
                LoadSigningState();

                Log($"Document : {resolved.Template.TemplateName}");
                Log($"PDF      : {resolved.PdfPath}");
                Log($"Sloturi  : {resolved.Slots.Count}");
                if (!string.IsNullOrEmpty(_officialRole))
                    Log($"Rol      : {_officialRole}");
                Log("Apasati pe un card pentru a semna.");

                UpdateProgress();
            }
            catch (DocumentAlreadyFinalizedException ex)
            {
                _session = null;
                Log("Document deja finalizat si sigilat.");
                ErrorDialog.Show(this, ex.Message, ErrorKind.DocumentFinalized);
            }
            catch (DocumentSignedNotSealedException ex)
            {
                _session = null;
                Log($"Document semnat dar nesigilat in Adobe: {ex.SemnatPath}");
                ErrorDialog.Show(this, ex.Message, ErrorKind.DocumentSignedNotSealed);
                Log("Se deschide in Adobe pentru sigilare...");
                try
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    { FileName = ex.SemnatPath, UseShellExecute = true });
                }
                catch (Exception openEx) { Log($"EROARE la deschidere: {openEx.Message}"); }
            }
            catch (Exception ex)
            {
                _session = null;
                Log($"EROARE: {ex.Message}");
                ErrorDialog.Show(this, ex.Message,
                    ex is FileNotFoundException ? ErrorKind.FileNotFound : ErrorKind.General);
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
                Log($"  [Restaurat] #{entry.SignatureId} {entry.SignerName} — {entry.SignedAt:g} pe {entry.MachineName}");
            }

            UpdateProgress();

            if (_cards.Where(c => c.Slot.Required).All(c => c.Signed))
            {
                btnFinish.Enabled = true;
                Log("Toate semnaturile obligatorii sunt completate. Apasati Finalizati.");
            }
        }

        #endregion

        #region Cancel / Unload Document

        private bool CancelCurrentDocument()
        {
            string pdfPath = _session?.Resolved?.PdfPath;
            string backupPath = pdfPath != null
                ? Path.Combine(Path.GetDirectoryName(pdfPath),
                      "Originally Generated Documents", Path.GetFileName(pdfPath))
                : null;
            bool backupExists = backupPath != null && File.Exists(backupPath);

            UnloadAction action;
            if (!(_session?.Service.HasNewCaptures ?? false))
            {
                // No new captures this session -- silently discard, no prompt needed
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
                if (MessageBox.Show(
                    "Exista semnaturi capturate. Sigur doriti sa descarcati documentul?",
                    "Confirmare", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
                    return false;
                action = UnloadAction.DiscardSession;
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

            switch (action)
            {
                case UnloadAction.SaveAndClose:
                    // Save signatures from this session, then unload
                    try
                    {
                        ClearPdfViewer();
                        _session.Service.SaveProgress();
                        Log("Sesiune salvata.");
                    }
                    catch (Exception ex) { Log($"EROARE la salvare: {ex.Message}"); }
                    ResetState();
                    break;

                case UnloadAction.ResetToOriginal:
                    try
                    {
                        _session?.Service.RestoreToSessionStart();
                        ClearPdfViewer();
                        if (backupExists)
                        {
                            File.Copy(backupPath, pdfPath, overwrite: true);
                            Log("Document resetat la originalul nesemnat.");
                        }
                        ResetState();
                    }
                    catch (IOException ex)
                    {
                        MessageBox.Show(ex.Message, "Fisier blocat", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return false;
                    }
                    break;

                default: // DiscardSession
                    try
                    {
                        _session?.Service.RestoreToSessionStart();
                        ClearPdfViewer();
                        Log("Semnaturile sesiunii curente au fost anulate.");
                        ResetState();
                    }
                    catch (IOException ex)
                    {
                        MessageBox.Show(ex.Message, "Fisier blocat", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return false;
                    }
                    break;
            }

            PopulateFolderDropdown();
            cmbTemplate.Enabled = _candidateFolder != null;
            btnLoad.Enabled = _candidateFolder != null;
            Log("Document descarcat. Selectati un alt document.");
            return true;
        }

        #endregion

        #region Save Progress

        private void btnSaveProgress_Click(object sender, EventArgs e)
        {
            if (_session == null || _session.SignatureCount == 0) return;

            try
            {
                ClearPdfViewer();
                _session.Service.SaveProgress();
                Log("Progres salvat. Documentul poate fi trimis la urmatoarea persoana.");
                MessageBox.Show(
                    "Progresul a fost salvat si documentul a fost eliberat din aplicatie.\n",
                    "Salvat", MessageBoxButtons.OK, MessageBoxIcon.Information);

                ResetState();
                PopulateFolderDropdown();
                cmbTemplate.Enabled = _candidateFolder != null;
                btnLoad.Enabled = _candidateFolder != null;
                Log("Document descarcat automat dupa salvare.");
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
            if (_suppressToggleEvents) return;
            _currentParty = toggleParty.IsOn ? SigningParty.Official : SigningParty.Candidate;
            UpdatePartyLabels();
            UpdateCurrentSignerLabel();
            ReflowCards();
            UpdateProgress();
        }

        private void UpdatePartyLabels()
        {
            bool candidate = _currentParty == SigningParty.Candidate;
            lblPartyCandidate.ForeColor = candidate ? AppTheme.AccentBlue : AppTheme.SidebarSub;
            lblPartyCandidate.Font = new Font("Segoe UI", 9.5f, candidate ? FontStyle.Bold : FontStyle.Regular);
            lblPartyOfficial.ForeColor = !candidate ? AppTheme.AccentGreen : AppTheme.SidebarSub;
            lblPartyOfficial.Font = new Font("Segoe UI", 9.5f, !candidate ? FontStyle.Bold : FontStyle.Regular);
        }

        private void UpdateCurrentSignerLabel()
        {
            if (chkManualSigner.Checked) { lblCurrentSigner.Text = "-"; return; }
            string name = _currentParty == SigningParty.Official ? _officialName : _candidateSignerName;
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

        // Visibility and interactability rules per filter mode:
        //   FilterMyOnly  → show only Official cards matching current role; all interactable
        //   FilterShowAll → show all cards for current party; unmatched Officials shown as ALT ROL
        //   Imputernicire → overrides role restriction in both modes
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
                    // Exact match only — Standard ("") matches slots with no role, named roles match their own.
                    // Empty OfficialRole = any role can sign
                    bool isMatchingOfficial = card.Slot.Party == "Official"
                        && (string.IsNullOrEmpty(card.Slot.OfficialRole) || card.Slot.OfficialRole == _officialRole);

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

                // Exact match: "" == "" for Standard, "HR" == "HR" for named roles.
                // Empty OfficialRole = any role can sign
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
                if (!c.Signed && !c.RoleRestricted)
                    c.Enabled = enabled;
        }

        #endregion

        #region Signature Capture

        private void OnCardClicked(SignatureSlot slot)
        {
            if (_session == null || _captureInProgress) return;

            var card = _cards.FirstOrDefault(c => c.Slot.SignatureId == slot.SignatureId);
            if (card == null || card.Signed || card.RoleRestricted) return;

            Log($"Semnatura #{slot.SignatureId} — {slot.Reason} (Pagina {slot.ResolvedPage})");

            string prefill = _currentParty == SigningParty.Official ? _officialName : _candidateSignerName;
            string signerName = chkManualSigner.Checked || string.IsNullOrWhiteSpace(slot.ResolvedSignerName)
                ? PromptSignerNameForSlot(slot.Reason, prefill)
                : slot.ResolvedSignerName;

            if (signerName == null) return;

            bool isImputernicire = chkManualSigner.Checked;

            if (SignatureService.IsFileLocked(_session.Resolved.PdfPath))
            {
                MessageBox.Show(
                    $"Fisierul '{Path.GetFileName(_session.Resolved.PdfPath)}' este deschis in alta aplicatie (ex. Adobe).\n"
                    + "Inchideti documentul si incercati din nou.",
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
                        slot.ResolvedPage,
                        slot.Location.X, slot.Location.Y,
                        slot.Location.W, slot.Location.H,
                        isImputernicire);
                }
                catch (OperationCanceledException) { cancelled = true; }
                catch (Exception ex) { caughtEx = ex; }

                Invoke(new Action(() =>
                {
                    _captureInProgress = false;
                    SetCardsEnabled(true);

                    if (cancelled) { Log($"Slot #{slot.SignatureId} anulat."); return; }

                    if (caughtEx != null)
                    {
                        Log($"EROARE: {caughtEx.Message}");
                        bool isDeviceError = caughtEx.Message.Contains("STU") || caughtEx.Message.Contains("device")
                                          || caughtEx.Message.Contains("pad") || caughtEx.Message.Contains("Pad");
                        ErrorDialog.Show(this, caughtEx.Message,
                            isDeviceError ? ErrorKind.DeviceNotConnected : ErrorKind.General);
                        return;
                    }

                    _session.SignatureCount++;
                    string displayName = isImputernicire ? signerName + " *" : signerName;
                    card.MarkSigned(displayName);
                    Log($"  Semnat: {displayName}  |  {slot.Reason}");
                    btnSaveProgress.Enabled = true;
                    UpdateProgress();

                    Task.Run(() => _session.Service.SaveIntermediate())
                        .ContinueWith(_ =>
                        {
                            RefreshPdfViewer(_session.Resolved.PdfPath);
                            if (_cards.Where(c => c.Slot.Required).All(c => c.Signed))
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
            if (_session == null || _session.SignatureCount == 0) return;

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
                    var captures = _session.Service.Finalize(openAfterSave: false);
                    Log($"Salvat: {_session.Resolved.PdfPath}");
                    foreach (var cap in captures)
                        Log($"  [{cap.SignerName}] Hash={cap.DocumentHash.Substring(0, 16)}... " +
                            $"TSA={(cap.TrustedAt.HasValue ? cap.TrustedAt.Value.ToString("u") : "indisponibil")}");
                    finalPath = _session.Service.FinalizedPath;
                }
                else
                {
                    finalPath = _session.Service.FinalizeFromState();
                    Log($"Finalizat din stare salvata: {finalPath}");
                }

                Log("Deschis in Adobe.");
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                { FileName = finalPath, UseShellExecute = true });

                // Auto-unload dupa finalizare -- echivalent "Doar descarcare"
                ResetState();
                ClearPdfViewer();
                PopulateFolderDropdown();
                Log("Document finalizat. Selectati urmatorul document.");
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
                if (page != _lastPage) { _lastPage = page; _mirrorForm.SyncPage(page); }

                double zoom = pdfViewer.Renderer.Zoom;
                if (Math.Abs(zoom - _lastZoom) > 0.001) { _lastZoom = zoom; _mirrorForm.SyncZoom(zoom); }

                PointF ratio = GetViewerScrollRatio(pdfViewer);
                if (ratio != _lastScrollRatio) { _lastScrollRatio = ratio; _mirrorForm.SyncScrollRatio(ratio); }
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

            if (_mirrorForm == null) _mirrorForm = new MirrorForm();
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
                int savedPage = pdfViewer.Renderer?.Page ?? 0;
                PointF savedRatio = GetViewerScrollRatio(pdfViewer);

                // Copy to temp so the working PDF stays unlocked during viewing
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

                pdfViewer.Renderer.Page = savedPage;
                RestoreScrollRatio(pdfViewer, savedRatio);

                if (_mirrorActive && _mirrorForm != null && _mirrorForm.Visible)
                    _mirrorForm.LoadFromPath(copy);
            }
            catch (Exception ex) { Log($"Eroare previzualizare: {ex.Message}"); }
        }

        private static void RestoreScrollRatio(PdfViewer viewer, PointF ratio)
        {
            if (viewer.Renderer == null || ratio == PointF.Empty) return;
            viewer.BeginInvoke(new Action(() =>
            {
                try
                {
                    var display = viewer.Renderer.DisplayRectangle;
                    int scrollableX = display.Width - viewer.Renderer.ClientSize.Width;
                    int scrollableY = display.Height - viewer.Renderer.ClientSize.Height;
                    viewer.Renderer.SetDisplayRectLocation(new System.Drawing.Point(
                        scrollableX > 0 ? -(int)(ratio.X * scrollableX) : 0,
                        scrollableY > 0 ? -(int)(ratio.Y * scrollableY) : 0));
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
                return new PointF(
                    scrollableX > 0 ? (float)(-display.X) / scrollableX : 0f,
                    scrollableY > 0 ? (float)(-display.Y) / scrollableY : 0f);
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
            lblPreviewCaption.Text = "Previzualizare Document PDF";
        }

        private void UpdateProgress()
        {
            if (_session == null) return;
            var visible = _cards.Where(c => c.Visible).ToList();
            lblProgress.Text = $"{visible.Count(c => c.Signed)} din {visible.Count} semnaturi completate";
        }

        private string PromptSignerName()
        {
            if (!string.IsNullOrWhiteSpace(_prefillSignerName))
                return _prefillSignerName;

            using (var dlg = new SignerNameDialog())
                return dlg.ShowDialog() == DialogResult.OK ? dlg.SignerName : null;
        }

        private string PromptSignerNameForSlot(string reason, string prefillName = null)
        {
            using (var dlg = new SignerNameDialog(reason, prefillName))
                return dlg.ShowDialog() == DialogResult.OK ? dlg.SignerName : null;
        }

        private void Log(string msg) { } // Log UI removed

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