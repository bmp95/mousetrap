@echo off
rem Builds dist\Mousetrap.exe with the C# compiler that ships with Windows.
setlocal
cd /d "%~dp0"
set CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe
if not exist "%CSC%" set CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe
if not exist dist mkdir dist

rem A copy that is still running, or that an antivirus is still inspecting, cannot
rem be overwritten, but it can be moved aside. Leftovers go once they are released.
del /q dist\*.old.exe >nul 2>&1
del /q dist\Mousetrap.exe >nul 2>&1
if exist dist\Mousetrap.exe ren dist\Mousetrap.exe Mousetrap.%RANDOM%.old.exe

"%CSC%" /nologo /codepage:65001 /target:winexe /optimize+ /win32manifest:app.manifest /win32icon:assets\mousetrap.ico /out:dist\Mousetrap.exe /r:System.Windows.Forms.dll /r:System.Drawing.dll src\AssemblyInfo.cs src\Core.cs src\Program.cs src\SettingsForm.cs || exit /b 1
echo Built dist\Mousetrap.exe
