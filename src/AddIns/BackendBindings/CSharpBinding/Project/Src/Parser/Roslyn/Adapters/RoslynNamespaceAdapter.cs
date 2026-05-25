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
using ICSharpCode.NRefactory.TypeSystem.Implementation;

using Microsoft.CodeAnalysis;

using NRSymbolKind = ICSharpCode.NRefactory.TypeSystem.SymbolKind;
using NRSymbolReference = ICSharpCode.NRefactory.TypeSystem.ISymbolReference;

namespace CSharpBinding.Parser.Roslyn.Adapters
{
	/// <summary>
	/// 将 Roslyn INamespaceSymbol 适配为 NRefactory INamespace 接口。
	/// 提供命名空间级别的导航功能，包括子命名空间和类型的访问。
	/// </summary>
	public class RoslynNamespaceAdapter : INamespace
	{
		readonly INamespaceSymbol roslynNamespace;
		readonly RoslynCompilationAdapter compilation;

		/// <summary>
		/// 创建 RoslynNamespaceAdapter 实例。
		/// </summary>
		public RoslynNamespaceAdapter(INamespaceSymbol roslynNamespace, RoslynCompilationAdapter compilation)
		{
			this.roslynNamespace = roslynNamespace ?? throw new ArgumentNullException(nameof(roslynNamespace));
			this.compilation = compilation ?? throw new ArgumentNullException(nameof(compilation));
		}

		/// <summary>
		/// 获取底层 Roslyn INamespaceSymbol。
		/// </summary>
		public INamespaceSymbol RoslynNamespaceSymbol => roslynNamespace;

		/// <summary>
		/// 获取所属编译对象。
		/// </summary>
		public ICompilation Compilation => compilation;

		/// <summary>
		/// 获取符号种类（始终为 Namespace）。
		/// </summary>
		public NRSymbolKind SymbolKind => NRSymbolKind.Namespace;

		/// <summary>
		/// 获取 extern alias。当前实现返回空字符串。
		/// </summary>
		public string ExternAlias => string.Empty;

		/// <summary>
		/// 获取命名空间全名（如 "System.Collections"）。
		/// </summary>
		public string FullName {
			get {
				var name = roslynNamespace.ToDisplayString();
				return name == "<global namespace>" ? string.Empty : name;
			}
		}

		/// <summary>
		/// 获取命名空间短名（如 "Collections"）。
		/// </summary>
		public string Name => roslynNamespace.Name;

		/// <summary>
		/// 获取父命名空间。根命名空间返回 null。
		/// </summary>
		public INamespace ParentNamespace {
			get {
				if (roslynNamespace.ContainingNamespace == null)
					return null;
				// 如果父命名空间是全局命名空间，也返回 null
				if (roslynNamespace.ContainingNamespace.IsGlobalNamespace)
					return null;
				return AdapterCache.GetOrCreateNamespaceAdapter(roslynNamespace.ContainingNamespace, compilation);
			}
		}

		/// <summary>
		/// 获取子命名空间列表。
		/// </summary>
		public IEnumerable<INamespace> ChildNamespaces {
			get {
				return roslynNamespace.GetNamespaceMembers()
					.Select(ns => (INamespace)AdapterCache.GetOrCreateNamespaceAdapter(ns, compilation));
			}
		}

		/// <summary>
		/// 获取此命名空间中的类型定义。
		/// </summary>
		public IEnumerable<ITypeDefinition> Types {
			get {
				return roslynNamespace.GetTypeMembers()
					.Select(t => (ITypeDefinition)AdapterCache.GetOrCreateTypeDefinitionAdapter(t, compilation));
			}
		}

		/// <summary>
		/// 获取贡献类型到此命名空间的程序集。
		/// </summary>
		public IEnumerable<IAssembly> ContributingAssemblies {
			get {
				// 返回包含此命名空间的程序集
				var asm = roslynNamespace.ContainingAssembly;
				if (asm != null)
					yield return AdapterCache.GetOrCreateAssemblyAdapter(asm, compilation);
			}
		}

		/// <summary>
		/// 根据短名获取子命名空间。
		/// </summary>
		public INamespace GetChildNamespace(string name)
		{
			if (name == null)
				return null;
			var child = roslynNamespace.GetNamespaceMembers()
				.FirstOrDefault(ns => compilation.NameComparer.Equals(ns.Name, name));
			if (child != null)
				return AdapterCache.GetOrCreateNamespaceAdapter(child, compilation);
			return null;
		}

		/// <summary>
		/// 根据短名和类型参数数量获取类型定义。
		/// </summary>
		public ITypeDefinition GetTypeDefinition(string name, int typeParameterCount)
		{
			if (name == null)
				return null;
			var type = roslynNamespace.GetTypeMembers(name, typeParameterCount).FirstOrDefault();
			if (type != null)
				return AdapterCache.GetOrCreateTypeDefinitionAdapter(type, compilation);
			return null;
		}

		/// <summary>
		/// 创建符号引用。
		/// </summary>
		public NRSymbolReference ToReference()
		{
			return new RoslynNamespaceReference(roslynNamespace);
		}

		public override string ToString()
		{
			return FullName;
		}
	}
}
