# 桥接 NRefactory 与 Abstractions 接口继承 Spec

## Why

NRefactory 在 `ICSharpCode.NRefactory.TypeSystem` 命名空间定义了类型系统接口（IType, IMember, IEntity 等），而 `ICSharpCode.TypeSystem.Abstractions` 项目在 `ICSharpCode.TypeSystem` 命名空间定义了同名接口。由于 C# 名义类型系统，这两个命名空间中的同名接口是完全不同的类型，导致无法将 NRefactory 对象直接用作 Abstractions 对象，这是整个 NRefactory 移除工作的关键阻塞点。

## What Changes

- 使 NRefactory 接口继承自 Abstractions 对应接口，使任何 NRefactory 对象同时满足两个接口
- 使用 `new` 关键字隐藏返回类型不兼容的成员（枚举、`IList<T>` 等）
- 依赖 C# 9 协变返回类型处理简单接口返回类型（IType, IMember 等）
- 在实现类中添加显式接口实现以桥接不兼容的成员签名
- **BREAKING**: 所有 NRefactory 实现类必须满足 Abstractions 基接口的所有成员

## Impact

- Affected specs: `remove-nrefactory-dependency` (Task 1.6)
- Affected code:
  - `src/Libraries/ICSharpCode.NRefactory/ICSharpCode.NRefactory/` — 所有接口文件和实现类
  - `src/Libraries/ICSharpCode.TypeSystem.Abstractions/` — 被继承的接口定义

## 当前状态

接口文件已部分修改（约 25 个接口文件已添加继承关系），三个基类（AbstractResolvedEntity, AbstractResolvedMember, AbstractType）已添加部分显式接口实现。但构建仍有 **1368 个错误**（CS0738: 778, CS0535: 456, CS0539: 106, CS0246: 22, CS0103: 4, CS9333: 2）。

## 根本原因分析

### 错误类型 1: CS0738 — 返回类型不匹配

实现类的属性/方法返回 NRefactory 类型，但 Abstractions 基接口期望 Abstractions 类型。

**示例**: `SpecializedMember.SymbolKind` 返回 `NRefactory.SymbolKind`，但 `ICSharpCode.TypeSystem.ISymbol.SymbolKind` 期望 `ICSharpCode.TypeSystem.SymbolKind`。

**解决策略**: 在基类中添加显式接口实现，将 NRefactory 类型转换为 Abstractions 类型。

### 错误类型 2: CS0535 — 缺少接口成员实现

实现类未实现 Abstractions 基接口的某些成员。

**示例**: `AbstractType` 未实现 `ICSharpCode.TypeSystem.IType` 的 `GetNestedTypes`, `GetMethods` 等方法（因为 NRefactory IType 使用 `new` 隐藏了这些方法，但 Abstractions 基接口成员仍需实现）。

**解决策略**: 在基类中添加显式接口实现，委托给 NRefactory 的实现方法。

### 错误类型 3: CS0539 — 显式接口实现引用不存在的成员

之前添加的显式接口实现引用了 Abstractions 接口中不存在的成员。

**解决策略**: 移除或修正这些显式接口实现。

## `new` 关键字策略（统一规则）

| 成员返回类型 | 是否使用 `new` | 原因 |
|---|---|---|
| 枚举（SymbolKind, TypeKind, Accessibility, EntityType） | 是 | 枚举不能隐式转换 |
| `IList<T>`（T 为接口类型） | 是 | `IList<T>` 不支持协变 |
| 简单接口类型（IType, IMember, ITypeDefinition 等） | 否 | C# 9 协变返回类型自动处理 |
| `IEnumerable<T>`（T 为接口类型） | 否 | `IEnumerable<out T>` 支持协变 |
| 值类型（DomRegion, FullTypeName 等） | 视情况 | 结构相同但不同类型，需显式实现 |
| 参数类型不同的方法 | 否 | 是不同的方法签名，不冲突 |
| `string`, `bool`, `int` 等基元类型 | 否 | 类型相同，直接继承 |

## ADDED Requirements

### Requirement: NRefactory 接口继承 Abstractions 接口

系统 SHALL 使所有 NRefactory TypeSystem 接口继承自对应的 Abstractions 接口，使 NRefactory 对象可同时用作 Abstractions 对象。

#### Scenario: NRefactory IType 作为 Abstractions IType 使用
- **WHEN** 代码期望 `ICSharpCode.TypeSystem.IType` 类型的参数
- **THEN** 可传入 `ICSharpCode.NRefactory.TypeSystem.IType` 的实现对象，无需转换

#### Scenario: 枚举类型转换
- **WHEN** 通过 Abstractions 接口访问 `SymbolKind` 属性
- **THEN** 返回 `ICSharpCode.TypeSystem.SymbolKind` 枚举值，与 NRefactory 的 `SymbolKind` 值一一对应

### Requirement: 显式接口实现桥接

系统 SHALL 在基类中提供显式接口实现，将 NRefactory 返回类型桥接到 Abstractions 返回类型。

#### Scenario: 枚举桥接
- **WHEN** 通过 Abstractions 接口访问 `ISymbol.SymbolKind`
- **THEN** 显式实现将 NRefactory `SymbolKind` 强制转换为 Abstractions `SymbolKind`（通过 `(byte)` 中间转换）

#### Scenario: IList 桥接
- **WHEN** 通过 Abstractions 接口访问 `IEntity.Attributes`
- **THEN** 显式实现使用 `CastList<TSource, TTarget>` 将 `IList<NRefactory.IAttribute>` 适配为 `IList<Abstractions.IAttribute>`

#### Scenario: 值类型桥接
- **WHEN** 通过 Abstractions 接口访问 `IEntity.Region`
- **THEN** 显式实现构造 `ICSharpCode.TypeSystem.DomRegion` 从 NRefactory `DomRegion` 的字段值

### Requirement: ICSharpCode.NRefactory 项目编译通过

系统 SHALL 使 ICSharpCode.NRefactory 项目在接口继承修改后编译通过，0 个错误。

#### Scenario: 构建成功
- **WHEN** 执行 `dotnet build ICSharpCode.NRefactory.csproj`
- **THEN** 构建成功，0 个错误，0 个警告（除原有警告外）

## MODIFIED Requirements

### Requirement: NRefactory 接口定义

所有 NRefactory TypeSystem 接口 SHALL 继承自对应的 Abstractions 接口，并使用 `new` 关键字隐藏返回类型不兼容的成员。

需要修改的接口文件（按依赖顺序）：
1. `INamedElement.cs` — 继承 `ICSharpCode.TypeSystem.INamedElement`
2. `Accessibility.cs` — `IHasAccessibility` 继承 `ICSharpCode.TypeSystem.IHasAccessibility`
3. `ISymbol.cs` — `ISymbol` 继承 `ICSharpCode.TypeSystem.ISymbol`
4. `ICompilation.cs` — `ICompilation` 继承 `ICSharpCode.TypeSystem.ICompilation`
5. `ITypeReference.cs` — `ITypeReference`/`ITypeResolveContext` 继承 Abstractions 对应接口
6. `IType.cs` — `IType` 继承 `ICSharpCode.TypeSystem.IType`
7. `IEntity.cs` — `IEntity`/`IUnresolvedEntity` 继承 Abstractions 对应接口
8. `IVariable.cs` — `IVariable` 继承 `ICSharpCode.TypeSystem.IVariable`
9. `IParameter.cs` — `IParameter`/`IUnresolvedParameter` 继承 Abstractions 对应接口
10. `ITypeParameter.cs` — `ITypeParameter`/`IUnresolvedTypeParameter` 继承 Abstractions 对应接口
11. `IUnresolvedMember.cs` — `IUnresolvedMember`/`IUnresolvedParameterizedMember` 继承 Abstractions 对应接口
12. `IMember.cs` — `IMember`/`IMemberReference` 继承 Abstractions 对应接口
13. `IParameterizedMember.cs` — `IParameterizedMember` 继承 Abstractions 对应接口
14. `IMethod.cs` — `IMethod`/`IUnresolvedMethod` 继承 Abstractions 对应接口
15. `IProperty.cs` — `IProperty`/`IUnresolvedProperty` 继承 Abstractions 对应接口
16. `IField.cs` — `IField`/`IUnresolvedField` 继承 Abstractions 对应接口
17. `IEvent.cs` — `IEvent`/`IUnresolvedEvent` 继承 Abstractions 对应接口
18. `ITypeDefinition.cs` — `ITypeDefinition`/`IUnresolvedTypeDefinition` 继承 Abstractions 对应接口
19. `IAttribute.cs` — `IAttribute`/`IUnresolvedAttribute` 继承 Abstractions 对应接口
20. `IAssembly.cs` — `IAssembly`/`IUnresolvedAssembly`/`IAssemblyReference` 继承 Abstractions 对应接口
21. `INamespace.cs` — `INamespace` 继承 Abstractions 对应接口
22. `ICodeContext.cs` — `ICodeContext` 继承 Abstractions 对应接口
23. `IConstantValue.cs` — `IConstantValue` 继承 Abstractions 对应接口
24. `ISolutionSnapshot.cs` — `ISolutionSnapshot` 继承 Abstractions 对应接口
25. `IProjectContent.cs` — `IProjectContent` 继承 Abstractions 对应接口
26. `IUnresolvedFile.cs` — `IUnresolvedFile` 继承 Abstractions 对应接口

## REMOVED Requirements

无。
