# MomoMemoryPlugin 打包脚本
# 使用方法: .\build.ps1 或 .\build.ps1 -SkipBackend

param(
    [switch]$SkipBackend,
    [switch]$SkipFrontend
)

$ErrorActionPreference = "Stop"
$ProjectRoot = $PSScriptRoot

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  MomoMemoryPlugin Build Script" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# 1. 编译并发布后端 (self-contained, 不需要安装 .NET 运行时)
if (-not $SkipBackend) {
    Write-Host "[1/3] Building and publishing backend..." -ForegroundColor Yellow
    $backendProj = Join-Path $ProjectRoot "MomoMemoryPlugin-backend\MomoBackend.csproj"
    $backendOutput = Join-Path $ProjectRoot "backend"

    dotnet publish $backendProj -c Release -r win-x64 --self-contained true -o $backendOutput
    if ($LASTEXITCODE -ne 0) {
        Write-Host "Backend build failed!" -ForegroundColor Red
        exit 1
    }
    Write-Host "Backend published to: $backendOutput" -ForegroundColor Green
} else {
    Write-Host "[1/3] Skipping backend build" -ForegroundColor Gray
}

Write-Host ""

# 2. 编译前端 TypeScript
if (-not $SkipFrontend) {
    Write-Host "[2/3] Compiling TypeScript..." -ForegroundColor Yellow
    Push-Location $ProjectRoot
    npm run compile
    if ($LASTEXITCODE -ne 0) {
        Pop-Location
        Write-Host "TypeScript compilation failed!" -ForegroundColor Red
        exit 1
    }
    Pop-Location
    Write-Host "TypeScript compiled successfully" -ForegroundColor Green
} else {
    Write-Host "[2/3] Skipping TypeScript compilation" -ForegroundColor Gray
}

Write-Host ""

# 3. 打包 VSIX
Write-Host "[3/3] Packaging VSIX..." -ForegroundColor Yellow
Push-Location $ProjectRoot
npx @vscode/vsce package
if ($LASTEXITCODE -ne 0) {
    Pop-Location
    Write-Host "VSIX packaging failed!" -ForegroundColor Red
    exit 1
}
Pop-Location

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  Build completed successfully!" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Cyan

# 显示生成的 vsix 文件
$vsixFiles = Get-ChildItem -Path $ProjectRoot -Filter "*.vsix" | Sort-Object LastWriteTime -Descending | Select-Object -First 1
if ($vsixFiles) {
    Write-Host ""
    Write-Host "Output: $($vsixFiles.FullName)" -ForegroundColor Cyan
    Write-Host "Size: $([math]::Round($vsixFiles.Length / 1MB, 2)) MB" -ForegroundColor Cyan
}
