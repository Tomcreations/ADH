Unicode true
ManifestDPIAware true
RequestExecutionLevel user
SilentInstall normal

!define APP_NAME "ADH"
!define APP_DIR_NAME "ADH"
!define OUT_DIR "..\..\..\outputs\installer"
!define BUILD_DIR "..\..\..\outputs\AestikModLoader"
!define ICON_PATH "..\src\App\ADH-Logo.ico"

Name "${APP_NAME}"
OutFile "${OUT_DIR}\ADH-Setup.exe"
InstallDir "$LOCALAPPDATA\Programs\${APP_DIR_NAME}"
Icon "${ICON_PATH}"
UninstallIcon "${ICON_PATH}"
BrandingText "ADH Installer"

VIProductVersion "1.0.0.0"
VIAddVersionKey "ProductName" "${APP_NAME}"
VIAddVersionKey "FileDescription" "ADH Installer"
VIAddVersionKey "CompanyName" "ADH"
VIAddVersionKey "LegalCopyright" "ADH"
VIAddVersionKey "FileVersion" "1.0.0.0"
VIAddVersionKey "ProductVersion" "1.0.0.0"

Page directory
Page instfiles
UninstPage uninstConfirm
UninstPage instfiles

Section "Install"
  SetOutPath "$INSTDIR"
  CreateDirectory "$INSTDIR"
  File "${BUILD_DIR}\ADH.exe"
  File "${BUILD_DIR}\ADH.Runtime.dll"
  File "${BUILD_DIR}\adh-ui.html"
  File "${BUILD_DIR}\ADH-Logo.ico"
  File "${BUILD_DIR}\ADH-Logo.png"
  File "${BUILD_DIR}\WebView2Loader.dll"
  File "${BUILD_DIR}\Microsoft.Web.WebView2.Core.dll"
  File "${BUILD_DIR}\Microsoft.Web.WebView2.WinForms.dll"
  File "${BUILD_DIR}\Mono.Cecil.dll"

  WriteUninstaller "$INSTDIR\Uninstall.exe"
  CreateDirectory "$SMPROGRAMS\${APP_DIR_NAME}"
  CreateShortcut "$SMPROGRAMS\${APP_DIR_NAME}\ADH.lnk" "$INSTDIR\ADH.exe" "" "$INSTDIR\ADH.exe"
  CreateShortcut "$DESKTOP\ADH.lnk" "$INSTDIR\ADH.exe" "" "$INSTDIR\ADH.exe"
SectionEnd

Section "Uninstall"
  Delete "$DESKTOP\ADH.lnk"
  Delete "$SMPROGRAMS\${APP_DIR_NAME}\ADH.lnk"
  RMDir "$SMPROGRAMS\${APP_DIR_NAME}"
  RMDir /r "$INSTDIR"
SectionEnd
