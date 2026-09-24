// 创建者: PlatyPus
// 创建时间: 2026-09-22
// 作用: 主面板视图（F4 数据查询与刷新 + F5 数据表格展示 + F7 CSV 导出 + F8 会话管理），提供仓库/对比项筛选与刷新按钮，
//       异步拉取库存差异并渲染 12 列表格；处理取消、登录失效与网络异常；单元格复制（F6）、导出 CSV（F7）已接入；
//       左栏「设置」「退出登录」按钮与 401 自动回登录页由 F8 接线。

using System.Diagnostics;
using System.Globalization;
using StockDiff.App.Dialogs;
using StockDiff.App.Services;
using StockDiff.Core.Api;
using StockDiff.Core.Config;
using StockDiff.Core.Convert;
using StockDiff.Core.Copy;
using StockDiff.Core.Export;
using StockDiff.Core.Models;
using StockDiff.Core.Session;
using StockDiff.Core.Table;

namespace StockDiff.App.Views;

public sealed class DashboardView : UserControl
{
    private readonly ApiClient _client;

    // F8 会话管理：持有外壳与持久化存储，供「设置」对话框与退出登录跳转登录页使用
    private readonly MainForm _mainForm;
    private readonly IBaseUrlStore _store;
    private readonly Button _settingsButton = new() { Text = "设置", Size = new Size(72, 32) };
    private readonly Button _logoutButton = new() { Text = "退出登录", Size = new Size(88, 32) };

    // 仓库筛选标签：直接引用 Core 转换器的中文口径常量，避免同一字面量在 UI 与 Core 两处重复
    private const string FcLabel = Converters.LabelFc;
    private const string AsrsLabel = Converters.LabelAsrs;

    // 布局常量：统一左间距，避免坐标魔数散落
    private const int PadX = 16;
    private const int TitleTop = 20;
    private const int AddressWrapWidth = 220;

    // 右栏五行高度：标题 / 工具栏 / 统计 / 状态栏固定，表格行（Percent 100）自适应；
    // 工具栏 32px 足以容纳含下拉框的 ToolStrip，避免行高不足导致工具栏项被裁切
    private const int TitleBarHeight = 52;
    private const int ToolbarHeight = 44;
    private const int StatsHeight = 84;
    private const int StatusHeight = 26;

    // 状态栏时间格式：配合 InvariantCulture 渲染，避免自定义格式串受系统区域性日历影响
    private const string TimeFormat = "HH:mm:ss";

    // 空数据 / 加载中浮层文案：集中定义，避免同一文案多处维护
    private const string NoDataText = "无差异数据";
    private const string LoadingText = "正在加载…";

    // 左栏数据操作按钮：刷新 / 导出（导出仍输出 CSV，仅按钮文案调整）
    private readonly Button _refreshButton = new() { Text = "刷新数据" };
    private readonly Button _exportButton = new() { Text = "导出 Excel", Enabled = false };
    private readonly ToolStripComboBox _diffTypeFilter = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    // 仓库类型筛选（内存投影，不触发接口请求）
    private readonly ToolStripComboBox _warehouseFilter = new() { DropDownStyle = ComboBoxStyle.DropDownList };

    // 状态栏分段：消息 / 行数 / 筛选条件 / 耗时 / 更新时间
    private readonly ToolStripStatusLabel _statusLabel = new() { Text = "等待刷新", ForeColor = Theme.Muted };
    private readonly ToolStripStatusLabel _countLabel = new() { Text = "0 行", ForeColor = Theme.Muted };
    private readonly ToolStripStatusLabel _filterLabel = new() { Text = "", ForeColor = Theme.Muted };
    private readonly ToolStripStatusLabel _elapsedLabel = new() { Text = "耗时: -", ForeColor = Theme.Muted };
    private readonly ToolStripStatusLabel _updatedLabel = new() { Text = "更新: -", ForeColor = Theme.Muted };

    // 统计摘要卡片数值标签（差异物料数）
    private readonly Label _statDiffValue = new();

    // 异常类型筛选项：标签与分类一一对应（首项 null 表示不限制类型）；
    // 文案与「异常种类」列同源，统一取 Core 的 KindLabel，避免字面量两处维护
    private static readonly string[] DiffTypeLabels =
    {
        "全部类型",
        DiffClassifier.KindLabel(DiffKind.Quantity),
        DiffClassifier.KindLabel(DiffKind.Location),
        DiffClassifier.KindLabel(DiffKind.Hold),
        DiffClassifier.KindLabel(DiffKind.Expiry),
        DiffClassifier.KindLabel(DiffKind.Other)
    };
    private static readonly DiffKind?[] DiffTypeKinds = { null, DiffKind.Quantity, DiffKind.Location, DiffKind.Hold, DiffKind.Expiry, DiffKind.Other };

    // 仓库类型筛选项：标签与仓库代码一一对应（首项 null 表示不限制仓库）
    private static readonly string[] WarehouseLabels = { "全部仓库", FcLabel, AsrsLabel };
    private static readonly string?[] WarehouseCodes = { null, Converters.WarehouseCodeFromLabel(FcLabel), Converters.WarehouseCodeFromLabel(AsrsLabel) };

    // 数据加载耗时计时器
    private readonly Stopwatch _loadStopwatch = new();

    // 当前视图投影（经异常类型 / 只看差异筛选后的行），导出仍使用全量 Rows
    private List<StockDiffRow> _viewRows = new();

    // F5 数据表格：只读 DataGridView（12 列）与空数据提示浮层
    private readonly DataGridView _grid = new();
    private readonly Label _emptyLabel = new()
    {
        Text = "暂无数据，请点击“刷新数据”",
        AutoSize = true,
        ForeColor = Theme.Muted,
        BackColor = Color.White
    };
    private readonly Panel _gridHost = new() { Dock = DockStyle.Fill, BackColor = Color.White };

    // 每次刷新前取消上一次在途请求，保证仅最新请求的结果被采用
    private CancellationTokenSource? _refreshCts;

    // 悬停行下标（-1 表示无）：CellFormatting 据此高亮整行；仅重绘变化的两行
    private int _hoverRow = -1;

    // F6 单元格复制：复制规则收口在 Core，视图只负责展示结果文案
    private readonly CellCopyService _cellCopy;

    // 最近一次成功拉取的记录（视图私有状态）：供表格填充、计数与 CSV 导出复用；
    // 对外无消费者，故不暴露可见性，避免无谓的封装泄漏
    private IReadOnlyList<StockDiffRow> Rows { get; set; } = Array.Empty<StockDiffRow>();

    // 构建主面板：左右分栏（筛选区 + 数据区），并接入刷新 / 设置 / 退出登录按钮
    public DashboardView(ApiClient client, string username, MainForm mainForm, IBaseUrlStore store)
    {
        _client = client;
        _mainForm = mainForm;
        _store = store;
        Dock = DockStyle.Fill;
        BackColor = Color.White;

        var split = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Vertical,
            SplitterWidth = 1,
            BackColor = Theme.Line
        };
        split.Panel1.BackColor = Color.White;
        split.Panel2.BackColor = Color.White;
        split.SizeChanged += (_, _) => SetSplitter(split);
        Controls.Add(split);

        BuildFilterPanel(split.Panel1, username);
        BuildDataPanel(split.Panel2);
        _refreshButton.Click += OnRefreshClick;
        _exportButton.Click += OnExportClick;
        _grid.CellClick += OnCellClick;
        _grid.CellFormatting += OnCellFormatting;
        _grid.RowPostPaint += OnRowPostPaint;
        _grid.CellMouseEnter += OnCellMouseEnter;
        _grid.CellMouseLeave += OnCellMouseLeave;
        _settingsButton.Click += OnSettingsClick;
        _logoutButton.Click += OnLogoutClick;
        _cellCopy = new CellCopyService(new WinClipboard());
    }

    // 左栏固定为总宽 20%，窗口缩放时保持比例；尺寸过小则跳过，避免 SplitterDistance 越界
    private static void SetSplitter(SplitContainer split)
    {
        var upper = split.Width - split.Panel2MinSize - split.SplitterWidth;
        if (upper < split.Panel1MinSize)
        {
            return;
        }

        split.SplitterDistance = Math.Clamp((int)(split.Width * 0.2), split.Panel1MinSize, upper);
    }

    // 左栏装配：用户名标题 + 数据操作按钮（刷新 / 导出）+ 底部会话区（地址 / 设置 / 退出登录 / 版本号）。
    // 仓库与对比项已改为默认全量（工具栏做内存筛选），左栏专注操作入口，按钮纵向铺满、不挤角落。
    private void BuildFilterPanel(Control panel, string username)
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 6,
            BackColor = Color.White,
            Padding = new Padding(PadX, TitleTop, PadX, PadX)
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 44F));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 42F));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 52F));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        root.Controls.Add(new Label
        {
            Text = username,
            Font = Theme.DialogTitleFont,
            ForeColor = Theme.Ink,
            AutoSize = true,
            Margin = new Padding(0)
        }, 0, 0);
        root.Controls.Add(new Label
        {
            Text = "数据操作",
            Font = Theme.BodyFont,
            ForeColor = Theme.Muted,
            AutoSize = true,
            Margin = new Padding(0, 12, 0, 8)
        }, 0, 1);
        root.Controls.Add(ConfigureActionButton(_refreshButton, primary: true), 0, 2);
        root.Controls.Add(ConfigureActionButton(_exportButton, primary: false), 0, 3);
        root.Controls.Add(BuildFooter(), 0, 5);
        panel.Controls.Add(root);
    }

    // 数据操作按钮统一外观：主操作蓝底、次操作白底，纵向铺满左栏可用宽度
    private static Button ConfigureActionButton(Button button, bool primary)
    {
        if (primary)
        {
            Theme.StylePrimary(button, Theme.BodyFont);
        }
        else
        {
            Theme.StyleSecondary(button, Theme.BodyFont);
        }

        button.Dock = DockStyle.Fill;
        button.Margin = new Padding(0, 0, 0, 10);
        return button;
    }

    // 会话区（左栏底部）：接口地址、设置/退出登录按钮行、版本号；随左栏宽度自适应、地址超宽自动换行
    private Control BuildFooter()
    {
        var footer = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Margin = new Padding(0, 8, 0, 0),
            BackColor = Color.Transparent
        };

        var address = new Label
        {
            Text = _client.BaseUrl,
            Font = Theme.AddressFont,
            ForeColor = Theme.Muted,
            AutoSize = true,
            MaximumSize = new Size(AddressWrapWidth, 0)
        };
        var sessionRow = BuildSessionRow();
        var version = new Label
        {
            Text = $"v{AppConfig.Version}",
            Font = Theme.AddressFont,
            ForeColor = Theme.Muted,
            AutoSize = true
        };

        footer.Controls.Add(address);
        footer.Controls.Add(sessionRow);
        footer.Controls.Add(version);
        footer.Resize += (_, _) => FitFooterWidth(footer, address, sessionRow);
        return footer;
    }

    // 设置 / 退出登录按钮行：等宽两列，随左栏宽度铺满
    private Control BuildSessionRow()
    {
        Theme.StyleSecondary(_settingsButton, Theme.BodyFont);
        Theme.StyleSecondary(_logoutButton, Theme.BodyFont);
        _settingsButton.Dock = DockStyle.Fill;
        _logoutButton.Dock = DockStyle.Fill;
        _settingsButton.Margin = new Padding(0, 0, 4, 0);
        _logoutButton.Margin = new Padding(4, 0, 0, 0);

        var row = new TableLayoutPanel
        {
            ColumnCount = 2,
            RowCount = 1,
            Height = 36,
            Width = AddressWrapWidth,
            Margin = new Padding(0, 8, 0, 8),
            BackColor = Color.Transparent
        };
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
        row.Controls.Add(_settingsButton, 0, 0);
        row.Controls.Add(_logoutButton, 1, 0);
        return row;
    }

    // 将地址标签与按钮行宽度对齐到左栏可用宽度：地址自动换行、按钮行铺满
    private static void FitFooterWidth(FlowLayoutPanel footer, Label address, Control sessionRow)
    {
        var available = footer.ClientSize.Width - footer.Margin.Horizontal;
        if (available <= 0)
        {
            return;
        }

        if (address.MaximumSize.Width != available)
        {
            address.MaximumSize = new Size(available, 0);
        }

        if (sessionRow.Width != available)
        {
            sessionRow.Width = available;
        }
    }

    // 右栏装配：用 TableLayoutPanel 固定五行（标题 / 工具栏 / 统计 / 表格 / 状态栏）。
    // 单元格天然不重叠，不依赖停靠顺序与 z 序，彻底避免顶部统计条被 Dock=Fill 的表格遮盖；
    // 窗口拉伸时仅表格行（Percent 100）变高。
    private void BuildDataPanel(Control panel)
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 5,
            BackColor = Color.White
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, TitleBarHeight));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, ToolbarHeight));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, StatsHeight));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, StatusHeight));

        BuildGridHost();
        root.Controls.Add(BuildTitleBar(), 0, 0);
        root.Controls.Add(BuildToolStrip(), 0, 1);
        root.Controls.Add(BuildStatsPanel(), 0, 2);
        root.Controls.Add(_gridHost, 0, 3);
        root.Controls.Add(BuildStatusBar(), 0, 4);
        panel.Controls.Add(root);
        CenterEmptyLabel();
    }

    // 标题栏：页面标题
    private static Control BuildTitleBar()
    {
        var bar = new Panel { Dock = DockStyle.Fill, BackColor = Color.White };
        bar.Controls.Add(new Label
        {
            Text = "差异明细",
            Font = Theme.PageTitleFont,
            ForeColor = Theme.Ink,
            AutoSize = true,
            Location = new Point(PadX, 8)
        });
        return bar;
    }

    // 工具栏：仓库类型筛选 / 异常类型筛选（均为内存投影，切换不触发接口请求）；数据操作按钮移至左栏
    private ToolStrip BuildToolStrip()
    {
        var strip = new ToolStrip
        {
            Dock = DockStyle.Fill,
            AutoSize = false,
            GripStyle = ToolStripGripStyle.Hidden,
            BackColor = Color.White,
            Font = Theme.FilterFont,
            Padding = new Padding(PadX - 8, 4, PadX, 4)
        };

        StyleFilterCombo(_warehouseFilter, 150);
        StyleFilterCombo(_diffTypeFilter, 190);
        _diffTypeFilter.Items.AddRange(DiffTypeLabels);
        _diffTypeFilter.SelectedIndex = 0;
        _diffTypeFilter.SelectedIndexChanged += OnFilterChanged;
        _warehouseFilter.Items.AddRange(WarehouseLabels);
        _warehouseFilter.SelectedIndex = 0;
        _warehouseFilter.SelectedIndexChanged += OnFilterChanged;

        strip.Items.Add(FilterLabel("仓库类型"));
        strip.Items.Add(_warehouseFilter);
        strip.Items.Add(FilterLabel("异常类型"));
        strip.Items.Add(_diffTypeFilter);
        return strip;
    }

    // 筛选标签：加大字号与颜色对比，统一右侧间距
    private static ToolStripLabel FilterLabel(string text) => new(text)
    {
        Font = Theme.FilterFont,
        ForeColor = Theme.Subtle,
        Margin = new Padding(0, 0, 8, 0)
    };

    // 筛选下拉：加大宽度与字号（高度随字号增大），右侧留出组间距
    private static void StyleFilterCombo(ToolStripComboBox combo, int width)
    {
        combo.AutoSize = false;
        combo.Width = width;
        combo.Font = Theme.FilterFont;
        combo.Margin = new Padding(0, 0, 28, 0);
    }

    // 统计摘要：差异物料数卡片 + 异常种类图例
    private Control BuildStatsPanel()
    {
        var panel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.White,
            Padding = new Padding(PadX, 8, PadX, 8)
        };
        var table = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            BackColor = Color.White
        };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 200F));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        table.Controls.Add(BuildStatsCard("差异物料数", _statDiffValue, Theme.Error), 0, 0);
        table.Controls.Add(BuildLegend(), 1, 0);
        panel.Controls.Add(table);
        return panel;
    }

    // 统计卡片：小标题 + 大字号数值
    private static Panel BuildStatsCard(string caption, Label value, Color valueColor)
    {
        value.Text = "-";
        value.Font = Theme.StatValueFont;
        value.ForeColor = valueColor;
        value.AutoSize = true;
        value.Location = new Point(14, 34);

        var card = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Canvas, Margin = new Padding(0, 0, 12, 0) };
        card.Controls.Add(new Label
        {
            Text = caption,
            Font = Theme.StatCaptionFont,
            ForeColor = Theme.Muted,
            AutoSize = true,
            Location = new Point(14, 12)
        });
        card.Controls.Add(value);
        return card;
    }

    // 异常种类图例：与表格异常种类列同色，单行横排便于对照（窄窗自动折行）
    private static Control BuildLegend()
    {
        var legend = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true,
            BackColor = Color.White,
            Padding = new Padding(8, 8, 0, 0)
        };
        legend.Controls.Add(LegendItem("数量差异", GridTheme.KindQuantityInk));
        legend.Controls.Add(LegendItem("储位差异", GridTheme.KindLocationInk));
        legend.Controls.Add(LegendItem("冻结差异", GridTheme.KindHoldInk));
        legend.Controls.Add(LegendItem("效期差异", GridTheme.KindExpiryInk));
        return legend;
    }

    // 图例单元：同色方块 + 文案（横排右间距）
    private static Label LegendItem(string text, Color color) => new()
    {
        Text = "■ " + text,
        ForeColor = color,
        Font = Theme.StatCaptionFont,
        AutoSize = true,
        Margin = new Padding(0, 0, 16, 2)
    };

    // 状态栏：消息 / 行数 / 筛选条件 / 耗时 / 更新时间
    private StatusStrip BuildStatusBar()
    {
        var status = new StatusStrip { Dock = DockStyle.Fill, BackColor = Color.White, SizingGrip = false };
        _filterLabel.Spring = true;
        _filterLabel.TextAlign = ContentAlignment.MiddleLeft;

        status.Items.Add(_statusLabel);
        status.Items.Add(Divider());
        status.Items.Add(_countLabel);
        status.Items.Add(Divider());
        status.Items.Add(_filterLabel);
        status.Items.Add(_elapsedLabel);
        status.Items.Add(Divider());
        status.Items.Add(_updatedLabel);
        return status;
    }

    // 状态栏分隔符
    private static ToolStripStatusLabel Divider() => new() { Text = "|", ForeColor = Theme.Line };

    // 中部：表格 + 空数据提示浮层
    private void BuildGridHost()
    {
        BuildGrid();
        _gridHost.Controls.Add(_grid);
        _gridHost.Controls.Add(_emptyLabel);
        // 后加入的控件处于 z 序底部，会被 Dock=Fill 的表格完全遮住，需提到最前才可见
        _emptyLabel.BringToFront();
        _gridHost.Resize += (_, _) => CenterEmptyLabel();
    }

    // 配置 12 列表格：只读、禁增删行/调行高、无行头、单元格选择；列顺序/对齐由 TableColumns 派生，
    // 列宽按表头与内容自动撑开，表头允许换行且高度自适应，避免表头文字被截断为省略号
    private void BuildGrid()
    {
        _grid.Dock = DockStyle.Fill;
        _grid.ReadOnly = true;
        _grid.AllowUserToAddRows = false;
        _grid.AllowUserToDeleteRows = false;
        _grid.AllowUserToResizeRows = false;
        _grid.RowHeadersVisible = false;
        _grid.MultiSelect = false;
        _grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;
        _grid.AllowUserToResizeColumns = true;
        _grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
        _grid.ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
        {
            Alignment = DataGridViewContentAlignment.MiddleCenter,
            WrapMode = DataGridViewTriState.True
        };

        foreach (var column in TableColumns.Columns)
        {
            _grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                HeaderText = column.Header,
                AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells,
                SortMode = DataGridViewColumnSortMode.NotSortable,
                DefaultCellStyle = new DataGridViewCellStyle
                {
                    Alignment = column.Align == ColumnAlign.Right
                        ? DataGridViewContentAlignment.MiddleRight
                        : DataGridViewContentAlignment.MiddleLeft
                }
            });
        }

        // 统一外观：字体、行高、表头、网格线、选择样式与双缓冲
        _grid.ApplyGridLook();
    }

    // 把拉取结果投影为表格模型并逐格填充；不用 DataSource，空值与对齐完全由列定义决定
    private void PopulateGrid(IReadOnlyList<StockDiffRow> rows)
    {
        if (IsDisposed || _grid.IsDisposed)
        {
            return;
        }

        var model = TableGrid.From(rows);

        // 批量填充：填充期间把列宽模式降为 None 并挂起布局，避免「每加一行就重算整表列宽」的 O(n²) 开销；
        // 填充后一次性恢复 AllCells，列宽最终结果与逐行填充完全一致。
        _hoverRow = -1;
        SetColumnAutoSize(DataGridViewAutoSizeColumnMode.None);
        _grid.SuspendLayout();
        try
        {
            _grid.Rows.Clear();
            // 每行挂载渲染元数据（分类/严重度/成对不一致掩码），CellFormatting 只做 O(1) 查表
            for (var i = 0; i < model.Rows.Count; i++)
            {
                var index = _grid.Rows.Add((object[])model.Rows[i]);
                _grid.Rows[index].Tag = GridStyler.BuildMeta(rows[i]);
            }
        }
        finally
        {
            _grid.ResumeLayout(false);
            SetColumnAutoSize(DataGridViewAutoSizeColumnMode.AllCells);
        }

        ShowOverlay(model.IsEmpty ? EmptyOverlayText() : null);
    }

    // 空状态浮层文案：无数据 → 提示刷新；有数据但筛选后无差异 → 一致性/筛选提示
    private string EmptyOverlayText()
    {
        if (Rows.Count == 0)
        {
            return "暂无数据，请点击“刷新数据”";
        }

        return HasViewFilter()
            ? "当前筛选条件下未发现差异"
            : "未发现差异，数据一致";
    }

    // 是否存在生效的视图筛选（仓库类型 / 异常类型）
    private bool HasViewFilter() => SelectedDiffKind() is not null || SelectedWarehouseCode() is not null;

    // 浮层显示：text 为 null 时隐藏；否则显示并居中
    private void ShowOverlay(string? text)
    {
        if (text is null)
        {
            _emptyLabel.Visible = false;
            return;
        }

        _emptyLabel.Text = text;
        _emptyLabel.Visible = true;
        CenterEmptyLabel();
    }

    // 统一设置全部列的自动列宽模式：填充期间置 None 抑制逐行重算，填充完成后恢复 AllCells 触发一次重算
    private void SetColumnAutoSize(DataGridViewAutoSizeColumnMode mode)
    {
        for (var i = 0; i < _grid.Columns.Count; i++)
        {
            _grid.Columns[i].AutoSizeMode = mode;
        }
    }

    // 空数据提示浮层在表格区域居中（窗口缩放时重算）
    private void CenterEmptyLabel()
    {
        _emptyLabel.Location = new Point(
            Math.Max(0, (_gridHost.ClientSize.Width - _emptyLabel.Width) / 2),
            Math.Max(0, (_gridHost.ClientSize.Height - _emptyLabel.Height) / 2));
    }

    // 刷新：UI 线程快照筛选条件 → 取消旧请求并新建 → 拉取 → 结果过期则丢弃 → 更新计数与状态
    private async void OnRefreshClick(object? sender, EventArgs e)
    {
        // 只取消上一次在途请求，不在此时 Dispose：该请求内部的链接令牌仍挂在旧 CTS 上，
        // 立即释放会与链接令牌的生命周期产生竞态。旧 CTS 改由它自己的 finally 释放。
        var previous = _refreshCts;
        var cts = new CancellationTokenSource();
        _refreshCts = cts;
        previous?.Cancel();

        SetBusy(true);
        _loadStopwatch.Restart();
        _statusLabel.ForeColor = Theme.Muted;
        _statusLabel.Text = "正在同步数据...";

        try
        {
            // 固定全量口径：仓库=全部、对比冻结与效期均开启；细粒度筛选改由工具栏内存投影完成
            var rows = await _client.FetchStockDiffAsync(
                Converters.WarehouseCodeFromLabel(Converters.LabelAll),
                compareHold: true,
                compareExpiry: true,
                cts.Token);
            if (cts.IsCancellationRequested)
            {
                return;
            }

            _loadStopwatch.Stop();
            Rows = rows;
            ApplyViewFilter();
            // 固定区域性：避免自定义格式串受当前区域性日历影响（非公历系统下时间显示异常）
            _updatedLabel.Text = $"更新: {DateTime.Now.ToString(TimeFormat, CultureInfo.InvariantCulture)}";
            _elapsedLabel.Text = $"耗时: {_loadStopwatch.ElapsedMilliseconds} ms";
            _statusLabel.Text = rows.Count > 0 ? "查询完成" : NoDataText;
        }
        catch (OperationCanceledException)
        {
            // 被新请求或视图销毁取消，属预期流程，不提示用户
        }
        catch (UnauthorizedException ex)
        {
            // 登录失效（F8 接线）：取消在途请求、清令牌、弹提示并跳回登录页
            var outcome = LogoutDecider.FromException(ex);
            _statusLabel.ForeColor = Theme.Error;
            _statusLabel.Text = outcome.Message;
            PerformLogout(outcome.Message);
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[数据查询] 失败: {ex}");
            _loadStopwatch.Stop();
            _statusLabel.ForeColor = Theme.Error;
            _statusLabel.Text = "数据获取失败";
            UpdateExportEnabled();
            ShowOverlay(_viewRows.Count == 0 ? "数据获取失败" : null);
            MessageBox.Show(this, ex.Message, "数据获取失败", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        finally
        {
            // 仅当自己仍是最新请求时才复位 UI，并断开引用，保证 _refreshCts 永不指向已释放对象
            if (ReferenceEquals(_refreshCts, cts))
            {
                _refreshCts = null;
                SetBusy(false);
            }

            cts.Dispose();
        }
    }

    // 逐格格式化：差异分级底色、悬停高亮、成对不一致单元格、差异数量与异常种类强调（逻辑收口在 GridStyler）
    private void OnCellFormatting(object? sender, DataGridViewCellFormattingEventArgs e) =>
        _grid.FormatCell(e, _hoverRow);

    // 选中行左侧主题色竖条（非绑定表格无 DataSource，RowPostPaint 可用）
    private void OnRowPostPaint(object? sender, DataGridViewRowPostPaintEventArgs e) =>
        _grid.PaintRowChrome(e);

    // 悬停进入：记录行号并只重绘变化的两行，避免整表重绘
    private void OnCellMouseEnter(object? sender, DataGridViewCellEventArgs e) => SetHoverRow(e.RowIndex);

    // 悬停离开：清空悬停行并重绘上一行
    private void OnCellMouseLeave(object? sender, DataGridViewCellEventArgs e) => SetHoverRow(-1);

    // 更新悬停行：相同则跳过，否则重绘旧行与新行
    private void SetHoverRow(int rowIndex)
    {
        if (_hoverRow == rowIndex)
        {
            return;
        }

        var previous = _hoverRow;
        _hoverRow = rowIndex;
        InvalidateRow(previous);
        InvalidateRow(rowIndex);
    }

    // 安全重绘指定行：越界或视图/表格已释放时跳过
    private void InvalidateRow(int rowIndex)
    {
        if (IsDisposed || _grid.IsDisposed || rowIndex < 0 || rowIndex >= _grid.Rows.Count)
        {
            return;
        }

        _grid.InvalidateRow(rowIndex);
    }

    // 筛选变更：仓库类型 / 异常类型，仅做内存投影，不触发接口请求
    private void OnFilterChanged(object? sender, EventArgs e)
    {
        if (IsDisposed)
        {
            return;
        }

        ApplyViewFilter();
    }

    // 由 Rows 按筛选条件（仓库类型 / 异常类型）投影视图，并刷新表格、统计摘要、状态栏与导出口径；纯内存，不触发接口请求
    private void ApplyViewFilter()
    {
        var kind = SelectedDiffKind();
        var warehouseCode = SelectedWarehouseCode();
        var view = new List<StockDiffRow>(Rows.Count);
        foreach (var row in Rows)
        {
            if (kind is not null && DiffClassifier.Classify(row) != kind)
            {
                continue;
            }

            if (warehouseCode is not null
                && !string.Equals(row.WarehouseType, warehouseCode, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            view.Add(row);
        }

        _viewRows = view;
        PopulateGrid(view);
        UpdateStats(view);
        UpdateStatusFilter();
        _countLabel.Text = $"{view.Count} 行 / 共 {Rows.Count} 行";
        UpdateExportEnabled();
    }

    // 当前选中的异常类型分类；首项「全部类型」返回 null
    private DiffKind? SelectedDiffKind()
    {
        var index = _diffTypeFilter.SelectedIndex;
        return index >= 0 && index < DiffTypeKinds.Length ? DiffTypeKinds[index] : null;
    }

    // 当前选中的仓库代码；首项「全部仓库」返回 null
    private string? SelectedWarehouseCode()
    {
        var index = _warehouseFilter.SelectedIndex;
        return index >= 0 && index < WarehouseCodes.Length ? WarehouseCodes[index] : null;
    }

    // 统计摘要：差异物料数 = 当前视图总条数（列表本身即差异数据）
    private void UpdateStats(IReadOnlyList<StockDiffRow> view)
    {
        _statDiffValue.Text = view.Count.ToString(CultureInfo.InvariantCulture);
    }

    // 状态栏筛选条件文字：汇总仓库类型 / 异常类型（对比项与仓库已固定为全量）
    private void UpdateStatusFilter()
    {
        var kind = _diffTypeFilter.SelectedItem?.ToString() ?? DiffTypeLabels[0];
        var warehouse = _warehouseFilter.SelectedItem?.ToString() ?? WarehouseLabels[0];
        _filterLabel.Text = $"筛选 仓库: {warehouse} | 异常: {kind}";
    }

    // 单元格点击复制：表头点击（RowIndex/ColumnIndex 为负）忽略；空值不写剪贴板；失败仅提示并记日志
    private void OnCellClick(object? sender, DataGridViewCellEventArgs e)
    {
        if (e.RowIndex < 0 || e.ColumnIndex < 0)
        {
            return;
        }

        var value = _grid.Rows[e.RowIndex].Cells[e.ColumnIndex].Value?.ToString();
        var result = _cellCopy.Copy(value);

        switch (result.Status)
        {
            case CopyStatus.Copied:
                _statusLabel.ForeColor = Theme.Success;
                _statusLabel.Text = result.Message;
                break;
            case CopyStatus.Failed:
                Trace.WriteLine($"[单元格复制] 失败: {result.Message}");
                _statusLabel.ForeColor = Theme.Error;
                _statusLabel.Text = result.Message;
                break;
            // CopyStatus.Empty：空单元格不复制，保持原状态提示不变
        }
    }

    // 导出 CSV：无数据直接提示；否则弹保存对话框 → 建文件 → 由 Core 纯逻辑写流（UTF-8 BOM）；
    // 成功/失败均更新状态栏并记日志，异常在此统一收口，不向外抛出。
    private async void OnExportClick(object? sender, EventArgs e)
    {
        if (Rows.Count == 0)
        {
            MessageBox.Show(this, "暂无数据可导出", "导出 CSV", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var target = PickExportTarget();
        if (target is null)
        {
            return;
        }

        var rows = Rows;

        try
        {
            await WriteCsvFileAsync(target, rows);

            if (IsDisposed)
            {
                return;
            }

            Trace.WriteLine($"[CSV 导出] 成功: {target} ({rows.Count} 条)");
            _statusLabel.ForeColor = Theme.Success;
            _statusLabel.Text = $"已导出: {Path.GetFileName(target)}";
            MessageBox.Show(this, "CSV 已保存", "导出 CSV", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            // 详细异常仅记日志；对用户只给固定文案，避免泄露本地路径等实现细节
            Trace.WriteLine($"[CSV 导出] 失败: {ex}");
            if (IsDisposed)
            {
                return;
            }

            _statusLabel.ForeColor = Theme.Error;
            _statusLabel.Text = "导出失败";
            MessageBox.Show(this, "导出失败，请检查目标文件是否被占用或磁盘空间是否充足。",
                "导出失败", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    // 弹出保存对话框并返回用户选择的目标路径；用户取消时返回 null
    private string? PickExportTarget()
    {
        using var dialog = new SaveFileDialog
        {
            Filter = "CSV 文件|*.csv",
            DefaultExt = "csv",
            FileName = CsvExporter.BuildDefaultFileName(Converters.LabelAll)
        };
        return dialog.ShowDialog(this) == DialogResult.OK ? dialog.FileName : null;
    }

    // 后台线程写盘：先写临时文件再原子替换（写失败既不残留半截文件，也不破坏已存在的同名文件）；
    // 导出为本地 IO，移到后台线程避免大数据量时阻塞界面；异常原样抛出，由调用方统一提示
    private static Task WriteCsvFileAsync(string target, IReadOnlyList<StockDiffRow> rows) => Task.Run(() =>
    {
        var temp = target + AppConfig.TempFileSuffix;
        try
        {
            using (var file = File.Create(temp))
            {
                CsvExporter.Write(file, rows);
            }

            File.Move(temp, target, overwrite: true);
        }
        catch
        {
            if (File.Exists(temp))
            {
                File.Delete(temp);
            }

            throw;
        }
    });

    // 「设置」（F8）：打开接口地址对话框；保存成功则地址已变更且令牌已清空，须退出重新登录
    private void OnSettingsClick(object? sender, EventArgs e)
    {
        using var dialog = new SettingsForm(_client, _store);
        dialog.ShowDialog(this);

        if (dialog.Saved)
        {
            PerformLogout(LogoutDecider.FromSettingsSaved().Message);
        }
    }

    // 「退出登录」（F8）：清令牌并切回登录页
    private void OnLogoutClick(object? sender, EventArgs e) =>
        PerformLogout(LogoutDecider.FromManualLogout().Message);

    // 退出登录（F8）：取消在途请求 → 清空令牌 → 弹提示 → 跳回登录页
    // 在途请求的 finally 会自行释放其 CTS，此处只取消并断开引用，避免释放正在使用的令牌源；
    // 跳转后当前视图被外壳 Dispose，调用方（如刷新 catch 的 finally）须以 ReferenceEquals 守卫避免触碰已释放控件
    private void PerformLogout(string message)
    {
        if (IsDisposed)
        {
            return;
        }

        _refreshCts?.Cancel();
        _refreshCts = null;
        _client.ClearToken();
        MessageBox.Show(this, message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);

        // 弹窗期间视图可能被外部销毁（理论上不会，保留守卫以防意外），销毁则不再切视图
        if (IsDisposed)
        {
            return;
        }

        _mainForm.ShowLogin();
    }

    // 导出可用性统一由最近一次拉取结果决定：有数据才可导出（刷新成功/失败两处共用）
    private void UpdateExportEnabled()
    {
        if (!IsDisposed)
        {
            _exportButton.Enabled = Rows.Count > 0;
        }
    }

    // 请求期间禁用交互并显示等待光标；刷新按钮与筛选项禁用，导出按数据可用性联动
    private void SetBusy(bool busy)
    {
        UiHelper.SetBusy(this, busy, this, _refreshButton);
        if (IsDisposed)
        {
            return;
        }

        _diffTypeFilter.Enabled = !busy;
        _warehouseFilter.Enabled = !busy;
        if (busy)
        {
            _exportButton.Enabled = false;
            ShowOverlay(LoadingText);
        }
        else
        {
            UpdateExportEnabled();
        }
    }

    // 视图销毁时取消在途请求，避免回调触碰已释放控件
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            // 只取消，不释放：在途请求的 finally 会释放自己的 CTS，避免释放正在使用的令牌源
            _refreshCts?.Cancel();
            _refreshCts = null;
        }

        base.Dispose(disposing);
    }
}