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

using ICSharpCode.NRefactory;
using ICSharpCode.NRefactory.TypeSystem;
using ICSharpCode.NRefactory.TypeSystem.Implementation;
using ICSharpCode.NRefactory.Documentation;
using ICSharpCode.NRefactory.Utils;

using Microsoft.CodeAnalysis;

using NRAccessibility = ICSharpCode.NRefactory.TypeSystem.Accessibility;
using NRSymbolKind = ICSharpCode.NRefactory.TypeSystem.SymbolKind;
using NRSymbolReference = ICSharpCode.NRefactory.TypeSystem.ISymbolReference;
using NRSpecialType = ICSharpCode.NRefactory.TypeSystem.SpecialType;
using RoslynISymbol = Microsoft.CodeAnalysis.ISymbol;
using RoslynSymbolKind = Microsoft.CodeAnalysis.SymbolKind;

namespace CSharpBinding.Parser.Roslyn.Adapters
{
	/// <summary>
	/// 将 Roslyn ISymbol 适配为 NRefactory IMember 接口。
	/// 这是一个通用的成员适配器，可以包装方法、属性、字段、事件等各种符号。
	/// </summary>
	public class RoslynMemberAdapter : IMember, IMethod, IProperty, IField, IEvent
	{
		readonly RoslynISymbol roslynSymbol;
		readonly RoslynCompilationAdapter compilation;

		/// <summary>
		/// 创建 RoslynMemberAdapter 实例。
		/// </summary>
		public RoslynMemberAdapter(RoslynISymbol roslynSymbol, RoslynCompilationAdapter compilation)
		{
			this.roslynSymbol = roslynSymbol ?? throw new ArgumentNullException(nameof(roslynSymbol));
			this.compilation = compilation ?? throw new ArgumentNullException(nameof(compilation));
		}

		/// <summary>
		/// 获取底层 Roslyn ISymbol。
		/// </summary>
		public RoslynISymbol RoslynSymbol => roslynSymbol;

		/// <summary>
		/// 获取所属编译对象。
		/// </summary>
		public ICompilation Compilation => compilation;

		/// <summary>
		/// 获取符号种类。
		/// </summary>
		public NRSymbolKind SymbolKind => RoslynTypeSystemMapper.MapSymbolKind(roslynSymbol.Kind, roslynSymbol);

		/// <summary>
		/// 获取成员名称。
		/// </summary>
		public string Name => roslynSymbol.Name;

		/// <summary>
		/// 获取完全限定名称。
		/// </summary>
		public string FullName {
			get {
				if (roslynSymbol.ContainingType != null)
					return roslynSymbol.ContainingType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat
						.WithGlobalNamespaceStyle(SymbolDisplayGlobalNamespaceStyle.Omitted)
						.WithGenericsOptions(SymbolDisplayGenericsOptions.None)) + "." + roslynSymbol.Name;
				if (roslynSymbol.ContainingNamespace != null)
					return roslynSymbol.ContainingNamespace.ToDisplayString() + "." + roslynSymbol.Name;
				return roslynSymbol.Name;
			}
		}

		/// <summary>
		/// 获取反射名称。
		/// </summary>
		public string ReflectionName {
			get {
				var method = roslynSymbol as IMethodSymbol;
				if (method != null) {
					var sb = new System.Text.StringBuilder();
					if (method.ContainingType != null)
						sb.Append(method.ContainingType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat
							.WithGlobalNamespaceStyle(SymbolDisplayGlobalNamespaceStyle.Omitted)
							.WithGenericsOptions(SymbolDisplayGenericsOptions.None)));
					else if (method.ContainingNamespace != null)
						sb.Append(method.ContainingNamespace.ToDisplayString());
					sb.Append('.');
					sb.Append(method.Name);
					if (method.Arity > 0) {
						sb.Append('`');
						sb.Append(method.Arity);
					}
					return sb.ToString();
				}
				return FullName;
			}
		}

		/// <summary>
		/// 获取命名空间。
		/// </summary>
		public string Namespace {
			get {
				if (roslynSymbol.ContainingNamespace == null)
					return string.Empty;
				var ns = roslynSymbol.ContainingNamespace.ToDisplayString();
				return ns == "<global namespace>" ? string.Empty : ns;
			}
		}

		/// <summary>
		/// 获取访问级别。
		/// </summary>
		public NRAccessibility Accessibility => RoslynTypeSystemMapper.MapAccessibility(roslynSymbol.DeclaredAccessibility);

		public bool IsPrivate => Accessibility == NRAccessibility.Private;
		public bool IsPublic => Accessibility == NRAccessibility.Public;
		public bool IsProtected => Accessibility == NRAccessibility.Protected;
		public bool IsInternal => Accessibility == NRAccessibility.Internal;
		public bool IsProtectedOrInternal => Accessibility == NRAccessibility.ProtectedOrInternal;
		public bool IsProtectedAndInternal => Accessibility == NRAccessibility.ProtectedAndInternal;

		/// <summary>
		/// 获取代码区域。
		/// </summary>
		public DomRegion Region {
			get {
				var loc = roslynSymbol.Locations.FirstOrDefault();
				if (loc == null)
					return DomRegion.Empty;
				var lineSpan = loc.GetLineSpan();
				return new DomRegion(lineSpan.Path, lineSpan.StartLinePosition.Line + 1, lineSpan.StartLinePosition.Character + 1);
			}
		}

		/// <summary>
		/// 获取代码体区域。
		/// </summary>
		public DomRegion BodyRegion => Region;

		/// <summary>
		/// 获取声明类型定义。
		/// </summary>
		public ITypeDefinition DeclaringTypeDefinition {
			get {
				if (roslynSymbol.ContainingType != null)
					return AdapterCache.GetOrCreateTypeDefinitionAdapter(roslynSymbol.ContainingType, compilation);
				return null;
			}
		}

		/// <summary>
		/// 获取声明类型。
		/// </summary>
		public IType DeclaringType {
			get {
				if (roslynSymbol.ContainingType != null)
					return AdapterCache.GetOrCreateTypeAdapter(roslynSymbol.ContainingType, compilation);
				return null;
			}
		}

		/// <summary>
		/// 获取父程序集。
		/// </summary>
		public IAssembly ParentAssembly {
			get {
				var asm = roslynSymbol.ContainingAssembly;
				if (asm != null)
					return AdapterCache.GetOrCreateAssemblyAdapter(asm, compilation);
				return compilation.MainAssembly;
			}
		}

		/// <summary>
		/// 获取特性列表。
		/// 当前实现返回空列表。
		/// </summary>
		public IList<IAttribute> Attributes => EmptyList<IAttribute>.Instance;

		/// <summary>
		/// 获取文档注释。
		/// </summary>
		public DocumentationComment Documentation {
			get {
				var xml = roslynSymbol.GetDocumentationCommentXml();
				if (string.IsNullOrEmpty(xml))
					return null;
				return new DocumentationComment(xml, new SimpleTypeResolveContext(compilation.MainAssembly));
			}
		}

		/// <summary>
		/// 获取是否为静态成员。
		/// </summary>
		public bool IsStatic => roslynSymbol.IsStatic;

		/// <summary>
		/// 获取是否为抽象成员。
		/// </summary>
		public bool IsAbstract => roslynSymbol.IsAbstract;

		/// <summary>
		/// 获取是否为密封成员。
		/// </summary>
		public bool IsSealed => roslynSymbol.IsSealed;

		/// <summary>
		/// 获取是否隐藏了基类成员。
		/// </summary>
		public bool IsShadowing {
			get {
				// Roslyn 中 IsHideBySignature 仅在 IMethodSymbol 上可用
				// 其他符号类型没有此属性
				return false;
			}
		}

		/// <summary>
		/// 获取是否为编译器生成的成员。
		/// </summary>
		public bool IsSynthetic => roslynSymbol.IsImplicitlyDeclared;

		/// <summary>
		/// 获取过时的实体类型。
		/// </summary>
		public EntityType EntityType => (EntityType)SymbolKind;

		// IMember 实现

		/// <summary>
		/// 获取成员定义（非特化版本）。
		/// </summary>
		public IMember MemberDefinition {
			get {
				var method = roslynSymbol as IMethodSymbol;
				if (method != null && method.OriginalDefinition != null && !SymbolEqualityComparer.Default.Equals(method, method.OriginalDefinition)) {
					return new RoslynMemberAdapter(method.OriginalDefinition, compilation);
				}
				return this;
			}
		}

		/// <summary>
		/// 获取未解析的成员。
		/// Roslyn 适配器不提供 IUnresolvedMember，返回 null。
		/// </summary>
		public IUnresolvedMember UnresolvedMember => null;

		/// <summary>
		/// 获取返回类型。
		/// </summary>
		public IType ReturnType {
			get {
				switch (roslynSymbol.Kind) {
					case RoslynSymbolKind.Method:
						return AdapterCache.GetOrCreateTypeAdapter(((IMethodSymbol)roslynSymbol).ReturnType, compilation);
					case RoslynSymbolKind.Property:
						return AdapterCache.GetOrCreateTypeAdapter(((IPropertySymbol)roslynSymbol).Type, compilation);
					case RoslynSymbolKind.Field:
						return AdapterCache.GetOrCreateTypeAdapter(((IFieldSymbol)roslynSymbol).Type, compilation);
					case RoslynSymbolKind.Event:
						return AdapterCache.GetOrCreateTypeAdapter(((IEventSymbol)roslynSymbol).Type, compilation);
					default:
						return NRSpecialType.UnknownType;
				}
			}
		}

		/// <summary>
		/// 获取实现的接口成员列表。
		/// </summary>
		public IList<IMember> ImplementedInterfaceMembers {
			get {
				var result = new List<IMember>();
				var method = roslynSymbol as IMethodSymbol;
				if (method != null) {
					foreach (var impl in method.ExplicitInterfaceImplementations) {
						result.Add(new RoslynMemberAdapter(impl, compilation));
					}
					if (method.ExplicitInterfaceImplementations.Length == 0) {
						foreach (var iface in method.ContainingType.AllInterfaces) {
							foreach (var ifaceMethod in iface.GetMembers(method.Name).OfType<IMethodSymbol>()) {
								if (method.ContainingType.FindImplementationForInterfaceMember(ifaceMethod) == method) {
									result.Add(new RoslynMemberAdapter(ifaceMethod, compilation));
								}
							}
						}
					}
				}
				var property = roslynSymbol as IPropertySymbol;
				if (property != null) {
					foreach (var impl in property.ExplicitInterfaceImplementations) {
						result.Add(new RoslynMemberAdapter(impl, compilation));
					}
				}
				var ev = roslynSymbol as IEventSymbol;
				if (ev != null) {
					foreach (var impl in ev.ExplicitInterfaceImplementations) {
						result.Add(new RoslynMemberAdapter(impl, compilation));
					}
				}
				return result.AsReadOnly();
			}
		}

		/// <summary>
		/// 获取是否为显式接口实现。
		/// </summary>
		public bool IsExplicitInterfaceImplementation {
			get {
				var method = roslynSymbol as IMethodSymbol;
				if (method != null)
					return method.ExplicitInterfaceImplementations.Length > 0;
				var property = roslynSymbol as IPropertySymbol;
				if (property != null)
					return property.ExplicitInterfaceImplementations.Length > 0;
				var ev = roslynSymbol as IEventSymbol;
				if (ev != null)
					return ev.ExplicitInterfaceImplementations.Length > 0;
				return false;
			}
		}

		/// <summary>
		/// 获取是否为虚成员。
		/// </summary>
		public bool IsVirtual => roslynSymbol.IsVirtual;

		/// <summary>
		/// 获取是否为重写成员。
		/// </summary>
		public bool IsOverride => roslynSymbol.IsOverride;

		/// <summary>
		/// 获取是否可被重写。
		/// </summary>
		public bool IsOverridable {
			get { return (roslynSymbol.IsAbstract || roslynSymbol.IsVirtual || roslynSymbol.IsOverride) && !roslynSymbol.IsSealed; }
		}

		/// <summary>
		/// 创建成员引用（已过时）。
		/// </summary>
		public IMemberReference ToMemberReference()
		{
			return new RoslynMemberReference(roslynSymbol);
		}

		/// <summary>
		/// 创建成员引用（IMember 接口实现）。
		/// </summary>
		IMemberReference IMember.ToReference()
		{
			return new RoslynMemberReference(roslynSymbol);
		}

		/// <summary>
		/// 创建成员引用（IField 接口实现，解决 IMember/IVariable 歧义）。
		/// </summary>
		IMemberReference IField.ToReference()
		{
			return new RoslynMemberReference(roslynSymbol);
		}

		/// <summary>
		/// 创建符号引用（ISymbol 接口实现）。
		/// </summary>
		ICSharpCode.NRefactory.TypeSystem.ISymbolReference ICSharpCode.NRefactory.TypeSystem.ISymbol.ToReference()
		{
			return new RoslynMemberReference(roslynSymbol);
		}

		/// <summary>
		/// 获取类型参数替换。
		/// </summary>
		public TypeParameterSubstitution Substitution => TypeParameterSubstitution.Identity;

		/// <summary>
		/// 使用指定替换特化此成员。
		/// </summary>
		public IMember Specialize(TypeParameterSubstitution substitution)
		{
			return this;
		}

		// IMethod 实现

		/// <summary>
		/// 获取方法的未解析部分。
		/// </summary>
		public IList<IUnresolvedMethod> Parts => EmptyList<IUnresolvedMethod>.Instance;

		/// <summary>
		/// 获取返回类型特性。
		/// </summary>
		public IList<IAttribute> ReturnTypeAttributes => EmptyList<IAttribute>.Instance;

		/// <summary>
		/// 获取方法的类型参数。
		/// </summary>
		public IList<ITypeParameter> TypeParameters => EmptyList<ITypeParameter>.Instance;

		/// <summary>
		/// 获取方法是否为已参数化的泛型方法。
		/// </summary>
		public bool IsParameterized {
			get {
				var method = roslynSymbol as IMethodSymbol;
				return method != null && method.IsGenericMethod && !method.IsDefinition;
			}
		}

		/// <summary>
		/// 获取方法的类型参数。
		/// </summary>
		public IList<IType> TypeArguments {
			get {
				var method = roslynSymbol as IMethodSymbol;
				if (method == null || method.Arity == 0)
					return EmptyList<IType>.Instance;
				var args = new List<IType>(method.TypeArguments.Length);
				foreach (var arg in method.TypeArguments) {
					args.Add(AdapterCache.GetOrCreateTypeAdapter(arg, compilation));
				}
				return args.AsReadOnly();
			}
		}

		/// <summary>
		/// 获取是否为扩展方法。
		/// </summary>
		public bool IsExtensionMethod {
			get {
				var method = roslynSymbol as IMethodSymbol;
				return method != null && method.IsExtensionMethod;
			}
		}

		/// <summary>
		/// 获取是否为构造函数。
		/// </summary>
		public bool IsConstructor {
			get {
				var method = roslynSymbol as IMethodSymbol;
				return method != null && method.MethodKind == MethodKind.Constructor;
			}
		}

		/// <summary>
		/// 获取是否为析构函数。
		/// </summary>
		public bool IsDestructor {
			get {
				var method = roslynSymbol as IMethodSymbol;
				return method != null && method.MethodKind == MethodKind.Destructor;
			}
		}

		/// <summary>
		/// 获取是否为运算符。
		/// </summary>
		public bool IsOperator {
			get {
				var method = roslynSymbol as IMethodSymbol;
				return method != null && (method.MethodKind == MethodKind.UserDefinedOperator || method.MethodKind == MethodKind.Conversion);
			}
		}

		/// <summary>
		/// 获取是否为 partial 方法。
		/// </summary>
		public bool IsPartial {
			get {
				var method = roslynSymbol as IMethodSymbol;
				return method != null && method.PartialDefinitionPart != null;
			}
		}

		/// <summary>
		/// 获取是否为 async 方法。
		/// </summary>
		public bool IsAsync {
			get {
				var method = roslynSymbol as IMethodSymbol;
				return method != null && method.IsAsync;
			}
		}

		/// <summary>
		/// 获取方法是否有方法体。
		/// </summary>
		public bool HasBody {
			get {
				var method = roslynSymbol as IMethodSymbol;
				if (method == null)
					return false;
				return !method.IsAbstract && !method.IsExtern;
			}
		}

		/// <summary>
		/// 获取是否为访问器。
		/// </summary>
		public bool IsAccessor {
			get {
				var method = roslynSymbol as IMethodSymbol;
				return method != null && method.AssociatedSymbol != null;
			}
		}

		/// <summary>
		/// 获取访问器所属的属性/事件。
		/// </summary>
		public IMember AccessorOwner {
			get {
				var method = roslynSymbol as IMethodSymbol;
				if (method != null && method.AssociatedSymbol != null)
					return new RoslynMemberAdapter(method.AssociatedSymbol, compilation);
				return null;
			}
		}

		/// <summary>
		/// 获取缩减自的扩展方法。
		/// </summary>
		public IMethod ReducedFrom {
			get {
				var method = roslynSymbol as IMethodSymbol;
				if (method != null && method.ReducedFrom != null)
					return new RoslynMemberAdapter(method.ReducedFrom, compilation) as IMethod;
				return null;
			}
		}

		/// <summary>
		/// 使用指定替换特化此方法。
		/// </summary>
		IMethod IMethod.Specialize(TypeParameterSubstitution substitution)
		{
			return this;
		}

		// IParameterizedMember 实现

		/// <summary>
		/// 获取参数列表。
		/// </summary>
		public IList<IParameter> Parameters {
			get {
				var method = roslynSymbol as IMethodSymbol;
				if (method != null)
					return CreateParameters(method.Parameters);

				var property = roslynSymbol as IPropertySymbol;
				if (property != null)
					return CreateParameters(property.Parameters);

				return EmptyList<IParameter>.Instance;
			}
		}

		/// <summary>
		/// 从 Roslyn 参数符号列表创建 IParameter 列表。
		/// </summary>
		IList<IParameter> CreateParameters(System.Collections.Immutable.ImmutableArray<IParameterSymbol> parameters)
		{
			if (parameters.IsEmpty)
				return EmptyList<IParameter>.Instance;
			var result = new List<IParameter>(parameters.Length);
			foreach (var p in parameters) {
				result.Add(new RoslynParameterAdapter(p, compilation));
			}
			return result.AsReadOnly();
		}

		// IProperty 实现

		public bool CanGet {
			get {
				var property = roslynSymbol as IPropertySymbol;
				return property != null && property.GetMethod != null;
			}
		}

		public bool CanSet {
			get {
				var property = roslynSymbol as IPropertySymbol;
				return property != null && property.SetMethod != null;
			}
		}

		public IMethod Getter {
			get {
				var property = roslynSymbol as IPropertySymbol;
				if (property != null && property.GetMethod != null)
					return new RoslynMemberAdapter(property.GetMethod, compilation);
				return null;
			}
		}

		public IMethod Setter {
			get {
				var property = roslynSymbol as IPropertySymbol;
				if (property != null && property.SetMethod != null)
					return new RoslynMemberAdapter(property.SetMethod, compilation);
				return null;
			}
		}

		public bool IsIndexer {
			get {
				var property = roslynSymbol as IPropertySymbol;
				return property != null && property.IsIndexer;
			}
		}

		// IField 实现

		public bool IsReadOnly {
			get {
				var field = roslynSymbol as IFieldSymbol;
				return field != null && field.IsReadOnly;
			}
		}

		public bool IsVolatile {
			get {
				var field = roslynSymbol as IFieldSymbol;
				return field != null && field.IsVolatile;
			}
		}

		public bool IsFixed {
			get {
				var field = roslynSymbol as IFieldSymbol;
				return field != null && field.IsFixedSizeBuffer;
			}
		}

		public bool IsConst {
			get {
				var field = roslynSymbol as IFieldSymbol;
				return field != null && field.IsConst;
			}
		}

		public object ConstantValue {
			get {
				var field = roslynSymbol as IFieldSymbol;
				if (field != null && field.HasConstantValue)
					return field.ConstantValue;
				return null;
			}
		}

		public IType Type => ReturnType;

		// IEvent 实现

		public bool CanAdd {
			get {
				var ev = roslynSymbol as IEventSymbol;
				return ev != null && ev.AddMethod != null;
			}
		}

		public bool CanRemove {
			get {
				var ev = roslynSymbol as IEventSymbol;
				return ev != null && ev.RemoveMethod != null;
			}
		}

		public bool CanInvoke {
			get {
				var ev = roslynSymbol as IEventSymbol;
				return ev != null && ev.RaiseMethod != null;
			}
		}

		public IMethod AddAccessor {
			get {
				var ev = roslynSymbol as IEventSymbol;
				if (ev != null && ev.AddMethod != null)
					return new RoslynMemberAdapter(ev.AddMethod, compilation);
				return null;
			}
		}

		public IMethod RemoveAccessor {
			get {
				var ev = roslynSymbol as IEventSymbol;
				if (ev != null && ev.RemoveMethod != null)
					return new RoslynMemberAdapter(ev.RemoveMethod, compilation);
				return null;
			}
		}

		public IMethod InvokeAccessor {
			get {
				var ev = roslynSymbol as IEventSymbol;
				if (ev != null && ev.RaiseMethod != null)
					return new RoslynMemberAdapter(ev.RaiseMethod, compilation);
				return null;
			}
		}

		/// <summary>
		/// 获取声明类型引用。
		/// </summary>
		public ITypeReference DeclaringTypeReference {
			get {
				if (roslynSymbol.ContainingType != null)
					return new RoslynTypeReference(roslynSymbol.ContainingType);
				return null;
			}
		}

		public override string ToString()
		{
			return roslynSymbol.ToDisplayString();
		}
	}
}
