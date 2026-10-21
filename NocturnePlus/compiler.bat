@echo off
rem Compile Nocturne Plus avec le csc fourni par Windows (.NET Framework 4)
rem cnc-ddraw (licence MIT, tiers\cnc-ddraw) est integre a l'exe en ressources.
cd /d "%~dp0"
set CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe
set FW=%WINDIR%\Microsoft.NET\Framework\v4.0.30319
"%CSC%" /nologo /target:winexe /platform:x86 /optimize+ /codepage:65001 /out:NocturnePlus.exe /win32icon:src\nocturne-plus.ico /win32manifest:src\app.manifest /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:System.Core.dll /r:"%FW%\System.IO.Compression.dll" /r:"%FW%\System.IO.Compression.FileSystem.dll" /resource:tiers\cnc-ddraw\ddraw.dll,cnc.ddraw.dll /resource:tiers\cnc-ddraw\ddraw.ini,cnc.ddraw.ini /resource:tiers\cnc-ddraw\lanczos2-sharp.glsl,cnc.lanczos2-sharp.glsl /resource:tiers\cnc-ddraw\LICENSE.txt,cnc.LICENSE.txt src\*.cs
exit /b %ERRORLEVEL%
