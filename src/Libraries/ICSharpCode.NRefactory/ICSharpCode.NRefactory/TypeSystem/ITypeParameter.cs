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
	/// 保留 NRefactory 自己的 VarianceModifier 枚举，与 Abstractions 中的值完全相同。
	/// 这是为了避免在 NRefactory 代码中添加 using 别名的需要。
	/// </summary>
	public enum VarianceModifier : byte
	{
		/// <summary>
		/// The type parameter is not variant.
		/// </summary>
		Invariant,
		/// <summary>
		/// The type parameter is covariant (used in output position).
		/// </summary>
		Covariant,
		/// <summary>
		/// The type parameter is contravariant (used in input position).
		/// </summary>
		Contravariant
	}
	
	/// <summary>
	/// 继承自 Abstractions 中的 IUnresolvedTypeParameter，使 NRefactory 类型同时满足两个接口。
	/// </summary>
	public interface IUnresolvedTypeParameter : ICSharpCode.TypeSystem.IUnresolvedTypeParameter, INamedElement
	{
		/// <summary>
		/// 使用 NRefactory 的 SymbolKind 枚举类型。
		/// </summary>
		new SymbolKind OwnerType { get; }
		
		/// <summary>
		/// Gets the list of attributes on this type parameter.
		/// </summary>
		new IList<IUnresolvedAttribute> Attributes { get; }
		
		/// <summary>
		/// Gets the variance of this type parameter.
		/// </summary>
		new VarianceModifier Variance { get; }
		
		/// <summary>
		/// Creates the resolved type parameter.
		/// </summary>
		new ITypeParameter CreateResolvedTypeParameter(ITypeResolveContext context);
	}
	
	/// <summary>
	/// 继承自 Abstractions 中的 ITypeParameter，使 NRefactory 类型同时满足两个接口。
	/// 使用 new 关键字隐藏返回 NRefactory 特定类型的成员。
	/// </summary>
	public interface ITypeParameter : ICSharpCode.TypeSystem.ITypeParameter, IType, ISymbol
	{
		/// <summary>
		/// 使用 NRefactory 的 SymbolKind 枚举类型。
		/// </summary>
		new SymbolKind SymbolKind { get; }
		
		/// <summary>
		/// Gets the type of this type parameter's owner.
		/// Uses NRefactory's SymbolKind enum.
		/// </summary>
		new SymbolKind OwnerType { get; }
		
		/// <summary>
		/// Gets the owner of this type parameter.
		/// </summary>
		new IEntity Owner { get; }
		
		/// <summary>
		/// Gets/Sets the name of the type parameter.
		/// </summary>
		new string Name { get; }
		
		/// <summary>
		/// Gets the type parameter's variance (covariance/contravariance).
		/// </summary>
		new VarianceModifier Variance { get; }
		
		/// <summary>
		/// Gets the position of this type parameter in the type parameter list of the owner.
		/// </summary>
		// Index 继承自基接口，类型为 int，无需隐藏

		/// <summary>
		/// Gets the list of attributes on this type parameter.
		/// </summary>
		new IList<IAttribute> Attributes { get; }
		
		/// <summary>
		/// Gets the direct base types.
		/// </summary>
		new IEnumerable<IType> DirectBaseTypes { get; }
		
		/// <summary>
		/// Gets the type constraints.
		/// </summary>
		new IList<IType> Constraints { get; }
		
		/// <summary>
		/// Gets whether this type parameter has the 'new()' constraint.
		/// </summary>
		// HasDefaultConstructorConstraint 继承自基接口，类型为 bool，无需隐藏

		/// <summary>
		/// Gets the region of this type parameter.
		/// </summary>
		// Region 继承自基接口（DomRegion 值类型在不同命名空间但结构相同）
		
		/// <summary>
		/// Gets the type parameter's effective base class.
		/// </summary>
		new IType EffectiveBaseClass { get; }
		
		/// <summary>
		/// Gets all effective interface types.
		/// </summary>
		new IEnumerable<IType> EffectiveInterfaceTypes { get; }
		
		/// <summary>
		/// Gets the effective interface set.
		/// </summary>
		new ICollection<IType> EffectiveInterfaceSet { get; }
		
		/// <summary>
		/// Gets whether this type parameter can be used with the specified type.
		/// </summary>
		new bool CanBeUsedAs(IType type);
		
		/// <summary>
		/// Gets the type parameter's effective base type.
		/// </summary>
		new IType EffectiveBaseType { get; }
	}
}
