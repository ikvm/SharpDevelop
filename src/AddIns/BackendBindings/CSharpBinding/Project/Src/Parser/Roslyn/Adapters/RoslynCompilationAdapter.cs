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
using ICSharpCode.NRefactory.Utils;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

using NRTypeKind = ICSharpCode.NRefactory.TypeSystem.TypeKind;
using NRAccessibility = ICSharpCode.NRefactory.TypeSystem.Accessibility;
using NRSpecialType = ICSharpCode.NRefactory.TypeSystem.SpecialType;
using RoslynTypeKind = Microsoft.CodeAnalysis.TypeKind;
using RoslynSpecialType = Microsoft.CodeAnalysis.SpecialType;

namespace CSharpBinding.Parser.Roslyn.Adapters
{
	/// <summary>
	/// 将 Roslyn CSharpCompilation 适配为 NRefactory ICompilation 接口。
	/// 这是类型系统适配层的核心类，提供了编译单元级别的类型查找和导航功能。
	/// </summary>
	public class RoslynCompilationAdapter : ICompilation
	{
		readonly CSharpCompilation roslynCompilation;

		// 懒加载的程序集列表
		IList<IAssembly> assemblies;
		IList<IAssembly> referencedAssemblies;
		RoslynAssemblyAdapter mainAssembly;
		INamespace rootNamespace;
		CacheManager cacheManager;
		SimpleSolutionSnapshot solutionSnapshot;

		/// <summary>
		/// 创建 RoslynCompilationAdapter 实例。
		/// </summary>
		/// <param name="roslynCompilation">被包装的 Roslyn CSharpCompilation 对象</param>
		public RoslynCompilationAdapter(CSharpCompilation roslynCompilation)
		{
			this.roslynCompilation = roslynCompilation ?? throw new ArgumentNullException(nameof(roslynCompilation));
		}

		/// <summary>
		/// 获取底层的 Roslyn CSharpCompilation 对象。
		/// </summary>
		public CSharpCompilation RoslynCompilation => roslynCompilation;

		/// <summary>
		/// 获取主程序集适配器。
		/// </summary>
		public IAssembly MainAssembly {
			get {
				if (mainAssembly == null)
					mainAssembly = AdapterCache.GetOrCreateAssemblyAdapter(roslynCompilation.Assembly, this);
				return mainAssembly;
			}
		}

		/// <summary>
		/// 获取类型解析上下文。
		/// </summary>
		public ITypeResolveContext TypeResolveContext {
			get { return new SimpleTypeResolveContext(this); }
		}

		/// <summary>
		/// 获取编译中所有程序集的列表，主程序集为第一个条目。
		/// </summary>
		public IList<IAssembly> Assemblies {
			get {
				if (assemblies == null) {
					var list = new List<IAssembly>();
					list.Add(MainAssembly);
					foreach (var refAssembly in roslynCompilation.References
						.OfType<MetadataReference>()
						.Select(r => roslynCompilation.GetAssemblyOrModuleSymbol(r))
						.OfType<IAssemblySymbol>()) {
						list.Add(AdapterCache.GetOrCreateAssemblyAdapter(refAssembly, this));
					}
					assemblies = list.AsReadOnly();
				}
				return assemblies;
			}
		}

		/// <summary>
		/// 获取引用的程序集列表（不包含主程序集）。
		/// </summary>
		public IList<IAssembly> ReferencedAssemblies {
			get {
				if (referencedAssemblies == null) {
					var list = new List<IAssembly>();
					foreach (var refAssembly in roslynCompilation.References
						.OfType<MetadataReference>()
						.Select(r => roslynCompilation.GetAssemblyOrModuleSymbol(r))
						.OfType<IAssemblySymbol>()) {
						list.Add(AdapterCache.GetOrCreateAssemblyAdapter(refAssembly, this));
					}
					referencedAssemblies = list.AsReadOnly();
				}
				return referencedAssemblies;
			}
		}

		/// <summary>
		/// 获取根命名空间（全局命名空间），合并了所有程序集的根命名空间。
		/// </summary>
		public INamespace RootNamespace {
			get {
				if (rootNamespace == null)
					rootNamespace = AdapterCache.GetOrCreateNamespaceAdapter(roslynCompilation.GlobalNamespace, this);
				return rootNamespace;
			}
		}

		/// <summary>
		/// 获取指定 extern alias 的根命名空间。
		/// </summary>
		public INamespace GetNamespaceForExternAlias(string alias)
		{
			// 当前实现不支持 extern alias，返回全局根命名空间
			if (string.IsNullOrEmpty(alias))
				return RootNamespace;
			return null;
		}

		/// <summary>
		/// 根据 KnownTypeCode 查找已知类型。
		/// 将 NRefactory 的 KnownTypeCode 映射为 Roslyn 的 SpecialType，
		/// 然后通过 Roslyn 编译对象获取对应的类型符号。
		/// </summary>
		public IType FindType(KnownTypeCode typeCode)
		{
			if (typeCode == KnownTypeCode.None)
				return NRSpecialType.UnknownType;

			var specialType = RoslynTypeSystemMapper.MapKnownTypeCodeToSpecialType(typeCode);
			if (specialType == RoslynSpecialType.None) {
				// 对于没有直接映射的类型（如 Task、TaskOfT 等），尝试通过名称查找
				var knownRef = KnownTypeReference.Get(typeCode);
				if (knownRef == null)
					return NRSpecialType.UnknownType;

				// 在全局命名空间中搜索
				var found = roslynCompilation.GetTypeByMetadataName(knownRef.Namespace + "." + knownRef.Name);
				if (found != null)
					return AdapterCache.GetOrCreateTypeAdapter(found, this);

				return NRSpecialType.UnknownType;
			}

			var typeSymbol = roslynCompilation.GetSpecialType(specialType);
			if (typeSymbol == null || typeSymbol.TypeKind == RoslynTypeKind.Error)
				return NRSpecialType.UnknownType;

			return AdapterCache.GetOrCreateTypeAdapter(typeSymbol, this);
		}

		/// <summary>
		/// 获取名称比较器，C# 使用序号比较。
		/// </summary>
		public StringComparer NameComparer {
			get { return StringComparer.Ordinal; }
		}

		/// <summary>
		/// 获取解决方案快照。
		/// </summary>
		public ISolutionSnapshot SolutionSnapshot {
			get {
				if (solutionSnapshot == null)
					solutionSnapshot = new SimpleSolutionSnapshot(this);
				return solutionSnapshot;
			}
		}

		/// <summary>
		/// 获取缓存管理器，用于缓存与编译相关的数据。
		/// </summary>
		public CacheManager CacheManager {
			get {
				if (cacheManager == null)
					cacheManager = new CacheManager();
				return cacheManager;
			}
		}

		/// <summary>
		/// 简单的解决方案快照实现。
		/// </summary>
		class SimpleSolutionSnapshot : ISolutionSnapshot
		{
			readonly RoslynCompilationAdapter compilation;
			readonly Dictionary<string, IProjectContent> projectContents =
				new Dictionary<string, IProjectContent>(StringComparer.OrdinalIgnoreCase);

			public SimpleSolutionSnapshot(RoslynCompilationAdapter compilation)
			{
				this.compilation = compilation;
			}

			public IProjectContent GetProjectContent(string projectFileName)
			{
				IProjectContent pc;
				if (projectContents.TryGetValue(projectFileName, out pc))
					return pc;
				return null;
			}

			public ICompilation GetCompilation(IProjectContent project)
			{
				return compilation;
			}
		}
	}
}
