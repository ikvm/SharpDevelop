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
	/// Base class for the visitor pattern on <see cref="IType"/>.
	/// </summary>
	public abstract class TypeVisitor
	{
		/// <summary>
		/// Checks if the specified type parameter can be used as the given type.
		/// </summary>
		public static bool CanBeUsedAs(ITypeParameter typeParameter, IType type)
		{
			if (typeParameter == null)
				throw new ArgumentNullException("typeParameter");
			if (type == null)
				throw new ArgumentNullException("type");
			if (typeParameter.Equals(type))
				return true;
			if (type.Kind == TypeKind.Unknown)
				return true;
			IType effectiveBaseClass = typeParameter.EffectiveBaseClass;
			if (effectiveBaseClass != null && effectiveBaseClass.Kind != TypeKind.Unknown) {
				if (effectiveBaseClass.Equals(type) || effectiveBaseClass.GetDefinition().IsDerivedFrom(type.GetDefinition()))
					return true;
			}
			foreach (IType iface in typeParameter.EffectiveInterfaceTypes) {
				if (iface.Equals(type))
					return true;
				ITypeDefinition ifaceDef = iface.GetDefinition();
				if (ifaceDef != null && ifaceDef.IsDerivedFrom(type.GetDefinition()))
					return true;
			}
			if (type.Kind == TypeKind.Class && typeParameter.HasReferenceTypeConstraint) {
				ITypeDefinition typeDef = type.GetDefinition();
				if (typeDef != null && typeDef.KnownTypeCode == KnownTypeCode.Object)
					return true;
			}
			if (type.Kind == TypeKind.TypeParameter) {
				var otherTP = (ITypeParameter)type;
				if (typeParameter.HasValueTypeConstraint && !otherTP.HasValueTypeConstraint)
					return false;
				if (typeParameter.HasReferenceTypeConstraint && !otherTP.HasReferenceTypeConstraint)
					return false;
				return true;
			}
			return false;
		}
		
		public virtual IType VisitTypeDefinition(ITypeDefinition type)
		{
			return type.VisitChildren(this);
		}
		
		public virtual IType VisitTypeParameter(ITypeParameter type)
		{
			return type.VisitChildren(this);
		}
		
		public virtual IType VisitParameterizedType(ParameterizedType type)
		{
			return type.VisitChildren(this);
		}
		
		public virtual IType VisitArrayType(ArrayType type)
		{
			return type.VisitChildren(this);
		}
		
		public virtual IType VisitPointerType(PointerType type)
		{
			return type.VisitChildren(this);
		}
		
		public virtual IType VisitByReferenceType(ByReferenceType type)
		{
			return type.VisitChildren(this);
		}
		
		public virtual IType VisitOtherType(IType type)
		{
			return type.VisitChildren(this);
		}
	}
}
