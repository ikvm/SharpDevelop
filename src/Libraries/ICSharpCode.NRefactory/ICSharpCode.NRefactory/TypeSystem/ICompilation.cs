﻿// Copyright (c) 2010-2013 AlphaSierraPapa for the SharpDevelop Team
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
using ICSharpCode.NRefactory.Utils;

namespace ICSharpCode.NRefactory.TypeSystem
{
	/// <summary>
	/// 继承自 Abstractions 中的 ICompilation，使 NRefactory 类型同时满足两个接口。
	/// 使用 new 关键字隐藏返回 NRefactory 特定类型的成员。
	/// </summary>
	public interface ICompilation : ICSharpCode.TypeSystem.ICompilation
	{
		/// <summary>
		/// Gets the current assembly.
		/// </summary>
		new IAssembly MainAssembly { get; }
		
		/// <summary>
		/// Gets the type resolve context that specifies this compilation and no current assembly or entity.
		/// </summary>
		new ITypeResolveContext TypeResolveContext { get; }
		
		/// <summary>
		/// Gets the list of all assemblies in the compilation.
		/// </summary>
		new IList<IAssembly> Assemblies { get; }
		
		/// <summary>
		/// Gets the referenced assemblies.
		/// This list does not include the main assembly.
		/// </summary>
		new IList<IAssembly> ReferencedAssemblies { get; }
		
		/// <summary>
		/// Gets the root namespace of this compilation.
		/// </summary>
		new INamespace RootNamespace { get; }
		
		/// <summary>
		/// Gets the root namespace for a given extern alias.
		/// </summary>
		new INamespace GetNamespaceForExternAlias(string alias);
		
		/// <summary>
		/// Finds a type by its known type code.
		/// 使用 NRefactory 的 KnownTypeCode 和 IType 类型。
		/// </summary>
		new IType FindType(KnownTypeCode typeCode);
		
		/// <summary>
		/// Gets the name comparer for the language being compiled.
		/// 继承自基接口，类型为 StringComparer，无需隐藏
		/// </summary>
		
		/// <summary>
		/// Gets the solution snapshot.
		/// 使用 NRefactory 的 ISolutionSnapshot 类型。
		/// </summary>
		new ISolutionSnapshot SolutionSnapshot { get; }
		
		// CacheManager 继承自基接口，类型相同，无需隐藏
	}
	
	/// <summary>
	/// 继承自 Abstractions 中的 ICompilationProvider，使 NRefactory 类型同时满足两个接口。
	/// 使用 new 关键字隐藏返回 NRefactory.ICompilation 的成员。
	/// </summary>
	public interface ICompilationProvider : ICSharpCode.TypeSystem.ICompilationProvider
	{
		/// <summary>
		/// Gets the parent compilation.
		/// This property never returns null.
		/// </summary>
		new ICompilation Compilation { get; }
	}
}
