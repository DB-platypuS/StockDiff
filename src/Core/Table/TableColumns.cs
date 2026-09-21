// 创建者: PlatyPus
// 创建时间: 2026-09-20
// 作用: 表格列定义单一数据源，表格渲染、列宽、单元格取值与 CSV 导出均由 Columns 派生。
// 说明: 实体类 StockDiff 与根命名空间 StockDiff 同名，此处以别名 StockDiffRow 引用类型。

using StockDiff.Core.Convert;
using StockDiffRow = StockDiff.Core.Models.StockDiff;

namespace StockDiff.Core.Table;

public enum ColumnAlign { Left, Center, Right }

public sealed record TableColumn(
    string Header, int Width, ColumnAlign Align, Func<StockDiffRow, string> Get);

public static class TableColumns
{
    public static readonly IReadOnlyList<TableColumn> Columns = new[]
    {
        new TableColumn("物料编码", 180, ColumnAlign.Left,  r => r.MaterialCode),
        new TableColumn("异常种类", 120, ColumnAlign.Left,  r => r.DiffType),
        new TableColumn("仓库类型",  80, ColumnAlign.Left,  r => Converters.WarehouseLabelFromCode(r.WarehouseType)),
        new TableColumn("仓库数量",  90, ColumnAlign.Left,  r => Converters.NumberToString(r.WarehouseQty)),
        new TableColumn("WMS数量",   90, ColumnAlign.Left,  r => Converters.NumberToString(r.WmsQty)),
        new TableColumn("差异数量",  90, ColumnAlign.Right, r => Converters.NumberToString(r.QtyDiff)),
        new TableColumn("储位",     120, ColumnAlign.Left,  r => r.Location),
        new TableColumn("WMS库位",  120, ColumnAlign.Left,  r => r.WarehouseNo),
        new TableColumn("仓库冻结",  80, ColumnAlign.Left,  r => r.WarehouseHold),
        new TableColumn("WMS冻结",   80, ColumnAlign.Left,  r => r.WmsHold),
        new TableColumn("仓库效期", 100, ColumnAlign.Left,  r => r.WarehouseExpiry),
        new TableColumn("WMS效期",  100, ColumnAlign.Left,  r => r.WmsExpiry),
    };

    public static string[] Headers() => Columns.Select(c => c.Header).ToArray();

    public static ColumnAlign Align(int col) =>
        col >= 0 && col < Columns.Count ? Columns[col].Align : ColumnAlign.Left;

    public static string CellValue(StockDiffRow row, int col) =>
        row is null || col < 0 || col >= Columns.Count ? "" : Columns[col].Get(row);
}