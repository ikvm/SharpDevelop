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
using ICSharpCode.NRefactory.Documentation;
using ICSharpCode.NRefactory.TypeSystem;
using ICSharpCode.NRefactory.TypeSystem.Implementation;
using ICSharpCode.NRefactory.Utils;

using Microsoft.CodeAnalysis;

using NRTypeKind = ICSharpCode.NRefactory.TypeSystem.TypeKind;
using NRAccessibility = ICSharpCode.NRefactory.TypeSystem.Accessibility;
using NRSpecialType = ICSharpCode.NRefactory.TypeSystem.SpecialType;
using NRSymbolKind = ICSharpCode.NRefactory.TypeSystem.SymbolKind;
using NRSymbolReference = ICSharpCode.NRefactory.TypeSystem.ISymbolReference;
using RoslynTypeKind = Microsoft.CodeAnalysis.TypeKind;

namespace CSharpBinding.Parser.Roslyn.Adapters
{
	/// <summary>
	/// 将 Roslyn INamedTypeSymbol 适配为 NRefactory ITypeDefinition 接口。
	/// 这是类型系统适配中最复杂的适配器，因为 ITypeDefinition 包含大量成员。
	/// 同时也实现了 IType 接口（因为 ITypeDefinition 继承自 IType）和 IEntity 接口。
	/// </summary>
	public class RoslynTypeDefinitionAdapter : RoslynTypeAdapter, ITypeDefinition
	{
		readonly INamedTypeSymbol roslynNamedType;

		/// <summary>
		/// 创建 RoslynTypeDefinitionAdapter 实例。
		/// </summary>
		public RoslynTypeDefinitionAdapter(INamedTypeSymbol roslynNamedType, RoslynCompilationAdapter compilation)
			: base(roslynNamedType, compilation)
		{
			this.roslynNamedType = roslynNamedType ?? throw new ArgumentNullException(nameof(roslynNamedType));
		}

		/// <summary>
		/// 获取底层 Roslyn INamedTypeSymbol。
		/// </summary>
		public INamedTypeSymbol RoslynNamedTypeSymbol => roslynNamedType;

		/// <summary>
		/// 获取辅助属性，返回编译适配器。
		/// </summary>
		RoslynCompilationAdapter CompilationAdapter => (RoslynCompilationAdapter)Compilation;

		#region IEntity / ISymbol / IHasAccessibility / ICompilationProvider 实现

		/// <summary>
		/// 获取符号种类（类型定义）。
		/// </summary>
		public new NRSymbolKind SymbolKind => NRSymbolKind.TypeDefinition;

		/// <summary>
		/// 获取所属编译对象（ICompilationProvider 成员）。
		/// </summary>
		public new ICompilation Compilation => CompilationAdapter;

		/// <summary>
		/// 获取实体类型（已过时，使用 SymbolKind）。
		/// </summary>
		public EntityType EntityType => EntityType.TypeDefinition;

		/// <summary>
		/// 获取访问级别。
		/// </summary>
		public NRAccessibility Accessibility => RoslynTypeSystemMapper.MapAccessibility(roslynNamedType.DeclaredAccessibility);

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
				var loc = roslynNamedType.Locations.FirstOrDefault();
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
				if (roslynNamedType.ContainingType != null)
					return AdapterCache.GetOrCreateTypeDefinitionAdapter(roslynNamedType.ContainingType, CompilationAdapter);
				return null;
			}
		}

		/// <summary>
		/// 获取声明类型（解决 IType.DeclaringType 和 IEntity.DeclaringType 的歧义）。
		/// </summary>
		public new IType DeclaringType {
			get {
				if (roslynNamedType.ContainingType != null)
					return AdapterCache.GetOrCreateTypeAdapter(roslynNamedType.ContainingType, CompilationAdapter);
				return null;
			}
		}

		/// <summary>
		/// 获取父程序集。
		/// </summary>
		public IAssembly ParentAssembly {
			get {
				var asm = roslynNamedType.ContainingAssembly;
				if (asm != null)
					return AdapterCache.GetOrCreateAssemblyAdapter(asm, CompilationAdapter);
				return CompilationAdapter.MainAssembly;
			}
		}

		/// <summary>
		/// 获取特性列表。
		/// 当前实现返回空列表。
		/// </summary>
		public new IList<IAttribute> Attributes => EmptyList<IAttribute>.Instance;

		/// <summary>
		/// 获取文档注释。
		/// </summary>
		public DocumentationComment Documentation {
			get {
				var xml = roslynNamedType.GetDocumentationCommentXml();
				if (string.IsNullOrEmpty(xml))
					return null;
				return new DocumentationComment(xml, new SimpleTypeResolveContext(CompilationAdapter.MainAssembly));
			}
		}

		/// <summary>
		/// 获取是否为静态类型。
		/// </summary>
		public new bool IsStatic => roslynNamedType.IsStatic;

		/// <summary>
		/// 获取是否为抽象类型。
		/// </summary>
		public new bool IsAbstract => roslynNamedType.IsAbstract;

		/// <summary>
		/// 获取是否为密封类型。
		/// </summary>
		public new bool IsSealed => roslynNamedType.IsSealed;

		/// <summary>
		/// 获取是否隐藏了基类成员。
		/// </summary>
		public bool IsShadowing => false;

		/// <summary>
		/// 获取是否为编译器生成的类型。
		/// </summary>
		public bool IsSynthetic => roslynNamedType.IsImplicitlyDeclared;

		/// <summary>
		/// 创建符号引用。
		/// </summary>
		public new NRSymbolReference ToReference()
		{
			return new RoslynTypeReference(roslynNamedType);
		}

		#endregion

		#region ITypeDefinition 实现

		/// <summary>
		/// 获取此类型定义的所有部分（partial 类）。
		/// Roslyn 适配器不提供 IUnresolvedTypeDefinition，返回空列表。
		/// </summary>
		public IList<IUnresolvedTypeDefinition> Parts => EmptyList<IUnresolvedTypeDefinition>.Instance;

		/// <summary>
		/// 获取类型参数列表。
		/// 当前实现返回空列表，完整的 ITypeParameter 支持需要更复杂的实现。
		/// </summary>
		public IList<ITypeParameter> TypeParameters => EmptyList<ITypeParameter>.Instance;

		/// <summary>
		/// 获取嵌套类型列表。
		/// </summary>
		public IList<ITypeDefinition> NestedTypes {
			get {
				var result = new List<ITypeDefinition>();
				foreach (var nested in roslynNamedType.GetTypeMembers()) {
					result.Add(AdapterCache.GetOrCreateTypeDefinitionAdapter(nested, CompilationAdapter));
				}
				return result.AsReadOnly();
			}
		}

		/// <summary>
		/// 获取成员列表。
		/// </summary>
		public IList<IMember> Members {
			get {
				var result = new List<IMember>();
				foreach (var member in roslynNamedType.GetMembers()) {
					if (member.Kind == Microsoft.CodeAnalysis.SymbolKind.Field ||
						member.Kind == Microsoft.CodeAnalysis.SymbolKind.Property ||
						member.Kind == Microsoft.CodeAnalysis.SymbolKind.Method ||
						member.Kind == Microsoft.CodeAnalysis.SymbolKind.Event) {
						result.Add(new RoslynMemberAdapter(member, CompilationAdapter));
					}
				}
				return result.AsReadOnly();
			}
		}

		/// <summary>
		/// 获取字段列表。
		/// </summary>
		public IEnumerable<IField> Fields {
			get {
				return roslynNamedType.GetMembers()
					.OfType<IFieldSymbol>()
					.Where(f => !f.IsConst || f.HasConstantValue)
					.Select(f => (IField)new RoslynMemberAdapter(f, CompilationAdapter));
			}
		}

		/// <summary>
		/// 获取方法列表（不包括构造函数和访问器）。
		/// </summary>
		public IEnumerable<IMethod> Methods {
			get {
				return roslynNamedType.GetMembers()
					.OfType<IMethodSymbol>()
					.Where(m => m.MethodKind == MethodKind.Ordinary
						|| m.MethodKind == MethodKind.ExplicitInterfaceImplementation
					|| m.MethodKind == MethodKind.UserDefinedOperator
					|| m.MethodKind == MethodKind.Conversion
					|| m.MethodKind == MethodKind.DelegateInvoke)
					.Select(m => (IMethod)new RoslynMemberAdapter(m, CompilationAdapter));
			}
		}

		/// <summary>
		/// 获取属性列表。
		/// </summary>
		public IEnumerable<IProperty> Properties {
			get {
				return roslynNamedType.GetMembers()
					.OfType<IPropertySymbol>()
					.Select(p => (IProperty)new RoslynMemberAdapter(p, CompilationAdapter));
			}
		}

		/// <summary>
		/// 获取事件列表。
		/// </summary>
		public IEnumerable<IEvent> Events {
			get {
				return roslynNamedType.GetMembers()
					.OfType<IEventSymbol>()
					.Select(e => (IEvent)new RoslynMemberAdapter(e, CompilationAdapter));
			}
		}

		/// <summary>
		/// 获取已知类型代码。
		/// </summary>
		public KnownTypeCode KnownTypeCode {
			get {
				switch (roslynNamedType.SpecialType) {
					case Microsoft.CodeAnalysis.SpecialType.System_Object:
						return KnownTypeCode.Object;
					case Microsoft.CodeAnalysis.SpecialType.System_Boolean:
						return KnownTypeCode.Boolean;
					case Microsoft.CodeAnalysis.SpecialType.System_Char:
						return KnownTypeCode.Char;
					case Microsoft.CodeAnalysis.SpecialType.System_SByte:
						return KnownTypeCode.SByte;
					case Microsoft.CodeAnalysis.SpecialType.System_Byte:
						return KnownTypeCode.Byte;
					case Microsoft.CodeAnalysis.SpecialType.System_Int16:
						return KnownTypeCode.Int16;
					case Microsoft.CodeAnalysis.SpecialType.System_UInt16:
						return KnownTypeCode.UInt16;
					case Microsoft.CodeAnalysis.SpecialType.System_Int32:
						return KnownTypeCode.Int32;
					case Microsoft.CodeAnalysis.SpecialType.System_UInt32:
						return KnownTypeCode.UInt32;
					case Microsoft.CodeAnalysis.SpecialType.System_Int64:
						return KnownTypeCode.Int64;
					case Microsoft.CodeAnalysis.SpecialType.System_UInt64:
						return KnownTypeCode.UInt64;
					case Microsoft.CodeAnalysis.SpecialType.System_Single:
						return KnownTypeCode.Single;
					case Microsoft.CodeAnalysis.SpecialType.System_Double:
						return KnownTypeCode.Double;
					case Microsoft.CodeAnalysis.SpecialType.System_Decimal:
						return KnownTypeCode.Decimal;
					case Microsoft.CodeAnalysis.SpecialType.System_DateTime:
						return KnownTypeCode.DateTime;
					case Microsoft.CodeAnalysis.SpecialType.System_String:
						return KnownTypeCode.String;
					case Microsoft.CodeAnalysis.SpecialType.System_Void:
						return KnownTypeCode.Void;
					default:
						return KnownTypeCode.None;
				}
			}
		}

		/// <summary>
		/// 获取枚举的基础类型。对于非枚举类型返回 UnknownType。
		/// </summary>
		public IType EnumUnderlyingType {
			get {
				if (roslynNamedType.TypeKind == RoslynTypeKind.Enum && roslynNamedType.EnumUnderlyingType != null) {
					return AdapterCache.GetOrCreateTypeAdapter(roslynNamedType.EnumUnderlyingType, CompilationAdapter);
				}
				return NRSpecialType.UnknownType;
			}
		}

		/// <summary>
		/// 获取完整类型名。
		/// </summary>
		public FullTypeName FullTypeName {
			get {
				return BuildFullTypeName(roslynNamedType);
			}
		}

		/// <summary>
		/// 从 Roslyn INamedTypeSymbol 构建 NRefactory FullTypeName。
		/// </summary>
		static FullTypeName BuildFullTypeName(INamedTypeSymbol type)
		{
			if (type.ContainingType != null) {
				var outer = BuildFullTypeName(type.ContainingType);
				return outer.NestedType(type.Name, type.Arity);
			}
			var ns = type.ContainingNamespace != null
				? type.ContainingNamespace.ToDisplayString()
				: string.Empty;
			if (ns == "<global namespace>")
				ns = string.Empty;
			return new TopLevelTypeName(ns, type.Name, type.Arity);
		}

		/// <summary>
		/// 获取是否包含扩展方法。
		/// </summary>
		public bool HasExtensionMethods {
			get {
				return roslynNamedType.GetMembers()
					.OfType<IMethodSymbol>()
					.Any(m => m.IsExtensionMethod);
			}
		}

		/// <summary>
		/// 获取是否为 partial 类型。
		/// </summary>
		public bool IsPartial {
			get {
				return roslynNamedType.Locations.Length > 1 ||
					roslynNamedType.IsScriptClass ||
					(roslynNamedType.DeclaringSyntaxReferences.Length > 1);
			}
		}

		/// <summary>
		/// 获取实现指定接口成员的类成员。
		/// </summary>
		public IMember GetInterfaceImplementation(IMember interfaceMember)
		{
			if (interfaceMember == null)
				return null;

			var results = GetInterfaceImplementation(new[] { interfaceMember });
			return results.Count > 0 ? results[0] : null;
		}

		/// <summary>
		/// 获取实现指定接口成员列表的类成员列表。
		/// </summary>
		public IList<IMember> GetInterfaceImplementation(IList<IMember> interfaceMembers)
		{
			if (interfaceMembers == null || interfaceMembers.Count == 0)
				return EmptyList<IMember>.Instance;

			var result = new List<IMember>();
			foreach (var ifaceMember in interfaceMembers) {
				var roslynAdapter = ifaceMember as RoslynMemberAdapter;
				if (roslynAdapter != null) {
					var impl = roslynNamedType.FindImplementationForInterfaceMember(roslynAdapter.RoslynSymbol);
					if (impl != null)
						result.Add(new RoslynMemberAdapter(impl, CompilationAdapter));
					else
						result.Add(null);
				} else {
					result.Add(null);
				}
			}
			return result;
		}

		/// <summary>
		/// 获取类型定义（ITypeDefinition 实现返回自身）。
		/// </summary>
		public new ITypeDefinition GetDefinition()
		{
			return this;
		}

		#endregion
	}
}
