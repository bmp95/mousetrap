@echo off
rem Builds dist\CentrarRaton.exe with the C# compiler that ships with Windows.
setlocal
cd /d "%~dp0"
set CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe
if not exist "%CSC%" set CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe
if not exist dist mkdir dist

rem A copy that is still running, or that an antivirus is still inspecting, cannot
rem be overwritten, but it can be moved aside. Leftovers go once they are released.
del /q dist\*.old.exe >nul 2>&1
del /q dist\CentrarRaton.exe >nul 2>&1
if exist dist\CentrarRaton.exe ren dist\CentrarRaton.exe CentrarRaton.%RANDOM%.old.exe

"%CSC%" /nologo /codepage:65001 /target:winexe /optimize+ /win32manifest:app.manifest /out:dist\CentrarRaton.exe /r:System.Windows.Forms.dll /r:System.Drawing.dll src\Core.cs src\Program.cs || exit /b 1
echo Built dist\CentrarRaton.exe
