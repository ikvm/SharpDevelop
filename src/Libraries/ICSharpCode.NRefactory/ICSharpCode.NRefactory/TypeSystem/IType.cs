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
	/// 继承自 Abstractions 中的 IType，使 NRefactory 类型同时满足两个接口。
	/// 使用 new 关键字隐藏返回 NRefactory 特定类型的成员。
	/// </summary>
	public interface IType : ICSharpCode.TypeSystem.IType, INamedElement, IEquatable<IType>
	{
		/// <summary>
		/// Gets the type kind.
		/// 使用 NRefactory 的 TypeKind 枚举类型。
		/// </summary>
		new TypeKind Kind { get; }
		
		/// <summary>
		/// Gets the underlying type definition.
		/// </summary>
		new ITypeDefinition GetDefinition();
		
		/// <summary>
		/// Gets the parent type, if this is a nested type.
		/// </summary>
		new IType DeclaringType { get; }
		
		/// <summary>
		/// Gets the type arguments passed to this type.
		/// </summary>
		new IList<IType> TypeArguments { get; }

		/// <summary>
		/// Calls ITypeVisitor.Visit for this type.
		/// </summary>
		new IType AcceptVisitor(TypeVisitor visitor);
		
		/// <summary>
		/// Calls ITypeVisitor.Visit for all children of this type.
		/// </summary>
		new IType VisitChildren(TypeVisitor visitor);
		
		/// <summary>
		/// Gets the direct base types.
		/// </summary>
		new IEnumerable<IType> DirectBaseTypes { get; }
		
		/// <summary>
		/// Creates a type reference.
		/// </summary>
		new ITypeReference ToTypeReference();

		/// <summary>
		/// Gets inner classes (including inherited inner classes).
		/// </summary>
		new IEnumerable<IType> GetNestedTypes(Predicate<ITypeDefinition> filter = null, GetMemberOptions options = GetMemberOptions.None);
		
		/// <summary>
		/// Gets inner classes with additional type parameters.
		/// </summary>
		new IEnumerable<IType> GetNestedTypes(IList<IType> typeArguments, Predicate<ITypeDefinition> filter = null, GetMemberOptions options = GetMemberOptions.None);
		
		/// <summary>
		/// Gets all instance constructors for this type.
		/// </summary>
		new IEnumerable<IMethod> GetConstructors(Predicate<IUnresolvedMethod> filter = null, GetMemberOptions options = GetMemberOptions.IgnoreInheritedMembers);
		
		/// <summary>
		/// Gets all methods that can be called on this type.
		/// </summary>
		new IEnumerable<IMethod> GetMethods(Predicate<IUnresolvedMethod> filter = null, GetMemberOptions options = GetMemberOptions.None);
		
		/// <summary>
		/// Gets all generic methods with the specified type arguments.
		/// </summary>
		new IEnumerable<IMethod> GetMethods(IList<IType> typeArguments, Predicate<IUnresolvedMethod> filter = null, GetMemberOptions options = GetMemberOptions.None);
		
		/// <summary>
		/// Gets all properties that can be called on this type.
		/// </summary>
		new IEnumerable<IProperty> GetProperties(Predicate<IUnresolvedProperty> filter = null, GetMemberOptions options = GetMemberOptions.None);
		
		/// <summary>
		/// Gets all fields that can be accessed on this type.
		/// </summary>
		new IEnumerable<IField> GetFields(Predicate<IUnresolvedField> filter = null, GetMemberOptions options = GetMemberOptions.None);
		
		/// <summary>
		/// Gets all events that can be accessed on this type.
		/// </summary>
		new IEnumerable<IEvent> GetEvents(Predicate<IUnresolvedEvent> filter = null, GetMemberOptions options = GetMemberOptions.None);
		
		/// <summary>
		/// Gets all members that can be called on this type.
		/// </summary>
		new IEnumerable<IMember> GetMembers(Predicate<IUnresolvedMember> filter = null, GetMemberOptions options = GetMemberOptions.None);
		
		/// <summary>
		/// Gets all accessors belonging to properties or events on this type.
		/// </summary>
		new IEnumerable<IMethod> GetAccessors(Predicate<IUnresolvedMethod> filter = null, GetMemberOptions options = GetMemberOptions.None);
	}
	
	/// <summary>
	/// 保留 NRefactory 自己的 GetMemberOptions 枚举，与 Abstractions 中的值完全相同。
	/// 这是为了避免在 NRefactory 代码中添加 using 别名的需要。
	/// </summary>
	public enum GetMemberOptions
	{
		None = 0x00,
		ReturnMemberDefinitions = 0x01,
		IgnoreInheritedMembers = 0x02
	}
}
