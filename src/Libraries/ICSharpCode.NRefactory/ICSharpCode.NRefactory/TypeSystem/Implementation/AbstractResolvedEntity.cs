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
using ICSharpCode.NRefactory.Documentation;

namespace ICSharpCode.NRefactory.TypeSystem.Implementation
{
	/// <summary>
	/// Implementation of <see cref="IEntity"/> that resolves an unresolved entity.
	/// </summary>
	public abstract class AbstractResolvedEntity : IEntity
	{
		protected readonly IUnresolvedEntity unresolved;
		protected readonly ITypeResolveContext parentContext;
		
		protected AbstractResolvedEntity(IUnresolvedEntity unresolved, ITypeResolveContext parentContext)
		{
			if (unresolved == null)
				throw new ArgumentNullException("unresolved");
			if (parentContext == null)
				throw new ArgumentNullException("parentContext");
			this.unresolved = unresolved;
			this.parentContext = parentContext;
			this.Attributes = unresolved.Attributes.CreateResolvedAttributes(parentContext);
		}
		
		public SymbolKind SymbolKind {
			get { return unresolved.SymbolKind; }
		}
		
		[Obsolete("Use the SymbolKind property instead.")]
		public EntityType EntityType {
			get { return (EntityType)unresolved.SymbolKind; }
		}
		
		public DomRegion Region {
			get { return unresolved.Region; }
		}
		
		public DomRegion BodyRegion {
			get { return unresolved.BodyRegion; }
		}
		
		public ITypeDefinition DeclaringTypeDefinition {
			get { return parentContext.CurrentTypeDefinition; }
		}
		
		public virtual IType DeclaringType {
			get { return parentContext.CurrentTypeDefinition; }
		}
		
		public IAssembly ParentAssembly {
			get { return parentContext.CurrentAssembly; }
		}
		
		public IList<IAttribute> Attributes { get; protected set; }
		
		public virtual DocumentationComment Documentation {
			get {
				IDocumentationProvider provider = FindDocumentation(parentContext);
				if (provider != null)
					return provider.GetDocumentation(this);
				else
					return null;
			}
		}
		
		internal static IDocumentationProvider FindDocumentation(ITypeResolveContext context)
		{
			IAssembly asm = context.CurrentAssembly;
			if (asm != null)
				return asm.UnresolvedAssembly as IDocumentationProvider;
			else
				return null;
		}

		public abstract ISymbolReference ToReference();
		
		public bool IsStatic { get { return unresolved.IsStatic; } }
		public bool IsAbstract { get { return unresolved.IsAbstract; } }
		public bool IsSealed { get { return unresolved.IsSealed; } }
		public bool IsShadowing { get { return unresolved.IsShadowing; } }
		public bool IsSynthetic { get { return unresolved.IsSynthetic; } }
		
		public ICompilation Compilation {
			get { return parentContext.Compilation; }
		}
		
		public string FullName { get { return unresolved.FullName; } }
		public string Name { get { return unresolved.Name; } }
		public string ReflectionName { get { return unresolved.ReflectionName; } }
		public string Namespace { get { return unresolved.Namespace; } }
		
		public virtual Accessibility Accessibility { get { return unresolved.Accessibility; } }
		public bool IsPrivate { get { return Accessibility == Accessibility.Private; } }
		public bool IsPublic { get { return Accessibility == Accessibility.Public; } }
		public bool IsProtected { get { return Accessibility == Accessibility.Protected; } }
		public bool IsInternal { get { return Accessibility == Accessibility.Internal; } }
		public bool IsProtectedOrInternal { get { return Accessibility == Accessibility.ProtectedOrInternal; } }
		public bool IsProtectedAndInternal { get { return Accessibility == Accessibility.ProtectedAndInternal; } }
		
		public override string ToString()
		{
			return "[" + this.SymbolKind.ToString() + " " + this.ReflectionName + "]";
		}
		
		#region 显式实现 Abstractions 接口成员
		// 这些显式接口实现使 NRefactory 类型同时满足 Abstractions 接口的要求
		// 当 NRefactory 返回类型继承自 Abstractions 返回类型时，直接委托给现有实现
		// 当类型不兼容（如枚举、值类型）时，进行适当的转换
		
		ICSharpCode.TypeSystem.SymbolKind ICSharpCode.TypeSystem.ISymbol.SymbolKind => (ICSharpCode.TypeSystem.SymbolKind)(byte)SymbolKind;
		ICSharpCode.TypeSystem.Accessibility ICSharpCode.TypeSystem.IHasAccessibility.Accessibility => (ICSharpCode.TypeSystem.Accessibility)(byte)Accessibility;
		bool ICSharpCode.TypeSystem.IHasAccessibility.IsPrivate => Accessibility == Accessibility.Private;
		bool ICSharpCode.TypeSystem.IHasAccessibility.IsPublic => Accessibility == Accessibility.Public;
		bool ICSharpCode.TypeSystem.IHasAccessibility.IsProtected => Accessibility == Accessibility.Protected;
		bool ICSharpCode.TypeSystem.IHasAccessibility.IsInternal => Accessibility == Accessibility.Internal;
		bool ICSharpCode.TypeSystem.IHasAccessibility.IsProtectedOrInternal => Accessibility == Accessibility.ProtectedOrInternal;
		bool ICSharpCode.TypeSystem.IHasAccessibility.IsProtectedAndInternal => Accessibility == Accessibility.ProtectedAndInternal;
		ICSharpCode.TypeSystem.ICompilation ICSharpCode.TypeSystem.ICompilationProvider.Compilation => Compilation;
		ICSharpCode.TypeSystem.ITypeDefinition ICSharpCode.TypeSystem.IEntity.DeclaringTypeDefinition => DeclaringTypeDefinition;
		ICSharpCode.TypeSystem.IType ICSharpCode.TypeSystem.IEntity.DeclaringType => DeclaringType;
		ICSharpCode.TypeSystem.IAssembly ICSharpCode.TypeSystem.IEntity.ParentAssembly => ParentAssembly;
		ICSharpCode.TypeSystem.DomRegion ICSharpCode.TypeSystem.IEntity.Region => new ICSharpCode.TypeSystem.DomRegion(Region.BeginLine, Region.BeginColumn, Region.EndLine, Region.EndColumn);
		ICSharpCode.TypeSystem.DomRegion ICSharpCode.TypeSystem.IEntity.BodyRegion => new ICSharpCode.TypeSystem.DomRegion(BodyRegion.BeginLine, BodyRegion.BeginColumn, BodyRegion.EndLine, BodyRegion.EndColumn);
		System.Collections.Generic.IList<ICSharpCode.TypeSystem.IAttribute> ICSharpCode.TypeSystem.IEntity.Attributes => new CastList<IAttribute, ICSharpCode.TypeSystem.IAttribute>(Attributes);
		ICSharpCode.TypeSystem.ISymbolReference ICSharpCode.TypeSystem.ISymbol.ToReference() => ToReference();
		[Obsolete] ICSharpCode.TypeSystem.EntityType ICSharpCode.TypeSystem.IEntity.EntityType => (ICSharpCode.TypeSystem.EntityType)(byte)EntityType;
		#endregion
	}
}
