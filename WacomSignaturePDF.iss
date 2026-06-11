; ============================================================
;  WacomSignaturePDF — Inno Setup Script
;  Necesita: Inno Setup 6.x  (https://jrsoftware.org/isinfo.php)
;
;  Inainte de build:
;    1. Compileaza proiectul in Release -> x86
;    2. Asigura-te ca fisierele din [Files] exista
; ============================================================

#define AppName      "WacomSignaturePDF"
#define AppVersion   "1.0.0"
#define AppPublisher "Compania Ta"
#define AppExe       "WacomSignaturePDF.Launcher.exe"

[Setup]
AppId={{B3F1A2C4-9D7E-4F2B-8A3C-1E5D6F7B0C9A}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppPublisher}
AppPublisherURL=
VersionInfoVersion={#AppVersion}
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
AllowNoIcons=no
OutputDir=Installer\Output
OutputBaseFilename=WacomSignaturePDF_Setup_v{#AppVersion}
SetupIconFile=contract.ico
UninstallDisplayIcon={app}\{#AppExe}
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
WizardSizePercent=120
DisableProgramGroupPage=yes

; Admin necesar pentru variabile de sistem HKLM
PrivilegesRequired=admin

; Arhitectura — x86 pentru compatibilitate cu Wacom STU SDK (COM 32-bit)
ArchitecturesInstallIn64BitMode=
ArchitecturesAllowed=x86 x64compatible

[Languages]
Name: "romanian"; MessagesFile: "compiler:Default.isl"

; ── Fisiere instalate ─────────────────────────────────────────
; .iss se afla in D:\Visual Studio Proiecte Vatra\
; Launcher-ul contine deja toate DLL-urile copiate la build.

[Files]
Source: "WacomSignaturePDF.Launcher\bin\x86\Release\WacomSignaturePDF.Launcher.exe";        DestDir: "{app}"; Flags: ignoreversion
Source: "WacomSignaturePDF.Launcher\bin\x86\Release\WacomSignaturePDF.Launcher.exe.config"; DestDir: "{app}"; Flags: ignoreversion skipifsourcedoesntexist
Source: "WacomSignaturePDF.Launcher\bin\x86\Release\WacomSignaturePdf.dll";                 DestDir: "{app}"; Flags: ignoreversion
Source: "WacomSignaturePDF.Launcher\bin\x86\Release\WacomSignaturePdf.dll.config";          DestDir: "{app}"; Flags: ignoreversion
Source: "WacomSignaturePDF.Launcher\bin\x86\Release\Newtonsoft.Json.dll";                   DestDir: "{app}"; Flags: ignoreversion
Source: "WacomSignaturePDF.Launcher\bin\x86\Release\PdfiumViewer.dll";                      DestDir: "{app}"; Flags: ignoreversion
Source: "WacomSignaturePDF.Launcher\bin\x86\Release\PdfSharp.dll";                          DestDir: "{app}"; Flags: ignoreversion
Source: "WacomSignaturePDF.Launcher\bin\x86\Release\pdfium.dll";                            DestDir: "{app}"; Flags: ignoreversion
Source: "WacomSignaturePDF.Launcher\bin\x86\Release\x64\*"; DestDir: "{app}\x64"; Flags: ignoreversion recursesubdirs
Source: "WacomSignaturePDF.Launcher\bin\x86\Release\x86\*"; DestDir: "{app}\x86"; Flags: ignoreversion recursesubdirs
Source: "contract.ico";                                                                  DestDir: "{app}"; Flags: ignoreversion

; NOTE: *.pdb excluse intentionat.
; DEPENDINTE EXTERNE necesare pe masina tinta:
;   1. Wacom STU SDK (FlSigCaptLib / FLSIGCTLLib) — COM components
;   2. Ghostscript 32-bit (gswin32c.exe in PATH) — pentru ghost slot preview

; ── Shortcut-uri ──────────────────────────────────────────────
[Icons]
Name: "{group}\{#AppName}";         Filename: "{app}\{#AppExe}"; IconFilename: "{app}\contract.ico"
Name: "{commondesktop}\{#AppName}"; Filename: "{app}\{#AppExe}"; IconFilename: "{app}\contract.ico"; Tasks: desktopicon

[Tasks]
Name: desktopicon; Description: "Creeaza shortcut pe Desktop"; GroupDescription: "Optiuni suplimentare:"

; ── Cod wizard ────────────────────────────────────────────────
[Code]

var
  PageRecruitement:  TInputDirWizardPage;
  PageFreeForm:      TInputDirWizardPage;
  PageTemplates:     TInputDirWizardPage;

// ── Citeste variabila de sistem existenta (pentru pre-populare) ──
function GetCurrentEnvVar(name: String): String;
var
  value: String;
begin
  if not RegQueryStringValue(HKEY_LOCAL_MACHINE,
    'SYSTEM\CurrentControlSet\Control\Session Manager\Environment',
    name, value) then
    value := '';
  Result := value;
end;

// ── Creeaza cele 3 pagini de configurare cai ──
procedure InitializeWizard;
begin
  PageRecruitement := CreateInputDirPage(
    wpSelectDir,
    'Configurare cai — Dosare Candidati',
    'RecruitmentDocsPath',
    'Selectati folderul radacina unde sunt stocate dosarele candidatilor.'#13#10 +
    'Fiecare subfolder reprezinta un candidat (format: "ID - Nume").',
    False, '');
  PageRecruitement.Add('');
  PageRecruitement.Values[0] := GetCurrentEnvVar('RecruitmentDocsPath');

  PageFreeForm := CreateInputDirPage(
    PageRecruitement.ID,
    'Configurare cai — Documente Semnatura Libera',
    'FreeFormDocumentsPath',
    'Selectati folderul pentru modul Semnatura Libera.'#13#10 +
    'Vor fi create automat subfolderele: "Documente In Original", "Documente In Proces", "Documente Semnate Complet".',
    False, '');
  PageFreeForm.Add('');
  PageFreeForm.Values[0] := GetCurrentEnvVar('FreeFormDocumentsPath');

  PageTemplates := CreateInputDirPage(
    PageFreeForm.ID,
    'Configurare cai — Sabloane Semnaturi',
    'TemplateDocsPath',
    'Selectati folderul unde se gasesc sabloanele JSON pentru semnaturi.'#13#10 +
    'Aplicatia va cauta automat subfolder-ul "Sabloane Semnaturi Electronice" in interiorul acestuia.',
    False, '');
  PageTemplates.Add('');
  PageTemplates.Values[0] := GetCurrentEnvVar('TemplateDocsPath');
end;

// ── Validare: caile nu pot fi goale ──
function NextButtonClick(CurPageID: Integer): Boolean;
begin
  Result := True;

  if CurPageID = PageRecruitement.ID then
  begin
    if Trim(PageRecruitement.Values[0]) = '' then
    begin
      MsgBox('Calea pentru dosarele candidatilor nu poate fi goala.', mbError, MB_OK);
      Result := False;
    end;
  end
  else if CurPageID = PageFreeForm.ID then
  begin
    if Trim(PageFreeForm.Values[0]) = '' then
    begin
      MsgBox('Calea pentru documentele Semnatura Libera nu poate fi goala.', mbError, MB_OK);
      Result := False;
    end;
  end
  else if CurPageID = PageTemplates.ID then
  begin
    if Trim(PageTemplates.Values[0]) = '' then
    begin
      MsgBox('Calea pentru sabloane nu poate fi goala.', mbError, MB_OK);
      Result := False;
    end;
  end;
end;

// ── Seteaza variabilele de sistem dupa instalare ──
procedure SetSystemEnvVar(name, value: String);
begin
  RegWriteExpandStringValue(
    HKEY_LOCAL_MACHINE,
    'SYSTEM\CurrentControlSet\Control\Session Manager\Environment',
    name, value);
end;

// ── Creeaza subfolderele necesare pentru FreeForm ──
procedure CreateFreeFormSubfolders(basePath: String);
begin
  ForceDirectories(basePath + '\Documente In Original');
  ForceDirectories(basePath + '\Documente In Proces');
  ForceDirectories(basePath + '\Documente Semnate Complet');
end;

procedure CurStepChanged(CurStep: TSetupStep);
var
  recruitPath:  String;
  freeFormPath: String;
  templatePath: String;
begin
  if CurStep <> ssPostInstall then Exit;

  recruitPath  := Trim(PageRecruitement.Values[0]);
  freeFormPath := Trim(PageFreeForm.Values[0]);
  templatePath := Trim(PageTemplates.Values[0]);

  // Seteaza variabilele de sistem (HKLM — sistem, nu utilizator)
  SetSystemEnvVar('RecruitmentDocsPath',   recruitPath);
  SetSystemEnvVar('FreeFormDocumentsPath', freeFormPath);
  SetSystemEnvVar('TemplateDocsPath',      templatePath);

  // Creeaza subfolderele FreeForm daca nu exista
  if DirExists(freeFormPath) or ForceDirectories(freeFormPath) then
    CreateFreeFormSubfolders(freeFormPath);

  // Notifica Windows de schimbarea variabilelor de sistem
  // (echivalent cu "setx /M" + refresh explorer)
  RegWriteStringValue(HKEY_LOCAL_MACHINE,
    'SYSTEM\CurrentControlSet\Control\Session Manager\Environment',
    '_WacomSigInstalled', GetDateTimeString('yyyy/mm/dd hh:nn:ss', '-', ':'));
end;

// ── Pagina de rezumat (inainte de instalare) ──
function UpdateReadyMemo(Space, NewLine, MemoUserInfoInfo, MemoDirInfo,
  MemoTypeInfo, MemoComponentsInfo, MemoGroupInfo, MemoTasksInfo: String): String;
begin
  Result :=
    MemoDirInfo + NewLine + NewLine +
    'Variabile de sistem ce vor fi configurate:' + NewLine +
    Space + 'RecruitmentDocsPath'   + NewLine + Space + Space + PageRecruitement.Values[0] + NewLine +
    Space + 'FreeFormDocumentsPath' + NewLine + Space + Space + PageFreeForm.Values[0]     + NewLine +
    Space + 'TemplateDocsPath'      + NewLine + Space + Space + PageTemplates.Values[0];

  if MemoTasksInfo <> '' then
    Result := Result + NewLine + NewLine + MemoTasksInfo;
end;
