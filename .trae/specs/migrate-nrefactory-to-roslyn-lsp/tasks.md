# Tasks

## 阶段一：Roslyn 解析器适配层

- [x] Task 1: 创建 RoslynParserAdapter 项目骨架
  - [x] 1.1 在 `src/AddIns/BackendBindings/CSharpBinding/` 下新建 `RoslynParserAdapter.cs`，实现 `IParser` 接口
  - [x] 1.2 在 CSharpBinding.csproj 中添加 `Microsoft.CodeAnalysis.CSharp` NuGet 包引用
  - [x] 1.3 实现 `Parse()` 方法：使用 `CSharpSyntaxTree.ParseText()` 解析代码，生成 `ParseInformation`
  - [x] 1.4 实现 `Resolve()` 方法：使用 Roslyn `SemanticModel` 在指定位置解析符号
  - [x] 1.5 实现 `ResolveContext()` 方法：适配 Roslyn 语义信息为 `ICodeContext`

- [x] Task 2: 创建 RoslynTypeSystemAdapter 类型系统适配层
  - [x] 2.1 新建 `RoslynCompilationAdapter.cs`，将 Roslyn `Compilation` 适配为 NRefactory `ICompilation`
  - [x] 2.2 新建 `RoslynTypeAdapter.cs`，将 Roslyn `ITypeSymbol` 适配为 NRefactory `IType`
  - [x] 2.3 新建 `RoslynMemberAdapter.cs`，将 Roslyn `ISymbol` 适配为 NRefactory `IMember`
  - [x] 2.4 新建 `RoslynUnresolvedFileAdapter.cs`，将 Roslyn `SyntaxTree` 适配为 `IUnresolvedFile`
  - [x] 2.5 实现 `IAssembly` / `IProjectContent` 适配器

- [x] Task 3: 修改 ParserService 支持多后端
  - [x] 3.1 创建 `ParserBackend` 枚举（NRefactory / Roslyn / Lsp）
  - [x] 3.2 修改 `ParserService.CreateParser()` 方法，根据配置选择后端
  - [x] 3.3 修改 `IParserService` 接口添加 `CurrentParserBackend` 属性
  - [x] 3.4 实现后端切换时的缓存清除和重新解析逻辑

- [x] Task 4: Roslyn Compilation 管理
  - [x] 4.1 新建 `RoslynCompilationManager.cs`，管理 Roslyn `CSharpCompilation` 的创建和更新
  - [x] 4.2 实现项目引用解析：将 SD 的 `IProject.ReferenceProjects` 转换为 Roslyn `MetadataReference`
  - [x] 4.3 实现框架引用解析：从目标框架自动添加 mscorlib/System.Runtime 等引用
  - [x] 4.4 实现增量编译更新：文件修改时仅重新解析变更文件

## 阶段二：LSP 客户端基础设施

- [x] Task 5: 创建 LSP 协议层项目
  - [x] 5.1 新建 `src/AddIns/Misc/LanguageServerClient/` 项目
  - [x] 5.2 实现 JSON-RPC 2.0 通信层（`JsonRpcClient`）
  - [x] 5.3 定义 LSP 协议消息类型（`InitializeParams/Result`、`Position`、`Range` 等）
  - [x] 5.4 实现消息序列化/反序列化（使用 Newtonsoft.Json）
  - [x] 5.5 实现 LSP 传输层（stdio）

- [x] Task 6: 实现 LSP 客户端核心
  - [x] 6.1 新建 `LspClient.cs`，封装 LSP 客户端生命周期管理
  - [x] 6.2 实现 `Initialize` / `Initialized` 握手流程
  - [x] 6.3 实现 `textDocument/didOpen` / `didChange` / `didClose` 文档同步
  - [x] 6.4 实现 `textDocument/completion` 请求
  - [x] 6.5 实现 `textDocument/hover` 请求
  - [x] 6.6 实现 `textDocument/definition` 请求
  - [x] 6.7 实现 `textDocument/references` 请求
  - [x] 6.8 实现 `textDocument/publishDiagnostics` 通知处理

- [x] Task 7: LSP 服务器进程管理
  - [x] 7.1 新建 `LspConnection.cs`，管理 LSP 服务器进程生命周期
  - [x] 7.2 实现服务器启动（从配置读取可执行文件路径和参数）
  - [x] 7.3 实现服务器健康监控（崩溃恢复、自动重连）
  - [x] 7.4 实现服务器优雅关闭（发送 `shutdown` → `exit`）

- [x] Task 8: LSP 到 SharpDevelop 适配层
  - [x] 8.1 新建 `LspParserAdapter.cs`，实现 `IParser` 接口，委托给 LSP 客户端
  - [x] 8.2 新建 `LspCompletionAdapter.cs`，将 LSP `CompletionItem` 转换为 SD `ICompletionData`
  - [x] 8.3 新建 `LspNavigationAdapter.cs`，将 LSP `Location` 转换为 SD 导航功能
  - [x] 8.4 新建 `LspDiagnosticsAdapter.cs`，将 LSP `Diagnostic` 转换为 SD `TaskList` 项

## 阶段三：功能迁移（Roslyn 后端）

- [ ] Task 9: 迁移代码补全到 Roslyn
  - [ ] 9.1 新建 `RoslynCompletionBinding.cs`，使用 Roslyn `CompletionService` 提供补全
  - [ ] 9.2 实现 `CompletionItem` 到 SD `ICompletionData` 的转换
  - [ ] 9.3 实现补全项的描述信息获取（`DescriptionService`）
  - [ ] 9.4 实现导入补全（using 自动添加）

- [ ] Task 10: 迁移语义高亮到 Roslyn
  - [ ] 10.1 新建 `RoslynSemanticHighlighter.cs`，使用 Roslyn `SemanticModel` 分类语法节点
  - [ ] 10.2 实现 Roslyn `SyntaxNode` 分类到 SD 高亮颜色的映射
  - [ ] 10.3 实现增量高亮更新（仅重新计算变更行）

- [ ] Task 11: 迁移代码导航到 Roslyn
  - [ ] 11.1 实现"转到定义"：使用 Roslyn `SemanticModel` 解析定义位置
  - [ ] 11.2 实现"查找所有引用"：使用 Roslyn `SymbolFinder.FindReferencesAsync`
  - [ ] 11.3 实现"转到实现"：使用 Roslyn `SymbolFinder.FindImplementationsAsync`

- [ ] Task 12: 迁移代码诊断到 Roslyn
  - [ ] 12.1 新建 `RoslynDiagnosticsProvider.cs`，使用 Roslyn `DiagnosticAnalyzer` 提供诊断
  - [ ] 12.2 将 Roslyn `Diagnostic` 转换为 SD `TaskList` 项
  - [ ] 12.3 支持代码修复（`CodeFixProvider`）

- [ ] Task 13: 迁移代码格式化到 Roslyn
  - [ ] 13.1 新建 `RoslynFormatter.cs`，使用 Roslyn `Formatter` 格式化代码
  - [ ] 13.2 适配 SD 格式化选项到 Roslyn `FormattingOptions`

## 阶段四：清理与优化

- [ ] Task 14: 移除 CSharpBinding 的 NRefactory 依赖
  - [ ] 14.1 从 CSharpBinding.csproj 移除 NRefactory 包引用
  - [ ] 14.2 删除 CSharpBinding 中所有 `using ICSharpCode.NRefactory` 语句
  - [ ] 14.3 删除 NRefactory 特定的实现类（NRefactory 版 Parser、Completion、Highlighter 等）

- [ ] Task 15: 评估其他 AddIn 的 NRefactory 依赖
  - [ ] 15.1 评估 XamlBinding 的 NRefactory 依赖（15 处引用）
  - [ ] 15.2 评估 Debugger.AddIn 的 NRefactory 依赖
  - [ ] 15.3 评估 ILSpyAddIn 的 NRefactory 依赖
  - [ ] 15.4 评估 UnitTesting 的 NRefactory 依赖
  - [ ] 15.5 为每个 AddIn 制定迁移或保留策略

- [ ] Task 16: 集成测试与验证
  - [ ] 16.1 验证 Roslyn 后端：打开包含 C# 9.0+ 语法的项目，确认解析正确
  - [ ] 16.2 验证 LSP 后端：连接 OmniSharp 服务器，确认补全/导航/诊断功能正常
  - [ ] 16.3 验证后端切换：在选项中切换后端，确认缓存清除和重新解析正常
  - [ ] 16.4 验证回退机制：模拟 Roslyn 初始化失败，确认回退到 NRefactory

# Task Dependencies

- Task 2 依赖 Task 1（类型系统适配需要解析器基础）
- Task 3 依赖 Task 1 和 Task 2（ParserService 需要完整的适配层）
- Task 4 依赖 Task 2（Compilation 管理需要类型系统适配）
- Task 5-8 相互独立，可与阶段一并行开发
- Task 8 依赖 Task 5 和 Task 6（适配层需要 LSP 客户端核心）
- Task 9-13 依赖 Task 3（功能迁移需要 ParserService 多后端支持）
- Task 14 依赖 Task 9-13（移除 NRefactory 需要所有功能已迁移）
- Task 15 依赖 Task 14（其他 AddIn 迁移在 CSharpBinding 迁移后进行）
- Task 16 依赖所有前置任务
