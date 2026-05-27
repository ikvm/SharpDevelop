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

namespace ICSharpCode.NRefactory.TypeSystem
{
	/// <summary>
	/// 继承自 Abstractions 中的 ITypeReference，使 NRefactory 类型同时满足两个接口。
	/// 使用 new 关键字隐藏返回 NRefactory 特定类型的成员。
	/// </summary>
	public interface ITypeReference : ICSharpCode.TypeSystem.ITypeReference
	{
		/// <summary>
		/// Resolves this type reference.
		/// </summary>
		new IType Resolve(ITypeResolveContext context);
	}
	
	/// <summary>
	/// 继承自 Abstractions 中的 ITypeResolveContext，使 NRefactory 类型同时满足两个接口。
	/// 使用 new 关键字隐藏返回 NRefactory 特定类型的成员。
	/// </summary>
	public interface ITypeResolveContext : ICSharpCode.TypeSystem.ITypeResolveContext
	{
		/// <summary>
		/// Gets the current compilation.
		/// </summary>
		new ICompilation Compilation { get; }
		
		/// <summary>
		/// Gets the current assembly.
		/// This property may return null if this context does not specify any assembly.
		/// </summary>
		new IAssembly CurrentAssembly { get; }
		
		/// <summary>
		/// Gets the current type definition.
		/// </summary>
		new ITypeDefinition CurrentTypeDefinition { get; }
		
		/// <summary>
		/// Gets the current member.
		/// </summary>
		new IMember CurrentMember { get; }
		
		new ITypeResolveContext WithCurrentTypeDefinition(ITypeDefinition typeDefinition);
		new ITypeResolveContext WithCurrentMember(IMember member);
	}
}