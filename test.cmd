@echo off
rem Unit tests:            test.cmd
rem Plus end-to-end test:  test.cmd e2e   (takes over the mouse and keyboard for ~30 s)
setlocal
cd /d "%~dp0"
set CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe
if not exist "%CSC%" set CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe
if not exist dist mkdir dist

"%CSC%" /nologo /codepage:65001 /out:dist\CoreTests.exe /r:System.Drawing.dll src\Core.cs tests\CoreTests.cs || exit /b 1
dist\CoreTests.exe || exit /b 1

if /i not "%1"=="e2e" exit /b 0

call "%~dp0build.cmd" || exit /b 1
"%CSC%" /nologo /codepage:65001 /win32manifest:app.manifest /out:dist\E2E.exe /r:System.Windows.Forms.dll /r:System.Drawing.dll tests\E2E.cs || exit /b 1
dist\E2E.exe dist\Mousetrap.exe
