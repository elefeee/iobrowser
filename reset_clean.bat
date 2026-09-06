@echo off
chcp 65001 >nul
set "EXE_NAME=index.exe"
set "DO_RESTART=1"

:: 1.杀掉主程序进程
taskkill /f /im "%EXE_NAME%" >nul 2>&1

:: 等待进程完全释放句柄，单位毫秒
timeout /t 1 /nobreak >nul

:: 2.删除state.txt（bat自身所在目录）
set "STATE=%~dp0state.txt"
if exist "%STATE%" (
    del /f /q "%STATE%"
)

::3.删除WebView2缓存目录 %localappdata%\indexCache
set "CACHE_DIR=%LOCALAPPDATA%\indexCache"
if exist "%CACHE_DIR%" (
    rmdir /s /q "%CACHE_DIR%"
)

::4.可选重启程序
if "%DO_RESTART%"=="1" (
    start "" "%~dp0%EXE_NAME%"
)