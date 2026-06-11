# WacomSignaturePDF — Project Context
**Data:** Iunie 2026 | **Stack:** .NET 4.7.2, WinForms, STA

---

## Arhitectura

**ShellForm** — form unic cu pill switcher `[Șablon] [Semnătură Liberă]`
- `ShellForm.cs` / `ShellForm_Layout.cs`
- `SharedOverlay` = `PdfDrawingOverlay` (viewer + drawing + ghost slots)
- Buton Oglindire în `panelPreviewHeader`, delegat la `TemplateSidebarPanel.ToggleMirror()` → `MainForm.btnMirror_Click`

**Template (Șablon)** — `TemplateSidebarPanel` wrapper peste `MainForm` (embedded)
- JSON templates din `App.config:TemplatesDir`
- Sloturi din template JSON cu `OfficialRole`, `Party`, `ResolvedPage`, `Location.X/Y/W/H`
- Ghost slots via `_embeddedShell.SharedOverlay.SetPreviewSlots(rects, signed)`
- `RoleLabel` = `OfficialRole` → `"Candidat / Angajat"` dacă `Party=="Candidate"` → `SignerName`

**FreeForm (Semnătură Liberă)** — `FreeFormSidebarPanel` + `FreeFormSidebarPanel_Layout`
- Sloturi configurate prin desenare pe PDF (overlay drawing)
- State persistent în `signing-state.json` (attachment în PDF)
- `_sessionSigned = true` doar când se semnează în sesiunea curentă (pentru `HasUnsavedWork`)
- `btnCancelLoad` afișează `ResetOrUnloadDialog` dacă `HasUnsavedWork`

---

## PdfDrawingOverlay (`Controls/PdfDrawingOverlay.cs`)

**Două funcționalități separate:**

### 1. Drawing (FreeForm)
- `EnableDrawing(bool)` → activează `DrawingOverlayControl` peste renderer
- Overlay cu `Dock=Fill`, cursor Cross, tint gri la drawing mode
- `OnOverlayRectDrawn` → `ConvertToPdfCoords` → `RectangleDrawn` event
- Coordonate: `DisplayRectangle` pentru scroll offset

### 2. Ghost Slots Preview (Template + FreeForm)
- `SetPreviewSlots(DrawnRectangle[], bool[])` / `ClearPreviewSlots()`
- Generare PDF temporar în `%TEMP%/wacom_preview_*.pdf`
- **Pass 1:** Ghostscript adaugă `/Group Transparency` (fără asta PdfSharp ignoră alpha)
- **Pass 2:** PdfSharp desenează dreptunghiuri cu alpha + text Arial via `WindowsFontResolver`
- `_realPdfPath` = documentul real; `_previewTempPath` = fișier temp (șters la unload/dispose)

### Scroll restore
- `ReloadDocument(path)` salvează zoom + page + center point
- `ScheduleScrollRestore(zoom, page, center, delayMs)` — 80ms normal, 400ms cu preview
- `RestoreTimer_Tick` → `renderer.Zoom` + `ScrollIntoView`

---

## Packages cheie
- `PdfiumViewer` — viewer PDF cu `PdfRenderer`, `PointToPdf`, `BoundsFromPdf`
- `PdfSharp 6.2.4` — modificare PDF, drawing cu alpha (necesită `/Group` în PDF)
- `WindowsFontResolver` — fonturi din `C:\Windows\Fonts\` pentru PdfSharp
- `iText 9.6.0` (Apryse) — instalat dar **nefolosit** în PdfDrawingOverlay (poate fi scos)
- Ghostscript (`gswin32c.exe`) — adaugă `/Group Transparency` pe PDF-uri fără el

---

## Fișiere modificate în această sesiune

| Fișier | Modificări cheie |
|--------|-----------------|
| `Controls/PdfDrawingOverlay.cs` | Ghost slots via PDF temp + Ghostscript, scroll restore, drawing overlay |
| `Forms/FreeFormSidebarPanel.cs` | `_sessionSigned`, `RefreshPreviewSlots` cu `RoleLabel`, `CanResetToOriginal` |
| `Forms/FreeFormSidebarPanel_Layout.cs` | `btnCancelLoad` cu `ResetOrUnloadDialog`, `BorderSize=0` butoane |
| `Forms/MainForm.cs` | `SetPreviewSlots` după load/semnare, `RoleLabel` cu "Candidat / Angajat", mirror fix embedded |
| `Forms/ShellForm.cs` | `BtnMirror_Click` handler |
| `Forms/ShellForm_Layout.cs` | Buton Oglindire în preview header |
| `Forms/ResetOrUnloadDialog.cs` | Fix NullRef `_pnlReset` când `canResetToOriginal=false` |
| `Forms/TemplateSidebarPanel.cs` | `ToggleMirror()`, `MirrorActive` expuse ca `internal` |

---

## Probleme rezolvate în această sesiune
1. **Ghost slots zoom** — fix: PDF temporar în loc de overlay pe renderer
2. **Transparență FreeForm** — fix: Ghostscript adaugă `/Group` înainte de PdfSharp
3. **Scroll position** — fix: `ScheduleScrollRestore` cu delay diferențiat
4. **Dialog la X** — fix: `HasUnsavedWork` bazat pe `_sessionSigned`
5. **Buton Oglindire dispărut** — re-adăugat în `ShellForm_Layout`
6. **Mirror în embedded mode** — fix: `GetScrollRatioFromRenderer` overload
7. **RoleLabel FreeForm** — fix: populat în `RefreshPreviewSlots`
8. **btnCancelLoad fără dialog** — fix: adăugat `ResetOrUnloadDialog`

---

## Probleme rămase / de verificat
- Ghost slots FreeForm: dacă Ghostscript nu e în PATH, fallback la PDF fără transparență (alpha devine solid)
- Scroll restore poate fi lent dacă Ghostscript > 400ms
- iText 9.6.0 poate fi scos din NuGet (neutilizat)
