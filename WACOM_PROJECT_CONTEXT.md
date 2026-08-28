# WacomSignaturePDF — Project Context
**Data:** Iunie 2026 | **Stack:** .NET 4.7.2, WinForms, STA, C# 7.3

---

## Arhitectura generală

**Entry points Softone (Program.cs):**
| Comandă | Clasă TXCode | Form deschis |
|---------|-------------|--------------|
| 4000500 | `Program` | `ShellForm` (Template + FreeForm) |
| 4000501 | `EvaluareProbaCommand` | `EvaluareProbaForm` |

**ShellForm** — form principal cu pill switcher `[Șablon] [Semnătură Liberă]`
- Constructor fără parametri pentru Softone DllForm (`Obiect/Fisier: .WacomSignaturePdf.dll;ShellForm`)
- `SharedOverlay` = `PdfDrawingOverlay` (viewer + drawing + ghost slots)
- Accent bar jos: text mod + rol curent (`lblRoleAccent`, `DockStyle.Right`)
- Mirror via `MirrorForm` + `Timer` 33ms sync

**Template (Șablon)** — `TemplateSidebarPanel` → `MainForm` embedded
**FreeForm (Semnătură Liberă)** — `FreeFormSidebarPanel` + `_Layout`

---

## MainForm (Șablon)

**Selecție dosar** — înlocuit dropdown + câmp căutare cu:
- Label "Dosar" + câmp read-only cu numele candidatului + buton "Alege"
  (pattern identic cu "Tip Doc. | dropdown | Incarca")
- `CandidatPickerDialog` (tema dark) deschis la click "Alege"
- `TryPreselectFolder(personId)` — preselecție automată când form deschis din dosar Softone
- `SelectFolder(folderPath, folderName)` — metodă reutilizabilă, apelată din picker și preselecție
- `PopulateFolderDropdown()` — acum doar refresh `_allFolders` (nu mai populează dropdown)

**Filtrul "Doar semnaturile mele":**
- Sloturi cu `OfficialRole = ""` (nespecificat) sunt **excluse** din filtru (nu apar la nimeni specific)
- Doar match exact `OfficialRole == _officialRole` apare în filtrul personal

**Ghost slots — 3 culori:**
- Verde = semnat
- Galben subtil = nesemnat, accesibil rolului curent (`IsAccessible = true`)
- Roșu = nesemnat, rol restricționat (`IsAccessible = false`)
- Text `RoleLabel` scalat dinamic (pornește de la `h * 0.22`, redus dacă depășește 88% lățime)

---

## FreeForm (Semnătură Liberă)

**Butoane cu iconițe embedded:**
- `file_browse.png` → "Incarca Document"
- `document_in_progress.png` → "Documente In Proces"  
- `open_folder.png` → "Deschide Dosarul Semnături Libere" (Paint custom centrat)
- `signature.png` → "Adauga Semnatura Electronica" (Paint custom, `Text=""`)

**Layout sidebar (sus → jos):**
```
[📁 Deschide Dosarul Semnături Libere]   Y=8, full width
DOCUMENT                                  Y=52
[📄 Incarca Document] sau [📋 In Proces] Y=70
Niciun document incarcat.                 Y=116
ZONA SEMNATURA                            Y=144
[hint text]                               Y=162
[✍ Adauga Semnatura Electronica]          Y=200
SEMNATURI                                 Y=238
[progress]                                Y=256
[IMPUTERNICIRE □]                         Y=278
[cards panel]                             Y=302
```

**Flux documente:**
- 3 foldere în `FreeFormDocumentsPath`: `Documente In Original`, `Documente In Proces`, `Documente Semnate Complet`
- Backup original comprimat cu Ghostscript la "Salveaza si Inchide"
- `InProcesPickerDialog` — picker cu 4 coloane: Nume | Status | Semnaturi | Modificat

**Drawing mode:**
- `DrawingAborted` event → `ExitDrawingMode()` la desen în afara paginii
- `SetSidebarButtonsEnabled(false/true)` dezactivează toate butoanele în timp ce se desenează
- Role check în `OnCardClicked` și `OnRectangleDrawn` (dialog "Semneaza Acum")

**SignatureCardPanel:**
- `showDeleteButton` — buton "Sterge" activ indiferent de `RoleRestricted`
- `SetRoleRestricted(bool)` nu mai dezactivează `btnDelete`

---

## EvaluareProbaForm

**Scop:** Form lightweight pentru semnarea Fișelor de Evaluare Probă Practică.
**Pattern PDF:** `Evaluare_Proba_Practica_*.pdf`
**Entry Softone:** Comandă 4000501, `Obiect/Fisier: .WacomSignaturePdf.dll;EvaluareProbaCommand`

**Selecție dosar:**
- Buton "Selectează dosar..." → `CandidatPickerDialog(lightTheme: true)`
- Picker filtrează doar folderele care conțin `Evaluare_Proba_Practica_*.pdf`

**Sloturi de semnătură:**
- Încărcate din template-ul `FisaExaminare_V1.json` din `TemplatesDir`
- `{{OfficialName}}` rezolvat la load
- `_selectedPdfPath` setat corect în `OnDocumentSelected()`

**Viewer:**
- `PdfDrawingOverlay` (nu PdfViewer simplu) → ghost slots vizibile (galben/verde)

**Toggle filtru:**
- OFF (default): documente nesemnate / parțial semnate
- ON: documente semnate + sigilate
- Re-filtrează la schimbare

**UI temă:** luminoasă/friendly (paletă clară, un nivel mai întunecat față de prima variantă)
- Divider vizibil sidebar/preview
- Status bar cu `DeviceStatusLabel`/`OneDriveStatusLabel` (shared controls)
- Zoom in/out dezactivate până se încarcă document
- Toolbar: `Anchor.Right`, centrat vertical, `ToolbarH` ajustabil

---

## CandidatPickerDialog

**Generic** — refolosit în MainForm și EvaluareProbaForm:
- Acceptă `IEnumerable<string> folderNames`, `string basePath`, `bool lightTheme = false`
- 2 coloane: **Nume** (extras după ` - `) | **ID** (extras înainte de ` - `)
- Sortare alfabetică după Nume
- Search: după Nume (OrdinalIgnoreCase) sau ID (prefix match)
- `lightTheme: false` → dark slate-blue (MainForm)
- `lightTheme: true` → paletă mai luminoasă (EvaluareProbaForm)

---

## PdfDrawingOverlay

**Ghost slots — GeneratePreviewPdf:**
```
DrawnRectangle.IsAccessible:
  true  → galben (fill rgba(210,180,40,35), border rgba(180,150,30,150))
  false → roșu
  signed → verde
```
Font text RoleLabel: pornește de la `h * 0.22` (max 18pt), redus proporțional dacă textul depășește 88% din lățimea slot-ului.

**Events:**
- `RectangleDrawn` — slot configurat cu succes
- `DrawingAborted` — desen în afara paginii → `FreeFormSidebarPanel.ExitDrawingMode()`

---

## Configurare

**AppConfig** — `ResolveFromEnvOrConfig(envVar, configKey)`:
- `WorkingRoot` ← env `RecruitmentDocsPath`
- `FreeFormDocumentsPath` ← env `FreeFormDocumentsPath`  
- `TemplatesDir` ← env `TemplateDocsPath` + `"Sabloane Semnaturi Electronice"`

**RoleHelper** — mapare Softone userId → OfficialRole:
```
13→ADMIN | 23→HR | 7→DIR. EC. | 111→HR | 108→HR | 110→DIR. EC. | 12001→HR | 12000→DIR. EC.
```

---

## Resurse embedded (Properties/Resources)
`zoom_in`, `zoom_out`, `file_browse`, `document_in_progress`, `signature`, `open_folder`

---

## Fișiere cheie

| Fișier | Rol |
|--------|-----|
| `Program.cs` | Entry points Softone (cmd 4000500, 4000501) |
| `ShellForm.cs` / `_Layout.cs` | Form principal pill switcher + mirror |
| `MainForm.cs` / `.layout.cs` | Template mode (~1188 linii) |
| `FreeFormSidebarPanel.cs` / `_Layout.cs` | FreeForm mode complet |
| `EvaluareProbaForm.cs` / `_Layout.cs` | Form lightweight Evaluare Proba |
| `PdfDrawingOverlay.cs` | Viewer + drawing + ghost slots |
| `SignatureService.cs` | Capture biometrică, embed PDF, signing state |
| `TemplateService.cs` | Resolve templates, status documente |
| `AppTheme.cs` | Toate culorile aplicației |
| `AppConfig.cs` | Configurare env vars |
| `SigningState.cs` | Model state semnaturi |
| `CandidatPickerDialog.cs` | Picker dosar candidat (dual theme) |
| `InProcesPickerDialog.cs` | Picker documente InProces cu status |
| `SignaturecardPanel.cs` | Card semnătură cu delete independent de rol |
| `ResetOrUnloadDialog.cs` | Dialog confirmare unload |
| `FreeFormSlotDialog.cs` | Dialog configurare slot FreeForm |

---

## Probleme rezolvate recent
- Filtrul "Doar semnaturile mele" excludea sloturi `OfficialRole=""` → fix: match exact
- Role check la "Semneaza Acum" din `FreeFormSlotDialog` + `OnCardClicked`
- "Candidat" → "Candidat / Angajat" în `FreeFormSlotDialog`
- Butoane dezactivate în drawing mode (`SetSidebarButtonsEnabled`)
- `btnDelete` activ indiferent de `RoleRestricted`
- `ShellForm` constructor fără parametri pentru Softone DllForm
- `Program.cs` singleton fix (folosea `_activeForm` niciodată asignat)
- Ghost slots font scalat dinamic

## De verificat / în lucru
- Testare buguri în curs (Tiberiu)
- Ghostscript fallback (fără transparență dacă nu e în PATH)
