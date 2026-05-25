// Copyright (c) 2010-2013 AlphaSierraPapa for the SharpDevelop Team
//
// Permission is hereby granted, free of charge, to any person obtaining a copy of this
// software and associated documentation files (the "Software"), to deal in the Software
// without restriction, including without limitation the rights to use, copy, modify, merge,
// publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons
// to whom the Software is furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in all copies or
// substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED,
// INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR
// PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE
// FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR
// OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER
// DEALINGS IN THE SOFTWARE.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using ICSharpCode.NRefactory;
using ICSharpCode.NRefactory.TypeSystem;
using ICSharpCode.NRefactory.TypeSystem.Implementation;
using ICSharpCode.NRefactory.Utils;

using Microsoft.CodeAnalysis;

using NRTypeKind = ICSharpCode.NRefactory.TypeSystem.TypeKind;
using NRAccessibility = ICSharpCode.NRefactory.TypeSystem.Accessibility;
using NRSpecialType = ICSharpCode.NRefactory.TypeSystem.SpecialType;
using RoslynTypeKind = Microsoft.CodeAnalysis.TypeKind;

namespace CSharpBinding.Parser.Roslyn.Adapters
{
	/// <summary>
	/// 将 Roslyn ITypeSymbol 适配为 NRefactory IType 接口。
	/// 这是类型适配的基础类，处理所有类型的通用属性和方法。
	/// 对于命名类型（INamedTypeSymbol），应使用 RoslynTypeDefinitionAdapter。
	/// </summary>
	public class RoslynTypeAdapter : IType
	{
		readonly ITypeSymbol roslynType;
		readonly RoslynCompilationAdapter compilation;

		/// <summary>
		/// 创建 RoslynTypeAdapter 实例。
		/// </summary>
		public RoslynTypeAdapter(ITypeSymbol roslynType, RoslynCompilationAdapter compilation)
		{
			this.roslynType = roslynType ?? throw new ArgumentNullException(nameof(roslynType));
			this.compilation = compilation ?? throw new ArgumentNullException(nameof(compilation));
		}

		/// <summary>
		/// 获取底层 Roslyn ITypeSymbol。
		/// </summary>
		public ITypeSymbol RoslynTypeSymbol => roslynType;

		/// <summary>
		/// 获取类型名称（不含命名空间）。
		/// </summary>
		public string Name => roslynType.Name;

		/// <summary>
		/// 获取完全限定名称（含命名空间，不含类型参数）。
		/// </summary>
		public string FullName => roslynType.ToDisplayString(new SymbolDisplayFormat(
			globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Omitted,
			typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
			genericsOptions: SymbolDisplayGenericsOptions.None));

		/// <summary>
		/// 获取反射名称。
		/// </summary>
		public string ReflectionName {
			get {
				var namedType = roslynType as INamedTypeSymbol;
				if (namedType != null) {
					var sb = new StringBuilder();
					BuildReflectionName(namedType, sb);
					return sb.ToString();
				}
				return roslynType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat
					.WithGlobalNamespaceStyle(SymbolDisplayGlobalNamespaceStyle.Omitted));
			}
		}

		/// <summary>
		/// 构建反射名称，格式与 NRefactory 一致。
		/// </summary>
		void BuildReflectionName(INamedTypeSymbol type, StringBuilder sb)
		{
			if (type.ContainingType != null) {
				BuildReflectionName(type.ContainingType, sb);
				sb.Append('+');
				sb.Append(type.Name);
			} else {
				if (!string.IsNullOrEmpty(type.ContainingNamespace?.Name)) {
					sb.Append(type.ContainingNamespace.ToDisplayString());
					sb.Append('.');
				}
				sb.Append(type.Name);
			}
			if (type.Arity > 0) {
				sb.Append('`');
				sb.Append(type.Arity);
			}
		}

		/// <summary>
		/// 获取包含此类型的命名空间名称。
		/// </summary>
		public string Namespace {
			get {
				if (roslynType.ContainingNamespace == null)
					return string.Empty;
				var ns = roslynType.ContainingNamespace.ToDisplayString();
				return ns == "<global namespace>" ? string.Empty : ns;
			}
		}

		/// <summary>
		/// 获取类型种类。
		/// </summary>
		public NRTypeKind Kind => RoslynTypeSystemMapper.MapTypeKind(roslynType.TypeKind);

		/// <summary>
		/// 获取是否为引用类型。
		/// </summary>
		public bool? IsReferenceType {
			get {
				switch (roslynType.TypeKind) {
					case RoslynTypeKind.Class:
					case RoslynTypeKind.Interface:
					case RoslynTypeKind.Delegate:
					case RoslynTypeKind.Dynamic:
					case RoslynTypeKind.Module:
						return true;
					case RoslynTypeKind.Struct:
					case RoslynTypeKind.Enum:
						return false;
					case RoslynTypeKind.TypeParameter:
						return null;
					default:
						return null;
				}
			}
		}

		/// <summary>
		/// 获取底层类型定义。
		/// 对于命名类型返回 RoslynTypeDefinitionAdapter，否则返回 null。
		/// </summary>
		public ITypeDefinition GetDefinition()
		{
			var namedType = roslynType as INamedTypeSymbol;
			if (namedType != null && namedType.TypeKind != RoslynTypeKind.Error)
				return AdapterCache.GetOrCreateTypeDefinitionAdapter(namedType, compilation);
			return null;
		}

		/// <summary>
		/// 获取声明类型（对于嵌套类型为外层类型）。
		/// </summary>
		public IType DeclaringType {
			get {
				if (roslynType.ContainingType != null)
					return AdapterCache.GetOrCreateTypeAdapter(roslynType.ContainingType, compilation);
				return null;
			}
		}

		/// <summary>
		/// 获取类型参数数量。
		/// </summary>
		public int TypeParameterCount {
			get {
				var namedType = roslynType as INamedTypeSymbol;
				return namedType?.Arity ?? 0;
			}
		}

		/// <summary>
		/// 获取类型参数列表。
		/// 如果是未参数化的泛型类型定义，返回类型参数自身。
		/// </summary>
		public IList<IType> TypeArguments {
			get {
				var namedType = roslynType as INamedTypeSymbol;
				if (namedType == null || namedType.Arity == 0)
					return EmptyList<IType>.Instance;

				var args = new List<IType>(namedType.Arity);
				foreach (var arg in namedType.TypeArguments) {
					args.Add(AdapterCache.GetOrCreateTypeAdapter(arg, compilation));
				}
				return args.AsReadOnly();
			}
		}

		/// <summary>
		/// 获取是否为已参数化的泛型类型实例。
		/// </summary>
		public bool IsParameterized {
			get {
				var namedType = roslynType as INamedTypeSymbol;
				return namedType != null && namedType.IsGenericType && !namedType.IsDefinition;
			}
		}

		/// <summary>
		/// 接受类型访问者。
		/// </summary>
		public IType AcceptVisitor(TypeVisitor visitor)
		{
			return visitor.VisitOtherType(this);
		}

		/// <summary>
		/// 访问子类型并重建类型。
		/// </summary>
		public IType VisitChildren(TypeVisitor visitor)
		{
			// 对于简单类型，没有子类型需要访问
			return this;
		}

		/// <summary>
		/// 获取直接基类型列表（包括接口）。
		/// </summary>
		public IEnumerable<IType> DirectBaseTypes {
			get {
				var namedType = roslynType as INamedTypeSymbol;
				if (namedType == null)
					return Enumerable.Empty<IType>();

				var bases = new List<IType>();
				if (namedType.BaseType != null) {
					bases.Add(AdapterCache.GetOrCreateTypeAdapter(namedType.BaseType, compilation));
				}
				foreach (var iface in namedType.Interfaces) {
					bases.Add(AdapterCache.GetOrCreateTypeAdapter(iface, compilation));
				}
				return bases;
			}
		}

		/// <summary>
		/// 创建类型引用。
		/// </summary>
		public ITypeReference ToTypeReference()
		{
			return new RoslynTypeReference(roslynType);
		}

		/// <summary>
		/// 获取类型参数替换。
		/// </summary>
		public TypeParameterSubstitution GetSubstitution()
		{
			if (!IsParameterized)
				return TypeParameterSubstitution.Identity;

			var namedType = roslynType as INamedTypeSymbol;
			if (namedType == null)
				return TypeParameterSubstitution.Identity;

			var classTypeArgs = new List<IType>();
			foreach (var arg in namedType.TypeArguments) {
				classTypeArgs.Add(AdapterCache.GetOrCreateTypeAdapter(arg, compilation));
			}
			return new TypeParameterSubstitution(classTypeArgs, null);
		}

		/// <summary>
		/// 获取包含方法类型参数替换的类型参数替换。
		/// </summary>
		public TypeParameterSubstitution GetSubstitution(IList<IType> methodTypeArguments)
		{
			var classSubst = GetSubstitution();
			if (methodTypeArguments == null || methodTypeArguments.Count == 0)
				return classSubst;
			return new TypeParameterSubstitution(classSubst.ClassTypeArguments, methodTypeArguments);
		}

		/// <summary>
		/// 获取嵌套类型。
		/// </summary>
		public IEnumerable<IType> GetNestedTypes(Predicate<ITypeDefinition> filter = null, GetMemberOptions options = GetMemberOptions.None)
		{
			var namedType = roslynType as INamedTypeSymbol;
			if (namedType == null)
				return Enumerable.Empty<IType>();

			var result = new List<IType>();
			foreach (var nested in namedType.GetTypeMembers()) {
				var typeDef = AdapterCache.GetOrCreateTypeDefinitionAdapter(nested, compilation);
				if (filter == null || filter(typeDef)) {
					if ((options & GetMemberOptions.ReturnMemberDefinitions) != 0)
						result.Add(typeDef);
					else
						result.Add(AdapterCache.GetOrCreateTypeAdapter(nested, compilation));
				}
			}
			return result;
		}

		/// <summary>
		/// 获取具有指定类型参数数量的嵌套类型。
		/// </summary>
		public IEnumerable<IType> GetNestedTypes(IList<IType> typeArguments, Predicate<ITypeDefinition> filter = null, GetMemberOptions options = GetMemberOptions.None)
		{
			return GetNestedTypes(filter, options)
				.Where(t => t.TypeParameterCount == (typeArguments?.Count ?? 0));
		}

		/// <summary>
		/// 获取构造函数。
		/// </summary>
		public IEnumerable<IMethod> GetConstructors(Predicate<IUnresolvedMethod> filter = null, GetMemberOptions options = GetMemberOptions.IgnoreInheritedMembers)
		{
			var namedType = roslynType as INamedTypeSymbol;
			if (namedType == null)
				return Enumerable.Empty<IMethod>();

			return namedType.InstanceConstructors
				.Where(m => filter == null)
				.Select(m => (IMethod)new RoslynMemberAdapter(m, compilation))
				.Where(m => m != null);
		}

		/// <summary>
		/// 获取方法。
		/// </summary>
		public IEnumerable<IMethod> GetMethods(Predicate<IUnresolvedMethod> filter = null, GetMemberOptions options = GetMemberOptions.None)
		{
			var namedType = roslynType as INamedTypeSymbol;
			if (namedType == null)
				return Enumerable.Empty<IMethod>();

			var members = (options & GetMemberOptions.IgnoreInheritedMembers) != 0
				? namedType.GetMembers()
				: namedType.GetMembers();

			return members
				.OfType<IMethodSymbol>()
				.Where(m => m.MethodKind == MethodKind.Ordinary || m.MethodKind == MethodKind.DelegateInvoke
					|| m.MethodKind == MethodKind.ExplicitInterfaceImplementation)
				.Where(m => filter == null)
				.Select(m => (IMethod)new RoslynMemberAdapter(m, compilation));
		}

		/// <summary>
		/// 获取具有指定类型参数的泛型方法。
		/// </summary>
		public IEnumerable<IMethod> GetMethods(IList<IType> typeArguments, Predicate<IUnresolvedMethod> filter = null, GetMemberOptions options = GetMemberOptions.None)
		{
			return GetMethods(filter, options)
				.Where(m => typeArguments == null || typeArguments.Count == 0 || m.TypeParameters.Count == typeArguments.Count);
		}

		/// <summary>
		/// 获取属性。
		/// </summary>
		public IEnumerable<IProperty> GetProperties(Predicate<IUnresolvedProperty> filter = null, GetMemberOptions options = GetMemberOptions.None)
		{
			var namedType = roslynType as INamedTypeSymbol;
			if (namedType == null)
				return Enumerable.Empty<IProperty>();

			var members = (options & GetMemberOptions.IgnoreInheritedMembers) != 0
				? namedType.GetMembers()
				: namedType.GetMembers();

			return members
				.OfType<IPropertySymbol>()
				.Where(m => filter == null)
				.Select(m => (IProperty)new RoslynMemberAdapter(m, compilation));
		}

		/// <summary>
		/// 获取字段。
		/// </summary>
		public IEnumerable<IField> GetFields(Predicate<IUnresolvedField> filter = null, GetMemberOptions options = GetMemberOptions.None)
		{
			var namedType = roslynType as INamedTypeSymbol;
			if (namedType == null)
				return Enumerable.Empty<IField>();

			var members = (options & GetMemberOptions.IgnoreInheritedMembers) != 0
				? namedType.GetMembers()
				: namedType.GetMembers();

			return members
				.OfType<IFieldSymbol>()
				.Where(m => filter == null)
				.Select(m => (IField)new RoslynMemberAdapter(m, compilation));
		}

		/// <summary>
		/// 获取事件。
		/// </summary>
		public IEnumerable<IEvent> GetEvents(Predicate<IUnresolvedEvent> filter = null, GetMemberOptions options = GetMemberOptions.None)
		{
			var namedType = roslynType as INamedTypeSymbol;
			if (namedType == null)
				return Enumerable.Empty<IEvent>();

			var members = (options & GetMemberOptions.IgnoreInheritedMembers) != 0
				? namedType.GetMembers()
				: namedType.GetMembers();

			return members
				.OfType<IEventSymbol>()
				.Where(m => filter == null)
				.Select(m => (IEvent)new RoslynMemberAdapter(m, compilation));
		}

		/// <summary>
		/// 获取所有成员（字段、属性、方法、事件，不包括构造函数）。
		/// </summary>
		public IEnumerable<IMember> GetMembers(Predicate<IUnresolvedMember> filter = null, GetMemberOptions options = GetMemberOptions.None)
		{
			var result = new List<IMember>();
			result.AddRange(GetFields(null, options));
			result.AddRange(GetProperties(null, options));
			result.AddRange(GetMethods(null as Predicate<IUnresolvedMethod>, options));
			result.AddRange(GetEvents(null, options));
			return result;
		}

		/// <summary>
		/// 获取访问器方法。
		/// </summary>
		public IEnumerable<IMethod> GetAccessors(Predicate<IUnresolvedMethod> filter = null, GetMemberOptions options = GetMemberOptions.None)
		{
			var namedType = roslynType as INamedTypeSymbol;
			if (namedType == null)
				return Enumerable.Empty<IMethod>();

			var accessors = new List<IMethod>();
			foreach (var member in namedType.GetMembers()) {
				var method = member as IMethodSymbol;
				if (method != null && method.AssociatedSymbol != null) {
					accessors.Add(new RoslynMemberAdapter(method, compilation));
				}
			}
			return accessors;
		}

		/// <summary>
		/// 判断类型是否相等。
		/// </summary>
		public bool Equals(IType other)
		{
			var otherAdapter = other as RoslynTypeAdapter;
			if (otherAdapter != null)
				return SymbolEqualityComparer.Default.Equals(roslynType, otherAdapter.roslynType);
			return false;
		}

		public override int GetHashCode()
		{
			return SymbolEqualityComparer.Default.GetHashCode(roslynType);
		}

		public override bool Equals(object obj)
		{
			return Equals(obj as IType);
		}

		public override string ToString()
		{
			return roslynType.ToDisplayString();
		}
	}
}
