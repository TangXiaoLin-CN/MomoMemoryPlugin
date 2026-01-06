@echo off
chcp 65001 >nul
setlocal enabledelayedexpansion

:: Parse arguments
set "NO_PAUSE="
for %%a in (%*) do (
    if /i "%%a"=="--no-pause" set "NO_PAUSE=1"
    if /i "%%a"=="-y" set "NO_PAUSE=1"
)

echo ========================================
echo   Momo Memory Plugin Build Script
echo ========================================
echo.

:: Check if dotnet is available
where dotnet >nul 2>nul
if %ERRORLEVEL% NEQ 0 (
    echo [ERROR] dotnet is not installed or not in PATH
    if not defined NO_PAUSE pause
    exit /b 1
)

:: Check if npm is available
where npm >nul 2>nul
if %ERRORLEVEL% NEQ 0 (
    echo [ERROR] npm is not installed or not in PATH
    if not defined NO_PAUSE pause
    exit /b 1
)

:: Build and publish backend (self-contained, no .NET runtime required)
echo [1/4] Building and publishing backend...
dotnet publish "%~dp0MomoMemoryPlugin-backend\MomoBackend.csproj" -c Release -r win-x64 --self-contained true -o "%~dp0backend"
if %ERRORLEVEL% NEQ 0 (
    echo [ERROR] Failed to build backend
    if not defined NO_PAUSE pause
    exit /b 1
)
echo [OK] Backend published to backend\
echo.

:: Install dependencies (optional, skip if node_modules exists)
if not exist "%~dp0node_modules" (
    echo [2/4] Installing dependencies...
    call npm install
    if %ERRORLEVEL% NEQ 0 (
        echo [ERROR] Failed to install dependencies
        if not defined NO_PAUSE pause
        exit /b 1
    )
) else (
    echo [2/4] Dependencies already installed, skipping...
)
echo.

:: Compile TypeScript
echo [3/4] Compiling TypeScript...
call npm run compile
if %ERRORLEVEL% NEQ 0 (
    echo [ERROR] Failed to compile TypeScript
    if not defined NO_PAUSE pause
    exit /b 1
)
echo [OK] TypeScript compiled
echo.

:: Package extension
echo [4/4] Packaging extension...
call npx @vscode/vsce package
if %ERRORLEVEL% NEQ 0 (
    echo [ERROR] Failed to package extension
    if not defined NO_PAUSE pause
    exit /b 1
)

echo.
echo ========================================
echo   Build completed successfully!
echo ========================================
echo.
echo VSIX file generated:
for /f "tokens=*" %%i in ('dir /b /o-d *.vsix 2^>nul') do (
    echo   %%i
    goto :showdone
)
:showdone
echo.
echo Install in VS Code: Ctrl+Shift+P ^> "Install from VSIX"
echo.

if not defined NO_PAUSE pause
