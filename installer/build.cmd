@echo off
setlocal
set CSC=C:\Windows\Microsoft.NET\Framework\v4.0.30319\csc.exe
set SRC=%~dp0Program.cs
set OUT=%~dp0..\out\DofusLocalSetup.exe
set RES=%~dp0resource.dat
set MAN=%~dp0app.manifest
if not exist "%~dp0..\out" mkdir "%~dp0..\out"
"%CSC%" /nologo /target:winexe /platform:anycpu /optimize+ ^
  /out:"%OUT%" ^
  /reference:System.dll /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.Core.dll ^
  /win32manifest:"%MAN%" ^
  /resource:"%RES%",DofusLocalSetup.resource.dat ^
  "%SRC%"
if errorlevel 1 exit /b 1
echo Built: %OUT%
dir "%OUT%"
