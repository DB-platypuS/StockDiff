// 创建者: PlatyPus
// 创建时间: 2026-09-22
// 作用: 主面板视图（F4 数据查询与刷新），提供仓库/对比项筛选与刷新按钮，异步拉取库存差异，
//       处理取消、登录失效与网络异常；表格展示、复制与导出由 F5-F7 在此基础上接入。

using System.Diagnostics;
using StockDiff.Core.Api;
using StockDiff.Core.Config;
using StockDiff.Core.Convert;
using StockDiff.Core.Models;

namespace StockDiff.App.Views;

public sealed class DashboardView : UserControl
{
    private readonly ApiClient _client;

    // 仓库筛选标签：UI 与 Core 转换器共用同一份中文口径，避免字面量在两处重复
    private const string AllLabel = "全部";
    private const string FcLabel = "方仓";
    private const string AsrsLabel = "立库";

    // 左栏与顶栏布局常量：统一左间距/行距，避免坐标魔数散落
    private const int PadX = 16;
    private const int TitleTop = 20;
    private const int CaptionTop = 64;
    private const int WarehouseFirstTop = 90;
    private const int OptionsCaptionTop = 186;
    private const int OptionsFirstTop = 212;
    private const int RowGap = 28;
    private const int HeaderButtonTop = 12;
    private const int AddressWrapWidth = 220;

    private readonly RadioButton _allRadio = new() { Text = AllLabel, Checked = true, AutoSize = true };
    private readonly RadioButton _fcRadio = new() { Text = FcLabel, AutoSize = true };
    private readonly RadioButton _asrsRadio = new() { Text = AsrsLabel, AutoSize = true };
    private readonly CheckBox _holdCheck = new() { Text = "对比冻结状态", AutoSize = true };
    private readonly CheckBox _expiryCheck = new() { Text = "对比过期日期", AutoSize = true };
    private readonly Button _refreshButton = new() { Text = "刷新数据" };
    private readonly Label _statusLabel = new() { Text = "等待刷新", AutoSize = true, ForeColor = Theme.Muted };
    private readonly Label _countLabel = new() { Text = "0 条记录", AutoSize = true, ForeColor = Theme.Muted };

    // 每次刷新前取消上一次在途请求，保证仅最新请求的结果被采用
    private CancellationTokenSource? _refreshCts;

    // 最近一次成功拉取的记录，供后续表格展示与导出模块复用
    internal IReadOnlyList<StockDiffRow> Rows { get; private set; } = Array.Empty<StockDiffRow>();

    // 构建主面板：左右分栏（筛选区 + 数据区），并接入刷新按钮
    public DashboardView(ApiClient client, string username)
    {
        _client = client;
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

    // 左栏：用户名、仓库单选、对比选项复选；底部显示接口地址与版本号
    private void BuildFilterPanel(Control panel, string username)
    {
        panel.Controls.Add(new Label
        {
            Text = username,
            Font = Theme.DialogTitleFont,
            ForeColor = Theme.Ink,
            AutoSize = true,
            Location = new Point(PadX, TitleTop)
        });
        panel.Controls.Add(Caption("仓库筛选", CaptionTop));
        _allRadio.Location = new Point(PadX, WarehouseFirstTop);
        _fcRadio.Location = new Point(PadX, WarehouseFirstTop + RowGap);
        _asrsRadio.Location = new Point(PadX, WarehouseFirstTop + RowGap * 2);
        panel.Controls.Add(Caption("对比选项", OptionsCaptionTop));
        _holdCheck.Location = new Point(PadX, OptionsFirstTop);
        _expiryCheck.Location = new Point(PadX, OptionsFirstTop + RowGap);
        panel.Controls.AddRange(new Control[] { _allRadio, _fcRadio, _asrsRadio, _holdCheck, _expiryCheck });

        var footer = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            FlowDirection = FlowDirection.TopDown,
            AutoSize = true,
            Padding = new Padding(PadX, 0, PadX, PadX),
            BackColor = Color.Transparent
        };
        footer.Controls.Add(new Label
        {
            Text = _client.BaseUrl,
            Font = Theme.AddressFont,
            ForeColor = Theme.Muted,
            AutoSize = true,
            MaximumSize = new Size(AddressWrapWidth, 0)
        });
        footer.Controls.Add(new Label
        {
            Text = $"v{AppConfig.Version}",
            Font = Theme.AddressFont,
            ForeColor = Theme.Muted,
            AutoSize = true
        });
        panel.Controls.Add(footer);
    }

    // 分组小标题：左栏统一左间距
    private static Label Caption(string text, int y) => new()
    {
        Text = text,
        Font = Theme.BodyFont,
        ForeColor = Theme.Muted,
        AutoSize = true,
        Location = new Point(PadX, y)
    };

    // 右栏：标题、刷新按钮、状态与计数；数据表格由 F5 接入
    private void BuildDataPanel(Control panel)
    {
        var header = new Panel { Dock = DockStyle.Top, Height = 56, BackColor = Color.White };
        header.Controls.Add(new Label
        {
            Text = "差异明细",
            Font = Theme.PageTitleFont,
            ForeColor = Theme.Ink,
            AutoSize = true,
            Location = new Point(PadX, HeaderButtonTop)
        });
        Theme.StylePrimary(_refreshButton, Theme.BodyFont);
        _refreshButton.Size = new Size(96, 32);
        header.Controls.Add(_refreshButton);
        void PlaceButton() => _refreshButton.Location =
            new Point(Math.Max(PadX, header.Width - _refreshButton.Width - PadX), HeaderButtonTop);
        header.Resize += (_, _) => PlaceButton();
        PlaceButton();

        var status = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 34,
            Padding = new Padding(PadX, 8, 0, 0),
            BackColor = Color.White
        };
        _countLabel.Margin = new Padding(24, 0, 0, 0);
        status.Controls.Add(_statusLabel);
        status.Controls.Add(_countLabel);

        panel.Controls.Add(header);
        panel.Controls.Add(status);
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
        _statusLabel.ForeColor = Theme.Muted;
        _statusLabel.Text = "正在同步数据...";

        try
        {
            var rows = await _client.FetchStockDiffAsync(
                CurrentWarehouse(), _holdCheck.Checked, _expiryCheck.Checked, cts.Token);
            if (cts.IsCancellationRequested)
            {
                return;
            }

            Rows = rows;
            _countLabel.Text = $"{rows.Count} 条记录";
            _statusLabel.Text = rows.Count > 0 ? $"更新时间: {DateTime.Now:HH:mm:ss}" : "无差异数据";
        }
        catch (OperationCanceledException)
        {
            // 被新请求或视图销毁取消，属预期流程，不提示用户
        }
        catch (UnauthorizedException ex)
        {
            // 登录失效：F4 仅提示，跳回登录页由 F8 会话模块接线
            _statusLabel.ForeColor = Theme.Error;
            _statusLabel.Text = ex.Message;
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[数据查询] 失败: {ex}");
            _statusLabel.ForeColor = Theme.Error;
            _statusLabel.Text = "数据获取失败";
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

    // 读取仓库单选的接口代码：由 Core 转换器唯一定义代码口径，避免 UI 重复硬编码 fc/asrs/all
    private string CurrentWarehouse()
    {
        if (_fcRadio.Checked)
        {
            return Converters.WarehouseCodeFromLabel(FcLabel);
        }

        return _asrsRadio.Checked
            ? Converters.WarehouseCodeFromLabel(AsrsLabel)
            : Converters.WarehouseCodeFromLabel(AllLabel);
    }

    // 请求期间禁用刷新与筛选项并显示等待光标，避免重复触发
    private void SetBusy(bool busy)
    {
        if (IsDisposed)
        {
            return;
        }

        _refreshButton.Enabled = !busy;
        _allRadio.Enabled = !busy;
        _fcRadio.Enabled = !busy;
        _asrsRadio.Enabled = !busy;
        _holdCheck.Enabled = !busy;
        _expiryCheck.Enabled = !busy;
        UseWaitCursor = busy;
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