@echo off
setlocal
set CSC=C:\Windows\Microsoft.NET\Framework\v4.0.30319\csc.exe
set OUT=%~dp0..\out\DofusLocalSetup.exe
if not exist "%~dp0..\out" mkdir "%~dp0..\out"
"%CSC%" /nologo /target:winexe /platform:anycpu /optimize+ ^
  /out:"%OUT%" ^
  /reference:System.dll /reference:System.Core.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll ^
  /win32manifest:"%~dp0app.manifest" ^
  "%~dp0Program.cs"
if errorlevel 1 exit /b 1
echo Built: %OUT%
dir "%OUT%"
