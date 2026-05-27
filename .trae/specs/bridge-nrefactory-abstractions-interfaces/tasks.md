# Tasks

## 阶段一：统一接口定义中的 `new` 关键字策略

- [ ] Task 1: 审查并修正所有接口文件的 `new` 关键字使用
  - [ ] 1.1 修正 IType.cs — 移除简单接口返回类型上的 `new`（GetDefinition, DeclaringType, ToTypeReference 等），仅保留枚举和 IList 上的 `new`
  - [ ] 1.2 修正 IEntity.cs — 移除 `new string Name`（string 类型相同，直接继承），移除 `new ITypeDefinition DeclaringTypeDefinition`、`new IType DeclaringType`、`new IAssembly ParentAssembly`（C# 9 协变），保留枚举和 IList 上的 `new`
  - [ ] 1.3 修正 ICompilation.cs — 移除简单接口返回类型上的 `new`
  - [ ] 1.4 修正 ITypeReference.cs — 移除简单接口返回类型上的 `new`
  - [ ] 1.5 修正 ITypeParameter.cs — 移除简单接口返回类型上的 `new`
  - [ ] 1.6 修正 IMethod.cs — 移除简单接口返回类型上的 `new`
  - [ ] 1.7 修正 IProperty.cs — 移除简单接口返回类型上的 `new`
  - [ ] 1.8 修正 IField.cs — 移除简单接口返回类型上的 `new`
  - [ ] 1.9 修正 IEvent.cs — 移除简单接口返回类型上的 `new`
  - [ ] 1.10 修正 ITypeDefinition.cs — 移除简单接口返回类型上的 `new`
  - [ ] 1.11 修正 IAttribute.cs — 移除简单接口返回类型上的 `new`
  - [ ] 1.12 修正 IAssembly.cs — 移除简单接口返回类型上的 `new`
  - [ ] 1.13 修正 INamespace.cs — 移除简单接口返回类型上的 `new`
  - [ ] 1.14 修正 IProjectContent.cs — 移除简单接口返回类型上的 `new`
  - [ ] 1.15 修正 IUnresolvedFile.cs — 移除简单接口返回类型上的 `new`
  - [ ] 1.16 修正 IParameter.cs — 移除简单接口返回类型上的 `new`
  - [ ] 1.17 修正 IParameterizedMember.cs — 移除简单接口返回类型上的 `new`
  - [ ] 1.18 修正 ICodeContext.cs — 移除简单接口返回类型上的 `new`
  - [ ] 1.19 修正 IVariable.cs — 移除 `new string Name`（直接继承），保留 `new IType Type`（C# 9 协变可处理，但需验证）

## 阶段二：修复基类的显式接口实现

- [ ] Task 2: 完善 AbstractResolvedEntity 的显式接口实现
  - [ ] 2.1 添加 `ICSharpCode.TypeSystem.INamedElement` 成员的显式实现（Name, FullName, ReflectionName, Namespace）
  - [ ] 2.2 添加 `ICSharpCode.TypeSystem.IUnresolvedEntity` 成员的显式实现（如果该类也实现 IUnresolvedEntity）
  - [ ] 2.3 验证现有显式实现的正确性（DomRegion 转换、CastList 使用等）

- [ ] Task 3: 完善 AbstractResolvedMember 的显式接口实现
  - [ ] 3.1 添加 `ICSharpCode.TypeSystem.IMember` 所有未实现成员的显式实现
  - [ ] 3.2 验证 MemberDefinition、UnresolvedMember、ReturnType 的协变返回是否正确

- [ ] Task 4: 完善 AbstractType 的显式接口实现
  - [ ] 4.1 添加 `ICSharpCode.TypeSystem.IType` 所有 Get* 方法的显式实现（GetNestedTypes, GetMethods, GetConstructors 等）
  - [ ] 4.2 验证 TypeArguments 的 CastList 适配
  - [ ] 4.3 验证 DirectBaseTypes 的 IEnumerable 协变

- [ ] Task 5: 完善 AbstractUnresolvedEntity 的显式接口实现
  - [ ] 5.1 添加 `ICSharpCode.TypeSystem.IUnresolvedEntity` 所有成员的显式实现
  - [ ] 5.2 处理 DomRegion、IList<IUnresolvedAttribute> 等不兼容类型

- [ ] Task 6: 完善 AbstractUnresolvedMember 的显式接口实现
  - [ ] 6.1 添加 `ICSharpCode.TypeSystem.IUnresolvedMember` 所有成员的显式实现
  - [ ] 6.2 处理 ITypeReference ReturnType、IList<IMemberReference> ExplicitInterfaceImplementations 等

- [ ] Task 7: 完善 AbstractResolvedTypeParameter 的显式接口实现
  - [ ] 7.1 添加 `ICSharpCode.TypeSystem.ITypeParameter` 所有成员的显式实现
  - [ ] 7.2 处理 IType 继承链中的所有 Abstractions 成员

## 阶段三：修复派生实现类

- [ ] Task 8: 修复 SpecializedMember 及其子类的编译错误
  - [ ] 8.1 SpecializedMember.cs — 添加缺失的显式接口实现
  - [ ] 8.2 SpecializedMethod.cs — 添加缺失的显式接口实现
  - [ ] 8.3 SpecializedProperty.cs — 添加缺失的显式接口实现
  - [ ] 8.4 SpecializedField.cs — 添加缺失的显式接口实现
  - [ ] 8.5 SpecializedEvent.cs — 添加缺失的显式接口实现

- [ ] Task 9: 修复 DefaultResolved* 类的编译错误
  - [ ] 9.1 DefaultResolvedTypeDefinition.cs — 添加缺失的显式接口实现
  - [ ] 9.2 DefaultResolvedMethod.cs — 添加缺失的显式接口实现
  - [ ] 9.3 DefaultResolvedParameter.cs — 添加缺失的显式接口实现

- [ ] Task 10: 修复 DefaultUnresolved* 类的编译错误
  - [ ] 10.1 DefaultUnresolvedTypeDefinition.cs — 添加缺失的显式接口实现
  - [ ] 10.2 DefaultUnresolvedMethod.cs — 添加缺失的显式接口实现
  - [ ] 10.3 DefaultUnresolvedProperty.cs — 添加缺失的显式接口实现
  - [ ] 10.4 DefaultUnresolvedEvent.cs — 添加缺失的显式接口实现
  - [ ] 10.5 DefaultUnresolvedField.cs — 添加缺失的显式接口实现
  - [ ] 10.6 DefaultUnresolvedAssembly.cs — 添加缺失的显式接口实现
  - [ ] 10.7 DefaultUnresolvedAttribute.cs — 添加缺失的显式接口实现
  - [ ] 10.8 DefaultUnresolvedParameter.cs — 添加缺失的显式接口实现
  - [ ] 10.9 DefaultUnresolvedTypeParameter.cs — 添加缺失的显式接口实现

- [ ] Task 11: 修复其他实现类的编译错误
  - [ ] 11.1 ParameterizedType.cs — 添加缺失的显式接口实现
  - [ ] 11.2 DummyTypeParameter.cs — 添加缺失的显式接口实现
  - [ ] 11.3 MergedNamespace.cs — 添加缺失的显式接口实现
  - [ ] 11.4 SimpleCompilation.cs — 添加缺失的显式接口实现
  - [ ] 11.5 DefaultMemberReference.cs — 添加缺失的显式接口实现
  - [ ] 11.6 各种 TypeReference 实现类（ArrayTypeReference, KnownTypeReference 等）
  - [ ] 11.7 Documentation 命名空间中的类
  - [ ] 11.8 其他有编译错误的类

## 阶段四：构建验证

- [ ] Task 12: 完整构建验证
  - [ ] 12.1 `dotnet build ICSharpCode.NRefactory.csproj` — 0 错误
  - [ ] 12.2 `dotnet build ICSharpCode.NRefactory.CSharp.csproj` — 0 错误
  - [ ] 12.3 验证下游项目（ICSharpCode.SharpDevelop 等）仍可编译

# Task Dependencies

- Task 1 是所有后续任务的前置条件（接口定义必须先正确）
- Task 2-7 相互独立，可并行（但都依赖 Task 1）
- Task 8-11 依赖 Task 2-7（基类修复后，派生类的错误模式会更清晰）
- Task 12 依赖所有前置任务
- 建议按 阶段一 → 阶段二 → 阶段三 → 阶段四 的顺序执行，每阶段内可并行
