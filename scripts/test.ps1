# 创建者: PlatyPus
# 创建时间: 2026-09-20
# 作用: 一键单元测试脚本，执行全解决方案的 xUnit 测试。

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root
dotnet test StockDiff.slnx -c Debug