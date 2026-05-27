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
using ICSharpCode.NRefactory.Documentation;

namespace ICSharpCode.NRefactory.TypeSystem
{
	/// <summary>
	/// 继承自 Abstractions 中的 IUnresolvedEntity，使 NRefactory 类型同时满足两个接口。
	/// </summary>
	public interface IUnresolvedEntity : ICSharpCode.TypeSystem.IUnresolvedEntity, INamedElement, IHasAccessibility
	{
		/// <summary>使用 NRefactory 的 SymbolKind 枚举类型</summary>
		new SymbolKind SymbolKind { get; }
		
		/// <summary>使用 NRefactory 的 DomRegion 值类型</summary>
		new DomRegion Region { get; }
		
		/// <summary>使用 NRefactory 的 DomRegion 值类型</summary>
		new DomRegion BodyRegion { get; }
		
		/// <summary>使用 NRefactory 的 IUnresolvedTypeDefinition 类型</summary>
		new IUnresolvedTypeDefinition DeclaringTypeDefinition { get; }
		
		/// <summary>使用 NRefactory 的 IUnresolvedFile 类型</summary>
		new IUnresolvedFile UnresolvedFile { get; }
		
		/// <summary>使用 NRefactory 的 IList 类型</summary>
		new IList<IUnresolvedAttribute> Attributes { get; }
		
		// IsStatic, IsAbstract, IsSealed, IsShadowing, IsSynthetic 继承自基接口，类型为 bool
	}
	
	/// <summary>
	/// 继承自 Abstractions 中的 IEntity，使 NRefactory 类型同时满足两个接口。
	/// 使用 new 关键字隐藏返回 NRefactory 特定类型的成员。
	/// </summary>
	public interface IEntity : ICSharpCode.TypeSystem.IEntity, ISymbol, ICompilationProvider, INamedElement, IHasAccessibility
	{
		/// <summary>使用 NRefactory 的 EntityType 枚举</summary>
		[Obsolete("Use the SymbolKind property instead.")]
		new EntityType EntityType { get; }
		
		/// <summary>使用 NRefactory 的 DomRegion 值类型</summary>
		new DomRegion Region { get; }
		
		/// <summary>使用 NRefactory 的 DomRegion 值类型</summary>
		new DomRegion BodyRegion { get; }
		
		/// <summary>使用 NRefactory 的 ITypeDefinition 类型</summary>
		new ITypeDefinition DeclaringTypeDefinition { get; }
		
		/// <summary>使用 NRefactory 的 IType 类型</summary>
		new IType DeclaringType { get; }
		
		/// <summary>使用 NRefactory 的 IAssembly 类型</summary>
		new IAssembly ParentAssembly { get; }
		
		/// <summary>使用 NRefactory 的 IList 类型</summary>
		new IList<IAttribute> Attributes { get; }
		
		/// <summary>NRefactory 特有成员，不存在于 Abstractions 中</summary>
		DocumentationComment Documentation { get; }
		
		// IsStatic, IsAbstract, IsSealed, IsShadowing, IsSynthetic 继承自基接口
	}
}
