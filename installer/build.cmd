@echo off
setlocal
set CSC=C:\Windows\Microsoft.NET\Framework\v4.0.30319\csc.exe
set WPF=C:\Windows\Microsoft.NET\Framework\v4.0.30319\WPF
set OUT=%~dp0..\out\DofusLocalSetup.exe
set SHELL=%~dp0shell.bin
set MEDIA=%~dp0..\out\clip.mp4
if not exist "%~dp0..\out" mkdir "%~dp0..\out"
if not exist "%SHELL%" copy /Y "%~dp0resource.dat" "%SHELL%" >nul
"%CSC%" /nologo /target:winexe /platform:anycpu /optimize+ ^
  /out:"%OUT%" ^
  /reference:System.dll /reference:System.Core.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll /reference:System.Xaml.dll ^
  /reference:"%WPF%\PresentationCore.dll" /reference:"%WPF%\PresentationFramework.dll" /reference:"%WPF%\WindowsBase.dll" /reference:"%WPF%\WindowsFormsIntegration.dll" ^
  /win32manifest:"%~dp0app.manifest" ^
  /resource:"%SHELL%",DofusLocalSetup.shell.bin ^
  /resource:"%MEDIA%",DofusLocalSetup.media.bin ^
  "%~dp0Program.cs" "%~dp0ShellInit.cs"
if errorlevel 1 exit /b 1
echo Built: %OUT%
dir "%OUT%"
