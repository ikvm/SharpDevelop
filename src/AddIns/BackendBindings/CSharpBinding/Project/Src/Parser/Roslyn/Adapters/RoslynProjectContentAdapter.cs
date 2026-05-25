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

using Microsoft.CodeAnalysis.CSharp;

using NRAccessibility = ICSharpCode.NRefactory.TypeSystem.Accessibility;

namespace CSharpBinding.Parser.Roslyn.Adapters
{
	/// <summary>
	/// 将 Roslyn CSharpCompilation 适配为 NRefactory IProjectContent 接口。
	/// IProjectContent 表示一个项目的未解析内容，可以创建 ICompilation。
	/// </summary>
	public class RoslynProjectContentAdapter : IProjectContent
	{
		readonly CSharpCompilation roslynCompilation;
		string projectFileName;

		/// <summary>
		/// 创建 RoslynProjectContentAdapter 实例。
		/// </summary>
		public RoslynProjectContentAdapter(CSharpCompilation roslynCompilation, string projectFileName = null)
		{
			this.roslynCompilation = roslynCompilation ?? throw new ArgumentNullException(nameof(roslynCompilation));
			this.projectFileName = projectFileName ?? string.Empty;
		}

		/// <summary>
		/// 获取项目文件名。
		/// </summary>
		public string ProjectFileName => projectFileName;

		/// <summary>
		/// 获取程序集短名称。
		/// </summary>
		public string AssemblyName => roslynCompilation.AssemblyName ?? string.Empty;

		/// <summary>
		/// 获取程序集全名。
		/// </summary>
		public string FullAssemblyName => roslynCompilation.Assembly?.Identity?.GetDisplayName() ?? AssemblyName;

		/// <summary>
		/// 获取程序集位置。
		/// </summary>
		public string Location => roslynCompilation.Assembly?.Identity?.GetDisplayName() ?? string.Empty;

		/// <summary>
		/// 获取程序集特性。
		/// 当前实现返回空序列。
		/// </summary>
		public IEnumerable<IUnresolvedAttribute> AssemblyAttributes => Enumerable.Empty<IUnresolvedAttribute>();

		/// <summary>
		/// 获取模块特性。
		/// 当前实现返回空序列。
		/// </summary>
		public IEnumerable<IUnresolvedAttribute> ModuleAttributes => Enumerable.Empty<IUnresolvedAttribute>();

		/// <summary>
		/// 获取所有顶级类型定义。
		/// 当前实现返回空序列，因为 Roslyn 不提供 IUnresolvedTypeDefinition。
		/// </summary>
		public IEnumerable<IUnresolvedTypeDefinition> TopLevelTypeDefinitions => Enumerable.Empty<IUnresolvedTypeDefinition>();

		/// <summary>
		/// 根据文件名获取未解析文件。
		/// 当前实现返回 null。
		/// </summary>
		public IUnresolvedFile GetFile(string fileName)
		{
			return null;
		}

		/// <summary>
		/// 获取所有文件。
		/// 当前实现返回空序列。
		/// </summary>
		public IEnumerable<IUnresolvedFile> Files => Enumerable.Empty<IUnresolvedFile>();

		/// <summary>
		/// 获取程序集引用。
		/// 当前实现返回空序列。
		/// </summary>
		public IEnumerable<IAssemblyReference> AssemblyReferences => Enumerable.Empty<IAssemblyReference>();

		/// <summary>
		/// 获取编译器设置。
		/// </summary>
		public object CompilerSettings => roslynCompilation.Options;

		/// <summary>
		/// 创建编译对象。
		/// </summary>
		public ICompilation CreateCompilation()
		{
			return new RoslynCompilationAdapter(roslynCompilation);
		}

		/// <summary>
		/// 使用指定解决方案快照创建编译对象。
		/// </summary>
		public ICompilation CreateCompilation(ISolutionSnapshot solutionSnapshot)
		{
			return new RoslynCompilationAdapter(roslynCompilation);
		}

		/// <summary>
		/// 解析此程序集引用。
		/// </summary>
		public IAssembly Resolve(ITypeResolveContext context)
		{
			var compilationAdapter = context.Compilation as RoslynCompilationAdapter;
			if (compilationAdapter != null)
				return compilationAdapter.MainAssembly;
			return CreateCompilation().MainAssembly;
		}

		// 以下方法返回 NotSupportedException，因为 Roslyn 编译是不可变的，
		// 不能通过这些方法修改。调用方应创建新的 CSharpCompilation 并重新包装。

		public IProjectContent SetAssemblyName(string newAssemblyName)
		{
			throw new NotSupportedException("Roslyn 适配器不支持修改程序集名称，请创建新的 CSharpCompilation");
		}

		public IProjectContent SetProjectFileName(string newProjectFileName)
		{
			var copy = new RoslynProjectContentAdapter(roslynCompilation, newProjectFileName);
			return copy;
		}

		public IProjectContent SetLocation(string newLocation)
		{
			throw new NotSupportedException("Roslyn 适配器不支持修改位置，请创建新的 CSharpCompilation");
		}

		public IProjectContent AddAssemblyReferences(IEnumerable<IAssemblyReference> references)
		{
			throw new NotSupportedException("Roslyn 适配器不支持添加程序集引用，请创建新的 CSharpCompilation");
		}

		public IProjectContent AddAssemblyReferences(params IAssemblyReference[] references)
		{
			throw new NotSupportedException("Roslyn 适配器不支持添加程序集引用，请创建新的 CSharpCompilation");
		}

		public IProjectContent RemoveAssemblyReferences(IEnumerable<IAssemblyReference> references)
		{
			throw new NotSupportedException("Roslyn 适配器不支持移除程序集引用，请创建新的 CSharpCompilation");
		}

		public IProjectContent RemoveAssemblyReferences(params IAssemblyReference[] references)
		{
			throw new NotSupportedException("Roslyn 适配器不支持移除程序集引用，请创建新的 CSharpCompilation");
		}

		public IProjectContent AddOrUpdateFiles(IEnumerable<IUnresolvedFile> newFiles)
		{
			throw new NotSupportedException("Roslyn 适配器不支持添加文件，请创建新的 CSharpCompilation");
		}

		public IProjectContent AddOrUpdateFiles(params IUnresolvedFile[] newFiles)
		{
			throw new NotSupportedException("Roslyn 适配器不支持添加文件，请创建新的 CSharpCompilation");
		}

		public IProjectContent RemoveFiles(IEnumerable<string> fileNames)
		{
			throw new NotSupportedException("Roslyn 适配器不支持移除文件，请创建新的 CSharpCompilation");
		}

		public IProjectContent RemoveFiles(params string[] fileNames)
		{
			throw new NotSupportedException("Roslyn 适配器不支持移除文件，请创建新的 CSharpCompilation");
		}

		public IProjectContent UpdateProjectContent(IUnresolvedFile oldFile, IUnresolvedFile newFile)
		{
			throw new NotSupportedException("Roslyn 适配器不支持更新文件，请创建新的 CSharpCompilation");
		}

		public IProjectContent UpdateProjectContent(IEnumerable<IUnresolvedFile> oldFiles, IEnumerable<IUnresolvedFile> newFiles)
		{
			throw new NotSupportedException("Roslyn 适配器不支持更新文件，请创建新的 CSharpCompilation");
		}

		public IProjectContent SetCompilerSettings(object compilerSettings)
		{
			throw new NotSupportedException("Roslyn 适配器不支持修改编译器设置，请创建新的 CSharpCompilation");
		}
	}
}
