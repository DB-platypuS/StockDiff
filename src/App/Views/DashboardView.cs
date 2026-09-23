// 创建者: PlatyPus
// 创建时间: 2026-09-22
// 作用: 主面板视图（F4 数据查询与刷新 + F5 数据表格展示 + F7 CSV 导出），提供仓库/对比项筛选与刷新按钮，
//       异步拉取库存差异并渲染 12 列表格；处理取消、登录失效与网络异常；单元格复制（F6）与导出 CSV（F7）已接入。

using System.Diagnostics;
using StockDiff.App.Services;
using StockDiff.Core.Api;
using StockDiff.Core.Config;
using StockDiff.Core.Convert;
using StockDiff.Core.Copy;
using StockDiff.Core.Export;
using StockDiff.Core.Models;
using StockDiff.Core.Table;

namespace StockDiff.App.Views;

public sealed class DashboardView : UserControl
{
    private readonly ApiClient _client;

    // 仓库筛选标签：直接引用 Core 转换器的中文口径常量，避免同一字面量在 UI 与 Core 两处重复
    private const string AllLabel = Converters.LabelAll;
    private const string FcLabel = Converters.LabelFc;
    private const string AsrsLabel = Converters.LabelAsrs;

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
    private readonly Button _exportButton = new() { Text = "导出 CSV", Enabled = false };
    private readonly Label _statusLabel = new() { Text = "等待刷新", AutoSize = true, ForeColor = Theme.Muted };
    private readonly Label _countLabel = new() { Text = "0 条记录", AutoSize = true, ForeColor = Theme.Muted };

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

    // F6 单元格复制：复制规则收口在 Core，视图只负责展示结果文案
    private readonly CellCopyService _cellCopy;

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
        _exportButton.Click += OnExportClick;
        _grid.CellClick += OnCellClick;
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

    // 右栏：标题、导出/刷新按钮、状态与计数、数据表格
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
        Theme.StyleSecondary(_exportButton, Theme.BodyFont);
        _exportButton.Size = new Size(96, 32);
        header.Controls.Add(_refreshButton);
        header.Controls.Add(_exportButton);
        void PlaceButton()
        {
            _refreshButton.Location =
                new Point(Math.Max(PadX, header.Width - _refreshButton.Width - PadX), HeaderButtonTop);
            _exportButton.Location =
                new Point(Math.Max(PadX, _refreshButton.Left - _exportButton.Width - 12), HeaderButtonTop);
        }
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

        BuildGrid();
        _gridHost.Controls.Add(_grid);
        _gridHost.Controls.Add(_emptyLabel);
        // 后加入的控件处于 z 序底部，会被 Dock=Fill 的表格完全遮住，需提到最前才可见
        _emptyLabel.BringToFront();
        _gridHost.Resize += (_, _) => CenterEmptyLabel();

        // 填充区先加入，Top/Bottom 后加入，确保表格占据中部剩余空间
        panel.Controls.Add(_gridHost);
        panel.Controls.Add(header);
        panel.Controls.Add(status);
        CenterEmptyLabel();
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
        _grid.SelectionMode = DataGridViewSelectionMode.CellSelect;
        _grid.MultiSelect = false;
        _grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;
        _grid.AllowUserToResizeColumns = true;
        _grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
        _grid.BackgroundColor = Color.White;
        _grid.BorderStyle = BorderStyle.None;
        _grid.EnableHeadersVisualStyles = false;
        _grid.ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
        {
            Alignment = DataGridViewContentAlignment.MiddleCenter,
            WrapMode = DataGridViewTriState.True,
            BackColor = Theme.SecondaryHover,
            ForeColor = Theme.Ink
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
        SetColumnAutoSize(DataGridViewAutoSizeColumnMode.None);
        _grid.SuspendLayout();
        try
        {
            _grid.Rows.Clear();
            foreach (var cells in model.Rows)
            {
                _grid.Rows.Add((object[])cells);
            }
        }
        finally
        {
            _grid.ResumeLayout(false);
            SetColumnAutoSize(DataGridViewAutoSizeColumnMode.AllCells);
        }

        _emptyLabel.Text = "无差异数据";
        _emptyLabel.Visible = model.IsEmpty;
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
            PopulateGrid(rows);
            UpdateExportEnabled();
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
            UpdateExportEnabled();
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

        using var dialog = new SaveFileDialog
        {
            Filter = "CSV 文件|*.csv",
            DefaultExt = "csv",
            FileName = CsvExporter.BuildDefaultFileName(CurrentWarehouseLabel())
        };
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        var target = dialog.FileName;
        var rows = Rows;

        try
        {
            // 先写临时文件再原子替换：写失败既不残留半截文件，也不破坏已存在的同名文件；
            // 导出为本地 IO，移到后台线程避免大数据量时阻塞界面
            await Task.Run(() =>
            {
                var temp = target + ".tmp";
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

    // 当前仓库筛选的中文标签，供导出默认文件名复用（不改变请求代码口径）
    private string CurrentWarehouseLabel() =>
        _fcRadio.Checked ? FcLabel : _asrsRadio.Checked ? AsrsLabel : AllLabel;

    // 导出可用性统一由最近一次拉取结果决定：有数据才可导出（刷新成功/失败两处共用）
    private void UpdateExportEnabled() => _exportButton.Enabled = Rows.Count > 0;

    // 读取仓库单选的接口代码：由标签经 Core 转换器派生代码口径，与默认文件名共用同一判断
    private string CurrentWarehouse() => Converters.WarehouseCodeFromLabel(CurrentWarehouseLabel());

    // 请求期间禁用刷新与筛选项并显示等待光标，避免重复触发（实现收口在 UiHelper）
    private void SetBusy(bool busy) =>
        UiHelper.SetBusy(this, busy, this, _refreshButton, _allRadio, _fcRadio, _asrsRadio, _holdCheck, _expiryCheck);

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