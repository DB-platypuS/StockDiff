# 库存差异比对系统（StockDiff）

比对 WMS 与「方仓 / 立库」库存差异的 Windows 桌面工具。由原 Go + Fyne 版本（`kc` v1.2.2）迁移而来，功能对齐原版。

## 技术栈

| 关注点  | 选型                                                         |
| ---- | ---------------------------------------------------------- |
| 运行时  | .NET 10（`net10.0-windows`）                                 |
| UI   | Windows Forms                                              |
| JSON | `System.Text.Json`（数量字段用 `JsonElement` 保留大数精度，禁用 `double`） |
| 测试   | xUnit + coverlet                                           |
| 数据来源 | 后端 HTTP 接口（不直连数据库）                                         |

## 环境要求

- Windows 10 / 11
- .NET SDK 10.0（`dotnet --version` 应为 `10.x`）
- 可访问的后端 HTTP 接口

## 目录结构

```
StockDiff/
├── src/
│   ├── Core/                零 UI 依赖的类库：Config / Models / Api / Convert / Table / Export
│   └── App/                 WinForms 可执行：Program / MainForm / Views / Dialogs / Settings
├── tests/
│   └── Core.Tests/          xUnit 测试，仅引用 Core
├── scripts/                 dev.ps1 / test.ps1 / format.ps1
└── Directory.Build.props    全解决方案统一编译属性
```

**分层约束**：`Core` 不引用 `System.Windows.Forms`，`App` 单向引用 `Core`，`Core.Tests` 只引用 `Core`。

## 常用命令

```powershell
.\scripts\dev.ps1            # 还原 + 编译 + 运行主程序
.\scripts\test.ps1           # 运行全部单元测试
.\scripts\format.ps1         # 按 .editorconfig 格式化并修复
.\scripts\format.ps1 -Check  # 仅校验不修改（供 CI 使用）

```

## 部署与分发

面向使用同事（无需安装 .NET 运行时、无需 IDE）：在构建机执行一次发布脚本，
产出免运行时依赖的 Windows x64 单文件程序，压缩包直接发给同事解压双击即用。

```powershell
.\scripts\publish.ps1        # 产出 publish\StockDiff-win-x64\StockDiff.exe 与 publish\StockDiff-win-x64.zip
```

- **分发内容**：`publish\StockDiff-win-x64.zip`（内含单个 `StockDiff.exe`，约 60~150MB）
- **同事侧要求**：Windows 10 / 11 x64，能访问后端接口即可
- **无需管理员权限**：配置与日志写入 `%AppData%\kc-stock-diff\`
- **升级**：替换 exe 即可，用户已保存的接口地址不受影响

### 默认接口地址的内网注入

源码内置默认值 `http://127.0.0.1:7880` 不代表真实环境，也不应将内网地址提交入库。
发布方在内网构建时，于本机新建 **未纳入版本库** 的 `Directory.Build.local.props`：

```xml
<Project>
  <PropertyGroup>
    <StockDiffDefaultBaseUrl>http://内网地址:端口</StockDiffDefaultBaseUrl>
  </PropertyGroup>
</Project>
```

该文件已被 `.gitignore` 忽略，其值在编译期以 `AssemblyMetadata` 注入，不会出现在源码里。
启动时的接口地址优先级为：

1. `%AppData%\kc-stock-diff\settings.json`（用户在「API 设置」里的显式设置）
2. 环境变量 `KC_STOCKDIFF_BASE_URL`
3. 编译期注入的本地默认地址（`Directory.Build.local.props`）
4. 源码内置默认值 `http://127.0.0.1:7880`

未提供第 3 项时程序行为与改动前完全一致（他人克隆源码、CI 与单元测试均不受影响）。

## 接口与配置

- 默认接口地址 `http://127.0.0.1:7880` ，路径前缀 `/api/v1`
- 接口地址可在程序内「API 设置」中修改，持久化到 `%AppData%\kc-stock-diff\settings.json`
- 接口契约、字段对照与架构决策记录见 [设计文档.md](./设计文档.md)

## 开发进度

模块清单、依赖关系与逐项完成状态见 [模块划分.md](./模块划分.md)。

## 更新日志

### v1.1.1

- **差异归类修正**：只要「仓库数量」与「WMS数量」不一致，一律归为「数量差异」；修复后端返回「仅WMS存在 / 仅立库存在」等非标准文本时该行被归入「其他异常」、导致「数量差异」筛选漏掉的问题。
- **异常种类列口径统一**：该列由展示后端原始文本改为展示归一化分类（数量差异 / 储位差异 / 冻结差异 / 效期差异 / 其他异常），与「异常类型」筛选口径一致，CSV 导出同步。

### v1.1.0

- **差异视觉强化**：按异常种类分级着色（数量差异浅红、储位/冻结浅橙、效期浅黄），正常行白/斑马纹；两来源不一致的成对单元格高亮；「差异数量」用等宽粗体红字强调。
- **表格交互**：整行悬停浅蓝高亮、选中行左侧主题色竖条、表头自动换行自适应、数字列右对齐。
- **统计摘要**：表格上方新增「差异物料数」卡片与异常种类图例。
- **工具栏与状态栏**：右侧新增「仓库类型 / 异常类型」筛选（内存筛选，切换不重新请求）；底栏显示筛选条件、行数、加载耗时与更新时间。
- **布局重构**：左栏移除仓库筛选与对比项（改为默认全量加载），刷新 / 导出按钮移至左栏并铺满宽度；右栏改用 TableLayoutPanel 分栏，修复顶部区块被表格遮挡的问题。
- **状态与反馈**：加载中显示「正在加载…」浮层并置灰控件；无差异时提示「未发现差异，数据一致」。

### v1.0.1

- **登录页**：密码框新增「显示 / 隐藏」切换按钮。
- **主面板**：修复窗口缩小时左栏「设置 / 退出登录」按钮被裁剪消失的问题（页脚按可用宽度换行）。
- **网络层**：响应开启自动解压（gzip / deflate / br），并改用流式反序列化读取响应。
- **文档**：修正设计文档 12.3 节的原表字段映射（物料编码 / 储位 / 数量等）。

## 代码约定

- 每个新建文件保留 `创建者 / 创建时间 / 作用` 三要素注释头（`.cs` 用 `//`、工程文件用 `<!-- -->`、`.ps1` 用 `#`）
- 不提交任何真实连接串、密码或内网地址

