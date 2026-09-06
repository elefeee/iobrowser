@echo off
set CSC=C:\Windows\Microsoft.NET\Framework\v4.0.30319\csc.exe
if not exist "%CSC%" (
    echo csc.exe not found
    pause
    exit /b 1
)
"%CSC%" /nologo /platform:x86 ^
/langversion:5 ^
/target:winexe ^
/out:index.exe ^
/win32icon:app.ico ^
/reference:Microsoft.Web.WebView2.Core.dll ^
/reference:Microsoft.Web.WebView2.WinForms.dll ^
/reference:System.dll ^
/reference:System.Windows.Forms.dll ^
/reference:System.Drawing.dll ^
/reference:Newtonsoft.Json.dll ^
/reference:Microsoft.VisualBasic.dll ^
wv2host.cs
if %ERRORLEVEL% equ 0 (
    echo OK
) else (
    echo FAIL
)
pause
