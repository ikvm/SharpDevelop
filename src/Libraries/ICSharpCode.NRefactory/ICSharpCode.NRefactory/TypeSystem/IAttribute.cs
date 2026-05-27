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
using ICSharpCode.NRefactory.Semantics;

namespace ICSharpCode.NRefactory.TypeSystem
{
	/// <summary>
	/// 继承自 Abstractions 中的 IUnresolvedAttribute，使 NRefactory 类型同时满足两个接口。
	/// Abstractions 的 IUnresolvedAttribute 只有 Region 和 CreateResolvedAttribute，
	/// NRefactory 额外添加了 AttributeType 成员。
	/// </summary>
	public interface IUnresolvedAttribute : ICSharpCode.TypeSystem.IUnresolvedAttribute
	{
		/// <summary>
		/// Gets the attribute type reference.
		/// NRefactory 特有成员，不存在于 Abstractions 中。
		/// </summary>
		ITypeReference AttributeType { get; }
		
		/// <summary>
		/// Creates the resolved attribute.
		/// 参数类型为 NRefactory 的 ITypeResolveContext，与 Abstractions 的不同，因此是独立的方法。
		/// </summary>
		IAttribute CreateResolvedAttribute(ITypeResolveContext context);
	}
	
	/// <summary>
	/// 继承自 Abstractions 中的 IAttribute，使 NRefactory 类型同时满足两个接口。
	/// 使用 new 关键字隐藏返回 NRefactory 特定类型的成员。
	/// 注意：NamedArguments 的键类型不同（NRefactory 用 string，Abstractions 用 IMember）。
	/// </summary>
	public interface IAttribute : ICSharpCode.TypeSystem.IAttribute
	{
		/// <summary>
		/// Gets the attribute type。
		/// 使用 NRefactory 的 IType 类型。
		/// </summary>
		new IType AttributeType { get; }
		
		/// <summary>
		/// Gets the constructor being used.
		/// 使用 NRefactory 的 IMethod 类型。
		/// </summary>
		new IMethod Constructor { get; }
		
		/// <summary>
		/// Gets the positional arguments.
		/// 使用 NRefactory 的 ResolveResult 类型。
		/// </summary>
		new IList<ResolveResult> PositionalArguments { get; }
		
		/// <summary>
		/// Gets the named arguments.
		/// NRefactory 使用 string 作为键类型，Abstractions 使用 IMember。
		/// </summary>
		new IList<KeyValuePair<string, ResolveResult>> NamedArguments { get; }
	}
}
