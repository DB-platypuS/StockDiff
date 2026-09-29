// 创建者: PlatyPus
// 创建时间: 2026-09-29
// 作用: 差异统计摘要（纯逻辑，零 UI 依赖）：按严重度对记录分组计数，供主面板统计卡片复用并可脱离界面单测。

using StockDiff.Core.Models;

namespace StockDiff.Core.Table;

// 差异统计摘要：Total 为记录总数（接口返回的列表本身即差异数据），Critical / Warning / Notice 为按严重度的分组计数
// （无差异的 Normal 行仅计入 Total，不计入任何分组）
public readonly record struct DiffSummary(int Total, int Critical, int Warning, int Notice)
{
    // 全零摘要：空集合 / null 输入的结果
    public static readonly DiffSummary Empty = new(0, 0, 0, 0);

    // 由记录集合统计各严重度数量；null → 全零
    public static DiffSummary From(IEnumerable<StockDiffRow>? rows)
    {
        if (rows is null)
        {
            return Empty;
        }

        var total = 0;
        var critical = 0;
        var warning = 0;
        var notice = 0;

        foreach (var row in rows)
        {
            total++;
            switch (DiffClassifier.Severity(DiffClassifier.Classify(row)))
            {
                case DiffSeverity.Critical:
                    critical++;
                    break;
                case DiffSeverity.Warning:
                    warning++;
                    break;
                case DiffSeverity.Notice:
                    notice++;
                    break;
            }
        }

        return new DiffSummary(total, critical, warning, notice);
    }
}
