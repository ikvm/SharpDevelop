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
using System.Linq;
using System.Threading;

namespace ICSharpCode.NRefactory.TypeSystem.Implementation
{
	public sealed class DummyTypeParameter : AbstractType, ITypeParameter
	{
		static ITypeParameter[] methodTypeParameters = { new DummyTypeParameter(SymbolKind.Method, 0) };
		static ITypeParameter[] classTypeParameters = { new DummyTypeParameter(SymbolKind.TypeDefinition, 0) };
		
		public static ITypeParameter GetMethodTypeParameter(int index)
		{
			return GetTypeParameter(ref methodTypeParameters, SymbolKind.Method, index);
		}
		
		public static ITypeParameter GetClassTypeParameter(int index)
		{
			return GetTypeParameter(ref classTypeParameters, SymbolKind.TypeDefinition, index);
		}
		
		static ITypeParameter GetTypeParameter(ref ITypeParameter[] typeParameters, SymbolKind symbolKind, int index)
		{
			ITypeParameter[] tps = typeParameters;
			while (index >= tps.Length) {
				// We don't have a normal type parameter for this index, so we need to extend our array.
				// Because the array can be used concurrently from multiple threads, we have to use
				// Interlocked.CompareExchange.
				ITypeParameter[] newTps = new ITypeParameter[index + 1];
				tps.CopyTo(newTps, 0);
				for (int i = tps.Length; i < newTps.Length; i++) {
					newTps[i] = new DummyTypeParameter(symbolKind, i);
				}
				ITypeParameter[] oldTps = Interlocked.CompareExchange(ref typeParameters, newTps, tps);
				if (oldTps == tps) {
					// exchange successful
					tps = newTps;
				} else {
					// exchange not successful
					tps = oldTps;
				}
			}
			return tps[index];
		}
		
		sealed class NormalizeMethodTypeParametersVisitor : TypeVisitor
		{
			public override IType VisitTypeParameter(ITypeParameter type)
			{
				if (type.OwnerType == SymbolKind.Method) {
					return DummyTypeParameter.GetMethodTypeParameter(type.Index);
				} else {
					return base.VisitTypeParameter(type);
				}
			}
		}
		sealed class NormalizeClassTypeParametersVisitor : TypeVisitor
		{
			public override IType VisitTypeParameter(ITypeParameter type)
			{
				if (type.OwnerType == SymbolKind.TypeDefinition) {
					return DummyTypeParameter.GetClassTypeParameter(type.Index);
				} else {
					return base.VisitTypeParameter(type);
				}
			}
		}
		
		static readonly NormalizeMethodTypeParametersVisitor normalizeMethodTypeParameters = new NormalizeMethodTypeParametersVisitor();
		static readonly NormalizeClassTypeParametersVisitor normalizeClassTypeParameters = new NormalizeClassTypeParametersVisitor();
		
		/// <summary>
		/// Replaces all occurrences of method type parameters in the given type
		/// by normalized type parameters. This allows comparing parameter types from different
		/// generic methods.
		/// </summary>
		public static IType NormalizeMethodTypeParameters(IType type)
		{
			return type.AcceptVisitor(normalizeMethodTypeParameters);
		}
		
		/// <summary>
		/// Replaces all occurrences of class type parameters in the given type
		/// by normalized type parameters. This allows comparing parameter types from different
		/// generic methods.
		/// </summary>
		public static IType NormalizeClassTypeParameters(IType type)
		{
			return type.AcceptVisitor(normalizeClassTypeParameters);
		}
		
		/// <summary>
		/// Replaces all occurrences of class and method type parameters in the given type
		/// by normalized type parameters. This allows comparing parameter types from different
		/// generic methods.
		/// </summary>
		public static IType NormalizeAllTypeParameters(IType type)
		{
			return type.AcceptVisitor(normalizeClassTypeParameters).AcceptVisitor(normalizeMethodTypeParameters);
		}
		
		readonly SymbolKind ownerType;
		readonly int index;
		
		private DummyTypeParameter(SymbolKind ownerType, int index)
		{
			this.ownerType = ownerType;
			this.index = index;
		}
		
		SymbolKind ISymbol.SymbolKind {
			get { return SymbolKind.TypeParameter; }
		}
		
		public override string Name {
			get {
				return (ownerType == SymbolKind.Method ? "!!" : "!") + index;
			}
		}
		
		public override string ReflectionName {
			get {
				return (ownerType == SymbolKind.Method ? "``" : "`") + index;
			}
		}
		
		public override string ToString()
		{
			return ReflectionName + " (dummy)";
		}
		
		public override bool? IsReferenceType {
			get { return null; }
		}
		
		public override TypeKind Kind {
			get { return TypeKind.TypeParameter; }
		}
		
		public override ITypeReference ToTypeReference()
		{
			return TypeParameterReference.Create(ownerType, index);
		}
		
		public override IType AcceptVisitor(TypeVisitor visitor)
		{
			return visitor.VisitTypeParameter(this);
		}
		
		public int Index {
			get { return index; }
		}
		
		IList<IAttribute> ITypeParameter.Attributes {
			get { return EmptyList<IAttribute>.Instance; }
		}
		
		SymbolKind ITypeParameter.SymbolKind {
			get { return SymbolKind.TypeParameter; }
		}
		
		public SymbolKind OwnerType {
			get { return ownerType; }
		}
		
		VarianceModifier ITypeParameter.Variance {
			get { return VarianceModifier.Invariant; }
		}

		IEntity ITypeParameter.Owner {
			get { return null; }
		}

		IType ITypeParameter.EffectiveBaseClass {
			get { return SpecialType.UnknownType; }
		}
		
		IList<IType> ITypeParameter.Constraints {
			get { return EmptyList<IType>.Instance; }
		}
		
		IEnumerable<IType> ITypeParameter.EffectiveInterfaceTypes {
			get { return EmptyList<IType>.Instance; }
		}
		
		ICollection<IType> ITypeParameter.EffectiveInterfaceSet {
			get { return EmptyList<IType>.Instance; }
		}
		
		bool ITypeParameter.CanBeUsedAs(IType type) {
			return TypeVisitor.CanBeUsedAs(this, type);
		}
		
		IType ITypeParameter.EffectiveBaseType {
			get { return SpecialType.UnknownType; }
		}

		public ISymbolReference ToReference()
		{
			return new TypeParameterReference(ownerType, index);
		}
		
		#region 显式实现 Abstractions 接口成员
		ICSharpCode.TypeSystem.SymbolKind ICSharpCode.TypeSystem.ISymbol.SymbolKind => (ICSharpCode.TypeSystem.SymbolKind)(byte)SymbolKind.TypeParameter;
		ICSharpCode.TypeSystem.TypeKind ICSharpCode.TypeSystem.IType.Kind => (ICSharpCode.TypeSystem.TypeKind)(byte)TypeKind.TypeParameter;
		ICSharpCode.TypeSystem.ITypeDefinition ICSharpCode.TypeSystem.IType.GetDefinition() => null;
		ICSharpCode.TypeSystem.IType ICSharpCode.TypeSystem.IType.DeclaringType => null;
		System.Collections.Generic.IList<ICSharpCode.TypeSystem.IType> ICSharpCode.TypeSystem.IType.TypeArguments => new CastList<IType, ICSharpCode.TypeSystem.IType>(EmptyList<IType>.Instance);
		ICSharpCode.TypeSystem.IType ICSharpCode.TypeSystem.IType.AcceptVisitor(ICSharpCode.TypeSystem.TypeVisitor visitor) => visitor.VisitTypeParameter(this);
		ICSharpCode.TypeSystem.IType ICSharpCode.TypeSystem.IType.VisitChildren(ICSharpCode.TypeSystem.TypeVisitor visitor) => this;
		System.Collections.Generic.IEnumerable<ICSharpCode.TypeSystem.IType> ICSharpCode.TypeSystem.IType.DirectBaseTypes => DirectBaseTypes.Cast<ICSharpCode.TypeSystem.IType>();
		ICSharpCode.TypeSystem.ITypeReference ICSharpCode.TypeSystem.IType.ToTypeReference() => ToTypeReference();
		ICSharpCode.TypeSystem.TypeParameterSubstitution ICSharpCode.TypeSystem.IType.GetSubstitution() => GetSubstitution();
		ICSharpCode.TypeSystem.TypeParameterSubstitution ICSharpCode.TypeSystem.IType.GetSubstitution(System.Collections.Generic.IList<ICSharpCode.TypeSystem.IType> methodTypeArguments) => GetSubstitution();
		System.Collections.Generic.IEnumerable<ICSharpCode.TypeSystem.IType> ICSharpCode.TypeSystem.IType.GetNestedTypes(System.Predicate<ICSharpCode.TypeSystem.ITypeDefinition> filter, ICSharpCode.TypeSystem.GetMemberOptions options) => EmptyList<ICSharpCode.TypeSystem.IType>.Instance;
		System.Collections.Generic.IEnumerable<ICSharpCode.TypeSystem.IType> ICSharpCode.TypeSystem.IType.GetNestedTypes(System.Collections.Generic.IList<ICSharpCode.TypeSystem.IType> typeArguments, System.Predicate<ICSharpCode.TypeSystem.ITypeDefinition> filter, ICSharpCode.TypeSystem.GetMemberOptions options) => EmptyList<ICSharpCode.TypeSystem.IType>.Instance;
		System.Collections.Generic.IEnumerable<ICSharpCode.TypeSystem.IMethod> ICSharpCode.TypeSystem.IType.GetConstructors(System.Predicate<ICSharpCode.TypeSystem.IUnresolvedMethod> filter, ICSharpCode.TypeSystem.GetMemberOptions options) => EmptyList<ICSharpCode.TypeSystem.IMethod>.Instance;
		System.Collections.Generic.IEnumerable<ICSharpCode.TypeSystem.IMethod> ICSharpCode.TypeSystem.IType.GetMethods(System.Predicate<ICSharpCode.TypeSystem.IUnresolvedMethod> filter, ICSharpCode.TypeSystem.GetMemberOptions options) => EmptyList<ICSharpCode.TypeSystem.IMethod>.Instance;
		System.Collections.Generic.IEnumerable<ICSharpCode.TypeSystem.IMethod> ICSharpCode.TypeSystem.IType.GetMethods(System.Collections.Generic.IList<ICSharpCode.TypeSystem.IType> typeArguments, System.Predicate<ICSharpCode.TypeSystem.IUnresolvedMethod> filter, ICSharpCode.TypeSystem.GetMemberOptions options) => EmptyList<ICSharpCode.TypeSystem.IMethod>.Instance;
		System.Collections.Generic.IEnumerable<ICSharpCode.TypeSystem.IProperty> ICSharpCode.TypeSystem.IType.GetProperties(System.Predicate<ICSharpCode.TypeSystem.IUnresolvedProperty> filter, ICSharpCode.TypeSystem.GetMemberOptions options) => EmptyList<ICSharpCode.TypeSystem.IProperty>.Instance;
		System.Collections.Generic.IEnumerable<ICSharpCode.TypeSystem.IField> ICSharpCode.TypeSystem.IType.GetFields(System.Predicate<ICSharpCode.TypeSystem.IUnresolvedField> filter, ICSharpCode.TypeSystem.GetMemberOptions options) => EmptyList<ICSharpCode.TypeSystem.IField>.Instance;
		System.Collections.Generic.IEnumerable<ICSharpCode.TypeSystem.IEvent> ICSharpCode.TypeSystem.IType.GetEvents(System.Predicate<ICSharpCode.TypeSystem.IUnresolvedEvent> filter, ICSharpCode.TypeSystem.GetMemberOptions options) => EmptyList<ICSharpCode.TypeSystem.IEvent>.Instance;
		System.Collections.Generic.IEnumerable<ICSharpCode.TypeSystem.IMember> ICSharpCode.TypeSystem.IType.GetMembers(System.Predicate<ICSharpCode.TypeSystem.IUnresolvedMember> filter, ICSharpCode.TypeSystem.GetMemberOptions options) => EmptyList<ICSharpCode.TypeSystem.IMember>.Instance;
		System.Collections.Generic.IEnumerable<ICSharpCode.TypeSystem.IMethod> ICSharpCode.TypeSystem.IType.GetAccessors(System.Predicate<ICSharpCode.TypeSystem.IUnresolvedMethod> filter, ICSharpCode.TypeSystem.GetMemberOptions options) => EmptyList<ICSharpCode.TypeSystem.IMethod>.Instance;
		ICSharpCode.TypeSystem.SymbolKind ICSharpCode.TypeSystem.ITypeParameter.OwnerType => (ICSharpCode.TypeSystem.SymbolKind)(byte)ownerType;
		ICSharpCode.TypeSystem.IEntity ICSharpCode.TypeSystem.ITypeParameter.Owner => null;
		System.Collections.Generic.IList<ICSharpCode.TypeSystem.IAttribute> ICSharpCode.TypeSystem.ITypeParameter.Attributes => EmptyList<ICSharpCode.TypeSystem.IAttribute>.Instance;
		ICSharpCode.TypeSystem.VarianceModifier ICSharpCode.TypeSystem.ITypeParameter.Variance => (ICSharpCode.TypeSystem.VarianceModifier)(byte)VarianceModifier.Invariant;
		ICSharpCode.TypeSystem.DomRegion ICSharpCode.TypeSystem.ITypeParameter.Region => new ICSharpCode.TypeSystem.DomRegion();
		ICSharpCode.TypeSystem.IType ICSharpCode.TypeSystem.ITypeParameter.EffectiveBaseClass => SpecialType.UnknownType;
		System.Collections.Generic.ICollection<ICSharpCode.TypeSystem.IType> ICSharpCode.TypeSystem.ITypeParameter.EffectiveInterfaceSet => EmptyList<ICSharpCode.TypeSystem.IType>.Instance;
		bool ICSharpCode.TypeSystem.ITypeParameter.HasDefaultConstructorConstraint => false;
		bool ICSharpCode.TypeSystem.ITypeParameter.HasReferenceTypeConstraint => false;
		bool ICSharpCode.TypeSystem.ITypeParameter.HasValueTypeConstraint => false;
		ICSharpCode.TypeSystem.ISymbolReference ICSharpCode.TypeSystem.ISymbol.ToReference() => ToReference();
		bool System.IEquatable<ICSharpCode.TypeSystem.IType>.Equals(ICSharpCode.TypeSystem.IType other) => Equals(other as IType);
		#endregion
	}
}
