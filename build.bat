@echo off
chcp 65001 >nul
setlocal enabledelayedexpansion

echo ========================================
echo   Momo Memory Plugin
echo   Backend + Frontend + Auto Version
echo ========================================
echo.

rem ---- Check environment ----
where dotnet >nul 2>nul
if %ERRORLEVEL% NEQ 0 (
    echo [Error] dotnet not found, please install .NET 8 SDK
    pause
    exit /b 1
)
where npm >nul 2>nul
if %ERRORLEVEL% NEQ 0 (
    echo [Error] npm not found, please install Node.js
    pause
    exit /b 1
)

rem ---- Create build output directory ----
if not exist "%~dp0build" mkdir "%~dp0build"

rem ---- Calculate version: scan build/ for latest vsix ----
set "LAST_PATCH=-1"
set "MAJOR=0"
set "MINOR=0"
for /f "tokens=*" %%f in ('dir /b /o-d "%~dp0build\momo-memory-plugin-*.vsix" 2^>nul') do (
    if "!LAST_PATCH!"=="-1" (
        set "FNAME=%%~nf"
        set "VER=!FNAME:momo-memory-plugin-=!"
        for /f "tokens=1,2,3 delims=." %%a in ("!VER!") do (
            set "MAJOR=%%a"
            set "MINOR=%%b"
            set "LAST_PATCH=%%c"
        )
    )
)

rem Calculate new version
if "!LAST_PATCH!"=="-1" (
    set "NEW_VERSION=0.0.1"
) else (
    set /a "NEW_PATCH=!LAST_PATCH!+1"
    set "NEW_VERSION=!MAJOR!.!MINOR!.!NEW_PATCH!"
)

echo [Version] !NEW_VERSION!
echo.

rem ---- Step 1: Build backend ----
echo [1/5] Building backend...
dotnet publish "%~dp0MomoMemoryPlugin-backend\MomoBackend.csproj" -c Release -r win-x64 --self-contained true -o "%~dp0backend" -v quiet
if %ERRORLEVEL% NEQ 0 (
    echo [Error] Backend build failed
    pause
    exit /b 1
)
echo [OK] Backend build complete
echo.

rem ---- Step 2: Install frontend dependencies ----
if exist "%~dp0node_modules" goto :skip_npm_install
echo [2/5] Installing frontend dependencies...
cd /d "%~dp0"
call npm install
if %ERRORLEVEL% NEQ 0 (
    echo [Error] npm install failed
    pause
    exit /b 1
)
goto :done_npm_install
:skip_npm_install
echo [2/5] Frontend dependencies exist, skip
:done_npm_install
echo.

rem ---- Step 3: Compile TypeScript ----
echo [3/5] Compiling TypeScript...
cd /d "%~dp0"
call npm run compile
if %ERRORLEVEL% NEQ 0 (
    echo [Error] TypeScript compile failed
    pause
    exit /b 1
)
echo [OK] TypeScript compile complete
echo.

rem ---- Step 4: Update package.json version ----
echo [4/5] Updating version to !NEW_VERSION!...
cd /d "%~dp0"
call npm version !NEW_VERSION! --no-git-tag-version --allow-same-version >nul 2>nul
echo [OK] package.json version updated
echo.

rem ---- Step 5: Package VSIX ----
echo [5/5] Packaging VSIX...
cd /d "%~dp0"
call npx @vscode/vsce package --allow-missing-repository --allow-star-activation -o "build\momo-memory-plugin-!NEW_VERSION!.vsix"
if %ERRORLEVEL% NEQ 0 (
    echo [Error] VSIX packaging failed
    pause
    exit /b 1
)

echo.
echo ========================================
echo   Done!
echo ========================================
echo.
echo   Version:  !NEW_VERSION!
echo   File:     build\momo-memory-plugin-!NEW_VERSION!.vsix
echo.

for %%f in ("%~dp0build\momo-memory-plugin-!NEW_VERSION!.vsix") do (
    set "SIZE=%%~zf"
    set /a "SIZE_MB=!SIZE!/1048576"
    echo   Size:     !SIZE_MB! MB
)
echo.
echo   Install: VS Code Ctrl+Shift+P - Install from VSIX
echo.
pause
