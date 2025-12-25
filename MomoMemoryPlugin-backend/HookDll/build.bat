@echo off
REM ========================================
REM  CursorHook DLL 编译脚本
REM  需要安装 Visual Studio 或 Build Tools
REM ========================================

echo 正在查找 Visual Studio 编译器...

REM 尝试 VS2022
if exist "C:\Program Files\Microsoft Visual Studio\2022\Community\VC\Auxiliary\Build\vcvars64.bat" (
    call "C:\Program Files\Microsoft Visual Studio\2022\Community\VC\Auxiliary\Build\vcvars64.bat"
    goto :compile
)

if exist "C:\Program Files\Microsoft Visual Studio\2022\Professional\VC\Auxiliary\Build\vcvars64.bat" (
    call "C:\Program Files\Microsoft Visual Studio\2022\Professional\VC\Auxiliary\Build\vcvars64.bat"
    goto :compile
)

if exist "C:\Program Files\Microsoft Visual Studio\2022\Enterprise\VC\Auxiliary\Build\vcvars64.bat" (
    call "C:\Program Files\Microsoft Visual Studio\2022\Enterprise\VC\Auxiliary\Build\vcvars64.bat"
    goto :compile
)

REM 尝试 VS2019
if exist "C:\Program Files (x86)\Microsoft Visual Studio\2019\Community\VC\Auxiliary\Build\vcvars64.bat" (
    call "C:\Program Files (x86)\Microsoft Visual Studio\2019\Community\VC\Auxiliary\Build\vcvars64.bat"
    goto :compile
)

if exist "C:\Program Files (x86)\Microsoft Visual Studio\2019\BuildTools\VC\Auxiliary\Build\vcvars64.bat" (
    call "C:\Program Files (x86)\Microsoft Visual Studio\2019\BuildTools\VC\Auxiliary\Build\vcvars64.bat"
    goto :compile
)

echo 错误: 未找到 Visual Studio 或 Build Tools
echo 请安装 Visual Studio 2019/2022 或 Build Tools
pause
exit /b 1

:compile
echo.
echo ========================================
echo  编译 64位 DLL
echo ========================================

cl.exe /LD /O2 /EHsc /W3 CursorHook.cpp /Fe:CursorHook64.dll /link /DEF:CursorHook.def user32.lib kernel32.lib

if %errorlevel% neq 0 (
    echo 编译 64位 DLL 失败!
    pause
    exit /b 1
)

echo.
echo ========================================
echo  编译 32位 DLL
echo ========================================

REM 切换到 32位 环境
if exist "C:\Program Files\Microsoft Visual Studio\2022\Community\VC\Auxiliary\Build\vcvars32.bat" (
    call "C:\Program Files\Microsoft Visual Studio\2022\Community\VC\Auxiliary\Build\vcvars32.bat"
) else if exist "C:\Program Files (x86)\Microsoft Visual Studio\2019\Community\VC\Auxiliary\Build\vcvars32.bat" (
    call "C:\Program Files (x86)\Microsoft Visual Studio\2019\Community\VC\Auxiliary\Build\vcvars32.bat"
)

cl.exe /LD /O2 /EHsc /W3 CursorHook.cpp /Fe:CursorHook32.dll /link /DEF:CursorHook.def user32.lib kernel32.lib

if %errorlevel% neq 0 (
    echo 编译 32位 DLL 失败! (可选)
)

echo.
echo ========================================
echo  编译完成!
echo ========================================
echo 生成的文件:
dir *.dll 2>nul
echo.

REM 复制到主程序目录
copy /Y CursorHook64.dll ..\bin\Debug\net8.0-windows\ 2>nul
copy /Y CursorHook32.dll ..\bin\Debug\net8.0-windows\ 2>nul

echo DLL 已复制到程序目录
pause
