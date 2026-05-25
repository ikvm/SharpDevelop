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
using ICSharpCode.NRefactory.Utils;

using Microsoft.CodeAnalysis;

namespace CSharpBinding.Parser.Roslyn.Adapters
{
	/// <summary>
	/// 将 Roslyn IAssemblySymbol 适配为 NRefactory IAssembly 接口。
	/// 提供程序集级别的类型导航功能。
	/// </summary>
	public class RoslynAssemblyAdapter : IAssembly
	{
		readonly IAssemblySymbol roslynAssembly;
		readonly RoslynCompilationAdapter compilation;

		/// <summary>
		/// 创建 RoslynAssemblyAdapter 实例。
		/// </summary>
		public RoslynAssemblyAdapter(IAssemblySymbol roslynAssembly, RoslynCompilationAdapter compilation)
		{
			this.roslynAssembly = roslynAssembly ?? throw new ArgumentNullException(nameof(roslynAssembly));
			this.compilation = compilation ?? throw new ArgumentNullException(nameof(compilation));
		}

		/// <summary>
		/// 获取底层 Roslyn IAssemblySymbol。
		/// </summary>
		public IAssemblySymbol RoslynAssemblySymbol => roslynAssembly;

		/// <summary>
		/// 获取所属的编译对象。
		/// </summary>
		public ICompilation Compilation => compilation;

		/// <summary>
		/// 获取原始未解析的程序集。
		/// Roslyn 适配器不提供 IUnresolvedAssembly，返回 null。
		/// </summary>
		public IUnresolvedAssembly UnresolvedAssembly => null;

		/// <summary>
		/// 获取此程序集是否为主程序集。
		/// </summary>
		public bool IsMainAssembly {
			get { return roslynAssembly == compilation.RoslynCompilation.Assembly; }
		}

		/// <summary>
		/// 获取程序集短名称。
		/// </summary>
		public string AssemblyName => roslynAssembly.Name;

		/// <summary>
		/// 获取程序集全名（包括公钥标记等）。
		/// </summary>
		public string FullAssemblyName => roslynAssembly.Identity.GetDisplayName();

		/// <summary>
		/// 获取程序集级别的特性列表。
		/// 当前实现返回空列表。
		/// </summary>
		public IList<IAttribute> AssemblyAttributes {
			get { return EmptyList<IAttribute>.Instance; }
		}

		/// <summary>
		/// 获取模块级别的特性列表。
		/// 当前实现返回空列表。
		/// </summary>
		public IList<IAttribute> ModuleAttributes {
			get { return EmptyList<IAttribute>.Instance; }
		}

		/// <summary>
		/// 判断此程序集的内部类型是否对指定程序集可见。
		/// </summary>
		public bool InternalsVisibleTo(IAssembly assembly)
		{
			var otherAdapter = assembly as RoslynAssemblyAdapter;
			if (otherAdapter == null)
				return false;
			return roslynAssembly.GivesAccessTo(otherAdapter.roslynAssembly);
		}

		/// <summary>
		/// 获取此程序集的根命名空间。
		/// </summary>
		public INamespace RootNamespace {
			get { return AdapterCache.GetOrCreateNamespaceAdapter(roslynAssembly.GlobalNamespace, compilation); }
		}

		/// <summary>
		/// 根据顶级类型名称获取类型定义。
		/// 使用序号名称比较。
		/// </summary>
		public ITypeDefinition GetTypeDefinition(TopLevelTypeName topLevelTypeName)
		{
			// 构建元数据名称用于 Roslyn 查找
			string metadataName = topLevelTypeName.ReflectionName;
			var typeSymbol = roslynAssembly.GetTypeByMetadataName(metadataName);
			if (typeSymbol != null)
				return AdapterCache.GetOrCreateTypeDefinitionAdapter(typeSymbol, compilation);

			// 回退：遍历全局命名空间中的类型
			foreach (var ns in roslynAssembly.GlobalNamespace.GetNamespaceMembers()) {
				var found = FindTypeInNamespace(ns, topLevelTypeName);
				if (found != null)
					return AdapterCache.GetOrCreateTypeDefinitionAdapter(found, compilation);
			}
			return null;
		}

		/// <summary>
		/// 在命名空间中递归查找类型。
		/// </summary>
		INamedTypeSymbol FindTypeInNamespace(INamespaceSymbol ns, TopLevelTypeName typeName)
		{
			foreach (var type in ns.GetTypeMembers()) {
				if (type.Name == typeName.Name && type.Arity == typeName.TypeParameterCount
					&& type.ContainingNamespace.ToDisplayString() == typeName.Namespace) {
					return type;
				}
			}
			foreach (var childNs in ns.GetNamespaceMembers()) {
				var found = FindTypeInNamespace(childNs, typeName);
				if (found != null)
					return found;
			}
			return null;
		}

		/// <summary>
		/// 获取此程序集中所有顶级类型定义。
		/// </summary>
		public IEnumerable<ITypeDefinition> TopLevelTypeDefinitions {
			get {
				return GetAllTypesInNamespace(roslynAssembly.GlobalNamespace);
			}
		}

		/// <summary>
		/// 递归获取命名空间中的所有类型。
		/// </summary>
		IEnumerable<ITypeDefinition> GetAllTypesInNamespace(INamespaceSymbol ns)
		{
			foreach (var type in ns.GetTypeMembers()) {
				yield return AdapterCache.GetOrCreateTypeDefinitionAdapter(type, compilation);
			}
			foreach (var childNs in ns.GetNamespaceMembers()) {
				foreach (var type in GetAllTypesInNamespace(childNs)) {
					yield return type;
				}
			}
		}
	}
}
