# Find Visual Studio
$vsPath = $null

# Check common VS paths
$paths = @(
    "C:\Program Files\Microsoft Visual Studio\2022\Community",
    "C:\Program Files\Microsoft Visual Studio\2022\Professional",
    "C:\Program Files\Microsoft Visual Studio\2022\Enterprise",
    "C:\Program Files\Microsoft Visual Studio\2022\Preview",
    "C:\Program Files (x86)\Microsoft Visual Studio\2019\Community",
    "C:\Program Files (x86)\Microsoft Visual Studio\2019\Professional",
    "C:\Program Files (x86)\Microsoft Visual Studio\2019\Enterprise",
    "C:\Program Files (x86)\Microsoft Visual Studio\2019\BuildTools"
)

foreach ($p in $paths) {
    if (Test-Path $p) {
        $vsPath = $p
        Write-Host "Found VS at: $p"
        break
    }
}

if (-not $vsPath) {
    # Try vswhere
    $vswhere = "C:\Program Files (x86)\Microsoft Visual Studio\Installer\vswhere.exe"
    if (Test-Path $vswhere) {
        $vsPath = & $vswhere -latest -property installationPath
        Write-Host "Found VS via vswhere: $vsPath"
    }
}

if (-not $vsPath) {
    Write-Host "ERROR: Visual Studio not found!"
    exit 1
}

# Find vcvars64.bat
$vcvars = Join-Path $vsPath "VC\Auxiliary\Build\vcvars64.bat"
if (-not (Test-Path $vcvars)) {
    Write-Host "ERROR: vcvars64.bat not found at $vcvars"
    exit 1
}

Write-Host "Using: $vcvars"

# Compile
$hookDir = "D:\project\MomoMemoryPlugin-backend\HookDll"
$outputDir = "D:\project\MomoMemoryPlugin-backend\bin\Debug\net8.0-windows"

$compileCmd = @"
call "$vcvars"
cd /d "$hookDir"
cl.exe /LD /O2 /EHsc /W3 CursorHook.cpp /Fe:CursorHook64.dll /link /DEF:CursorHook.def user32.lib kernel32.lib
if exist CursorHook64.dll (
    copy /Y CursorHook64.dll "$outputDir\"
    echo SUCCESS: CursorHook64.dll compiled and copied
) else (
    echo ERROR: Compilation failed
)
"@

$compileCmd | Out-File -FilePath "$hookDir\compile_temp.bat" -Encoding ASCII
cmd /c "$hookDir\compile_temp.bat"
Remove-Item "$hookDir\compile_temp.bat" -ErrorAction SilentlyContinue
