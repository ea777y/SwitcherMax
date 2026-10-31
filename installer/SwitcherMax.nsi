; SwitcherMax 0.1.0 — per-user, offline, x64-compatible installer.
; Build with: makensis.exe installer\SwitcherMax.nsi from project root
Unicode true
!include "MUI2.nsh"
!include "LogicLib.nsh"
!include "x64.nsh"
!define MUI_ICON "..\assets\icons\SwitcherMax.ico"
!define MUI_UNICON "..\assets\icons\SwitcherMax.ico"

!define APP_NAME "SwitcherMax"
!define APP_VERSION "0.1.0"
!define UNINSTALL_KEY "Software\Microsoft\Windows\CurrentVersion\Uninstall\SwitcherMax"
!define RUN_KEY "Software\Microsoft\Windows\CurrentVersion\Run"

Name "${APP_NAME} ${APP_VERSION}"
OutFile "..\dist\setup\SwitcherMax_Setup_v0.1.0_x64.exe"
InstallDir "$LOCALAPPDATA\Programs\SwitcherMax"
InstallDirRegKey HKCU "${UNINSTALL_KEY}" "InstallLocation"
RequestExecutionLevel user
SetCompressor /SOLID zlib
ShowInstDetails show
ShowUninstDetails show

!define MUI_ABORTWARNING
!define MUI_FINISHPAGE_RUN "$INSTDIR\SwitcherMax.exe"
!insertmacro MUI_PAGE_WELCOME
!insertmacro MUI_PAGE_DIRECTORY
!insertmacro MUI_PAGE_INSTFILES
!insertmacro MUI_PAGE_FINISH
!insertmacro MUI_UNPAGE_CONFIRM
!insertmacro MUI_UNPAGE_INSTFILES
!insertmacro MUI_LANGUAGE "Russian"
!insertmacro MUI_LANGUAGE "English"

; Block updates/uninstall while the running application owns its mutex.
; This does not terminate the user process and cannot discard unsaved work.
Function CheckNotRunning
    Push $0
    System::Call 'kernel32::OpenMutexW(i 0x00100000, i 0, w "Local\SwitcherMax-SingleInstance") p .r0'
    ${If} $0 != 0
        System::Call 'kernel32::CloseHandle(p r0)'
        MessageBox MB_OK|MB_ICONEXCLAMATION "SwitcherMax запущен. Закройте его через меню значка в трее и повторите действие."
        Pop $0
        Abort
    ${EndIf}
    Pop $0
FunctionEnd

Function .onInit
    ${IfNot} ${RunningX64}
        MessageBox MB_OK|MB_ICONSTOP "Этот пакет предназначен для Windows x64."
        Abort
    ${EndIf}
    Call CheckNotRunning
FunctionEnd

Function un.onInit
    ; Run the same check in the uninstaller (uninstaller uses its own function namespace).
    System::Call 'kernel32::OpenMutexW(i 0x00100000, i 0, w "Local\SwitcherMax-SingleInstance") p .r0'
    ${If} $0 != 0
        System::Call 'kernel32::CloseHandle(p r0)'
        MessageBox MB_OK|MB_ICONEXCLAMATION "Сначала закройте SwitcherMax через значок в трее."
        Abort
    ${EndIf}
FunctionEnd

Section "SwitcherMax (required)" SecMain
    SectionIn RO
    SetOutPath "$INSTDIR"
    File "..\dist\win-x64\SwitcherMax.exe"
    CreateDirectory "$SMPROGRAMS\SwitcherMax"
    CreateShortcut "$SMPROGRAMS\SwitcherMax\SwitcherMax.lnk" "$INSTDIR\SwitcherMax.exe"
    CreateShortcut "$SMPROGRAMS\SwitcherMax\Удалить SwitcherMax.lnk" "$INSTDIR\Uninstall.exe"
    WriteUninstaller "$INSTDIR\Uninstall.exe"
    WriteRegStr HKCU "${UNINSTALL_KEY}" "DisplayName" "SwitcherMax ${APP_VERSION}"
    WriteRegStr HKCU "${UNINSTALL_KEY}" "DisplayVersion" "${APP_VERSION}"
    WriteRegStr HKCU "${UNINSTALL_KEY}" "Publisher" "SwitcherMax"
    WriteRegStr HKCU "${UNINSTALL_KEY}" "InstallLocation" "$INSTDIR"
    WriteRegStr HKCU "${UNINSTALL_KEY}" "DisplayIcon" "$INSTDIR\SwitcherMax.exe"
    WriteRegStr HKCU "${UNINSTALL_KEY}" "UninstallString" '"$INSTDIR\Uninstall.exe"'
    WriteRegDWORD HKCU "${UNINSTALL_KEY}" "NoModify" 1
    WriteRegDWORD HKCU "${UNINSTALL_KEY}" "NoRepair" 1
    WriteRegDWORD HKCU "${UNINSTALL_KEY}" "EstimatedSize" 155000
SectionEnd

Section "Uninstall"
    ; Delete autostart ONLY when it points to this installed copy.
    ReadRegStr $0 HKCU "${RUN_KEY}" "SwitcherMax"
    StrCpy $1 '"$INSTDIR\SwitcherMax.exe"'
    ${If} $0 == $1
        DeleteRegValue HKCU "${RUN_KEY}" "SwitcherMax"
    ${EndIf}
    Delete "$SMPROGRAMS\SwitcherMax\SwitcherMax.lnk"
    Delete "$SMPROGRAMS\SwitcherMax\Удалить SwitcherMax.lnk"
    RMDir "$SMPROGRAMS\SwitcherMax"
    Delete "$INSTDIR\SwitcherMax.exe"
    Delete "$INSTDIR\Uninstall.exe"
    RMDir "$INSTDIR"
    DeleteRegKey HKCU "${UNINSTALL_KEY}"
    ; Preserve %LOCALAPPDATA%\SwitcherMax\settings.json intentionally.
SectionEnd
