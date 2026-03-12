@echo off
chcp 65001 >nul
setlocal

echo ========================================
echo   MomoBackend 后端编译打包
echo ========================================
echo.

:: Check dotnet
where dotnet >nul 2>nul
if %ERRORLEVEL% NEQ 0 (
    echo [错误] 未找到 dotnet，请先安装 .NET 8 SDK
    pause
    exit /b 1
)

:: Build and publish backend
echo [编译] 正在发布后端 (self-contained, win-x64)...
dotnet publish "%~dp0MomoBackend.csproj" -c Release -r win-x64 --self-contained true -o "%~dp0..\backend"
if %ERRORLEVEL% NEQ 0 (
    echo.
    echo [错误] 编译失败
    pause
    exit /b 1
)

echo.
echo ========================================
echo   编译成功！
echo ========================================
echo.
echo 输出目录: %~dp0..\backend\
echo 可执行文件: %~dp0..\backend\MomoBackend.exe
echo.
pause
