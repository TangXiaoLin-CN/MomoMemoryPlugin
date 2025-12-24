@echo off
echo ========================================
echo   Momo Memory Plugin Build Script
echo ========================================
echo.

:: Check if npm is available
where npm >nul 2>nul
if %ERRORLEVEL% NEQ 0 (
    echo [ERROR] npm is not installed or not in PATH
    pause
    exit /b 1
)

:: Install dependencies
echo [1/4] Installing dependencies...
call npm install
if %ERRORLEVEL% NEQ 0 (
    echo [ERROR] Failed to install dependencies
    pause
    exit /b 1
)

:: Compile TypeScript
echo.
echo [2/4] Compiling TypeScript...
call npm run compile
if %ERRORLEVEL% NEQ 0 (
    echo [ERROR] Failed to compile TypeScript
    pause
    exit /b 1
)

:: Check if vsce is installed
echo.
echo [3/4] Checking vsce...
where vsce >nul 2>nul
if %ERRORLEVEL% NEQ 0 (
    echo [INFO] vsce not found, installing globally...
    call npm install -g @vscode/vsce
    if %ERRORLEVEL% NEQ 0 (
        echo [ERROR] Failed to install vsce
        pause
        exit /b 1
    )
)

:: Package extension
echo.
echo [4/4] Packaging extension...
call vsce package --allow-missing-repository
if %ERRORLEVEL% NEQ 0 (
    echo [ERROR] Failed to package extension
    pause
    exit /b 1
)

echo.
echo ========================================
echo   Build completed successfully!
echo ========================================
echo.
echo VSIX file generated. You can install it in VS Code:
echo   1. Open VS Code
echo   2. Press Ctrl+Shift+P
echo   3. Type "Install from VSIX"
echo   4. Select the generated .vsix file
echo.

dir /b *.vsix 2>nul

pause
