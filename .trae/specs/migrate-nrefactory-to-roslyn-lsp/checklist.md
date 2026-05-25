# Checklist

## 阶段一：Roslyn 解析器适配层

- [ ] RoslynParserAdapter 实现了 IParser 接口的所有方法（Parse, Resolve, ResolveContext, FindLocalReferences, CreateCompilationForSingleFile, ResolveSnippet）
- [ ] RoslynParserAdapter 使用 CSharpSyntaxTree.ParseText() 正确解析 C# 代码
- [ ] RoslynTypeSystemAdapter 将 Roslyn Compilation 适配为 NRefactory ICompilation，接口行为一致
- [ ] RoslynTypeSystemAdapter 将 Roslyn ITypeSymbol 适配为 NRefactory IType
- [ ] RoslynTypeSystemAdapter 将 Roslyn ISymbol 适配为 NRefactory IMember
- [ ] RoslynUnresolvedFileAdapter 将 Roslyn SyntaxTree 适配为 IUnresolvedFile
- [ ] CSharpBinding.csproj 包含 Microsoft.CodeAnalysis.CSharp NuGet 包引用
- [ ] ParserService.CreateParser() 根据配置选择 NRefactory 或 Roslyn 后端
- [ ] 解析器后端配置选项已添加到 SD 选项面板
- [ ] 后端切换时缓存正确清除并重新解析
- [ ] Roslyn Compilation 管理器正确解析项目引用和框架引用
- [ ] 增量编译更新仅重新解析变更文件

## 阶段二：LSP 客户端基础设施

- [ ] LSP 协议层项目已创建，包含 JSON-RPC 2.0 通信实现
- [ ] LSP 协议消息类型完整定义（Initialize, Completion, Hover, Definition, References, Diagnostics 等）
- [ ] 消息序列化/反序列化使用 Newtonsoft.Json 正确工作
- [ ] LSP 传输层支持 stdio 通信方式
- [ ] LspClient 正确实现 Initialize/Initialized 握手流程
- [ ] 文档同步（didOpen/didChange/didClose）正确发送
- [ ] textDocument/completion 请求正确发送并解析响应
- [ ] textDocument/hover 请求正确发送并解析响应
- [ ] textDocument/definition 请求正确发送并解析响应
- [ ] textDocument/references 请求正确发送并解析响应
- [ ] textDocument/publishDiagnostics 通知正确处理并显示
- [ ] LSP 服务器进程可从配置启动
- [ ] LSP 服务器崩溃时正确检测并提供重启选项
- [ ] LSP 服务器优雅关闭（shutdown → exit）正确执行
- [ ] LspParserAdapter 实现了 IParser 接口
- [ ] LSP CompletionItem 正确转换为 SD ICompletionData
- [ ] LSP Location 正确转换为 SD 导航功能
- [ ] LSP Diagnostic 正确转换为 SD TaskList 项

## 阶段三：功能迁移（Roslyn 后端）

- [ ] RoslynCompletionBinding 使用 CompletionService 提供补全
- [ ] Roslyn CompletionItem 正确转换为 SD ICompletionData
- [ ] 补全项描述信息正确获取和显示
- [ ] 导入补全（using 自动添加）正常工作
- [ ] RoslynSemanticHighlighter 使用 SemanticModel 分类语法节点
- [ ] Roslyn SyntaxNode 分类正确映射到 SD 高亮颜色
- [ ] 增量高亮更新仅重新计算变更行
- [ ] "转到定义"使用 Roslyn SymbolFinder 正确工作
- [ ] "查找所有引用"使用 Roslyn FindReferencesAsync 正确工作
- [ ] "转到实现"使用 Roslyn FindImplementationsAsync 正确工作
- [ ] RoslynDiagnosticsProvider 使用 DiagnosticAnalyzer 提供诊断
- [ ] Roslyn Diagnostic 正确转换为 SD TaskList 项
- [ ] 代码修复（CodeFixProvider）支持正常工作
- [ ] RoslynFormatter 使用 Formatter 格式化代码
- [ ] SD 格式化选项正确适配到 Roslyn FormattingOptions

## 阶段四：清理与验证

- [ ] CSharpBinding.csproj 不再包含 NRefactory 包引用
- [ ] CSharpBinding 中无 using ICSharpCode.NRefactory 语句
- [ ] Roslyn 后端可正确解析 C# 9.0+ 语法（record, 顶级语句, 全局 using 等）
- [ ] LSP 后端连接 OmniSharp 服务器后补全/导航/诊断功能正常
- [ ] 后端切换功能正常（NRefactory → Roslyn → LSP）
- [ ] Roslyn 初始化失败时正确回退到 NRefactory
- [ ] 其他 AddIn（XamlBinding, Debugger, ILSpyAddIn 等）的 NRefactory 依赖已评估并制定策略
