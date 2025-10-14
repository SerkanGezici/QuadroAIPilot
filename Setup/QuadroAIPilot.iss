; QuadroAIPilot Installer Script - Windows 11 Only
; Inno Setup 6.2+ gerekli
; Minimal müdahale prensibi - Sadece eksik olanları kur

#define AppName "QuadroAIPilot"
#define AppVersion "1.2.0"
#define AppPublisher "QuadroAI"
#define AppURL "https://quadroai.com"
#define AppExeName "QuadroAIPilot.exe"

[Setup]
AppId={{A7B3C4D5-E6F7-8901-2345-6789ABCDEF01}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppPublisher}
AppPublisherURL={#AppURL}
AppSupportURL={#AppURL}
AppUpdatesURL={#AppURL}
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
AllowNoIcons=yes
LicenseFile=..\LICENSE.txt
OutputDir=..\Output
OutputBaseFilename=QuadroAIPilot_Setup_{#AppVersion}_Win11_Final_v10
SetupIconFile=..\Assets\icon.ico
Compression=lzma2/ultra64
SolidCompression=yes
CompressionThreads=auto
DiskSpanning=no
UninstallDisplayIcon={app}\QuadroAIPilot.exe
WizardStyle=modern
PrivilegesRequired=admin
ArchitecturesInstallIn64BitMode=x64
ArchitecturesAllowed=x64
DisableWelcomePage=no
DisableDirPage=no
DisableProgramGroupPage=yes

[Languages]
Name: "turkish"; MessagesFile: "compiler:Languages\Turkish.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Types]
Name: "typical"; Description: "Tipik Kurulum (Önerilen)"
Name: "full"; Description: "Tam Kurulum (Tüm özellikler)"
Name: "custom"; Description: "Özel Kurulum"; Flags: iscustom

[Components]
Name: "main"; Description: "QuadroAIPilot Ana Uygulama"; Types: typical full custom; Flags: fixed
Name: "runtime"; Description: "Gerekli Çalışma Zamanı Bileşenleri"; Types: typical full custom; Flags: fixed
Name: "runtime\webview2"; Description: "Microsoft Edge WebView2"; Types: typical full custom; Flags: fixed
Name: "runtime\vcredist"; Description: "Visual C++ Redistributables"; Types: typical full custom; Flags: fixed
; .NET 8.0 Runtime - Self-contained deployment - KALDIRILDI
; Windows App SDK - Self-contained deployment - KALDIRILDI
; Türkçe dil paketi ve TTS - Edge TTS kullanıldığı için KALDIRILDI
Name: "email"; Description: "E-posta Entegrasyonu (Outlook)"; Types: typical full custom
; Browser eklentileri kaldırıldı - otomatik kuruluyor
; Name: "browser"; Description: "Tarayıcı Eklentileri"; Types: full custom
; Name: "browser\chrome"; Description: "Google Chrome Eklentisi"; Types: full custom; Check: IsChromeInstalled
; Name: "browser\edge"; Description: "Microsoft Edge Eklentisi"; Types: full custom

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "quicklaunchicon"; Description: "{cm:CreateQuickLaunchIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked; OnlyBelowVersion: 6.1; Check: not IsAdminInstallMode
Name: "startup"; Description: "Windows başlangıcında otomatik başlat"; GroupDescription: "Ek seçenekler:"; Flags: unchecked
Name: "contextmenu"; Description: "Sağ tık menüsüne ekle"; GroupDescription: "Ek seçenekler:"; Flags: unchecked

[Files]
; Ana uygulama dosyaları - publish klasöründen kopyala
Source: "..\bin\x64\Release\net8.0-windows10.0.19041.0\win-x64\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs; Components: main; Excludes: "Logs\*,*.pdb,*.xml,*.vshost.*,af-ZA\*,am-ET\*,ar-SA\*,as-IN\*,az-Latn-AZ\*,bg-BG\*,bn-IN\*,bs-Latn-BA\*,ca-ES\*,ca-Es-VALENCIA\*,cs\*,cs-CZ\*,cy-GB\*,da-DK\*,de-DE\*,el-GR\*,en-GB\*,es-ES\*,es-MX\*,et-EE\*,eu-ES\*,fa-IR\*,fi-FI\*,fil-PH\*,fr-CA\*,fr-FR\*,ga-IE\*,gd-gb\*,gl-ES\*,gu-IN\*,he-IL\*,hi-IN\*,hr-HR\*,hu-HU\*,hy-AM\*,id-ID\*,is-IS\*,it-IT\*,ja-JP\*,ka-GE\*,kk-KZ\*,km-KH\*,kn-IN\*,ko-KR\*,kok-IN\*,lb-LU\*,lo-LA\*,lt-LT\*,lv-LV\*,mi-NZ\*,mk-MK\*,ml-IN\*,mr-IN\*,ms-MY\*,mt-MT\*,nb-NO\*,ne-NP\*,nl-NL\*,nn-NO\*,or-IN\*,pa-IN\*,pl-PL\*,pt-BR\*,pt-PT\*,quz-PE\*,ro-RO\*,ru-RU\*,sk-SK\*,sl-SI\*,sq-AL\*,sr-Cyrl-BA\*,sr-Cyrl-RS\*,sr-Latn-RS\*,sv-SE\*,ta-IN\*,te-IN\*,th-TH\*,tk-TM\*,tt-RU\*,ug-CN\*,uk-UA\*,ur-PK\*,uz-Latn-UZ\*,vi-VN\*,zh-CN\*,zh-HK\*,zh-TW\*"

; LICENSE.txt dosyasını da kopyala
Source: "..\LICENSE.txt"; DestDir: "{app}"; Flags: ignoreversion; Components: main

; PowerShell scriptleri - Sadece gerekli olanlar
Source: "Scripts\EnableWindowsFeatures.ps1"; DestDir: "{app}\Scripts"; Flags: ignoreversion; Components: main
Source: "Scripts\ConfigureSecurity.ps1"; DestDir: "{app}\Scripts"; Flags: ignoreversion; Components: main
Source: "Scripts\CheckMicrophoneAccess.ps1"; DestDir: "{app}\Scripts"; Flags: ignoreversion; Components: main
Source: "Scripts\ConfigureEmail.ps1"; DestDir: "{app}\Scripts"; Flags: ignoreversion; Components: email
; Browser eklentileri kaldırıldı
; Source: "Scripts\InstallBrowserExtensions.ps1"; DestDir: "{app}\Scripts"; Flags: ignoreversion; Components: browser
; Python ve TTS kurulum scripti (optimize edilmis)
Source: "Scripts\InstallPythonOptimized.bat"; DestDir: "{app}\Scripts"; Flags: ignoreversion; Components: main
Source: "Scripts\InstallTurkishVoices.ps1"; DestDir: "{app}\Scripts"; Flags: ignoreversion; Components: main

; WebDriver dosyaları
Source: "Prerequisites\chromedriver.exe"; DestDir: "{app}\Drivers"; Flags: ignoreversion; Components: main
Source: "Prerequisites\msedgedriver.exe"; DestDir: "{app}\Drivers"; Flags: ignoreversion; Components: main

; Bağımlılık yükleyiciler - Sadece gerçekten gerekli olanlar
Source: "Prerequisites\MicrosoftEdgeWebView2Setup.exe"; DestDir: "{tmp}"; Flags: deleteafterinstall; Components: runtime\webview2
Source: "Prerequisites\VC_redist.x64.exe"; DestDir: "{tmp}"; Flags: deleteafterinstall; Components: runtime\vcredist
; .NET 8 Runtime - KALDIRILDI (Self-contained)
; Windows App SDK - KALDIRILDI (Self-contained)

; Browser eklentileri tamamen kaldırıldı
; Source: "..\BrowserExtensions\Chrome\manifest.json"; DestDir: "{app}\Extensions\Chrome"; Flags: ignoreversion
; Source: "..\BrowserExtensions\Chrome\background.js"; DestDir: "{app}\Extensions\Chrome"; Flags: ignoreversion
; Source: "..\BrowserExtensions\Chrome\icon128.png"; DestDir: "{app}\Extensions\Chrome"; Flags: ignoreversion
; Source: "..\BrowserExtensions\Chrome\icon48.png"; DestDir: "{app}\Extensions\Chrome"; Flags: ignoreversion
; Source: "..\BrowserExtensions\Chrome\icon16.png"; DestDir: "{app}\Extensions\Chrome"; Flags: ignoreversion
; Source: "..\BrowserExtensions\Edge\manifest.json"; DestDir: "{app}\Extensions\Edge"; Flags: ignoreversion
; Source: "..\BrowserExtensions\Edge\background.js"; DestDir: "{app}\Extensions\Edge"; Flags: ignoreversion
; Source: "..\BrowserExtensions\Edge\icon128.png"; DestDir: "{app}\Extensions\Edge"; Flags: ignoreversion
; Source: "..\BrowserExtensions\Edge\icon48.png"; DestDir: "{app}\Extensions\Edge"; Flags: ignoreversion
; Source: "..\BrowserExtensions\Edge\icon16.png"; DestDir: "{app}\Extensions\Edge"; Flags: ignoreversion

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppExeName}"; IconFilename: "{app}\Assets\icon.ico"
Name: "{group}\{cm:UninstallProgram,{#AppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExeName}"; IconFilename: "{app}\Assets\icon.ico"; Tasks: desktopicon
Name: "{userappdata}\Microsoft\Internet Explorer\Quick Launch\{#AppName}"; Filename: "{app}\{#AppExeName}"; IconFilename: "{app}\Assets\icon.ico"; Tasks: quicklaunchicon
Name: "{userstartmenu}\Programs\{#AppName}"; Filename: "{app}\{#AppExeName}"; IconFilename: "{app}\Assets\icon.ico"

[Registry]
; Uygulama kayıtları
Root: HKLM; Subkey: "SOFTWARE\QuadroAI\{#AppName}"; ValueType: string; ValueName: "InstallPath"; ValueData: "{app}"; Flags: uninsdeletekey
Root: HKLM; Subkey: "SOFTWARE\QuadroAI\{#AppName}"; ValueType: string; ValueName: "Version"; ValueData: "{#AppVersion}"

; Windows başlangıç
Root: HKCU; Subkey: "SOFTWARE\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "{#AppName}"; ValueData: """{app}\{#AppExeName}"" --startup"; Tasks: startup; Flags: uninsdeletevalue

; Sağ tık menüsü
Root: HKCR; Subkey: "*\shell\QuadroAIPilot"; ValueType: string; ValueName: ""; ValueData: "QuadroAIPilot ile aç"; Tasks: contextmenu; Flags: uninsdeletekey
Root: HKCR; Subkey: "*\shell\QuadroAIPilot"; ValueType: string; ValueName: "Icon"; ValueData: "{app}\{#AppExeName},0"; Tasks: contextmenu
Root: HKCR; Subkey: "*\shell\QuadroAIPilot\command"; ValueType: string; ValueName: ""; ValueData: """{app}\{#AppExeName}"" ""%1"""; Tasks: contextmenu

[Run]
; Visual C++ Redistributables - Kontrol et, yoksa kur
Filename: "{tmp}\VC_redist.x64.exe"; Parameters: "/quiet /norestart"; StatusMsg: "Visual C++ Runtime kuruluyor..."; Flags: waituntilterminated runhidden; Check: not IsVCRedistInstalled

; WebView2 Runtime - Kontrol et, yoksa kur
Filename: "{tmp}\MicrosoftEdgeWebView2Setup.exe"; Parameters: "/silent /install"; StatusMsg: "Microsoft Edge WebView2 kuruluyor..."; Flags: waituntilterminated runhidden; Check: not IsWebView2Installed

; Python ve Edge-TTS kurulumu (EN ÖNCE) - optimize edilmis
Filename: "{app}\Scripts\InstallPythonOptimized.bat"; WorkingDir: "{app}\Scripts"; StatusMsg: "Python ve TTS kuruluyor... (Bu islem 2-5 dakika surebilir)"; Flags: waituntilterminated runhidden; BeforeInstall: LogInstallStart('Python'); AfterInstall: CheckPythonInstallation; Components: main

; Python kurulumunu test et ve cache olustur
Filename: "{cmd}"; Parameters: "/c ""%LOCALAPPDATA%\QuadroAIPilot\Python\python.exe"" -c ""import edge_tts; print('TTS hazir')"""; StatusMsg: "TTS sistemi hazirlaniyor..."; Flags: waituntilterminated runhidden; Components: main

; Türkçe ses paketleri kurulumu
Filename: "powershell.exe"; Parameters: "-ExecutionPolicy Bypass -File ""{app}\Scripts\InstallTurkishVoices.ps1"""; WorkingDir: "{app}\Scripts"; StatusMsg: "Turkce ses paketleri kuruluyor..."; Flags: waituntilterminated runhidden; Components: main

; Windows özellikleri
Filename: "powershell.exe"; Parameters: "-ExecutionPolicy Bypass -File ""{app}\Scripts\EnableWindowsFeatures.ps1"""; StatusMsg: "Windows ozellikleri etkinlestiriliyor..."; Flags: waituntilterminated runhidden; Components: main

; .NET 8 Runtime - KALDIRILDI (Self-contained)
; Windows App SDK - KALDIRILDI (Self-contained)
; Türkçe dil paketi ve TTS - KALDIRILDI (Edge TTS kullanılıyor)

; Güvenlik yapılandırması
Filename: "powershell.exe"; Parameters: "-ExecutionPolicy Bypass -File ""{app}\Scripts\ConfigureSecurity.ps1"" -AppPath ""{app}"""; StatusMsg: "Guvenlik ayarlari yapilandiriliyor..."; Flags: waituntilterminated runhidden; Components: main

; Mikrofon erişim kontrolü
Filename: "powershell.exe"; Parameters: "-ExecutionPolicy Bypass -File ""{app}\Scripts\CheckMicrophoneAccess.ps1"""; StatusMsg: "Mikrofon erisimi kontrol ediliyor..."; Flags: waituntilterminated runhidden; Components: main

; E-posta yapılandırması
Filename: "powershell.exe"; Parameters: "-ExecutionPolicy Bypass -File ""{app}\Scripts\ConfigureEmail.ps1"""; StatusMsg: "E-posta sistemi yapılandırılıyor..."; Flags: runhidden; Components: email

; Browser eklentileri kaldırıldı
; Filename: "powershell.exe"; Parameters: "-ExecutionPolicy Bypass -File ""{app}\Scripts\InstallBrowserExtensions.ps1"" -AppPath ""{app}"""; StatusMsg: "Tarayıcı eklentileri yükleniyor..."; Flags: runhidden

; İlk çalıştırma
Filename: "{app}\{#AppExeName}"; Parameters: "--first-run"; Description: "{cm:LaunchProgram,{#StringChange(AppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent

; Windows Defender istisnası ekle (performans için)
Filename: "powershell.exe"; Parameters: "-ExecutionPolicy Bypass -Command Add-MpPreference -ExclusionPath '{app}' -Force"; StatusMsg: "Windows Defender istisnası ekleniyor..."; Flags: runhidden; Check: IsAdminInstallMode

[Code]
// var bölümü kaldırıldı - download page kullanmıyoruz

// Sistem kontrol fonksiyonları - Windows 11 için sadeleştirilmiş

// Python kontrolü
function IsPythonInstalled: Boolean;
var
  ResultCode: Integer;
  Output: AnsiString;
begin
  Result := False;
  if Exec('cmd.exe', '/c python --version 2>&1', '', SW_HIDE, ewWaitUntilTerminated, ResultCode) then
  begin
    Result := (ResultCode = 0);
  end;
  
  if not Result then
  begin
    if Exec('cmd.exe', '/c python3 --version 2>&1', '', SW_HIDE, ewWaitUntilTerminated, ResultCode) then
    begin
      Result := (ResultCode = 0);
    end;
  end;
end;

// Edge-TTS kontrolü
function IsEdgeTTSInstalled: Boolean;
var
  ResultCode: Integer;
begin
  Result := False;
  if IsPythonInstalled then
  begin
    if Exec('cmd.exe', '/c python -m pip show edge-tts 2>&1', '', SW_HIDE, ewWaitUntilTerminated, ResultCode) then
    begin
      Result := (ResultCode = 0);
    end;
  end;
end;

function IsWebView2Installed: Boolean;
begin
  Result := RegKeyExists(HKLM, 'SOFTWARE\WOW6432Node\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}');
end;

function IsVCRedistInstalled: Boolean;
begin
  Result := RegKeyExists(HKLM, 'SOFTWARE\Microsoft\VisualStudio\14.0\VC\Runtimes\x64');
end;

// Windows App SDK kontrolü KALDIRILDI - Self-contained deployment

function IsChromeInstalled: Boolean;
begin
  Result := RegKeyExists(HKLM, 'SOFTWARE\Google\Chrome') or 
            RegKeyExists(HKCU, 'SOFTWARE\Google\Chrome') or
            FileExists(ExpandConstant('{pf}\Google\Chrome\Application\chrome.exe')) or
            FileExists(ExpandConstant('{pf32}\Google\Chrome\Application\chrome.exe'));
end;

function IsEdgeInstalled: Boolean;
begin
  Result := RegKeyExists(HKLM, 'SOFTWARE\Microsoft\Edge') or
            FileExists(ExpandConstant('{pf}\Microsoft\Edge\Application\msedge.exe')) or
            FileExists(ExpandConstant('{pf32}\Microsoft\Edge\Application\msedge.exe'));
end;


// Türkçe dil paketi kontrolü KALDIRILDI - Edge TTS kullanılıyor

// Mikrofon erişim kontrolü
function IsMicrophoneAccessEnabled: Boolean;
var
  Value: String;
begin
  Result := RegQueryStringValue(HKCU, 'SOFTWARE\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\microphone', 'Value', Value);
  if Result then
    Result := Value = 'Allow';
end;

// Port kontrolü - 19741 portunu kontrol et
function IsPortAvailable: Boolean;
var
  ResultCode: Integer;
  Output: AnsiString;
begin
  // netstat ile port kontrolü
  if Exec(ExpandConstant('{cmd}'), '/c netstat -an | findstr :19741', '', SW_HIDE, ewWaitUntilTerminated, ResultCode) then
  begin
    // ResultCode 0 ise port kullanımda, 1 ise boş
    Result := ResultCode <> 0;
  end
  else
    Result := True; // Kontrol başarısız olduysa devam et
end;

// MAPI/Outlook profil kontrolü
function HasOutlookProfile: Boolean;
begin
  Result := RegKeyExists(HKCU, 'SOFTWARE\Microsoft\Office\16.0\Outlook\Profiles') or
            RegKeyExists(HKCU, 'SOFTWARE\Microsoft\Office\15.0\Outlook\Profiles') or
            RegKeyExists(HKCU, 'SOFTWARE\Microsoft\Windows NT\CurrentVersion\Windows Messaging Subsystem\Profiles');
end;

// Outlook kurulu mu kontrolü
function IsOutlookInstalled: Boolean;
var
  OutlookPath: String;
begin
  Result := RegQueryStringValue(HKLM, 'SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\OUTLOOK.EXE', '', OutlookPath);
  if not Result then
    Result := RegQueryStringValue(HKLM32, 'SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\OUTLOOK.EXE', '', OutlookPath);
  if Result then
    Result := FileExists(OutlookPath);
end;

// Sistem durumu raporu
function GetSystemStatus: String;
begin
  Result := 'Sistem Durumu:' + #13#10 + #13#10;
  
  // Temel bileşenler
  // .NET 8 Runtime - Self-contained deployment
  Result := Result + '✓ .NET 8 Runtime: Uygulama içinde mevcut (Self-contained)' + #13#10;
  
  // Python ve Edge-TTS kontrolü
  if IsPythonInstalled then
    Result := Result + '✓ Python: Kurulu' + #13#10
  else
    Result := Result + '✗ Python: Eksik (Kurulacak)' + #13#10;
    
  if IsEdgeTTSInstalled then
    Result := Result + '✓ Edge-TTS: Kurulu' + #13#10
  else
    Result := Result + '✗ Edge-TTS: Eksik (Kurulacak)' + #13#10;
    
  if IsWebView2Installed then
    Result := Result + '✓ WebView2: Kurulu' + #13#10
  else
    Result := Result + '✗ WebView2: Eksik (İndirilecek)' + #13#10;
    
  if IsVCRedistInstalled then
    Result := Result + '✓ Visual C++ Runtime: Kurulu' + #13#10
  else
    Result := Result + '✗ Visual C++ Runtime: Eksik (İndirilecek)' + #13#10;
    
  // Windows App SDK - Self-contained deployment
  Result := Result + '✓ Windows App SDK: Uygulama içinde mevcut (Self-contained)' + #13#10;
    
  // Edge kontrolü
  Result := Result + #13#10;
  if IsEdgeInstalled then
    Result := Result + '✓ Microsoft Edge: Kurulu' + #13#10
  else
    Result := Result + '✗ Microsoft Edge: KURULU DEĞİL! (Gerekli)' + #13#10;
    
  // Türkçe dil paketi kontrolü KALDIRILDI - Edge TTS kullanıyor
    
  // Tarayıcılar
  Result := Result + #13#10 + 'Tarayıcılar:' + #13#10;
  if IsChromeInstalled then
    Result := Result + '✓ Google Chrome: Kurulu' + #13#10;
    
  // Port kontrolü
  Result := Result + #13#10 + 'Port Durumu:' + #13#10;
  if IsPortAvailable then
    Result := Result + '✓ Port 19741: Kullanılabilir' + #13#10
  else
    Result := Result + '⚠ Port 19741: Kullanımda (Sorun olabilir)' + #13#10;
    
  // Outlook/MAPI kontrolü
  Result := Result + #13#10 + 'E-posta Sistemi:' + #13#10;
  if IsOutlookInstalled then
  begin
    Result := Result + '✓ Microsoft Outlook: Kurulu' + #13#10;
    if HasOutlookProfile then
      Result := Result + '✓ Outlook Profili: Mevcut' + #13#10
    else
      Result := Result + '⚠ Outlook Profili: Yapılandırılmamış' + #13#10;
  end
  else
    Result := Result + '⚠ Microsoft Outlook: Kurulu değil (E-posta özellikleri çalışmayacak)' + #13#10;
end;

// QuadroAIPilot çalışıyor mu kontrolü
function IsQuadroAIPilotRunning: Boolean;
var
  ResultCode: Integer;
begin
  Result := False;
  if Exec(ExpandConstant('{cmd}'), '/c tasklist /FI "IMAGENAME eq QuadroAIPilot.exe" 2>nul | find /I "QuadroAIPilot.exe"', '', SW_HIDE, ewWaitUntilTerminated, ResultCode) then
    Result := (ResultCode = 0);
end;

// .NET Runtime kontrolü (Self-contained olsa bile yöntem olarak saklayalım)
function IsDotNet8Installed: Boolean;
begin
  // Self-contained olduğu için her zaman true dön
  Result := True;
end;

// Python kurulum başlangıcını logla
procedure LogInstallStart(Component: String);
begin
  Log('Kurulum baslatiliyor: ' + Component);
end;

// Python kurulum kontrolü ve hata mesajı
procedure CheckPythonInstallation;
var
  LogFile: String;
  LogContent: AnsiString;
  ErrorFound: Boolean;
begin
  LogFile := ExpandConstant('{%TEMP}\QuadroAI_PythonInstall.log');
  ErrorFound := False;
  
  // Log dosyası var mı kontrol et
  if FileExists(LogFile) then
  begin
    if LoadStringFromFile(LogFile, LogContent) then
    begin
      // Hata var mı kontrol et
      if Pos('[HATA]', LogContent) > 0 then
      begin
        ErrorFound := True;
        // Sadece log'a yaz, kullanıcıya gösterme
        Log('[UYARI] Python kurulumunda hata tespit edildi. TTS özellikleri sınırlı olabilir.');
      end
      else if Pos('[UYARI]', LogContent) > 0 then
      begin
        Log('Python kurulumunda uyarı var ama devam ediliyor');
      end;
    end;
  end
  else
  begin
    // Sadece log'a yaz, kullanıcıya gösterme
    Log('[UYARI] Python kurulum scripti çalıştırılamadı. TTS özellikleri kısıtlı olabilir.');
  end;
end;

function NextButtonClick(CurPageID: Integer): Boolean;
var
  ResultCode: Integer;
  SystemStatus: String;
  WinVersion: String;
begin
  Result := True;
  
  if CurPageID = wpReady then
  begin
    // QuadroAIPilot çalışıyor mu kontrol et
    if IsQuadroAIPilotRunning then
    begin
      MsgBox('QuadroAIPilot şu anda çalışıyor!' + #13#10 + 
             'Lütfen kuruluma devam etmeden önce uygulamayı kapatın.',
             mbError, MB_OK);
      Result := False;
      Exit;
    end;
    // Windows sürüm kontrolü
    if not RegQueryStringValue(HKLM, 'SOFTWARE\Microsoft\Windows NT\CurrentVersion', 'CurrentBuild', WinVersion) then
      WinVersion := '0';
      
    if StrToIntDef(WinVersion, 0) < 22000 then
    begin
      MsgBox('Bu uygulama sadece Windows 11''de çalışır!' + #13#10 + 
             'QuadroAIPilot için Windows 11 (Build 22000+) gerekli.' + #13#10 + 
             'Lütfen Windows 11''e yükseltin.' + #13#10#13#10 + 
             'Mevcut sürüm: Build ' + WinVersion,
             mbError, MB_OK);
      Result := False;
      Exit;
    end;
    
    // Sistem durumu raporu göster
    SystemStatus := GetSystemStatus;
    MsgBox(SystemStatus, mbInformation, MB_OK);
    
    // Edge kontrolü - Windows 11'de her zaman kurulu olmalı, sadece uyarı ver
    if not IsEdgeInstalled then
    begin
      MsgBox('Microsoft Edge bulunamadı!' + #13#10 + 
             'Windows 11''de Edge varsayılan olarak kurulu gelir.' + #13#10 + 
             'Sisteminizde bir sorun olabilir.',
             mbError, MB_OK);
      // Kuruluma devam etmesine izin ver, Edge Windows 11'de mutlaka vardır
    end;
    
    // Port uyarısı
    if not IsPortAvailable then
    begin
      if MsgBox('Port 19741 başka bir uygulama tarafından kullanılıyor.' + #13#10 + 
                'Bu durum QuadroAIPilot''un çalışmasını etkileyebilir.' + #13#10#13#10 + 
                'Yine de devam etmek istiyor musunuz?',
                mbConfirmation, MB_YESNO) = IDNO then
      begin
        Result := False;
        Exit;
      end;
    end;
    
    // Tüm runtime'lar offline olarak dahil edildi
    // Online indirme yok, direkt kuruluma geç
  end;
end;

// Kurulum sonrası test batch dosyası oluştur
procedure CreatePostInstallTestBatch;
var
  TestScript: String;
begin
  TestScript := '@echo off' + #13#10 +
                'echo QuadroAIPilot Kurulum Testi' + #13#10 +
                'echo =============================' + #13#10 +
                'echo.' + #13#10 +
                'echo [1/6] Dosya varligi kontrol ediliyor...' + #13#10 +
                'if exist "%~dp0QuadroAIPilot.exe" (echo OK: Ana uygulama mevcut) else (echo HATA: Ana uygulama bulunamadi & exit /b 1)' + #13#10 +
                'if exist "%~dp0QuadroAIPilot.dll" (echo OK: DLL dosyasi mevcut) else (echo HATA: DLL dosyasi bulunamadi & exit /b 1)' + #13#10 +
                'if exist "%~dp0Drivers\chromedriver.exe" (echo OK: Chrome driver mevcut) else (echo UYARI: Chrome driver bulunamadi)' + #13#10 +
                'if exist "%~dp0Drivers\msedgedriver.exe" (echo OK: Edge driver mevcut) else (echo UYARI: Edge driver bulunamadi)' + #13#10 +
                'echo.' + #13#10 +
                'echo [2/6] WebView2 kontrol ediliyor...' + #13#10 +
                'reg query "HKLM\SOFTWARE\WOW6432Node\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}" >nul 2>&1' + #13#10 +
                'if %errorlevel% equ 0 (echo OK: WebView2 kurulu) else (echo HATA: WebView2 bulunamadi & exit /b 1)' + #13#10 +
                'echo.' + #13#10 +
                'echo [3/6] Port kontrolu yapiliyor...' + #13#10 +
                'netstat -an | findstr :19741 >nul 2>&1' + #13#10 +
                'if %errorlevel% equ 1 (echo OK: Port 19741 kullanilabilir) else (echo UYARI: Port 19741 kullanimda)' + #13#10 +
                'echo.' + #13#10 +
                'echo [4/6] Kayit defteri kontrol ediliyor...' + #13#10 +
                'reg query "HKLM\SOFTWARE\QuadroAI\QuadroAIPilot" >nul 2>&1' + #13#10 +
                'if %errorlevel% equ 0 (echo OK: Kayit defteri girisleri mevcut) else (echo HATA: Kayit defteri girisleri bulunamadi & exit /b 1)' + #13#10 +
                'echo.' + #13#10 +
                'echo [5/6] Python ve TTS kontrol ediliyor...' + #13#10 +
                'if exist "%LOCALAPPDATA%\QuadroAIPilot\Python\python.exe" (' + #13#10 +
                '    echo OK: Python kurulu' + #13#10 +
                '    "%LOCALAPPDATA%\QuadroAIPilot\Python\python.exe" -c "import edge_tts" 2>nul' + #13#10 +
                '    if %errorlevel% equ 0 (echo OK: Edge-TTS modulu kurulu) else (echo HATA: Edge-TTS modulu bulunamadi)' + #13#10 +
                ') else (' + #13#10 +
                '    echo HATA: Python kurulumu bulunamadi' + #13#10 +
                ')' + #13#10 +
                'echo.' + #13#10 +
                'echo [6/6] Turkce ses kontrolu yapiliyor...' + #13#10 +
                '"%LOCALAPPDATA%\QuadroAIPilot\Python\python.exe" -c "import asyncio, edge_tts; voices = asyncio.run(edge_tts.list_voices()); tr = [v for v in voices if ''tr-TR'' in v[''Locale'']]; print(''Turkce Sesler:''); [print(f''  - {v[\"ShortName\"]}: {v[\"Gender\"]}'') for v in tr]" 2>nul | findstr /C:"tr-TR-EmelNeural" >nul' + #13#10 +
                'if %errorlevel% equ 0 (' + #13#10 +
                '    echo OK: Turkce kadin sesi (Emel) hazir' + #13#10 +
                ') else (' + #13#10 +
                '    echo HATA: Turkce kadin sesi bulunamadi!' + #13#10 +
                '    echo Lutfen internet baglantinizi kontrol edin ve uygulamayi yeniden baslatin.' + #13#10 +
                ')' + #13#10 +
                '"%LOCALAPPDATA%\QuadroAIPilot\Python\python.exe" -c "import asyncio, edge_tts; voices = asyncio.run(edge_tts.list_voices()); tr = [v for v in voices if ''tr-TR'' in v[''Locale'']]; print(''Turkce Sesler:''); [print(f''  - {v[\"ShortName\"]}: {v[\"Gender\"]}'') for v in tr]" 2>nul | findstr /C:"tr-TR-AhmetNeural" >nul' + #13#10 +
                'if %errorlevel% equ 0 (' + #13#10 +
                '    echo OK: Turkce erkek sesi (Ahmet) hazir' + #13#10 +
                ') else (' + #13#10 +
                '    echo HATA: Turkce erkek sesi bulunamadi!' + #13#10 +
                '    echo Lutfen internet baglantinizi kontrol edin ve uygulamayi yeniden baslatin.' + #13#10 +
                ')' + #13#10 +
                'echo.' + #13#10 +
                'echo.' + #13#10 +
                'echo =============================' + #13#10 +
                'echo TUM TESTLER TAMAMLANDI!' + #13#10 +
                'echo =============================' + #13#10 +
                'pause' + #13#10 +
                'exit /b 0';
                
  SaveStringToFile(ExpandConstant('{app}\PostInstallTest.bat'), TestScript, False);
end;

// Kurulum sonrası işlemler
procedure CurStepChanged(CurStep: TSetupStep);
var
  ResultCode: Integer;
begin
  if CurStep = ssPostInstall then
  begin
    // İlk çalıştırma işaretçisi
    SaveStringToFile(ExpandConstant('{app}\FirstRun.flag'), 'true', False);
    
    // Test batch dosyası oluştur
    CreatePostInstallTestBatch;
    
    // Kullanıcı bilgilendirmesi
    MsgBox('Kurulum başarıyla tamamlandı!' + #13#10#13#10 +
           'İlk çalıştırmada:' + #13#10 +
           '• Mikrofon erişim izni istenecek' + #13#10 +
           '• Temel ayarlar yapılandırılacak' + #13#10#13#10 +
           'DİKKAT: Dikte özelliği için:' + #13#10 +
           '• İnternet bağlantısı gerekli' + #13#10 +
           '• İlk kullanımda mikrofon izni istenecek (tek seferlik)' + #13#10 +
           '• Windows Gizlilik ayarlarından mikrofon erişimini kontrol edin' + #13#10#13#10 +
           'Tarayıcı Eklentileri (İsteğe Bağlı):' + #13#10 +
           '• Chrome: chrome://extensions → Geliştirici modu → Paketi açılmış öğe yükle' + #13#10 +
           '• Edge: edge://extensions → Geliştirici modu → Paketi açılmış öğe yükle' + #13#10#13#10 +
           'E-posta özelliklerini kullanmak için Outlook gereklidir.',
           mbInformation, MB_OK);
  end;
end;

// Kaldırma işlemleri
procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  ResultCode: Integer;
begin
  if CurUninstallStep = usUninstall then
  begin
    // QuadroAIPilot çalışıyorsa kapat
    if IsQuadroAIPilotRunning then
    begin
      Exec(ExpandConstant('{cmd}'), '/c taskkill /F /IM QuadroAIPilot.exe', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
      Sleep(1000); // Kapanması için bekle
    end;
  end;
  
  if CurUninstallStep = usPostUninstall then
  begin
    // Kullanıcı verilerini silme seçeneği
    if MsgBox('Kullanıcı ayarları ve verilerini de silmek istiyor musunuz?', 
              mbConfirmation, MB_YESNO) = IDYES then
    begin
      DelTree(ExpandConstant('{userappdata}\QuadroAIPilot'), True, True, True);
      // Registry anahtarlarını temizle
      RegDeleteKeyIncludingSubkeys(HKCU, 'SOFTWARE\QuadroAI\QuadroAIPilot');
    end;
  end;
end;