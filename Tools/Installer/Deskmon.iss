; 데스크몬 인스톨러 (Inno Setup 6)
;
; 만드는 법:
;   1. [Deskmon/빌드만 (배포)] 로 Build/Deskmon 을 만든다
;   2. Inno Setup 6 설치 후 (https://jrsoftware.org/isinfo.php):
;      "C:\Program Files (x86)\Inno Setup 6\ISCC.exe" Tools\Installer\Deskmon.iss
;   3. 산출물: Build\Installer\Deskmon-Setup-<버전>.exe
;
; 설계 결정:
;   - 사용자별 설치 (관리자 권한 불필요). 서명 없는 앱은 UAC 관리자 승격 화면에서
;     "알 수 없는 게시자" 경고가 가장 험악하게 나온다 - 사용자 폴더 설치는 그 화면
;     자체를 건너뛴다.
;   - 로그인 시 자동 시작을 기본 체크로 제안한다. 상주 데스크탑 펫은 부팅하면
;     떠 있는 것이 정체성이다 (원치 않으면 설치 화면에서 끌 수 있다).
;   - 제거해도 세이브(%USERPROFILE%\AppData\LocalLow\Deskmon)는 남긴다.
;     재설치가 초기화가 되어버리면 안 된다.

#define MyAppName "Deskmon"
; ProjectSetup.cs 의 bundleVersion 과 함께 올린다
#define MyAppVersion "0.9.0"
#define MyAppExeName "Deskmon.exe"
#define BuildDir "..\..\Build\Deskmon"

[Setup]
; AppId 는 업그레이드 식별자 - 절대 바꾸지 않는다 (바꾸면 별개 앱으로 중복 설치된다)
AppId={{7E1D5C0A-9B84-4A63-8F2B-DE5C0DE5C0DE}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher=Deskmon
DefaultDirName={localappdata}\Programs\{#MyAppName}
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
OutputDir=..\..\Build\Installer
OutputBaseFilename=Deskmon-Setup-{#MyAppVersion}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
CloseApplications=yes
; Tools/MakeIcon.ps1 이 생성한다 - 원본(Assets/AppIcon)이 바뀌면 다시 실행
SetupIconFile=deskmon.ico
UninstallDisplayIcon={app}\{#MyAppExeName}

; 코드 서명 - 인증서를 확보하면 주석 해제하고 Inno 설정에서 SignTool을 등록한다.
; 이 앱은 투명 오버레이 + 전역 키 폴링이라 백신 휴리스틱에 특히 잘 걸린다.
; SignTool=mysign $f

[Languages]
; 한국어 UI를 원하면 Inno Setup Translations 에서 Korean.isl 을 받아
; Languages 폴더에 넣고 아래 주석을 해제한다.
; Name: "korean"; MessagesFile: "compiler:Languages\Korean.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "autostart"; Description: "Windows 시작 시 자동 실행 (데스크탑 펫 권장)"
Name: "desktopicon"; Description: "바탕화면 바로가기"; Flags: unchecked

[Files]
; 배포 빌드 전체. Development 빌드를 패키징하지 않도록 빌드 메뉴를 확인할 것 -
; 배포 메뉴로 만든 빌드에는 개발 HUD가 없다.
Source: "{#BuildDir}\*"; DestDir: "{app}"; Flags: recursesubdirs ignoreversion

[Icons]
Name: "{userprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{userdesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Registry]
; 로그인 시 자동 시작. uninsdeletevalue - 제거하면 등록도 사라진다.
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; \
    ValueType: string; ValueName: "{#MyAppName}"; ValueData: """{app}\{#MyAppExeName}"""; \
    Flags: uninsdeletevalue; Tasks: autostart

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "지금 실행"; \
    Flags: nowait postinstall skipifsilent

[UninstallRun]
; 실행 중이면 제거가 파일을 못 지운다. 이 앱은 작업표시줄에 없어 사용자가
; 켜져 있는지도 모르므로 제거 전에 강제 종료한다.
Filename: "{cmd}"; Parameters: "/C taskkill /IM {#MyAppExeName} /F"; \
    Flags: runhidden; RunOnceId: "KillDeskmon"

; 참고: 세이브는 의도적으로 지우지 않는다.
;   %USERPROFILE%\AppData\LocalLow\Deskmon\Deskmon\save.json
; 완전 삭제를 원하는 사용자는 이 폴더를 직접 지운다.
