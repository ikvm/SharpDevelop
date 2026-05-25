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

using ICSharpCode.NRefactory.TypeSystem;

using Microsoft.CodeAnalysis;

using NRSymbol = ICSharpCode.NRefactory.TypeSystem.ISymbol;
using NRSymbolReference = ICSharpCode.NRefactory.TypeSystem.ISymbolReference;
using NRSpecialType = ICSharpCode.NRefactory.TypeSystem.SpecialType;
using RoslynSpecialType = Microsoft.CodeAnalysis.SpecialType;

namespace CSharpBinding.Parser.Roslyn.Adapters
{
	/// <summary>
	/// 基于 Roslyn 类型符号的 ITypeReference 实现。
	/// 存储类型的元数据信息，可以在另一个编译中重新查找类型。
	/// </summary>
	public class RoslynTypeReference : ITypeReference, NRSymbolReference
	{
		readonly string metadataName;
		readonly string namespaceName;
		readonly int arity;
		readonly bool isNested;
		readonly string declaringTypeMetadataName;

		/// <summary>
		/// 从 Roslyn ITypeSymbol 创建类型引用。
		/// </summary>
		public RoslynTypeReference(ITypeSymbol typeSymbol)
		{
			if (typeSymbol == null)
				throw new ArgumentNullException(nameof(typeSymbol));

			metadataName = typeSymbol.MetadataName;
			arity = (typeSymbol as INamedTypeSymbol)?.Arity ?? 0;

			if (typeSymbol.ContainingNamespace != null) {
				var ns = typeSymbol.ContainingNamespace.ToDisplayString();
				namespaceName = ns == "<global namespace>" ? string.Empty : ns;
			} else {
				namespaceName = string.Empty;
			}

			isNested = typeSymbol.ContainingType != null;
			if (isNested) {
				declaringTypeMetadataName = typeSymbol.ContainingType.MetadataName;
			}
		}

		/// <summary>
		/// 解析此类型引用。
		/// </summary>
		public IType Resolve(ITypeResolveContext context)
		{
			var compilationAdapter = context.Compilation as RoslynCompilationAdapter;
			if (compilationAdapter == null)
				return NRSpecialType.UnknownType;

			var roslynCompilation = compilationAdapter.RoslynCompilation;

			INamedTypeSymbol typeSymbol;

			if (isNested && !string.IsNullOrEmpty(declaringTypeMetadataName)) {
				// 嵌套类型：先查找声明类型
				var declaringType = roslynCompilation.GetTypeByMetadataName(declaringTypeMetadataName);
				if (declaringType == null)
					return NRSpecialType.UnknownType;
				typeSymbol = declaringType.GetTypeMembers(metadataName, arity).FirstOrDefault();
			} else {
				// 顶级类型
				string fullMetadataName = string.IsNullOrEmpty(namespaceName)
					? metadataName
					: namespaceName + "." + metadataName;
				typeSymbol = roslynCompilation.GetTypeByMetadataName(fullMetadataName);
			}

			if (typeSymbol == null)
				return NRSpecialType.UnknownType;

			return AdapterCache.GetOrCreateTypeAdapter(typeSymbol, compilationAdapter);
		}

		/// <summary>
		/// 显式实现 ISymbolReference.Resolve。
		/// </summary>
		NRSymbol NRSymbolReference.Resolve(ITypeResolveContext context)
		{
			return Resolve(context) as NRSymbol;
		}
	}

	/// <summary>
	/// 基于 Roslyn 成员符号的 IMemberReference 实现。
	/// 存储成员的元数据信息，可以在另一个编译中重新查找成员。
	/// </summary>
	public class RoslynMemberReference : IMemberReference
	{
		readonly string memberName;
		readonly RoslynTypeReference declaringTypeRef;
		readonly Microsoft.CodeAnalysis.SymbolKind symbolKind;

		public RoslynMemberReference(Microsoft.CodeAnalysis.ISymbol symbol)
		{
			if (symbol == null)
				throw new ArgumentNullException(nameof(symbol));

			memberName = symbol.Name;
			symbolKind = symbol.Kind;

			if (symbol.ContainingType != null)
				declaringTypeRef = new RoslynTypeReference(symbol.ContainingType);
		}

		/// <summary>
		/// 获取声明类型引用。
		/// </summary>
		public ITypeReference DeclaringTypeReference => declaringTypeRef;

		/// <summary>
		/// 解析此成员引用。
		/// </summary>
		public IMember Resolve(ITypeResolveContext context)
		{
			if (declaringTypeRef == null)
				return null;

			var declaringType = declaringTypeRef.Resolve(context) as RoslynTypeAdapter;
			if (declaringType == null)
				return null;

			var namedType = declaringType.RoslynTypeSymbol as INamedTypeSymbol;
			if (namedType == null)
				return null;

			// 在类型中查找同名成员
			foreach (var member in namedType.GetMembers(memberName)) {
				if (member.Kind == symbolKind) {
					var compilationAdapter = context.Compilation as RoslynCompilationAdapter;
					if (compilationAdapter != null)
						return new RoslynMemberAdapter(member, compilationAdapter);
				}
			}
			return null;
		}

		/// <summary>
		/// 显式实现 ISymbolReference.Resolve，将调用委托给 Resolve 方法。
		/// </summary>
		NRSymbol NRSymbolReference.Resolve(ITypeResolveContext context)
		{
			return Resolve(context);
		}
	}

	/// <summary>
	/// 基于 Roslyn 命名空间符号的 ISymbolReference 实现。
	/// </summary>
	public class RoslynNamespaceReference : NRSymbolReference
	{
		readonly string namespaceName;

		public RoslynNamespaceReference(INamespaceSymbol namespaceSymbol)
		{
			if (namespaceSymbol == null)
				throw new ArgumentNullException(nameof(namespaceSymbol));
			var name = namespaceSymbol.ToDisplayString();
			namespaceName = name == "<global namespace>" ? string.Empty : name;
		}

		public NRSymbol Resolve(ITypeResolveContext context)
		{
			var compilationAdapter = context.Compilation as RoslynCompilationAdapter;
			if (compilationAdapter == null)
				return null;

			var roslynCompilation = compilationAdapter.RoslynCompilation;
			INamespaceSymbol ns;

			if (string.IsNullOrEmpty(namespaceName)) {
				ns = roslynCompilation.GlobalNamespace;
			} else {
				ns = roslynCompilation.GlobalNamespace.GetNamespaceMembers()
					.FirstOrDefault(n => n.ToDisplayString() == namespaceName)
					?? roslynCompilation.GlobalNamespace;
			}

			if (ns != null)
				return AdapterCache.GetOrCreateNamespaceAdapter(ns, compilationAdapter);
			return null;
		}
	}
}
