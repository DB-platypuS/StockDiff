// 创建者: PlatyPus
// 创建时间: 2026-09-23
// 作用: 库存差异的语义分级与成对列判定（纯逻辑，零 UI 依赖）：把后端自由文本 diff_type 归一为枚举分类，
//       给出严重度分级、两来源成对列比较与「成对不一致」位掩码，供视图差异着色与详情对比复用。

using System.Globalization;
using System.Text.Json;
using StockDiff.Core.Convert;
using StockDiff.Core.Models;

namespace StockDiff.Core.Table;

// 差异种类：None 为无差异（正常行），Other 为无法归类的非空文本
public enum DiffKind { None, Quantity, Location, Hold, Expiry, Other }

// 差异严重度：决定行底色深浅；Normal 对应正常行
public enum DiffSeverity { Normal, Critical, Warning, Notice }

// 两来源成对列：A=仓库侧，B=WMS 侧；Header 用于与 TableColumns 列序对齐，Kind 决定高亮类别
public sealed record DiffPair(
    string Label,
    DiffKind Kind,
    string LeftHeader,
    string RightHeader,
    Func<StockDiffRow, string> Left,
    Func<StockDiffRow, string> Right);

public static class DiffClassifier
{
    // 成对列定义：数量（数值比较）、储位、冻结、效期（文本比较）
    public static readonly IReadOnlyList<DiffPair> Pairs = new[]
    {
        new DiffPair("数量", DiffKind.Quantity, "仓库数量", "WMS数量",
            r => Converters.NumberToString(r.WarehouseQty), r => Converters.NumberToString(r.WmsQty)),
        new DiffPair("储位", DiffKind.Location, "储位", "WMS库位",
            r => r.Location, r => r.WarehouseNo),
        new DiffPair("冻结", DiffKind.Hold, "仓库冻结", "WMS冻结",
            r => r.WarehouseHold, r => r.WmsHold),
        new DiffPair("效期", DiffKind.Expiry, "仓库效期", "WMS效期",
            r => r.WarehouseExpiry, r => r.WmsExpiry),
    };

    // 列下标 → 成对列下标（-1 表示不属于任何配对）；由表头文本一次性解析，列序调整后自动跟随
    public static readonly IReadOnlyList<int> ColumnToPair = BuildColumnToPair();

    // 归一分类：数量差异优先（两来源数量不等或差值非零），再按关键词匹配 diff_type，
    // 最后按成对列首个不一致项兜底，避免漏标
    public static DiffKind Classify(StockDiffRow? row)
    {
        if (row is null)
        {
            return DiffKind.None;
        }

        // 数量优先：只要仓库数量与 WMS 数量不等，一律归数量差异，不被后端其他自由文本掩盖
        if (IsNonZero(row.QtyDiff) || HasMismatch(row, Pairs[0]))
        {
            return DiffKind.Quantity;
        }

        var type = (row.DiffType ?? "").Trim();
        if (type.Length > 0)
        {
            // 数量类文本：明确的数量不一致，以及「仅WMS存在 / 仅立库存在」——后者一侧数量为 0，本质即数量差异
            if (type.Contains("数量") || type.Contains("仅WMS存在") || type.Contains("仅立库存在"))
            {
                return DiffKind.Quantity;
            }

            if (type.Contains("储位") || type.Contains("库位")) return DiffKind.Location;
            if (type.Contains("冻结")) return DiffKind.Hold;
            if (type.Contains("效期") || type.Contains("过期")) return DiffKind.Expiry;
            return DiffKind.Other;
        }

        // diff_type 缺失：按成对列首个不一致项兜底，避免漏标
        foreach (var pair in Pairs)
        {
            if (HasMismatch(row, pair))
            {
                return pair.Kind;
            }
        }

        return DiffKind.None;
    }

    // 严重度：数量=Critical，储位/冻结=Warning，效期/其他=Notice，无差异=Normal
    public static DiffSeverity Severity(DiffKind kind) => kind switch
    {
        DiffKind.Quantity => DiffSeverity.Critical,
        DiffKind.Location or DiffKind.Hold => DiffSeverity.Warning,
        DiffKind.Expiry or DiffKind.Other => DiffSeverity.Notice,
        _ => DiffSeverity.Normal
    };

    // 分类中文标签：「异常种类」列与「异常类型」筛选项共用同一文案来源，避免字面量两处维护
    public static string KindLabel(DiffKind kind) => kind switch
    {
        DiffKind.Quantity => "数量差异",
        DiffKind.Location => "储位差异",
        DiffKind.Hold => "冻结差异",
        DiffKind.Expiry => "效期差异",
        DiffKind.Other => "其他异常",
        _ => ""
    };

    // 是否差异行：分类非 None（成对列不一致已在 Classify 中兜底为对应类别）
    public static bool IsDiff(StockDiffRow? row) => Classify(row) != DiffKind.None;

    // 成对列两来源值是否不一致：两侧都空视为一致；数量列按数值比较，其余按文本比较
    public static bool HasMismatch(StockDiffRow? row, DiffPair pair)
    {
        if (row is null)
        {
            return false;
        }

        var left = (pair.Left(row) ?? "").Trim();
        var right = (pair.Right(row) ?? "").Trim();
        if (left.Length == 0 && right.Length == 0)
        {
            return false;
        }

        if (pair.Kind == DiffKind.Quantity
            && decimal.TryParse(left, NumberStyles.Any, CultureInfo.InvariantCulture, out var a)
            && decimal.TryParse(right, NumberStyles.Any, CultureInfo.InvariantCulture, out var b))
        {
            return a != b;
        }

        return !string.Equals(left, right, StringComparison.Ordinal);
    }

    // 该行各成对列的「不一致」位掩码：第 i 位对应 Pairs[i]，供视图按列着色
    public static int MismatchMask(StockDiffRow? row)
    {
        if (row is null)
        {
            return 0;
        }

        var mask = 0;
        for (var i = 0; i < Pairs.Count; i++)
        {
            if (HasMismatch(row, Pairs[i]))
            {
                mask |= 1 << i;
            }
        }

        return mask;
    }

    // 列下标对应的成对列下标；越界或未配对返回 -1
    public static int PairIndexForColumn(int col) =>
        col >= 0 && col < ColumnToPair.Count ? ColumnToPair[col] : -1;

    // 数量字段是否为非零：空串视为 0；可解析则判数值；不可解析的非空文本按非零处理（宁可高亮不放过）
    private static bool IsNonZero(JsonElement n)
    {
        var text = Converters.NumberToString(n).Trim();
        if (text.Length == 0)
        {
            return false;
        }

        return !decimal.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, out var value) || value != 0m;
    }

    // 解析列 → 成对列映射：逐列按表头文本匹配任一成对列的左/右表头
    private static int[] BuildColumnToPair()
    {
        var map = new int[TableColumns.Columns.Count];
        Array.Fill(map, -1);

        for (var col = 0; col < TableColumns.Columns.Count; col++)
        {
            var header = TableColumns.Columns[col].Header;
            for (var pair = 0; pair < Pairs.Count; pair++)
            {
                if (Pairs[pair].LeftHeader == header || Pairs[pair].RightHeader == header)
                {
                    map[col] = pair;
                    break;
                }
            }
        }

        return map;
    }
}
