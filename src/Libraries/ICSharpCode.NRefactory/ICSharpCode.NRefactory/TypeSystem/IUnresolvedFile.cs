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
using ICSharpCode.NRefactory.Editor;

namespace ICSharpCode.NRefactory.TypeSystem
{
	/// <summary>
	/// 继承自 Abstractions 中的 IUnresolvedFile，使 NRefactory 类型同时满足两个接口。
	/// 使用 new 关键字隐藏返回 NRefactory 特定类型的成员。
	/// </summary>
	public interface IUnresolvedFile : ICSharpCode.TypeSystem.IUnresolvedFile
	{
		/// <summary>
		/// Gets the file name.
		/// </summary>
		// FileName 继承自基接口，类型为 string，无需隐藏

		/// <summary>
		/// Gets the top-level type definitions.
		/// </summary>
		new IList<IUnresolvedTypeDefinition> TopLevelTypeDefinitions { get; }
		
		/// <summary>
		/// Gets the assembly attributes.
		/// </summary>
		new IList<IUnresolvedAttribute> AssemblyAttributes { get; }
		
		/// <summary>
		/// Gets the attributes on the module.
		/// </summary>
		new IList<IUnresolvedAttribute> ModuleAttributes { get; }
		
		// UsingScopeNamespaces, UsingAliases 继承自基接口

		/// <summary>
		/// Gets the type definitions in this file.
		/// </summary>
		new IEnumerable<IUnresolvedTypeDefinition> GetAllTypeDefinitions();
		
		/// <summary>
		/// Finds the innermost type definition containing the specified location.
		/// </summary>
		new IUnresolvedTypeDefinition GetInnermostTypeDefinition(TextLocation location);
	}
}
