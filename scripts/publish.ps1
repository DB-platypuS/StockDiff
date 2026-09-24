# 创建者: PlatyPus

# 创建时间: 2026-09-24

# 作用: 一键发布脚本，生成 Windows x64 自包含单文件 exe（同事免装 .NET 运行时），并打包为 zip。

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root

$outDir = Join-Path $root "publish\win-x64"
if (Test-Path $outDir) { Remove-Item $outDir -Recurse -Force }

dotnet publish src\App\StockDiff.App.csproj `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true `
    -o $outDir

# 单文件模式下 pdb 无意义，删除以免误分发
Get-ChildItem $outDir -Filter *.pdb -ErrorAction SilentlyContinue | Remove-Item -Force

# 附带使用说明，随包分发给同事（含「刷新较慢」等注意事项）。
# 说明文件用通配符查找、不写中文文件名：Windows PowerShell 5.1 会把无 BOM 的 UTF-8 源码
# 按 GBK 误读，脚本内的中文字面量会变成乱码路径（表现为 Copy-Item 报路径不存在）
$guide = @(
    (Join-Path $PSScriptRoot '*.txt'),
    (Join-Path $root '*.txt')
) |
    ForEach-Object { Get-ChildItem $_ -File -ErrorAction SilentlyContinue } |
    Select-Object -First 1

if ($guide) {
    Copy-Item $guide.FullName -Destination $outDir -Force
    Write-Host "[OK] guide: $($guide.Name)"
}
else {
    Write-Warning '[WARN] guide txt not found; package built without it'
}

$zip = Join-Path $root "publish\StockDiff-win-x64.zip"
if (Test-Path $zip) { Remove-Item $zip -Force }
Compress-Archive -Path (Join-Path $outDir "*") -DestinationPath $zip

Write-Host "[OK] package: $zip"
