# NRefactory 迁移到 Roslyn 及 LSP 支持规划

## Why

SharpDevelop 当前依赖 ICSharpCode.NRefactory 5.5.1 作为 C# 语言服务基础设施，但 NRefactory 已停止维护多年，不支持 C# 9.0+ 语法特性（如 record、顶级语句、全局 using 等），且解析器存在已知崩溃问题（如表达式体属性的 InvalidCastException）。迁移到 Roslyn（Microsoft.CodeAnalysis）可获得完整的 C# 语言支持；增加 LSP 客户端支持可让 SharpDevelop 利用任何语言服务器（如 OmniSharp、csharp-ls）提供语言智能，同时为未来多语言支持奠定基础。

## What Changes

### 阶段一：Roslyn 解析器适配层
- 新建 `RoslynParserAdapter` 项目，实现 `IParser` 接口，将 Roslyn 的 `SyntaxTree` 和 `SemanticModel` 适配为 SharpDevelop 现有的 `ParseInformation` / `IUnresolvedFile` 体系
- 新建 `RoslynTypeSystemAdapter` 项目，将 Roslyn 的 `Compilation` / `ISymbol` 适配为 NRefactory 的 `ICompilation` / `IType` 等类型系统接口
- 修改 `CSharpBinding.csproj`，添加 Roslyn NuGet 包引用（`Microsoft.CodeAnalysis.CSharp`）
- 修改 `ParserService`，支持按配置选择 NRefactory 或 Roslyn 解析器后端

### 阶段二：LSP 客户端基础设施
- 新建 `SharpDevelop.Lsp` 项目，实现 LSP 客户端协议（JSON-RPC 2.0 通信层）
- 新建 `LspLanguageService` 项目，将 LSP 语言服务器功能适配到 SharpDevelop 的 `IParserService` 接口
- 修改 SharpDevelop AddIn 树配置，注册 LSP 相关服务
- 支持 LSP 服务器进程管理（启动、停止、重启）

### 阶段三：功能迁移（Roslyn 后端）
- 迁移代码补全：用 Roslyn `CompletionService` 替换 `CSharpCompletionEngine`
- 迁移语义高亮：用 Roslyn `SemanticModel` 替换 `CSharpSemanticHighlighterVisitor`
- 迁移代码导航（转到定义、查找引用）：用 Roslyn `SymbolFinder` / `FindReferencesAsync` 替换 NRefactory 的 `FindReferences`
- 迁移代码诊断：用 Roslyn `DiagnosticAnalyzer` 替换 NRefactory 的 `CodeIssue`
- 迁移代码格式化：用 Roslyn `Formatter` 替换 NRefactory 的 `CSharpFormatter`

### 阶段四：清理 NRefactory 依赖
- 从 CSharpBinding 项目移除 NRefactory 引用
- 从核心项目（ICSharpCode.SharpDevelop）移除 NRefactory.TypeSystem 依赖
- 评估其他 AddIn（XamlBinding、Debugger、ILSpyAddIn 等）的 NRefactory 依赖，逐步替换或保留

## Impact

- **Affected specs**: CSharpBinding 解析器、代码补全、语义高亮、重构、代码导航、代码诊断、格式化
- **Affected code**:
  - `src/AddIns/BackendBindings/CSharpBinding/` — 核心影响区域（98 个 .cs 文件）
  - `src/Main/Base/Project/Parser/` — IParserService 接口可能需要扩展
  - `src/Main/SharpDevelop/Parser/` — ParserService 实现需要修改
  - `src/Main/Base/Project/Util/NRefactory/` — 适配器层需要重写
  - `src/Libraries/ICSharpCode.NRefactory/` — 最终移除（1100+ 文件）
  - 18+ 个 csproj 文件的 NuGet 引用需要更新

## ADDED Requirements

### Requirement: Roslyn 解析器后端

系统 SHALL 提供 Roslyn 作为 CSharpBinding 的可选解析器后端，与现有 NRefactory 后端并行运行。

#### Scenario: 使用 Roslyn 解析 C# 文件
- **WHEN** 用户配置使用 Roslyn 后端并打开 C# 文件
- **THEN** 系统使用 `Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree` 解析文件，生成 `ParseInformation`，并通过 `RoslynTypeSystemAdapter` 提供类型系统信息

#### Scenario: Roslyn 解析器支持现代 C# 语法
- **WHEN** 用户打开包含 C# 9.0+ 语法（record、顶级语句、全局 using 等）的文件
- **THEN** 系统正确解析并显示语法树，不产生解析错误

#### Scenario: 回退到 NRefactory
- **WHEN** Roslyn 后端初始化失败或用户未配置 Roslyn
- **THEN** 系统自动回退到 NRefactory 后端，保证基本功能可用

### Requirement: LSP 客户端支持

系统 SHALL 提供 LSP 客户端，能够连接外部语言服务器并利用其语言智能功能。

#### Scenario: 连接 OmniSharp 语言服务器
- **WHEN** 用户配置 LSP 服务器路径并打开 C# 项目
- **THEN** 系统启动 LSP 服务器进程，建立 JSON-RPC 通信，发送 `initialize` 请求，并接收语言服务能力

#### Scenario: LSP 代码补全
- **WHEN** 用户在编辑器中触发代码补全且 LSP 服务器支持 `textDocument/completion`
- **THEN** 系统发送 `textDocument/completion` 请求，将 LSP 返回的补全项转换为 SharpDevelop 补全数据并显示

#### Scenario: LSP 服务器崩溃恢复
- **WHEN** LSP 服务器进程意外退出
- **THEN** 系统检测到连接断开，通知用户，并提供重启服务器的选项

### Requirement: 解析器后端可切换

系统 SHALL 支持在运行时切换解析器后端（NRefactory / Roslyn / LSP）。

#### Scenario: 切换解析器后端
- **WHEN** 用户在选项中更改解析器后端设置
- **THEN** 系统清除现有解析缓存，使用新后端重新解析所有打开的文件

### Requirement: 类型系统适配层

系统 SHALL 提供 Roslyn 类型系统到 NRefactory 类型系统的适配层，使现有上层功能（类浏览器、项目系统等）无需修改即可工作。

#### Scenario: 适配 ICompilation
- **WHEN** 上层代码通过 `IParserService.GetCompilation()` 获取类型系统
- **THEN** 无论底层使用 NRefactory 还是 Roslyn，返回的 `ICompilation` 接口行为一致

## MODIFIED Requirements

### Requirement: IParser 接口扩展

现有 `IParser` 接口需要扩展以支持 Roslyn 后端的额外能力：

- 新增 `bool SupportsRoslyn { get; }` 属性，标识解析器是否基于 Roslyn
- 新增 `Task<SemanticModel> GetSemanticModelAsync(FileName, CancellationToken)` 方法（可选实现）
- 现有方法签名不变，保证 NRefactory 后端兼容

### Requirement: ParserService 后端选择

`ParserService` 需要修改以支持多后端：

- 从 `SD.PropertyService` 读取用户选择的解析器后端
- 根据后端类型创建对应的 `IParser` 实现
- 后端切换时清除缓存并重新解析

## REMOVED Requirements

### Requirement: NRefactory CSharpParser 直接依赖
**Reason**: 迁移到 Roslyn 后不再需要直接使用 NRefactory 的 CSharpParser
**Migration**: 通过 `RoslynParserAdapter` 间接提供解析功能，接口层不变

### Requirement: NRefactory CSharpCompletionEngine 直接依赖
**Reason**: 迁移到 Roslyn 后使用 Roslyn CompletionService 或 LSP 补全
**Migration**: 通过适配层保持 `ICodeCompletionBinding` 接口不变
