# 微软拼音词库管理器 (MicrosoftIMELexManager) 开发计划

## 技术栈与架构概览

- **框架**: WinUI 3 (.NET 8) + Windows App SDK
- **架构**: MVVM (CommunityToolkit.Mvvm)
- **UI 组件**: CommunityToolkit.WinUI.UI.Controls（含 DataGrid）
- **文件格式**: mschxudp (.lex)、IH.dat、UDL.dat（均已逆向，有完整文档）

---

## 支持的词库文件及能力矩阵

| 文件 | 路径 | 内容 | 支持操作 |
|------|------|------|----------|
| `ChsPinyinEUDPv1.lex` | `%APPDATA%\...\Chs\` | 自定义短语 | 增 / 删 / 改 词语、拼音、候选位置 |
| `ChsPinyinIH.dat` | `%APPDATA%\...\Chs\` | 输入历史 + 词频 | 改词频(1-4)、删条目 |
| `ChsPinyinUDL.dat` | `%APPDATA%\...\Chs\` | 自学习词汇 | 删条目、查看拼音 |

---

## 第一阶段：基础数据层

### Step 1 — 创建数据模型 (`Models/`)

- [ ] 创建 `Models/LexEntry.cs`：`Pinyin`、`Phrase`、`CandidateIndex` (int 1-9)
- [ ] 创建 `Models/IHEntry.cs`：`Word`、`Frequency` (uint32 1-4)、`Timestamp` (uint32)
- [ ] 创建 `Models/UDLEntry.cs`：`Word`、`PinyinText` (从索引解码)、`Timestamp`

### Step 2 — 创建 mschxudp `.lex` 解析器/写入器 (`Services/LexFileService.cs`)

- [ ] 实现读取：Magic 校验 → 解析偏移表 → 逐条解析 UTF-16LE body（拼音 + 短语）
- [ ] 实现写入：重建偏移表 → 重新打包二进制
- [ ] 参考 `lex-struct.md` 中的 Node.js 参考实现移植为 C#

### Step 3 — 创建 IH.dat 解析器/写入器 (`Services/IHFileService.cs`)

- [ ] 实现读取：Magic `0xAA55 / 0x8088` → 头部 WordCount → 数据区 0x1400，每条 60 字节，偏移 +8 为 uint32 频率
- [ ] 实现写入：原位替换 frequency 字段（偏移固定，无需重建整文件）
- [ ] 词语解析：UTF-16LE，长度来自偏移 +0

### Step 4 — 创建 UDL.dat 解析器 (`Services/UDLFileService.cs`)

- [ ] 实现读取：Magic `0xAA55 / 0x8188` → 头部 WordCount → 数据区 0x2400，每条 60 字节
- [ ] 拼音索引解码：内置 415 条标准拼音表，将 int16[] 转为可读拼音字符串
- [ ] 实现删除：将记录 Marker 字节（偏移 +0x0B）从 `0x5A` 改为 `0x00` 标记无效

### Step 5 — 创建拼音索引表 (`Data/PinyinTable.cs`)

- [ ] 内置 415 条微软内部排序拼音表（静态只读数组）
- [ ] 供 UDL 解码和 LEX 辅助输入使用

---

## 第二阶段：ViewModel 层

### Step 6 — 配置 MVVM 基础 (`ViewModels/`)

- [ ] 安装 `CommunityToolkit.Mvvm` NuGet 包
- [ ] 安装 `CommunityToolkit.WinUI.UI.Controls` NuGet 包（DataGrid）
- [ ] 创建 `ViewModels/LexViewModel.cs`：`ObservableCollection<LexEntry>` + 增/删/改命令 + 保存命令
- [ ] 创建 `ViewModels/IHViewModel.cs`：`ObservableCollection<IHEntry>` + 修改词频命令 + 批量删除命令
- [ ] 创建 `ViewModels/UDLViewModel.cs`：`ObservableCollection<UDLEntry>` + 删除命令

---

## 第三阶段：UI 层

### Step 7 — 主窗口布局 (`MainWindow.xaml`)

- [ ] 顶部 `NavigationView` 或 `TabView`（三个 Tab：自定义短语 / 输入历史 / 自学习词汇）
- [ ] 顶部工具栏：打开文件夹按钮（自动定位到 `%APPDATA%\...\Chs\`）、保存按钮、备份按钮
- [ ] 底部状态栏：当前文件路径 + 条目总数

### Step 8 — 自定义短语页面 (`Pages/LexPage.xaml`) ⭐ 核心功能

- [ ] 引入 `DataGrid` 控件
- [ ] 列：`拼音`（可编辑 TextBox）、`词语`（可编辑 TextBox）、`候选位置`（1-9 NumberBox）
- [ ] 工具栏：新增行、删除行、搜索框（实时过滤）
- [ ] 支持导入 TSV/JSON
- [ ] 支持导出 TSV

### Step 9 — 输入历史页面 (`Pages/IHPage.xaml`)

- [ ] DataGrid 列：`词语`（只读）、`词频`（1-4 Slider 或 NumberBox，可编辑）、`时间戳`（只读，格式化显示）
- [ ] 工具栏：搜索框、删除行
- [ ] 一键清零词频（危险操作，需二次确认 ContentDialog）

### Step 10 — 自学习词汇页面 (`Pages/UDLPage.xaml`)

- [ ] DataGrid 列：`词语`（只读）、`拼音`（解码显示，只读）、`插入时间`（只读）
- [ ] 工具栏：搜索框、删除行

---

## 第四阶段：安全与辅助功能

### Step 11 — 备份 & 安全写入 (`Services/BackupService.cs`)

- [ ] 写入前自动创建 `.bak` 备份（时间戳命名，如 `ChsPinyinEUDPv1.lex.20260515_143022.bak`）
- [ ] 检测输入法进程是否运行（`InputMethod.exe` / `ctfmon.exe`），运行时弹出警告提示

### Step 12 — 文件路径自动发现 (`Services/IMEPathService.cs`)

- [ ] 自动检测 `%APPDATA%\Microsoft\InputMethod\Chs\` 是否存在
- [ ] 支持手动选择文件夹（`FolderPicker`）
- [ ] 记住上次打开路径（使用 `ApplicationData.LocalSettings`）

---

## 第五阶段：收尾与打磨

### Step 13 — 集成测试 & 异常处理

- [ ] 对三个 Service 编写单元测试（读取 → 修改 → 写入 → 重新读取对比）
- [ ] UI 层全局 try-catch + `ContentDialog` 错误提示

### Step 14 — UI 打磨

- [ ] 应用 Mica 背景（已有，保留）
- [ ] 未保存修改时在 Tab 标题显示 `*` 标记
- [ ] 关闭窗口时若有未保存修改，弹出保存提示
- [ ] 键盘快捷键：`Ctrl+S` 保存、`Ctrl+Z` 撤销（ObservableCollection 快照实现）
- [ ] DataGrid 虚拟化（应对数万条词库条目的性能）

---

## 关键技术要点备忘

| 问题 | 方案 |
|------|------|
| LEX 偏移表重建 | 写入时遍历所有条目，重新计算每条相对 `firstBlockPos` 的偏移，打包 `uint32[]` |
| IH.dat 词频原位修改 | 仅在偏移 `0x1400 + i*60 + 8` 写入新的 `uint32`，无需重建整文件 |
| UDL 拼音解码 | 维护 415 条内置拼音表，`PinyinIndices` 数组依次查表拼接 |
| DataGrid 大数据量 | IH.dat 最多 8192 条，UDL/LEX 可达数万条，使用虚拟化 + 搜索过滤降低渲染压力 |
| 线程安全 | 文件 IO 使用 `async/await`，通过 `DispatcherQueue` 回到 UI 线程更新集合 |

---

## 推荐实现顺序

```
Step 1-5 (数据层)
  → Step 6 (ViewModel)
  → Step 7 (主窗口框架)
  → Step 8 (LEX 页面，最核心)
  → Step 9 (IH 页面)
  → Step 10 (UDL 页面)
  → Step 11-12 (安全功能)
  → Step 13-14 (测试与打磨)
```
