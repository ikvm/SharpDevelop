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
	/// 继承自 Abstractions 中的 IUnresolvedTypeDefinition，使 NRefactory 类型同时满足两个接口。
	/// </summary>
	public interface IUnresolvedTypeDefinition : ICSharpCode.TypeSystem.IUnresolvedTypeDefinition, ITypeReference, IUnresolvedEntity
	{
		/// <summary>使用 NRefactory 的 TypeKind 枚举类型</summary>
		new TypeKind Kind { get; }
		
		/// <summary>使用 NRefactory 的 FullTypeName 值类型</summary>
		new FullTypeName FullTypeName { get; }
		
		/// <summary>使用 NRefactory 的 IList 类型</summary>
		new IList<IUnresolvedTypeParameter> TypeParameters { get; }
		
		/// <summary>
		/// Gets the base types.
		/// </summary>
		new IList<ITypeReference> BaseTypes { get; }
		
		/// <summary>
		/// Gets the nested types.
		/// </summary>
		new IList<IUnresolvedTypeDefinition> NestedTypes { get; }
		
		/// <summary>
		/// Gets the members.
		/// </summary>
		new IList<IUnresolvedMember> Members { get; }
		
		/// <summary>
		/// Gets the methods.
		/// </summary>
		new IList<IUnresolvedMethod> Methods { get; }
		
		/// <summary>
		/// Gets the properties.
		/// </summary>
		new IList<IUnresolvedProperty> Properties { get; }
		
		/// <summary>
		/// Gets the fields.
		/// </summary>
		new IList<IUnresolvedField> Fields { get; }
		
		/// <summary>
		/// Gets the events.
		/// </summary>
		new IList<IUnresolvedEvent> Events { get; }
		
		/// <summary>
		/// Resolves this type reference and returns the resolved type.
		/// </summary>
		new IType Resolve(ITypeResolveContext context);
		
		/// <summary>
		/// Creates the resolved type definition.
		/// </summary>
		new ITypeDefinition CreateResolvedTypeDefinition(ITypeResolveContext context);
		
		// IsPartial, AddPartialPart 继承自基接口
	}
	
	/// <summary>
	/// 继承自 Abstractions 中的 ITypeDefinition，使 NRefactory 类型同时满足两个接口。
	/// 使用 new 关键字隐藏返回 NRefactory 特定类型的成员。
	/// </summary>
	public interface ITypeDefinition : ICSharpCode.TypeSystem.ITypeDefinition, IType, IEntity
	{
		/// <summary>使用 NRefactory 的 FullTypeName 值类型</summary>
		new FullTypeName FullTypeName { get; }
		
		/// <summary>使用 NRefactory 的 IList 类型</summary>
		new IList<ITypeParameter> TypeParameters { get; }
		
		/// <summary>使用 NRefactory 的 IList 类型</summary>
		new IList<ITypeDefinition> NestedTypes { get; }
		
		/// <summary>使用 NRefactory 的 IList 类型</summary>
		new IList<IMember> Members { get; }
		
		/// <summary>
		/// Gets the methods.
		/// </summary>
		new IEnumerable<IMethod> Methods { get; }
		
		/// <summary>
		/// Gets the properties.
		/// </summary>
		new IEnumerable<IProperty> Properties { get; }
		
		/// <summary>
		/// Gets the fields.
		/// </summary>
		new IEnumerable<IField> Fields { get; }
		
		/// <summary>
		/// Gets the events.
		/// </summary>
		new IEnumerable<IEvent> Events { get; }
		
		/// <summary>
		/// Gets the direct base types.
		/// </summary>
		new IEnumerable<IType> DirectBaseTypes { get; }
		
		/// <summary>
		/// Gets the declaring type definition (if this is a nested type).
		/// </summary>
		new ITypeDefinition DeclaringTypeDefinition { get; }
		
		/// <summary>使用 NRefactory 的 IType 类型</summary>
		new IType DeclaringType { get; }
		
		/// <summary>使用 NRefactory 的 IAssembly 类型</summary>
		new IAssembly ParentAssembly { get; }
		
		/// <summary>使用 NRefactory 的 KnownTypeCode 枚举</summary>
		new KnownTypeCode KnownTypeCode { get; }
		
		/// <summary>
		/// Gets whether this type definition is partial.
		/// </summary>
		// IsPartial 继承自基接口，类型为 bool，无需隐藏

		/// <summary>
		/// Gets the unresolved type definition parts.
		/// </summary>
		new IList<IUnresolvedTypeDefinition> Parts { get; }
		
		/// <summary>
		/// Gets the type kind.
		/// </summary>
		new TypeKind Kind { get; }
		
		/// <summary>
		/// Gets whether the type is a reference type or value type.
		/// </summary>
		// IsReferenceType 继承自基接口，类型为 bool?，无需隐藏

		/// <summary>
		/// Gets whether this type is a delegate type.
		/// </summary>
		// IsDelegate 继承自基接口，类型为 bool，无需隐藏

		/// <summary>
		/// Gets whether this type has unmanaged constraint.
		/// </summary>
		// IsUnmanagedConstraint 继承自基接口，类型为 bool，无需隐藏

		/// <summary>
		/// Gets whether this type has readonly struct constraint.
		/// </summary>
		// IsReadOnlyStruct 继承自基接口，类型为 bool，无需隐藏

		/// <summary>
		/// Gets whether this type has byref-like constraint.
		/// </summary>
		// IsByRefLike 继承自基接口，类型为 bool，无需隐藏

		/// <summary>
		/// Gets whether this type is a record.
		/// </summary>
		// IsRecord 继承自基接口，类型为 bool，无需隐藏

		/// <summary>
		/// Gets the accessibility.
		/// </summary>
		new Accessibility Accessibility { get; }
	}
}
