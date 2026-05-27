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
	/// 继承自 Abstractions 中的 IMemberReference，使 NRefactory 类型同时满足两个接口。
	/// Resolve 方法参数类型不同，是独立方法，不需要 new 关键字。
	/// </summary>
	public interface IMemberReference : ICSharpCode.TypeSystem.IMemberReference, ISymbolReference, ITypeReference
	{
		/// <summary>
		/// Resolves the member reference.
		/// </summary>
		IMember Resolve(ITypeResolveContext context);
	}
	
	/// <summary>
	/// 继承自 Abstractions 中的 IMember，使 NRefactory 类型同时满足两个接口。
	/// 策略：
	/// - 简单接口返回类型（IType, IMember 等）：不使用 new，依赖 C# 9 协变返回类型
	/// - 枚举返回类型：使用 new 关键字（枚举不能隐式转换）
	/// - 泛型容器返回类型（IList&lt;T&gt;）：使用 new 关键字（IList 不支持协变）
	/// - 参数类型不同的方法：不需要 new（是不同的方法）
	/// </summary>
	public interface IMember : ICSharpCode.TypeSystem.IMember, IEntity, IMemberReference
	{
		/// <summary>使用 new 因为返回类型是 NRefactory 的 IMember</summary>
		new IMember MemberDefinition { get; }
		
		/// <summary>使用 new 因为返回类型是 NRefactory 的 IUnresolvedMember</summary>
		new IUnresolvedMember UnresolvedMember { get; }
		
		/// <summary>使用 new 因为返回类型是 NRefactory 的 IType</summary>
		new IType ReturnType { get; }
		
		/// <summary>使用 new 因为 IList 不支持协变</summary>
		new IList<IMember> ImplementedInterfaceMembers { get; }
		
		/// <summary>使用 new 因为返回类型是 NRefactory 的 IMemberReference</summary>
		new IMemberReference ToReference();
		new IMemberReference ToMemberReference();
		
		/// <summary>使用 new 因为 TypeParameterSubstitution 类型不同</summary>
		new TypeParameterSubstitution Substitution { get; }
		
		/// <summary>使用 new 因为返回类型是 NRefactory 的 IMember</summary>
		new IMember Specialize(TypeParameterSubstitution substitution);
		
		/// <summary>使用 new 因为 Accessibility 枚举不能隐式转换</summary>
		new Accessibility AccessibilityDomain { get; }
		
		/// <summary>参数类型不同，是独立方法</summary>
		new bool IsAccessibleFrom(IAssembly assembly);
	}
}
