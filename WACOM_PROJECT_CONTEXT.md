# WacomSignaturePDF — Project Context
**Data:** Iunie 2026 | **Stack:** .NET 4.7.2, WinForms, STA, C# 7.3

---

## Arhitectura

**ShellForm** — form unic cu pill switcher `[Șablon] [Semnătură Liberă]`
- `ShellForm.cs` / `ShellForm_Layout.cs`
- Constructor fără parametri → apel direct din Softone (`Tip operatie: Dll Form`, `Obiect/Fisier: .WacomSignaturePdf.dll;ShellForm`)
- `SharedOverlay` = `PdfDrawingOverlay` (viewer + drawing + ghost slots)
- Mirror via `MirrorForm` + `Timer` 33ms sync
- `Instance` static pentru singleton guard din `Program.cs`

**Template (Șablon)** — `TemplateSidebarPanel` wrapper peste `MainForm` (embedded)
- JSON templates din `App.config:TemplatesDir`
- Sloturi din template JSON cu `OfficialRole`, `Party`, `ResolvedPage`, `Location.X/Y/W/H`
- Ghost slots via `SharedOverlay.SetPreviewSlots(rects, signed)`
- `RoleLabel` = `OfficialRole` → `"Candidat / Angajat"` dacă `Party=="Candidate"`

**FreeForm (Semnătură Liberă)** — `FreeFormSidebarPanel` + `FreeFormSidebarPanel_Layout`
- Sloturi configurate prin desenare pe PDF (overlay drawing)
- State persistent în `signing-state.json` (attachment în PDF)
- `HasUnsavedWork` = orice slot configurat (`_slots.Count > 0`)
- `btnCancelLoad` afișează `ResetOrUnloadDialog` dacă `HasUnsavedWork`
- `DrawingAborted` event pe `PdfDrawingOverlay` → `ExitDrawingMode()` la desen în afara paginii

---

## PdfDrawingOverlay (`Controls/PdfDrawingOverlay.cs`)

### Drawing (FreeForm)
- `EnableDrawing(bool)` → activează `DrawingOverlayControl` cu overlay gălbui
- `DrawingAborted` event — invocat când rect desenat e în afara paginii (resetează header)
- `RectangleDrawn` event → `ConvertToPdfCoords` → slot configurat

### Ghost Slots Preview — 3 culori
- **Verde** — slot semnat
- **Galben subtil** — slot nesemnat, accesibil rolului curent (`IsAccessible = true`)
- **Roșu** — slot nesemnat, rol restricționat (`IsAccessible = false`)
- Text `RoleLabel` scalat dinamic: `fontSize = h * 0.22`, redus dacă depășește 88% din lățime
- `DrawnRectangle.IsAccessible` — setat în `MainForm.UpdateGhostSlots()` și `FreeFormSidebarPanel.RefreshPreviewSlots()`

### Scroll restore
- `ReloadDocument(path)` salvează zoom + page + center
- `ScheduleScrollRestore` cu delay diferențiat (80ms normal, 400ms cu preview)

---

## Butoane cu iconițe (FreeForm Sidebar)

Resurse embedded în `Properties/Resources.resx`:
- `file_browse.png` → buton "Incarca Document"
- `document_in_progress.png` → buton "Documente In Proces"
- `signature.png` → buton "Adauga Semnatura Electronica" (Paint custom, `Text=""`, centrat manual)

---

## InProcesPickerDialog (`Forms/InProcesPickerDialog.cs`)

Dialog custom pentru "Documente In Proces":
- Search live cu `EM_SETCUEBANNER` (placeholder .NET 4.7.2)
- ListView owner-drawn, tema teal, rânduri alternate
- **4 coloane**: Nume document (flex) | Status | Semnaturi | Modificat
- Status citit din `SignatureService.ReadSigningState()` — Nesemnat / Partial semnat / Semnat complet
- Culori status: gri / portocaliu / verde
- Fișiere sortate descrescător după dată modificare
- Coloana "Nume" se redimensionează dinamic

---

## Packages cheie
- `PdfiumViewer` — viewer PDF cu `PdfRenderer`, `PointToPdf`, `BoundsFromPdf`
- `PdfSharp 6.2.4` — modificare PDF, drawing cu alpha (necesită `/Group` în PDF)
- `WindowsFontResolver` — fonturi din `C:\Windows\Fonts\` pentru PdfSharp
- `Newtonsoft.Json` — serializare SigningState
- Ghostscript (`gswin32c.exe`) — adaugă `/Group Transparency` pe PDF-uri

---

## Configurare (`Config/AppConfig.cs`)
- `WorkingRoot` ← env `RecruitmentDocsPath` sau app.config
- `FreeFormDocumentsPath` ← env `FreeFormDocumentsPath` sau app.config
- `TemplatesDir` ← env `TemplateDocsPath` + `"Sabloane Semnaturi Electronice"` sau app.config
- Pattern comun extras în `ResolveFromEnvOrConfig(envVar, configKey)`

## Role Helper (`Config/RoleHelper.cs`)
```
13 → ADMIN | 23 → HR | 7 → DIR. EC. | 111 → HR
108 → HR | 110 → DIR. EC. | 12001 → HR | 12000 → DIR. EC.
```

---

## FreeForm — Flux documente
3 foldere în `FreeFormDocumentsPath`:
- `Documente In Original` — backup audit (comprimat Ghostscript `/ebook`)
- `Documente In Proces` — lucru activ
- `Documente Semnate Complet` — finalizate

Backup original → la "Salveaza si Inchide".
Finalizare → copie în Semnate Complet, șterge din InProces.

---

## SignatureCardPanel
- `showDeleteButton` parameter — buton "Sterge" pe card (FreeForm)
- `DeleteClicked` event
- `SetRoleRestricted(bool)` — card vizual restricționat, **dar `btnDelete` rămâne activ** (poate șterge sloturi de orice rol)
- Dimensiuni: 62/76px fără delete, +28px cu delete

---

## Fișiere cheie

| Fișier | Rol |
|--------|-----|
| `Program.cs` | Entry point Softone (TXCode), singleton via `ShellForm.Instance` |
| `ShellForm.cs` / `_Layout.cs` | Form principal, pill switcher, mirror, constructor fără parametri |
| `MainForm.cs` / `_layout.cs` | Template mode logic (~1188 linii) |
| `FreeFormSidebarPanel.cs` / `_Layout.cs` | FreeForm mode complet |
| `PdfDrawingOverlay.cs` | Viewer + drawing + ghost slots |
| `SignatureService.cs` | Capture biometrică, embed PDF, signing state |
| `TemplateService.cs` | Resolve templates, status documente |
| `AppTheme.cs` | Toate culorile aplicației |
| `AppConfig.cs` | Configurare env vars |
| `SigningState.cs` | Model state semnaturi |
| `InProcesPickerDialog.cs` | Picker documente InProces cu search + status |
| `SignaturecardPanel.cs` | Card semnătură cu delete |
| `ResetOrUnloadDialog.cs` | Dialog confirmare unload |

---

## Probleme rezolvate în această sesiune
1. Iconițe 24x24 embedded pe butoane FreeForm sidebar
2. `DrawingAborted` event — header drawing mode resetat la desen în afara paginii
3. `InProcesPickerDialog` custom cu search + coloane Status/Semnaturi
4. Ghost slots 3 culori (verde/galben/roșu) + text scalat dinamic
5. `SetRoleRestricted` — `btnDelete` activ indiferent de rol
6. `ShellForm` constructor fără parametri pentru Softone DllForm
7. Refactorizare majoră: `MainForm` 1540→1188 linii, dead code eliminat
8. `Program.cs` — fix singleton guard (folosea `_activeForm` niciodată asignat)
9. `AppConfig` — pattern `ResolveFromEnvOrConfig` extras
10. `AppTheme` — `LogBg`, `LogText`, `AccentGreenBorder` eliminate

## Probleme rămase / de verificat
- Ghost slots FreeForm: dacă Ghostscript nu e în PATH → fallback fără transparență
- Scroll restore poate fi lent dacă Ghostscript > 400ms
- Testare buguri în curs
