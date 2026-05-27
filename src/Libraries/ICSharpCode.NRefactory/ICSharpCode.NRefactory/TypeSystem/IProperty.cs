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
	/// 继承自 Abstractions 中的 IUnresolvedProperty，使 NRefactory 类型同时满足两个接口。
	/// </summary>
	public interface IUnresolvedProperty : ICSharpCode.TypeSystem.IUnresolvedProperty, IUnresolvedParameterizedMember
	{
		/// <summary>
		/// Gets the getter method.
		/// </summary>
		new IUnresolvedMethod Getter { get; }
		
		/// <summary>
		/// Gets the setter method.
		/// </summary>
		new IUnresolvedMethod Setter { get; }
		
		/// <summary>
		/// Resolves this member reference.
		/// </summary>
		new IProperty Resolve(ITypeResolveContext context);
	}
	
	/// <summary>
	/// 继承自 Abstractions 中的 IProperty，使 NRefactory 类型同时满足两个接口。
	/// 使用 new 关键字隐藏返回 NRefactory 特定类型的成员。
	/// </summary>
	public interface IProperty : ICSharpCode.TypeSystem.IProperty, IParameterizedMember
	{
		/// <summary>
		/// Gets whether this property can be read.
		/// </summary>
		// CanGet 继承自基接口，类型为 bool，无需隐藏

		/// <summary>
		/// Gets whether this property can be set.
		/// </summary>
		// CanSet 继承自基接口，类型为 bool，无需隐藏

		/// <summary>
		/// Gets the getter method.
		/// </summary>
		new IMethod Getter { get; }
		
		/// <summary>
		/// Gets the setter method.
		/// </summary>
		new IMethod Setter { get; }
		
		/// <summary>
		/// Gets whether this property is an indexer.
		/// </summary>
		// IsIndexer 继承自基接口，类型为 bool，无需隐藏

		/// <summary>
		/// Gets whether this is a WinRT property.
		/// </summary>
		// IsWinRtProperty 继承自基接口，类型为 bool，无需隐藏

		/// <summary>
		/// Gets the member on which this member is based on.
		/// Returns this member if it is not based on another member.
		/// </summary>
		new IMember MemberDefinition { get; }
		
		/// <summary>
		/// Creates a member reference that can be used to rediscover this member in another compilation.
		/// </summary>
		new IMemberReference ToReference();
	}
}
