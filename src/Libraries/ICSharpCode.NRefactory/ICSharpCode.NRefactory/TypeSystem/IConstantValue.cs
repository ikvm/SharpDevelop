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
using ICSharpCode.NRefactory.Semantics;

namespace ICSharpCode.NRefactory.TypeSystem
{
	/// <summary>
	/// 继承自 Abstractions 中的 IConstantValue，使 NRefactory 类型同时满足两个接口。
	/// Resolve 方法使用 NRefactory 的 ITypeResolveContext 和 ResolveResult 类型，
	/// 与 Abstractions 的同名方法参数类型不同，因此是独立的方法（不需要 new 关键字）。
	/// </summary>
	public interface IConstantValue : ICSharpCode.TypeSystem.IConstantValue
	{
		/// <summary>
		/// Resolves the value of this constant.
		/// 使用 NRefactory 的 ITypeResolveContext 和 ResolveResult 类型。
		/// </summary>
		ResolveResult Resolve(ITypeResolveContext context);
	}
}
