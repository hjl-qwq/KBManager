# KBManager

> 面向**本地 Markdown 知识库**的标签管理与 Git 同步工具。
> 用标签而不是目录来组织和反查笔记，并在同一个界面里完成「扫描 → 打标签 → 搜索 → 提交 → 推送」的完整流程。

[![.NET Build Check](https://github.com/hjl-qwq/KBManager/actions/workflows/dotnet-build-check.yml/badge.svg)](https://github.com/hjl-qwq/KBManager/actions/workflows/dotnet-build-check.yml)

---

## 项目背景

长期以来，我的笔记以在线文档平台（语雀）为主，但平台侧提供的标签能力无法满足「一个文件挂多个标签、再按任意标签快速反查」的用法；同时我希望笔记数据以 Markdown 文件的形式完全自持，可以被 Git 版本管理、被任意编辑器打开、随时迁移。

于是有了 KBManager：它不试图重建一个笔记平台，而是给**任意一个存放 Markdown 的目录**补上缺失的那一层——文件级标签索引，以及围绕这个仓库的同步操作。

设计上遵循三条原则：

- **文件系统是唯一真实来源**：Markdown 文件本身不写入任何元数据，标签只是旁路索引。用其他编辑器、其他笔记软件打开这个目录，内容依然完全可读可用；索引损坏或丢失时，重新扫描即可重建。
- **索引随仓库一起走**：SQLite 索引数据库存放在仓库内部的 `.kbdatabase/` 下，跟着仓库一起 clone、迁移、备份，不需要额外的服务端。
- **核心逻辑与界面解耦**：所有业务逻辑都在 `KBManager.core` 中，不依赖任何 UI；桌面 GUI 与命令行 CLI 只是共享同一套核心的两层外壳。

## 核心特性

- 🏷️ **文件级多标签**：单个文件可挂任意数量标签，标签以芯片（chip）形式展示，支持一键移除
- ✨ **标签自动补全**：输入即提示，候选按「被多少文件使用」降序排列，直接双击即可完成打标签
- 🌲 **树形文件浏览**：按目录层级展示已索引的 Markdown 文件，并实时显示每个文件的标签数量
- 🔍 **按标签检索**：标签名模糊匹配 + 补全下拉（带使用热度），结果为「文件名 + 标签」表格
- 🔄 **一键同步索引**：递归扫描仓库中的 Markdown 文件，增量写入索引数据库，首次自动建库
- 🧹 **自动清理孤儿标签**：移除标签或删除文件记录后，不再被任何文件使用的标签会自动回收
- 📦 **Git 集成**：Clone（含子模块初始化）、主仓库与子模块**分离**的 Add / Commit、SSH Push（先推子模块再推主仓库）
- 🎨 **可换肤**：`theme.json` 提供 37 个语义化颜色标记，改颜色重启即可生效
- 🖥️ **双前端**：Avalonia 桌面 GUI + 交互式命令行 CLI，共享同一套核心服务
- 🧪 **xUnit 测试**：覆盖子模块感知的 Git 行为、扫描排除规则、配置校验等核心逻辑

## 界面与功能

GUI 采用左侧导航 + 主内容区 + 底部状态栏的布局：

```
┌──────────────────────────────────────────────────────────┐
│  📚 KBManager — Knowledge Base Manager                   │
├───────────┬──────────────────────────────────────────────┤
│  🔍 搜索   │                                              │
│  📁 文件   │              主内容区域                       │
│  ⚙ 设置   │                                              │
│  📦 Git   │                                              │
├───────────┴──────────────────────────────────────────────┤
│  仓库: D:\MyKB    │  📄 文件: 128  │  🏷️ 标签: 45         │
└──────────────────────────────────────────────────────────┘
```

| 页面 | 功能 |
| --- | --- |
| 🔍 搜索 | 输入标签名查找文件，聚焦即加载全部标签建议（按文件数降序），点选建议直接搜索 |
| 📁 文件管理 | 树形浏览 Markdown 文件；右侧面板管理选中文件的标签；工具栏提供同步、打开编辑、移除记录 |
| ⚙ 设置 | 配置 Git 用户名 / 邮箱、HTTPS 与 SSH 远程地址、本地仓库目录（支持文件夹选择对话框） |
| 📦 Git 操作 | Clone、Main Add/Commit、Sub Add/Commit、SSH Push、查看当前配置；所有操作带时间戳日志输出 |

CLI 版本提供与 GUI 等价的菜单式操作（方向键选择 + 彩色输出）：

```
KBManager — Knowledge Base CLI
  🔍  Search Files by Tag
  🏷   Change Files & Tags
  ⚙   Settings
  📦  Repository Operations
  ✕   Exit
```

更详细的界面说明见 [`docs/GUI使用说明.md`](docs/GUI使用说明.md)。

## 工作原理

**文件扫描规则**（`FileScanService`）

- 递归遍历仓库目录，只收录 Markdown 相关扩展名：`.md` `.markdown` `.mdown` `.mkd` `.mkdn` `.mdwn`
- 排除 `.git`、`.kbdatabase` 目录以及隐藏文件
- 入库的是**相对仓库根的路径**（统一使用 `/` 分隔符），因此索引可以跨平台复用
- 同步为增量方式：已存在的记录跳过，只补齐新文件

**数据模型**（`KbDbContext`）

| 表 | 字段 | 说明 |
| --- | --- | --- |
| `Files` | `Id`, `FileName` | `FileName` 为相对路径，唯一索引 |
| `Tags` | `Id`, `TagName` | 唯一索引 |
| `FileTagRelations` | `FileId`, `TagId` | 多对多关系表 |

**Git 操作**（`GitHelper`，基于 LibGit2Sharp，不依赖系统 git 命令）

- **Clone**：优先尝试 SSH 地址，失败后回落 HTTPS；先克隆到临时目录，完成子模块初始化后再拷贝到目标目录
- **主仓库 Add**：遍历变更并逐个暂存，显式跳过子模块内部文件，只把子模块指针交给主仓库
- **子模块 Add / Commit**：对每个子模块单独执行暂存与提交，提交信息自动追加 `[submodule: <name>]` 标记
- **Push**：先推送所有子模块，再推送主仓库；自动探测 `~/.ssh/id_ed25519`，回落到 `~/.ssh/id_rsa`

## 项目结构

```
KBManager/
├── KBManager.core/                  # 领域层（无 UI 依赖）
│   ├── KnowledgeBaseService.cs      # 文件 / 标签的增删查改
│   ├── FileScanService.cs           # Markdown 扫描与批量入库
│   ├── KbDbContext.cs               # EF Core 实体与上下文
│   ├── GitHelper.cs                 # Clone / Add / Commit / Push 封装
│   ├── GitConfigModel.cs            # Git 配置模型与校验
│   ├── KBConfigStore.cs             # 跨平台 config.json 读写
│   └── ServiceResult.cs             # 统一返回值（成功 / 失败 + 消息 + 数据）
├── KBManager.GUI/                   # Avalonia 桌面前端（MVVM）
│   ├── ViewModels/                  # 搜索 / 文件 / 设置 / Git 四个页面 VM
│   ├── Views/                       # 对应的 XAML 视图与对话框
│   ├── Services/                    # 主题、文件打开、对话框
│   └── Config/theme.json            # 内嵌默认主题
├── KBManager.CLI/                   # 交互式命令行前端
├── KBManager.Tests/                 # xUnit 测试
├── docs/GUI使用说明.md               # GUI 使用手册
├── .github/workflows/               # GitHub Actions 构建检查
└── KBManager.slnx                   # 解决方案文件
```

## 技术栈

| 层次 | 技术 |
| --- | --- |
| 运行时 | .NET 8 / C# 12 |
| 桌面 UI | Avalonia 11.1.5（Fluent 主题）+ CommunityToolkit.Mvvm 8.4 |
| 数据存储 | SQLite + Entity Framework Core 6.0.26 |
| Git 操作 | LibGit2Sharp 0.31 + LibGit2Sharp.NativeBinaries |
| 序列化 | System.Text.Json |
| 测试 | xUnit 2.5.3 |
| 持续集成 | GitHub Actions（Ubuntu + .NET 8，编译检查） |

## 快速开始

### 环境要求

- [.NET SDK 8.0](https://dotnet.microsoft.com/download/dotnet/8.0) 或更高版本
- 操作系统：GUI 目前以 Windows 为主要验证平台；CLI 与核心库跨平台
- 一个存放 Markdown 笔记的目录（建议是 Git 仓库）
- 可选：SSH 密钥（仅 Clone / Push 使用 SSH 地址时需要）

### 获取与构建

```bash
git clone git@github.com:hjl-qwq/KBManager.git
cd KBManager

# 构建桌面版
dotnet build KBManager.GUI/KBManager.GUI.csproj -c Release

# 构建命令行版
dotnet build KBManager.CLI/KBManager.CLI.csproj -c Release
```

或者直接运行：

```bash
dotnet run --project KBManager.GUI
dotnet run --project KBManager.CLI
```

> 解决方案文件 `KBManager.slnx` 使用的是新的 SLNX 格式，需要在 .NET SDK 9.0.200+ 或 Visual Studio 2022 17.13+ 中打开；只构建单个项目时，.NET 8 SDK 即可。

### 首次使用

1. 启动 GUI，进入左侧 **⚙ 设置**，填写 Git 用户名、邮箱与本地仓库目录（远程地址可选），点击保存
2. 进入 **📁 文件管理**，点击 **🔄 同步文件**：首次会自动创建索引数据库，并扫描入库所有 Markdown 文件
3. 在左侧文件树中选中一个文件，在右侧输入标签并回车（或双击补全建议）完成打标签
4. 进入 **🔍 搜索**，输入标签名即可反查文件
5. 需要同步到远程时，进入 **📦 Git 操作**，依次执行 Add / Commit / Push

## 使用说明

- **GUI 完整手册**：[`docs/GUI使用说明.md`](docs/GUI使用说明.md)，包含界面布局、各页面操作细节、主题自定义与常见问题
- **CLI**：直接运行 `KBManager.CLI`，通过方向键在「搜索 / 文件与标签 / 设置 / 仓库操作」之间切换。首次运行且没有配置时，会引导进入设置流程

## 配置与数据存储

| 内容 | 位置 |
| --- | --- |
| 应用配置 | Windows：`%APPDATA%\KBManager\config.json`；Linux：`~/.config/KBManager/config.json` |
| 知识库索引 | `<仓库目录>/.kbdatabase/KbInfo.db` |
| 主题文件 | GUI 可执行文件同目录下的 `theme.json`（不存在时首次运行会自动生成带注释的样例） |
| 崩溃日志 | 桌面目录下的 `KBManager_crash.log` |

配置项说明：

| 字段 | 必填 | 说明 |
| --- | --- | --- |
| `UserName` / `UserEmail` | 是 | Git 提交时使用的作者信息 |
| `RepositoryDirectory` | 是 | 本地知识库根目录 |
| `RemoteAddressHttps` | 否 | HTTPS 远程地址，用于 Clone 的回落方案 |
| `RemoteAddressSsh` | 否 | SSH 远程地址，用于 Clone 与 Push |

## 开发与测试

```bash
# 运行测试
dotnet test KBManager.Tests/KBManager.Tests.csproj

# 代码分析检查
dotnet build KBManager.CLI/KBManager.CLI.csproj /p:RunCodeAnalysis=true
```

- 业务逻辑请写在 `KBManager.core` 中，并通过 `ServiceResult` 返回结果，不要在核心层直接使用 `Console`，以保证 CLI 与 GUI 可以共用
- GUI 遵循 MVVM：视图只做绑定，逻辑放在 `ViewModels`，平台相关能力（对话框、打开文件、主题）放在 `Services`
- 推送与 PR 会触发 [`.github/workflows/dotnet-build-check.yml`](.github/workflows/dotnet-build-check.yml) 的构建检查

## 路线图

- **同步增强**：识别文件重命名与删除，提供「清理失效记录」；支持自定义忽略规则
- **标签体系**：标签重命名与合并、批量打标签、标签颜色与分组
- **检索能力**：多标签组合过滤（AND / OR / NOT）、正文全文检索、结果导出
- **数据层**：引入 EF Core Migrations 管理索引结构版本，支持索引导出与重建
- **Git 能力**：支持带口令的私钥与 ssh-agent、Pull 与冲突处理、可选切换系统 git 后端
- **跨平台**：GUI 在 Linux / macOS 上的验证与打包发布（单文件、自带运行时）
- **工程化**：补充核心层单元测试，CI 增加 GUI 构建与测试执行

## 已知限制

- 当前同步是**只增不删**的：扫描只补齐新文件，仓库中已删除或重命名的文件仍会留在索引里，需要在文件管理页手动移除记录（对应路线图中的「同步增强」）
- 仅索引 Markdown 相关扩展名，其他文本格式不参与索引
- 一次只服务一个知识库：配置为全局单仓库，切换知识库需要修改配置
- SSH 推送目前以私钥内容作为凭据，适用于无口令私钥；带口令的加密私钥尚未支持
- 索引库与配置格式仍在演进，暂不保证跨版本兼容

## 许可证

本仓库当前未包含 LICENSE 文件。在补充许可证之前，请视为保留所有权利（All rights reserved）。
