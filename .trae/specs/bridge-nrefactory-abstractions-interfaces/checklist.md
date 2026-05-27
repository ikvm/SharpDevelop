# Checklist

## 阶段一：接口定义

- [ ] IType.cs 的 `new` 关键字策略正确（仅枚举和 IList 使用 `new`）
- [ ] IEntity.cs 的 `new` 关键字策略正确
- [ ] ICompilation.cs 的 `new` 关键字策略正确
- [ ] ITypeReference.cs 的 `new` 关键字策略正确
- [ ] ITypeParameter.cs 的 `new` 关键字策略正确
- [ ] IMethod.cs 的 `new` 关键字策略正确
- [ ] IProperty.cs 的 `new` 关键字策略正确
- [ ] IField.cs 的 `new` 关键字策略正确
- [ ] IEvent.cs 的 `new` 关键字策略正确
- [ ] ITypeDefinition.cs 的 `new` 关键字策略正确
- [ ] IAttribute.cs 的 `new` 关键字策略正确
- [ ] IAssembly.cs 的 `new` 关键字策略正确
- [ ] INamespace.cs 的 `new` 关键字策略正确
- [ ] IProjectContent.cs 的 `new` 关键字策略正确
- [ ] IUnresolvedFile.cs 的 `new` 关键字策略正确
- [ ] IParameter.cs 的 `new` 关键字策略正确
- [ ] IParameterizedMember.cs 的 `new` 关键字策略正确
- [ ] ICodeContext.cs 的 `new` 关键字策略正确
- [ ] IVariable.cs 的 `new` 关键字策略正确
- [ ] 所有接口文件都正确继承 Abstractions 对应接口

## 阶段二：基类显式接口实现

- [ ] AbstractResolvedEntity 实现了所有 Abstractions IEntity/ISymbol/ICompilationProvider/INamedElement/IHasAccessibility 成员
- [ ] AbstractResolvedMember 实现了所有 Abstractions IMember 成员
- [ ] AbstractType 实现了所有 Abstractions IType 成员
- [ ] AbstractUnresolvedEntity 实现了所有 Abstractions IUnresolvedEntity 成员
- [ ] AbstractUnresolvedMember 实现了所有 Abstractions IUnresolvedMember 成员
- [ ] AbstractResolvedTypeParameter 实现了所有 Abstractions ITypeParameter 成员

## 阶段三：派生类修复

- [ ] SpecializedMember 及其子类编译无错误
- [ ] DefaultResolved* 类编译无错误
- [ ] DefaultUnresolved* 类编译无错误
- [ ] ParameterizedType, DummyTypeParameter, MergedNamespace 等编译无错误
- [ ] TypeReference 实现类编译无错误
- [ ] Documentation 命名空间中的类编译无错误

## 阶段四：构建验证

- [ ] `dotnet build ICSharpCode.NRefactory.csproj` — 0 错误
- [ ] `dotnet build ICSharpCode.NRefactory.CSharp.csproj` — 0 错误
- [ ] NRefactory 对象可通过 Abstractions 接口类型使用（类型兼容性验证）
