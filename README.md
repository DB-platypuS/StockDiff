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

## 接口与配置

- 默认接口地址 `http://127.0.0.1:7880` ，路径前缀 `/api/v1`
- 接口地址可在程序内「API 设置」中修改，持久化到 `%AppData%\kc-stock-diff\settings.json`
- 接口契约、字段对照与架构决策记录见 [设计文档.md](./设计文档.md)

## 开发进度

模块清单、依赖关系与逐项完成状态见 [模块划分.md](./模块划分.md)。

## 更新日志

### v1.0.1

- **登录页**：密码框新增「显示 / 隐藏」切换按钮。
- **主面板**：修复窗口缩小时左栏「设置 / 退出登录」按钮被裁剪消失的问题（页脚按可用宽度换行）。
- **网络层**：响应开启自动解压（gzip / deflate / br），并改用流式反序列化读取响应。
- **文档**：修正设计文档 12.3 节的原表字段映射（物料编码 / 储位 / 数量等）。

## 代码约定

- 每个新建文件保留 `创建者 / 创建时间 / 作用` 三要素注释头（`.cs` 用 `//`、工程文件用 `<!-- -->`、`.ps1` 用 `#`）
- 不提交任何真实连接串、密码或内网地址

