# 移除 NRefactory 依赖可行性分析 Spec

## Why

SharpDevelop 解决方案中有 57 个消费者项目引用了 ICSharpCode.NRefactory，涉及约 780 条 using 语句和 450 个 .cs 文件。在 Roslyn 适配层和 LSP 客户端已实现的基础上，需要系统性地评估每个项目移除 NRefactory 依赖的可行性，制定分阶段迁移策略，避免一次性大范围改动导致不可控风险。

## What Changes

### 策略一：保留 TypeSystem 接口层（推荐）
- **不删除** NRefactory.TypeSystem 命名空间中的接口定义（ICompilation, IType, IMember, IProjectContent 等）
- 将这些接口从 NRefactory 库中**提取到独立的抽象层项目** `ICSharpCode.TypeSystem.Abstractions`
- Roslyn 适配器实现这些接口，NRefactory 也实现这些接口
- 消费者项目仅依赖接口层，不依赖具体实现
- **好处**：改动最小，约 250 条 TypeSystem using 语句无需修改
- **代价**：需要维护接口层项目

### 策略二：逐步替换为 Roslyn API
- 每个项目的 NRefactory using 语句逐个替换为 Roslyn 等价 API
- **好处**：最终完全脱离 NRefactory
- **代价**：改动量大，450 个文件需要修改，风险高

### 策略三：混合策略（采用）
- TypeSystem 接口层提取为独立项目（策略一）
- C# 特定功能（Parser/Resolver/Completion/Refactoring/Formatter）替换为 Roslyn（策略二）
- Xml 解析替换为 System.Xml.Linq
- Utils 工具类自行实现或用 C# 内置替代
- 旧版 Ast/PrettyPrinter 代码完全重写

## Impact

- **Affected specs**: 所有使用 NRefactory 的项目（57 个）
- **Affected code**:
  - `src/Libraries/ICSharpCode.NRefactory/` — 1100+ 文件，需拆分或移除
  - `src/Main/Base/Project/` — 84 个文件使用 NRefactory
  - `src/AddIns/BackendBindings/CSharpBinding/` — 92 个文件使用 NRefactory
  - `src/AddIns/BackendBindings/XamlBinding/` — 27 个文件使用 NRefactory
  - `src/AddIns/Debugger/` — 39 个文件使用 NRefactory
  - 其他 50+ 个项目

## 项目迁移可行性分级

### 容易（仅 TypeSystem 接口，适配层即可解决）— 11 个项目
| 项目 | using数 | 说明 |
|------|---------|------|
| TypeScript | 12 | 仅 TypeSystem |
| CppBinding | 4 | 仅 TypeSystem |
| WpfDesign.AddIn | 6 | 仅 TypeSystem + 少量 CSharp |
| Scripting | 5 | 仅 TypeSystem |
| SettingsEditor | 2 | 仅 TypeSystem |
| ResourceEditor | 1 | 仅 TypeSystem |
| Reporting | 1 | 仅 TypeSystem |
| SearchAndReplace | 0 | 仅 csproj 引用 |
| ICSharpCode.Build.Tasks | 0 | 仅 csproj 引用 |
| CodeCoverage | 0 | 仅 csproj 引用 |
| Samples | 7 | 仅 TypeSystem |

### 中等（使用特定功能，有 Roslyn 等价物）— 9 个项目
| 项目 | using数 | 主要障碍 |
|------|---------|---------|
| ICSharpCode.SharpDevelop (Base) | 115 | TypeSystem 主导 + 少量 CSharp/Semantics + VB转换器 |
| ICSharpCode.SharpDevelop (Main) | 31 | TypeSystem + Utils 主导 |
| PackageManagement | 63 | TypeSystem 主导 + 少量 Refactoring |
| Analysis (UnitTesting等) | 87 | TypeSystem 主导 |
| AvalonEdit.AddIn | 21 | TypeSystem + Semantics |
| LanguageServerClient | 6 | 已有适配器 |
| XmlEditor | 16 | 仅 Xml，替换为 System.Xml.Linq |
| WixBinding | 11 | 仅 Xml，替换为 System.Xml.Linq |
| AspNet.Mvc | 10 | TypeSystem + CSharp.Completion |

### 困难（深度使用 NRefactory 特有功能）— 5 个项目
| 项目 | using数 | 主要障碍 |
|------|---------|---------|
| CSharpBinding | 228 | 使用所有子系统，已有 Roslyn 适配器框架 |
| XamlBinding | 53 | NRefactory.Xml 增量解析器 + CSharp Resolver |
| Debugger | 65 | ExpressionEvaluationVisitor + 旧版 Ast |
| ILSpyAddIn | 22 | CSharpParser + Cecil + Documentation |
| FormsDesigner | 10 | Editor + CSharp Refactoring 代码生成 |

## ADDED Requirements

### Requirement: TypeSystem 接口层独立项目

系统 SHALL 将 NRefactory.TypeSystem 中的核心接口提取到独立项目 `ICSharpCode.TypeSystem.Abstractions`，使消费者项目仅依赖接口而非 NRefactory 实现。

#### Scenario: 项目引用 TypeSystem 接口层
- **WHEN** 项目仅需 ICompilation, IType, IMember 等接口
- **THEN** 项目引用 `ICSharpCode.TypeSystem.Abstractions` 而非 `ICSharpCode.NRefactory`

#### Scenario: 接口层兼容性
- **WHEN** 现有代码使用 `ICSharpCode.NRefactory.TypeSystem.IType`
- **THEN** 可通过 type alias 或 namespace 重定向无缝迁移到 `ICSharpCode.TypeSystem.Abstractions.IType`

### Requirement: NRefactory.Xml 替换

系统 SHALL 将 NRefactory.Xml 的增量 XML 解析器替换为 System.Xml.Linq 或自定义实现。

#### Scenario: XmlEditor 使用新 XML 解析器
- **WHEN** XmlEditor 或 WixBinding 解析 XML 文档
- **THEN** 使用 System.Xml.Linq.XDocument 替代 NRefactory.Xml.AXmlParser

### Requirement: Utils 工具类替换

系统 SHALL 将 NRefactory.Utils 中的工具类替换为独立实现或 C# 内置替代。

#### Scenario: EmptyList 替换
- **WHEN** 代码使用 `EmptyList<T>.Instance`
- **THEN** 替换为 `Array.Empty<T>()` 或 `Enumerable.Empty<T>()`

#### Scenario: TreeTraversal 替换
- **WHEN** 代码使用 `TreeTraversal.PreOrder()` / `PostOrder()`
- **THEN** 替换为自定义扩展方法

### Requirement: 旧版 Ast/PrettyPrinter 代码重写

系统 SHALL 将使用 NRefactory 3.x 旧版 Ast/PrettyPrinter 的代码重写为使用 Roslyn API。

#### Scenario: VB 转换器重写
- **WHEN** ConvertBuffer/CSharpConvertBuffer 执行 VB→C# 转换
- **THEN** 使用 Roslyn API 或外部工具替代旧版 NRefactory.Ast/PrettyPrinter

### Requirement: Debugger 表达式求值重写

系统 SHALL 将 Debugger 的 ExpressionEvaluationVisitor 从 NRefactory CSharp Resolver 迁移到 Roslyn 脚本 API。

#### Scenario: 调试时表达式求值
- **WHEN** 用户在调试器监视窗口中输入表达式
- **THEN** 使用 Roslyn `CSharpScript.EvaluateAsync()` 或 `SemanticModel` 替代 NRefactory ExpressionEvaluationVisitor

## MODIFIED Requirements

### Requirement: CSharpBinding 完全脱离 NRefactory

CSharpBinding 项目 SHALL 在 Roslyn 后端激活时完全不依赖 NRefactory 运行时。

- NRefactory Parser/Completion/Refactoring/Formatter 代码保留但仅在 NRefactory 后端激活时使用
- Roslyn 后端激活时，所有语言服务由 Roslyn 适配器提供
- csproj 中 NRefactory 引用可标记为条件编译或保留为可选依赖

## REMOVED Requirements

### Requirement: NRefactory 库整体保留
**Reason**: 迁移完成后不再需要 NRefactory 库作为整体依赖
**Migration**: TypeSystem 接口层提取为独立项目，具体实现由 Roslyn 适配器替代
