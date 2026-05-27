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
using System.Globalization;
using System.Linq;

using ICSharpCode.NRefactory.Utils;

namespace ICSharpCode.NRefactory.TypeSystem.Implementation
{
	public abstract class AbstractTypeParameter : ITypeParameter, ICompilationProvider
	{
		readonly ICompilation compilation;
		readonly SymbolKind ownerType;
		readonly IEntity owner;
		readonly int index;
		readonly string name;
		readonly IList<IAttribute> attributes;
		readonly DomRegion region;
		readonly VarianceModifier variance;
		
		protected AbstractTypeParameter(IEntity owner, int index, string name, VarianceModifier variance, IList<IAttribute> attributes, DomRegion region)
		{
			if (owner == null)
				throw new ArgumentNullException("owner");
			this.owner = owner;
			this.compilation = owner.Compilation;
			this.ownerType = owner.SymbolKind;
			this.index = index;
			this.name = name ?? ((this.OwnerType == SymbolKind.Method ? "!!" : "!") + index.ToString(CultureInfo.InvariantCulture));
			this.attributes = attributes ?? EmptyList<IAttribute>.Instance;
			this.region = region;
			this.variance = variance;
		}
		
		protected AbstractTypeParameter(ICompilation compilation, SymbolKind ownerType, int index, string name, VarianceModifier variance, IList<IAttribute> attributes, DomRegion region)
		{
			if (compilation == null)
				throw new ArgumentNullException("compilation");
			this.compilation = compilation;
			this.ownerType = ownerType;
			this.index = index;
			this.name = name ?? ((this.OwnerType == SymbolKind.Method ? "!!" : "!") + index.ToString(CultureInfo.InvariantCulture));
			this.attributes = attributes ?? EmptyList<IAttribute>.Instance;
			this.region = region;
			this.variance = variance;
		}
		
		SymbolKind ISymbol.SymbolKind {
			get { return SymbolKind.TypeParameter; }
		}
		
		SymbolKind ITypeParameter.SymbolKind {
			get { return SymbolKind.TypeParameter; }
		}
		
		public SymbolKind OwnerType {
			get { return ownerType; }
		}
		
		public IEntity Owner {
			get { return owner; }
		}
		
		public int Index {
			get { return index; }
		}
		
		public IList<IAttribute> Attributes {
			get { return attributes; }
		}
		
		public VarianceModifier Variance {
			get { return variance; }
		}
		
		public DomRegion Region {
			get { return region; }
		}
		
		public ICompilation Compilation {
			get { return compilation; }
		}
		
		volatile IType effectiveBaseClass;
		
		public IType EffectiveBaseClass {
			get {
				if (effectiveBaseClass == null) {
					// protect against cyclic type parameters
					using (var busyLock = BusyManager.Enter(this)) {
						if (!busyLock.Success)
							return SpecialType.UnknownType; // don't cache this error
						effectiveBaseClass = CalculateEffectiveBaseClass();
					}
				}
				return effectiveBaseClass;
			}
		}
		
		IType CalculateEffectiveBaseClass()
		{
			if (HasValueTypeConstraint)
				return this.Compilation.FindType(KnownTypeCode.ValueType);
			
			List<IType> classTypeConstraints = new List<IType>();
			foreach (IType constraint in this.DirectBaseTypes) {
				if (constraint.Kind == TypeKind.Class) {
					classTypeConstraints.Add(constraint);
				} else if (constraint.Kind == TypeKind.TypeParameter) {
					IType baseClass = ((ITypeParameter)constraint).EffectiveBaseClass;
					if (baseClass.Kind == TypeKind.Class)
						classTypeConstraints.Add(baseClass);
				}
			}
			if (classTypeConstraints.Count == 0)
				return this.Compilation.FindType(KnownTypeCode.Object);
			// Find the derived-most type in the resulting set:
			IType result = classTypeConstraints[0];
			for (int i = 1; i < classTypeConstraints.Count; i++) {
				if (classTypeConstraints[i].GetDefinition().IsDerivedFrom(result.GetDefinition()))
					result = classTypeConstraints[i];
			}
			return result;
		}
		
		ICollection<IType> effectiveInterfaceSet;
		
		public ICollection<IType> EffectiveInterfaceSet {
			get {
				var result = LazyInit.VolatileRead(ref effectiveInterfaceSet);
				if (result != null) {
					return result;
				} else {
					// protect against cyclic type parameters
					using (var busyLock = BusyManager.Enter(this)) {
						if (!busyLock.Success)
							return EmptyList<IType>.Instance; // don't cache this error
						return LazyInit.GetOrSet(ref effectiveInterfaceSet, CalculateEffectiveInterfaceSet());
					}
				}
			}
		}
		
		ICollection<IType> CalculateEffectiveInterfaceSet()
		{
			HashSet<IType> result = new HashSet<IType>();
			foreach (IType constraint in this.DirectBaseTypes) {
				if (constraint.Kind == TypeKind.Interface) {
					result.Add(constraint);
				} else if (constraint.Kind == TypeKind.TypeParameter) {
					result.UnionWith(((ITypeParameter)constraint).EffectiveInterfaceTypes);
				}
			}
			return result;
		}
		
		public abstract bool HasDefaultConstructorConstraint { get; }
		public abstract bool HasReferenceTypeConstraint { get; }
		public abstract bool HasValueTypeConstraint { get; }
		
		public virtual IList<IType> Constraints {
			get { return EmptyList<IType>.Instance; }
		}
		
		public virtual IEnumerable<IType> EffectiveInterfaceTypes {
			get { return EffectiveInterfaceSet; }
		}
		
		public virtual bool CanBeUsedAs(IType type)
		{
			if (type == null)
				throw new ArgumentNullException("type");
			return TypeVisitor.CanBeUsedAs(this, type);
		}
		
		public virtual IType EffectiveBaseType {
			get { return EffectiveBaseClass; }
		}
		
		public TypeKind Kind {
			get { return TypeKind.TypeParameter; }
		}
		
		public bool? IsReferenceType {
			get {
				if (this.HasValueTypeConstraint)
					return false;
				if (this.HasReferenceTypeConstraint)
					return true;
				
				// A type parameter is known to be a reference type if it has the reference type constraint
				// or its effective base class is not object or System.ValueType.
				IType effectiveBaseClass = this.EffectiveBaseClass;
				if (effectiveBaseClass.Kind == TypeKind.Class || effectiveBaseClass.Kind == TypeKind.Delegate) {
					ITypeDefinition effectiveBaseClassDef = effectiveBaseClass.GetDefinition();
					if (effectiveBaseClassDef != null) {
						switch (effectiveBaseClassDef.KnownTypeCode) {
							case KnownTypeCode.Object:
							case KnownTypeCode.ValueType:
							case KnownTypeCode.Enum:
								return null;
						}
					}
					return true;
				} else if (effectiveBaseClass.Kind == TypeKind.Struct || effectiveBaseClass.Kind == TypeKind.Enum) {
					return false;
				}
				return null;
			}
		}
		
		IType IType.DeclaringType {
			get { return null; }
		}
		
		int ICSharpCode.TypeSystem.IType.TypeParameterCount {
			get { return 0; }
		}

		bool ICSharpCode.TypeSystem.IType.IsParameterized { 
			get { return false; }
		}

		readonly static IList<IType> emptyTypeArguments = new IType[0];
		IList<IType> IType.TypeArguments {
			get { return emptyTypeArguments; }
		}

		public abstract IEnumerable<IType> DirectBaseTypes { get; }
		
		public string Name {
			get { return name; }
		}
		
		string ICSharpCode.TypeSystem.INamedElement.Namespace {
			get { return string.Empty; }
		}
		
		string ICSharpCode.TypeSystem.INamedElement.FullName {
			get { return name; }
		}
		
		public string ReflectionName {
			get {
				return (this.OwnerType == SymbolKind.Method ? "``" : "`") + index.ToString(CultureInfo.InvariantCulture);
			}
		}
		
		ITypeDefinition IType.GetDefinition()
		{
			return null;
		}
		
		public IType AcceptVisitor(TypeVisitor visitor)
		{
			return visitor.VisitTypeParameter(this);
		}
		
		public IType VisitChildren(TypeVisitor visitor)
		{
			return this;
		}
		
		public ITypeReference ToTypeReference()
		{
			return TypeParameterReference.Create(this.OwnerType, this.Index);
		}
		
		IEnumerable<IType> IType.GetNestedTypes(Predicate<ITypeDefinition> filter, GetMemberOptions options)
		{
			return EmptyList<IType>.Instance;
		}
		
		IEnumerable<IType> IType.GetNestedTypes(IList<IType> typeArguments, Predicate<ITypeDefinition> filter, GetMemberOptions options)
		{
			return EmptyList<IType>.Instance;
		}
		
		public IEnumerable<IMethod> GetConstructors(Predicate<IUnresolvedMethod> filter = null, GetMemberOptions options = GetMemberOptions.IgnoreInheritedMembers)
		{
			if ((options & GetMemberOptions.IgnoreInheritedMembers) == GetMemberOptions.IgnoreInheritedMembers) {
				if (this.HasDefaultConstructorConstraint || this.HasValueTypeConstraint) {
					if (filter == null || filter(DefaultUnresolvedMethod.DummyConstructor)) {
						return new [] { DefaultResolvedMethod.GetDummyConstructor(compilation, this) };
					}
				}
				return EmptyList<IMethod>.Instance;
			} else {
				return GetMembersHelper.GetConstructors(this, filter, options);
			}
		}
		
		public IEnumerable<IMethod> GetMethods(Predicate<IUnresolvedMethod> filter = null, GetMemberOptions options = GetMemberOptions.None)
		{
			if ((options & GetMemberOptions.IgnoreInheritedMembers) == GetMemberOptions.IgnoreInheritedMembers)
				return EmptyList<IMethod>.Instance;
			else
				return GetMembersHelper.GetMethods(this, FilterNonStatic(filter), options);
		}
		
		public IEnumerable<IMethod> GetMethods(IList<IType> typeArguments, Predicate<IUnresolvedMethod> filter = null, GetMemberOptions options = GetMemberOptions.None)
		{
			if ((options & GetMemberOptions.IgnoreInheritedMembers) == GetMemberOptions.IgnoreInheritedMembers)
				return EmptyList<IMethod>.Instance;
			else
				return GetMembersHelper.GetMethods(this, typeArguments, FilterNonStatic(filter), options);
		}
		
		public IEnumerable<IProperty> GetProperties(Predicate<IUnresolvedProperty> filter = null, GetMemberOptions options = GetMemberOptions.None)
		{
			if ((options & GetMemberOptions.IgnoreInheritedMembers) == GetMemberOptions.IgnoreInheritedMembers)
				return EmptyList<IProperty>.Instance;
			else
				return GetMembersHelper.GetProperties(this, FilterNonStatic(filter), options);
		}
		
		public IEnumerable<IField> GetFields(Predicate<IUnresolvedField> filter = null, GetMemberOptions options = GetMemberOptions.None)
		{
			if ((options & GetMemberOptions.IgnoreInheritedMembers) == GetMemberOptions.IgnoreInheritedMembers)
				return EmptyList<IField>.Instance;
			else
				return GetMembersHelper.GetFields(this, FilterNonStatic(filter), options);
		}
		
		public IEnumerable<IEvent> GetEvents(Predicate<IUnresolvedEvent> filter = null, GetMemberOptions options = GetMemberOptions.None)
		{
			if ((options & GetMemberOptions.IgnoreInheritedMembers) == GetMemberOptions.IgnoreInheritedMembers)
				return EmptyList<IEvent>.Instance;
			else
				return GetMembersHelper.GetEvents(this, FilterNonStatic(filter), options);
		}
		
		public IEnumerable<IMember> GetMembers(Predicate<IUnresolvedMember> filter = null, GetMemberOptions options = GetMemberOptions.None)
		{
			if ((options & GetMemberOptions.IgnoreInheritedMembers) == GetMemberOptions.IgnoreInheritedMembers)
				return EmptyList<IMember>.Instance;
			else
				return GetMembersHelper.GetMembers(this, FilterNonStatic(filter), options);
		}
		
		public IEnumerable<IMethod> GetAccessors(Predicate<IUnresolvedMethod> filter = null, GetMemberOptions options = GetMemberOptions.None)
		{
			if ((options & GetMemberOptions.IgnoreInheritedMembers) == GetMemberOptions.IgnoreInheritedMembers)
				return EmptyList<IMethod>.Instance;
			else
				return GetMembersHelper.GetAccessors(this, FilterNonStatic(filter), options);
		}

		public TypeParameterSubstitution GetSubstitution()
		{
			return TypeParameterSubstitution.Identity;
		}
		
		public TypeParameterSubstitution GetSubstitution(IList<IType> methodTypeArguments)
		{
			return TypeParameterSubstitution.Identity;
		}

		static Predicate<T> FilterNonStatic<T>(Predicate<T> filter) where T : class, IUnresolvedMember
		{
			if (filter == null)
				return member => !member.IsStatic;
			else
				return member => !member.IsStatic && filter(member);
		}
		
		public sealed override bool Equals(object obj)
		{
			return Equals(obj as IType);
		}
		
		public override int GetHashCode()
		{
			return base.GetHashCode();
		}
		
		public virtual bool Equals(IType other)
		{
			return this == other; // use reference equality for type parameters
		}

		public virtual ISymbolReference ToReference()
		{
			if (owner == null)
				return TypeParameterReference.Create(ownerType, index);
			return (ISymbolReference)new OwnedTypeParameterReference((ISymbolReference)owner.ToReference(), index);
		}
		
		public override string ToString()
		{
			return this.ReflectionName + " (owner=" + owner + ")";
		}
		
		#region 显式实现 Abstractions 接口成员
		ICSharpCode.TypeSystem.SymbolKind ICSharpCode.TypeSystem.ISymbol.SymbolKind => (ICSharpCode.TypeSystem.SymbolKind)(byte)((ISymbol)this).SymbolKind;
		ICSharpCode.TypeSystem.TypeKind ICSharpCode.TypeSystem.IType.Kind => (ICSharpCode.TypeSystem.TypeKind)(byte)TypeKind.TypeParameter;
		ICSharpCode.TypeSystem.SymbolKind ICSharpCode.TypeSystem.ITypeParameter.OwnerType => (ICSharpCode.TypeSystem.SymbolKind)(byte)OwnerType;
		ICSharpCode.TypeSystem.IEntity ICSharpCode.TypeSystem.ITypeParameter.Owner => Owner;
		ICSharpCode.TypeSystem.IType ICSharpCode.TypeSystem.IType.DeclaringType => null;
		ICSharpCode.TypeSystem.ITypeDefinition ICSharpCode.TypeSystem.IType.GetDefinition() => null;
		ICSharpCode.TypeSystem.IType ICSharpCode.TypeSystem.IType.AcceptVisitor(ICSharpCode.TypeSystem.TypeVisitor visitor) => visitor.VisitTypeParameter(this);
		ICSharpCode.TypeSystem.IType ICSharpCode.TypeSystem.IType.VisitChildren(ICSharpCode.TypeSystem.TypeVisitor visitor) => this;
		System.Collections.Generic.IEnumerable<ICSharpCode.TypeSystem.IType> ICSharpCode.TypeSystem.IType.DirectBaseTypes => DirectBaseTypes.Cast<ICSharpCode.TypeSystem.IType>();
		ICSharpCode.TypeSystem.ITypeReference ICSharpCode.TypeSystem.IType.ToTypeReference() => ToTypeReference();
		ICSharpCode.TypeSystem.TypeParameterSubstitution ICSharpCode.TypeSystem.IType.GetSubstitution() => GetSubstitution();
		ICSharpCode.TypeSystem.TypeParameterSubstitution ICSharpCode.TypeSystem.IType.GetSubstitution(System.Collections.Generic.IList<ICSharpCode.TypeSystem.IType> methodTypeArguments) => GetSubstitution();
		System.Collections.Generic.IList<ICSharpCode.TypeSystem.IType> ICSharpCode.TypeSystem.IType.TypeArguments => new CastList<IType, ICSharpCode.TypeSystem.IType>(emptyTypeArguments);
		System.Collections.Generic.IEnumerable<ICSharpCode.TypeSystem.IType> ICSharpCode.TypeSystem.IType.GetNestedTypes(System.Predicate<ICSharpCode.TypeSystem.ITypeDefinition> filter, ICSharpCode.TypeSystem.GetMemberOptions options) => EmptyList<ICSharpCode.TypeSystem.IType>.Instance;
		System.Collections.Generic.IEnumerable<ICSharpCode.TypeSystem.IType> ICSharpCode.TypeSystem.IType.GetNestedTypes(System.Collections.Generic.IList<ICSharpCode.TypeSystem.IType> typeArguments, System.Predicate<ICSharpCode.TypeSystem.ITypeDefinition> filter, ICSharpCode.TypeSystem.GetMemberOptions options) => EmptyList<ICSharpCode.TypeSystem.IType>.Instance;
		System.Collections.Generic.IEnumerable<ICSharpCode.TypeSystem.IMethod> ICSharpCode.TypeSystem.IType.GetConstructors(System.Predicate<ICSharpCode.TypeSystem.IUnresolvedMethod> filter, ICSharpCode.TypeSystem.GetMemberOptions options) => GetConstructors().Cast<ICSharpCode.TypeSystem.IMethod>();
		System.Collections.Generic.IEnumerable<ICSharpCode.TypeSystem.IMethod> ICSharpCode.TypeSystem.IType.GetMethods(System.Predicate<ICSharpCode.TypeSystem.IUnresolvedMethod> filter, ICSharpCode.TypeSystem.GetMemberOptions options) => GetMethods().Cast<ICSharpCode.TypeSystem.IMethod>();
		System.Collections.Generic.IEnumerable<ICSharpCode.TypeSystem.IMethod> ICSharpCode.TypeSystem.IType.GetMethods(System.Collections.Generic.IList<ICSharpCode.TypeSystem.IType> typeArguments, System.Predicate<ICSharpCode.TypeSystem.IUnresolvedMethod> filter, ICSharpCode.TypeSystem.GetMemberOptions options) => GetMethods().Cast<ICSharpCode.TypeSystem.IMethod>();
		System.Collections.Generic.IEnumerable<ICSharpCode.TypeSystem.IProperty> ICSharpCode.TypeSystem.IType.GetProperties(System.Predicate<ICSharpCode.TypeSystem.IUnresolvedProperty> filter, ICSharpCode.TypeSystem.GetMemberOptions options) => GetProperties().Cast<ICSharpCode.TypeSystem.IProperty>();
		System.Collections.Generic.IEnumerable<ICSharpCode.TypeSystem.IField> ICSharpCode.TypeSystem.IType.GetFields(System.Predicate<ICSharpCode.TypeSystem.IUnresolvedField> filter, ICSharpCode.TypeSystem.GetMemberOptions options) => GetFields().Cast<ICSharpCode.TypeSystem.IField>();
		System.Collections.Generic.IEnumerable<ICSharpCode.TypeSystem.IEvent> ICSharpCode.TypeSystem.IType.GetEvents(System.Predicate<ICSharpCode.TypeSystem.IUnresolvedEvent> filter, ICSharpCode.TypeSystem.GetMemberOptions options) => GetEvents().Cast<ICSharpCode.TypeSystem.IEvent>();
		System.Collections.Generic.IEnumerable<ICSharpCode.TypeSystem.IMember> ICSharpCode.TypeSystem.IType.GetMembers(System.Predicate<ICSharpCode.TypeSystem.IUnresolvedMember> filter, ICSharpCode.TypeSystem.GetMemberOptions options) => GetMembers().Cast<ICSharpCode.TypeSystem.IMember>();
		System.Collections.Generic.IEnumerable<ICSharpCode.TypeSystem.IMethod> ICSharpCode.TypeSystem.IType.GetAccessors(System.Predicate<ICSharpCode.TypeSystem.IUnresolvedMethod> filter, ICSharpCode.TypeSystem.GetMemberOptions options) => GetAccessors().Cast<ICSharpCode.TypeSystem.IMethod>();
		System.Collections.Generic.IList<ICSharpCode.TypeSystem.IAttribute> ICSharpCode.TypeSystem.ITypeParameter.Attributes => new CastList<IAttribute, ICSharpCode.TypeSystem.IAttribute>(Attributes);
		ICSharpCode.TypeSystem.VarianceModifier ICSharpCode.TypeSystem.ITypeParameter.Variance => (ICSharpCode.TypeSystem.VarianceModifier)(byte)Variance;
		ICSharpCode.TypeSystem.DomRegion ICSharpCode.TypeSystem.ITypeParameter.Region => new ICSharpCode.TypeSystem.DomRegion(Region.BeginLine, Region.BeginColumn, Region.EndLine, Region.EndColumn);
		ICSharpCode.TypeSystem.IType ICSharpCode.TypeSystem.ITypeParameter.EffectiveBaseClass => EffectiveBaseClass;
		System.Collections.Generic.ICollection<ICSharpCode.TypeSystem.IType> ICSharpCode.TypeSystem.ITypeParameter.EffectiveInterfaceSet => new CastList<IType, ICSharpCode.TypeSystem.IType>(new List<IType>(EffectiveInterfaceSet));
		ICSharpCode.TypeSystem.ICompilation ICSharpCode.TypeSystem.ICompilationProvider.Compilation => Compilation;
		ICSharpCode.TypeSystem.ISymbolReference ICSharpCode.TypeSystem.ISymbol.ToReference() => ToReference();
		bool System.IEquatable<ICSharpCode.TypeSystem.IType>.Equals(ICSharpCode.TypeSystem.IType other) => Equals(other as IType);
		#endregion
	}
	
	public sealed class OwnedTypeParameterReference : ISymbolReference
	{
		ISymbolReference owner;
		int index;
		
		public OwnedTypeParameterReference(ISymbolReference owner, int index)
		{
			if (owner == null)
				throw new ArgumentNullException("owner");
			this.owner = owner;
			this.index = index;
		}
		
		public ISymbol Resolve(ITypeResolveContext context)
		{
			var entity = owner.Resolve(context) as IEntity;
			if (entity is ITypeDefinition)
				return ((ITypeDefinition)entity).TypeParameters[index];
			if (entity is IMethod)
				return ((IMethod)entity).TypeParameters[index];
			return null;
		}
		
		ICSharpCode.TypeSystem.ISymbol ICSharpCode.TypeSystem.ISymbolReference.Resolve(ICSharpCode.TypeSystem.ITypeResolveContext context) => Resolve((ITypeResolveContext)context) as ICSharpCode.TypeSystem.ISymbol;
	}
}
