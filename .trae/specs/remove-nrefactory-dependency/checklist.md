# Checklist

## 阶段一：TypeSystem 接口层提取

- [ ] ICSharpCode.TypeSystem.Abstractions 项目已创建，包含所有核心接口
- [ ] ICompilation, IType, IMember, ITypeDefinition 等核心接口已提取
- [ ] KnownTypeCode, TypeKind, Accessibility, SymbolKind 等枚举已提取
- [ ] FullTypeName, TopLevelTypeName, DomRegion 等结构体已提取
- [ ] CacheManager, EmptyList, TreeTraversal 等工具类已提供
- [ ] NRefactory 原接口继承自 Abstractions 接口，向后兼容
- [ ] ICSharpCode.SharpDevelop (Base) 编译通过，使用 Abstractions
- [ ] ICSharpCode.SharpDevelop (Main) 编译通过，使用 Abstractions

## 阶段二：容易项目迁移

- [ ] TypeScript 项目不再引用 ICSharpCode.NRefactory
- [ ] CppBinding 项目不再引用 ICSharpCode.NRefactory
- [ ] WpfDesign.AddIn 项目不再引用 ICSharpCode.NRefactory
- [ ] Scripting 项目不再引用 ICSharpCode.NRefactory
- [ ] SettingsEditor 项目不再引用 ICSharpCode.NRefactory
- [ ] ResourceEditor 项目不再引用 ICSharpCode.NRefactory
- [ ] Reporting 项目不再引用 ICSharpCode.NRefactory
- [ ] SearchAndReplace 项目不再引用 ICSharpCode.NRefactory
- [ ] ICSharpCode.Build.Tasks 项目不再引用 ICSharpCode.NRefactory
- [ ] CodeCoverage 项目不再引用 ICSharpCode.NRefactory
- [ ] 所有容易项目编译通过

## 阶段三：中等项目迁移

- [ ] 新 XML 增量解析器项目已创建，基于 System.Xml.Linq
- [ ] XmlEditor 使用新 XML 解析器，不再引用 NRefactory.Xml
- [ ] WixBinding 使用新 XML 解析器，不再引用 NRefactory.Xml
- [ ] PackageManagement 不再引用 ICSharpCode.NRefactory
- [ ] UnitTesting 不再引用 ICSharpCode.NRefactory
- [ ] CodeAnalysis 不再引用 ICSharpCode.NRefactory
- [ ] AvalonEdit.AddIn 不再引用 ICSharpCode.NRefactory
- [ ] AspNet.Mvc 不再引用 ICSharpCode.NRefactory
- [ ] 所有中等项目编译通过

## 阶段四：困难项目迁移

- [ ] CSharpBinding 在 Roslyn 后端模式下无 NRefactory 运行时依赖
- [ ] XamlBinding 不再引用 NRefactory.Xml 和 NRefactory.CSharp.Resolver
- [ ] Debugger ExpressionEvaluationVisitor 使用 Roslyn API
- [ ] Debugger 不再引用旧版 NRefactory.Ast
- [ ] ILSpyAddIn 使用 Roslyn CSharpSyntaxTree 替代 NRefactory CSharpParser
- [ ] FormsDesigner 使用 Roslyn SyntaxNode 替代 NRefactory Refactoring 代码生成
- [ ] 所有困难项目编译通过

## 阶段五：NRefactory 完全移除

- [ ] ICSharpCode.SharpDevelop (Base) 不再引用任何 NRefactory 包
- [ ] ICSharpCode.SharpDevelop (Main) 不再引用任何 NRefactory 包
- [ ] VB 转换器已重写或标记为过时
- [ ] AssemblyInfoProvider 使用 Roslyn 替代 NRefactory CSharpParser
- [ ] 解决方案中不再包含 ICSharpCode.NRefactory 项目
- [ ] 全解决方案编译通过，0 个 NRefactory 引用
- [ ] 现有测试全部通过
