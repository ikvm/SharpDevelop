# Tasks

## 阶段一：TypeSystem 接口层提取（核心基础设施）

- [ ] Task 1: 创建 ICSharpCode.TypeSystem.Abstractions 项目
  - [ ] 1.1 在 `src/Libraries/` 下新建 `ICSharpCode.TypeSystem.Abstractions` 项目
  - [ ] 1.2 从 NRefactory 提取核心接口：ICompilation, IType, IMember, ITypeDefinition, IAssembly, IProjectContent, INamespace, IUnresolvedFile, IUnresolvedTypeDefinition, ITypeResolveContext, ISolutionSnapshot, IEntity, IParameterizedMember, IMethod, IProperty, IField, IEvent, IParameter, ITypeParameter, IAttribute, IAssemblyReference, IMemberReference, ITypeReference
  - [ ] 1.3 提取枚举：KnownTypeCode, TypeKind, Accessibility, SymbolKind, EntityType
  - [ ] 1.4 提取结构体：FullTypeName, TopLevelTypeName, DomRegion
  - [ ] 1.5 提取工具类：CacheManager, EmptyList, TreeTraversal（或使用 C# 内置替代）
  - [ ] 1.6 在 NRefactory 项目中让原接口继承自 Abstractions 接口，保持向后兼容
  - [ ] 1.7 编译验证：所有现有项目仍可正常编译

- [ ] Task 2: 迁移核心项目到 TypeSystem.Abstractions
  - [ ] 2.1 修改 ICSharpCode.SharpDevelop.csproj (Base)：替换 NRefactory TypeSystem 引用为 Abstractions
  - [ ] 2.2 修改 ICSharpCode.SharpDevelop.csproj (Main)：替换 NRefactory TypeSystem 引用为 Abstractions
  - [ ] 2.3 更新 using 语句：`ICSharpCode.NRefactory.TypeSystem` → `ICSharpCode.TypeSystem.Abstractions`
  - [ ] 2.4 编译验证

## 阶段二：容易项目迁移（仅 TypeSystem 接口）

- [ ] Task 3: 迁移"容易"项目到 TypeSystem.Abstractions
  - [ ] 3.1 TypeScript — 替换 NRefactory 引用为 Abstractions
  - [ ] 3.2 CppBinding — 替换 NRefactory 引用为 Abstractions
  - [ ] 3.3 WpfDesign.AddIn — 替换 NRefactory 引用为 Abstractions
  - [ ] 3.4 Scripting — 替换 NRefactory 引用为 Abstractions
  - [ ] 3.5 SettingsEditor — 替换 NRefactory 引用为 Abstractions
  - [ ] 3.6 ResourceEditor — 替换 NRefactory 引用为 Abstractions
  - [ ] 3.7 Reporting — 替换 NRefactory 引用为 Abstractions
  - [ ] 3.8 SearchAndReplace — 移除 NRefactory 引用（无实际 using）
  - [ ] 3.9 ICSharpCode.Build.Tasks — 移除 NRefactory 引用（无实际 using）
  - [ ] 3.10 CodeCoverage — 移除 NRefactory 引用（无实际 using）
  - [ ] 3.11 Samples — 替换 NRefactory 引用为 Abstractions
  - [ ] 3.12 编译验证

## 阶段三：中等项目迁移

- [ ] Task 4: 迁移 XmlEditor 和 WixBinding（NRefactory.Xml 替换）
  - [ ] 4.1 创建 `ICSharpCode.Xml.IncrementalParser` 项目，基于 System.Xml.Linq 实现增量 XML 解析
  - [ ] 4.2 实现 IXmlParser 接口，提供与 NRefactory.Xml.AXmlParser 等价的折叠/导航功能
  - [ ] 4.3 迁移 XmlEditor：替换 NRefactory.Xml using 为新 XML 解析器
  - [ ] 4.4 迁移 WixBinding：替换 NRefactory.Xml using 为新 XML 解析器
  - [ ] 4.5 编译验证

- [ ] Task 5: 迁移 PackageManagement 和 Analysis 项目
  - [ ] 5.1 PackageManagement：替换 TypeSystem 引用为 Abstractions，处理少量 Refactoring 引用
  - [ ] 5.2 UnitTesting：替换 TypeSystem 引用为 Abstractions
  - [ ] 5.3 CodeAnalysis：替换 TypeSystem + Semantics 引用
  - [ ] 5.4 CodeQuality：替换 TypeSystem 引用为 Abstractions
  - [ ] 5.5 Profiler：替换 TypeSystem + Semantics 引用
  - [ ] 5.6 编译验证

- [ ] Task 6: 迁移 AvalonEdit.AddIn 和 AspNet.Mvc
  - [ ] 6.1 AvalonEdit.AddIn：替换 TypeSystem + Semantics 引用
  - [ ] 6.2 AspNet.Mvc：替换 TypeSystem + CSharp.Completion 引用（使用 Roslyn CompletionService）
  - [ ] 6.3 编译验证

## 阶段四：困难项目迁移

- [ ] Task 7: CSharpBinding 完全脱离 NRefactory 运行时
  - [ ] 7.1 确保 Roslyn 后端所有功能可用（Parser/Completion/Highlighting/Navigation/Diagnostics/Formatting）
  - [ ] 7.2 将 NRefactory Parser/Completion/Refactoring/Formatter 代码标记为条件编译（`#if USE_NREFATORY`）
  - [ ] 7.3 从 csproj 移除 NRefactory 包引用（保留为可选依赖）
  - [ ] 7.4 编译验证：Roslyn 后端模式下无 NRefactory 运行时依赖

- [ ] Task 8: XamlBinding 迁移
  - [ ] 8.1 替换 NRefactory.Xml 为新 XML 解析器
  - [ ] 8.2 替换 CSharp Resolver 为 Roslyn SemanticModel（XAML 中的 C# 表达式解析）
  - [ ] 8.3 重写 CompletionDataGenerator 使用 Roslyn API
  - [ ] 8.4 编译验证

- [ ] Task 9: Debugger 迁移
  - [ ] 9.1 替换 TypeSystem 引用为 Abstractions
  - [ ] 9.2 重写 ExpressionEvaluationVisitor 使用 Roslyn CSharpScript 或 SemanticModel
  - [ ] 9.3 替换旧版 NRefactory.Ast 为 Roslyn SyntaxNode
  - [ ] 9.4 编译验证

- [ ] Task 10: ILSpyAddIn 迁移
  - [ ] 10.1 替换 CSharpParser 为 Roslyn CSharpSyntaxTree
  - [ ] 10.2 替换 Cecil 加载器为直接使用 Mono.Cecil
  - [ ] 10.3 替换 Documentation 命名空间为 Roslyn DocumentationComment
  - [ ] 10.4 编译验证

- [ ] Task 11: FormsDesigner 迁移
  - [ ] 11.1 替换 Editor 命名空间为 AvalonEdit 原生接口
  - [ ] 11.2 替换 CSharp Refactoring 代码生成为 Roslyn SyntaxNode 生成
  - [ ] 11.3 编译验证

## 阶段五：核心项目 NRefactory 完全移除

- [ ] Task 12: 移除核心项目的 NRefactory 依赖
  - [ ] 12.1 ICSharpCode.SharpDevelop (Base)：移除所有 NRefactory 引用，仅保留 Abstractions
  - [ ] 12.2 ICSharpCode.SharpDevelop (Main)：移除所有 NRefactory 引用
  - [ ] 12.3 处理 VB 转换器（ConvertBuffer/CSharpConvertBuffer）— 重写或标记为过时
  - [ ] 12.4 处理 AssemblyInfoProvider 中的 CSharpParser 引用 — 替换为 Roslyn
  - [ ] 12.5 编译验证

- [ ] Task 13: 移除 NRefactory 库项目
  - [ ] 13.1 从解决方案中移除 ICSharpCode.NRefactory 项目（9 个子项目）
  - [ ] 13.2 清理 packages 目录中的 NRefactory NuGet 包
  - [ ] 13.3 全解决方案编译验证
  - [ ] 13.4 运行现有测试验证功能正确性

# Task Dependencies

- Task 2 依赖 Task 1（核心项目迁移需要 Abstractions 项目）
- Task 3 依赖 Task 1（容易项目迁移需要 Abstractions 项目）
- Task 4-6 依赖 Task 1（中等项目迁移需要 Abstractions 项目）
- Task 4 和 Task 8 可部分并行（XML 解析器独立）
- Task 7 依赖 Roslyn 适配器功能完善（已有基础）
- Task 8 依赖 Task 4（XamlBinding 需要 XML 解析器）
- Task 9-11 相互独立，可并行
- Task 12 依赖 Task 2-11（所有项目迁移完成后才能移除核心依赖）
- Task 13 依赖 Task 12（所有项目脱离 NRefactory 后才能移除库项目）
