@echo off
rem Builds Zoomy.exe using the C# compiler that comes with Windows 10 (.NET Framework 4).
setlocal
cd /d "%~dp0"
set "CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%CSC%" set "CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe"
if not exist "%CSC%" (
    echo Couldn't find the .NET Framework 4 C# compiler.
    exit /b 1
)
"%CSC%" /nologo /target:winexe /optimize+ /out:Zoomy.exe /win32icon:src\zoomy.ico ^
    /r:System.Windows.Forms.dll /r:System.Drawing.dll src\Zoomy.cs
if errorlevel 1 exit /b 1
echo Built Zoomy.exe
