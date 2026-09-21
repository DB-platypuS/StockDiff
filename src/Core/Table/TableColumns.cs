// 创建者: PlatyPus
// 创建时间: 2026-09-20
// 作用: 表格列定义单一数据源，表格渲染、列宽、单元格取值与 CSV 导出均由 Columns 派生。
using StockDiff.Core.Convert;
using StockDiff.Core.Models;

namespace StockDiff.Core.Table;

// 单元格水平对齐方式
public enum ColumnAlign { Left, Center, Right }

// 单个列定义：表头、列宽、对齐方式与取值函数
public sealed record TableColumn(
    string Header, int Width, ColumnAlign Align, Func<StockDiffRow, string> Get);

public static class TableColumns
{
    // 12 列按显示顺序定义；表格渲染、列宽、单元格取值与 CSV 导出均由此派生
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

    // 表头缓存：避免每次调用重复 LINQ 投影；对外返回新数组，防止调用方修改共享状态
    private static readonly string[] HeaderCache = Columns.Select(c => c.Header).ToArray();

    // 返回按序排列的表头文本，供 DataGridView 与 CSV 复用
    public static string[] Headers() => (string[])HeaderCache.Clone();

    // 取第 col 列的对齐方式，下标越界时回退左对齐
    public static ColumnAlign Align(int col) =>
        col >= 0 && col < Columns.Count ? Columns[col].Align : ColumnAlign.Left;

    // 取第 col 列的单元格文本，行对象或下标非法时返回空串
    public static string CellValue(StockDiffRow row, int col) =>
        row is null || col < 0 || col >= Columns.Count ? "" : Columns[col].Get(row);
}