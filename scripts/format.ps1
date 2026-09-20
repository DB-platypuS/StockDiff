# 创建者: PlatyPus
# 创建时间: 2026-09-20
# 作用: 代码格式与风格检查脚本；默认按 .editorconfig 格式化，-Check 仅校验不修改。

param([switch]$Check)
$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root
if ($Check) {
    dotnet format StockDiff.slnx --verify-no-changes
} else {
    dotnet format StockDiff.slnx
}