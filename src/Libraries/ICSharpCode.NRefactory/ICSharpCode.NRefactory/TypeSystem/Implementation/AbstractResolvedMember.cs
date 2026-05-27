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
using ICSharpCode.NRefactory.Documentation;
using ICSharpCode.NRefactory.Utils;

namespace ICSharpCode.NRefactory.TypeSystem.Implementation
{
	/// <summary>
	/// Implementation of <see cref="IMember"/> that resolves an unresolved member.
	/// </summary>
	public abstract class AbstractResolvedMember : AbstractResolvedEntity, IMember
	{
		protected new readonly IUnresolvedMember unresolved;
		protected readonly ITypeResolveContext context;
		volatile IType returnType;
		IList<IMember> implementedInterfaceMembers;
		
		protected AbstractResolvedMember(IUnresolvedMember unresolved, ITypeResolveContext parentContext)
			: base(unresolved, parentContext)
		{
			this.unresolved = unresolved;
			this.context = parentContext.WithCurrentMember(this);
		}
		
		IMember IMember.MemberDefinition {
			get { return this; }
		}
		
		public IType ReturnType {
			get {
				return this.returnType ?? (this.returnType = unresolved.ReturnType.Resolve(context));
			}
		}
		
		public IUnresolvedMember UnresolvedMember {
			get { return unresolved; }
		}
		
		public IList<IMember> ImplementedInterfaceMembers {
			get {
				IList<IMember> result = LazyInit.VolatileRead(ref this.implementedInterfaceMembers);
				if (result != null) {
					return result;
				} else {
					return LazyInit.GetOrSet(ref implementedInterfaceMembers, FindImplementedInterfaceMembers());
				}
			}
		}
		
		IList<IMember> FindImplementedInterfaceMembers()
		{
			if (unresolved.IsExplicitInterfaceImplementation) {
				List<IMember> result = new List<IMember>();
				foreach (var memberReference in unresolved.ExplicitInterfaceImplementations) {
					IMember member = (IMember)memberReference.Resolve(context);
					if (member != null)
						result.Add(member);
				}
				return result.ToArray();
			} else if (unresolved.IsStatic || !unresolved.IsPublic || DeclaringTypeDefinition == null || DeclaringTypeDefinition.Kind == TypeKind.Interface) {
				return EmptyList<IMember>.Instance;
			} else {
				// TODO: implement interface member mappings correctly
				var result = InheritanceHelper.GetBaseMembers(this, true)
					.Where(m => m.DeclaringTypeDefinition != null && m.DeclaringTypeDefinition.Kind == TypeKind.Interface)
					.ToArray();

				IEnumerable<IMember> otherMembers = DeclaringTypeDefinition.Members;
				if (SymbolKind == SymbolKind.Accessor)
					otherMembers = DeclaringTypeDefinition.GetAccessors(options: GetMemberOptions.IgnoreInheritedMembers);
				result = result.Where(item => !otherMembers.Any(m => m.IsExplicitInterfaceImplementation && m.ImplementedInterfaceMembers.Contains(item))).ToArray();

				return result;
			}
		}
		
		public override DocumentationComment Documentation {
			get {
				IUnresolvedDocumentationProvider docProvider = unresolved.UnresolvedFile as IUnresolvedDocumentationProvider;
				if (docProvider != null) {
					var doc = docProvider.GetDocumentation(unresolved, this);
					if (doc != null)
						return doc;
				}
				return base.Documentation;
			}
		}
		
		public bool IsExplicitInterfaceImplementation {
			get { return unresolved.IsExplicitInterfaceImplementation; }
		}
		
		public bool IsVirtual {
			get { return unresolved.IsVirtual; }
		}
		
		public bool IsOverride {
			get { return unresolved.IsOverride; }
		}
		
		public bool IsOverridable {
			get { return unresolved.IsOverridable; }
		}

		public TypeParameterSubstitution Substitution {
			get { return TypeParameterSubstitution.Identity; }
		}

		public abstract IMember Specialize(TypeParameterSubstitution substitution);
		
		IMemberReference IMember.ToReference()
		{
			return (IMemberReference)ToReference();
		}
		
		public override ISymbolReference ToReference()
		{
			var declType = this.DeclaringType;
			var declTypeRef = declType != null ? declType.ToTypeReference() : SpecialType.UnknownType;
			if (IsExplicitInterfaceImplementation && ImplementedInterfaceMembers.Count == 1) {
				return new ExplicitInterfaceImplementationMemberReference(declTypeRef, ImplementedInterfaceMembers[0].ToReference());
			} else {
				return new DefaultMemberReference(this.SymbolKind, declTypeRef, this.Name);
			}
		}
		
		public virtual IMemberReference ToMemberReference()
		{
			return (IMemberReference)ToReference();
		}
		
		internal IMethod GetAccessor(ref IMethod accessorField, IUnresolvedMethod unresolvedAccessor)
		{
			if (unresolvedAccessor == null)
				return null;
			IMethod result = LazyInit.VolatileRead(ref accessorField);
			if (result != null) {
				return result;
			} else {
				return LazyInit.GetOrSet(ref accessorField, CreateResolvedAccessor(unresolvedAccessor));
			}
		}
		
		protected virtual IMethod CreateResolvedAccessor(IUnresolvedMethod unresolvedAccessor)
		{
			return (IMethod)unresolvedAccessor.CreateResolved(context);
		}
		
		public Accessibility AccessibilityDomain {
			get {
				var declType = DeclaringTypeDefinition;
				if (declType != null) {
					// The accessibility domain of a member is the more restrictive of
					// the member's own accessibility and its declaring type's accessibility domain
					Accessibility declAccessibility = declType.Accessibility;
					if (declAccessibility < Accessibility)
						return declAccessibility;
				}
				return Accessibility;
			}
		}
		
		public bool IsAccessibleFrom(IAssembly assembly)
		{
			if (assembly == null)
				throw new ArgumentNullException("assembly");
			var domain = AccessibilityDomain;
			if (domain == Accessibility.Public)
				return true;
			if (domain == Accessibility.None)
				return false;
			if (domain == Accessibility.Private)
				return false;
			var thisAssembly = ParentAssembly;
			if (thisAssembly == null)
				return false;
			bool sameAssembly = thisAssembly.Equals(assembly);
			if (domain == Accessibility.Internal || domain == Accessibility.ProtectedAndInternal)
				return sameAssembly;
			if (domain == Accessibility.ProtectedOrInternal)
				return sameAssembly;
			return false;
		}
		
		IMember IMemberReference.Resolve(ITypeResolveContext context)
		{
			return this;
		}
		
		IType ITypeReference.Resolve(ITypeResolveContext context)
		{
			return ReturnType;
		}
		
		#region 显式实现 Abstractions IMember 接口成员
		ICSharpCode.TypeSystem.IUnresolvedMember ICSharpCode.TypeSystem.IMember.UnresolvedMember => UnresolvedMember;
		ICSharpCode.TypeSystem.IType ICSharpCode.TypeSystem.IMember.ReturnType => ReturnType;
		ICSharpCode.TypeSystem.IMember ICSharpCode.TypeSystem.IMember.MemberDefinition => this;
		System.Collections.Generic.IList<ICSharpCode.TypeSystem.IMember> ICSharpCode.TypeSystem.IMember.ImplementedInterfaceMembers => new CastList<IMember, ICSharpCode.TypeSystem.IMember>(ImplementedInterfaceMembers);
		ICSharpCode.TypeSystem.IMemberReference ICSharpCode.TypeSystem.IMember.ToReference() => ToReference() as ICSharpCode.TypeSystem.IMemberReference ?? ToMemberReference();
		ICSharpCode.TypeSystem.IMemberReference ICSharpCode.TypeSystem.IMember.ToMemberReference() => ToMemberReference();
		ICSharpCode.TypeSystem.TypeParameterSubstitution ICSharpCode.TypeSystem.IMember.Substitution => Substitution;
		ICSharpCode.TypeSystem.IMember ICSharpCode.TypeSystem.IMember.Specialize(ICSharpCode.TypeSystem.TypeParameterSubstitution substitution) => Specialize(substitution);
		// IMemberReference 显式实现
		ICSharpCode.TypeSystem.ITypeReference ICSharpCode.TypeSystem.IMemberReference.DeclaringTypeReference => DeclaringType?.ToTypeReference();
		ICSharpCode.TypeSystem.IMember ICSharpCode.TypeSystem.IMemberReference.Resolve(ICSharpCode.TypeSystem.ITypeResolveContext context) => this;
		// ISymbolReference 显式实现
		ICSharpCode.TypeSystem.ISymbol ICSharpCode.TypeSystem.ISymbolReference.Resolve(ICSharpCode.TypeSystem.ITypeResolveContext context) => this as ICSharpCode.TypeSystem.ISymbol;
		// ITypeReference 显式实现
		ICSharpCode.TypeSystem.IType ICSharpCode.TypeSystem.ITypeReference.Resolve(ICSharpCode.TypeSystem.ITypeResolveContext context) => ReturnType as ICSharpCode.TypeSystem.IType ?? ReturnType;
		#endregion
	}
}
