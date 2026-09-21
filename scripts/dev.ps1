# 创建者: PlatyPus
# 创建时间: 2026-09-20
# 作用: 一键开发启动脚本，依次执行还原、编译并运行 WinForms 主程序。

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root
dotnet restore StockDiff.slnx
dotnet build StockDiff.slnx -c Debug --no-restore
dotnet run --project src\App\StockDiff.App.csproj -c Debug --no-build