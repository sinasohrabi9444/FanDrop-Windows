@echo off
rem Builds FanDrop.exe with the C# compiler that ships with Windows (.NET Framework 4.8) - nothing to install.
setlocal
cd /d "%~dp0"
set FW=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319
set WM=%WINDIR%\System32\WinMetadata
set REFS=/r:"%FW%\WPF\PresentationCore.dll" /r:"%FW%\WPF\PresentationFramework.dll" /r:"%FW%\WPF\WindowsBase.dll" ^
 /r:System.Xaml.dll /r:System.Xml.Linq.dll /r:System.Windows.Forms.dll /r:System.Drawing.dll ^
 /r:System.IO.Compression.dll /r:System.IO.Compression.FileSystem.dll /r:Microsoft.VisualBasic.dll /r:Microsoft.CSharp.dll ^
 /r:"%FW%\System.Runtime.dll" /r:"%WM%\Windows.Foundation.winmd" /r:"%WM%\Windows.Storage.winmd" /r:"%WM%\Windows.Data.winmd" /r:"%WM%\Windows.Graphics.winmd"
set OPTS=/nologo /target:winexe /platform:anycpu /optimize+ /utf8output

if not exist build mkdir build

rem pass 1: plain build, used only to draw the app icon
"%FW%\csc.exe" %OPTS% /out:build\FanDrop.icon-tool.exe %REFS% src\*.cs || goto :fail
build\FanDrop.icon-tool.exe --make-icon build\FanDrop.ico || goto :fail
del build\FanDrop.icon-tool.exe

rem pass 2: final exe with the icon embedded
"%FW%\csc.exe" %OPTS% /out:FanDrop.exe /win32icon:build\FanDrop.ico %REFS% src\*.cs || goto :fail
echo.
echo Built %CD%\FanDrop.exe
exit /b 0

:fail
echo BUILD FAILED
exit /b 1
