@echo off
rem Compile Nocturne Plus avec le csc fourni par Windows (.NET Framework 4)
cd /d "%~dp0"
set CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe
"%CSC%" /nologo /target:winexe /platform:x86 /optimize+ /codepage:65001 /out:NocturnePlus.exe /win32icon:src\nocturne-plus.ico /win32manifest:src\app.manifest /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:System.Core.dll src\*.cs
exit /b %ERRORLEVEL%
