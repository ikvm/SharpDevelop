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

namespace ICSharpCode.NRefactory.TypeSystem
{
	/// <summary>
	/// 继承自 Abstractions 中的 IAssemblyReference，使 NRefactory 类型同时满足两个接口。
	/// Resolve 方法参数类型不同，是独立方法。
	/// </summary>
	public interface IAssemblyReference : ICSharpCode.TypeSystem.IAssemblyReference
	{
		/// <summary>
		/// Resolves this assembly reference.
		/// 使用 NRefactory 的 ITypeResolveContext 和 IAssembly 类型。
		/// </summary>
		IAssembly Resolve(ITypeResolveContext context);
	}
	
	/// <summary>
	/// 继承自 Abstractions 中的 IUnresolvedAssembly，使 NRefactory 类型同时满足两个接口。
	/// </summary>
	public interface IUnresolvedAssembly : ICSharpCode.TypeSystem.IUnresolvedAssembly
	{
		/// <summary>
		/// Gets the assembly attributes.
		/// 使用 NRefactory 的 IUnresolvedAttribute 类型。
		/// </summary>
		new IList<IUnresolvedAttribute> AssemblyAttributes { get; }
		
		/// <summary>
		/// Gets the top-level type definitions.
		/// 使用 NRefactory 的 IUnresolvedTypeDefinition 类型。
		/// </summary>
		new IList<IUnresolvedTypeDefinition> TopLevelTypeDefinitions { get; }
	}
	
	/// <summary>
	/// 继承自 Abstractions 中的 IAssembly，使 NRefactory 类型同时满足两个接口。
	/// 使用 new 关键字隐藏返回 NRefactory 特定类型的成员。
	/// </summary>
	public interface IAssembly : ICSharpCode.TypeSystem.IAssembly, ICompilationProvider
	{
		/// <summary>
		/// Gets the unresolved assembly from which this assembly was created.
		/// </summary>
		new IUnresolvedAssembly UnresolvedAssembly { get; }
		
		/// <summary>
		/// Gets the type resolve context for this assembly.
		/// NRefactory 特有成员，不存在于 Abstractions 中。
		/// </summary>
		ITypeResolveContext TypeResolveContext { get; }
		
		/// <summary>
		/// Gets the assembly attributes.
		/// </summary>
		new IList<IAttribute> AssemblyAttributes { get; }
		
		/// <summary>
		/// Gets the top-level type definitions.
		/// </summary>
		new IEnumerable<ITypeDefinition> TopLevelTypeDefinitions { get; }
		
		/// <summary>
		/// Gets the root namespace.
		/// </summary>
		new INamespace RootNamespace { get; }
		
		/// <summary>
		/// Gets a type definition by its full type name.
		/// </summary>
		new ITypeDefinition GetTypeDefinition(TopLevelTypeName fullTypeName);
		
		/// <summary>
		/// Gets a type definition by its full type name (as a string).
		/// NRefactory 特有成员，不存在于 Abstractions 中。
		/// </summary>
		ITypeDefinition GetTypeDefinition(string ns, string name, int typeParameterCount = 0);
	}
}
