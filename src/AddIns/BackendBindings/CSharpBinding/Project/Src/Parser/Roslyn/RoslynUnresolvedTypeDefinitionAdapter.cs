// Copyright (c) 2014 AlphaSierraPapa for the SharpDevelop Team
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

using ICSharpCode.NRefactory.TypeSystem;
using ICSharpCode.NRefactory.TypeSystem.Implementation;

namespace CSharpBinding.Parser.Roslyn
{
	/// <summary>
	/// 从 Roslyn 类型声明提取的 IUnresolvedTypeDefinition 适配器。
	/// 提供基本的类型信息（名称、命名空间、种类、区域、成员、嵌套类型）。
	/// </summary>
	public class RoslynUnresolvedTypeDefinitionAdapter : AbstractUnresolvedEntity, IUnresolvedTypeDefinition
	{
		TypeKind kind;
		string namespaceName;
		IList<ITypeReference> baseTypes;
		IList<IUnresolvedTypeParameter> typeParameters;
		IList<IUnresolvedTypeDefinition> nestedTypes;
		IList<IUnresolvedMember> members;

		public RoslynUnresolvedTypeDefinitionAdapter(string name, string namespaceName, TypeKind kind, IUnresolvedFile file, IUnresolvedTypeDefinition declaringType)
		{
			this.SymbolKind = SymbolKind.TypeDefinition;
			this.Name = name ?? throw new ArgumentNullException(nameof(name));
			this.namespaceName = namespaceName ?? string.Empty;
			this.kind = kind;
			this.UnresolvedFile = file;
			this.DeclaringTypeDefinition = declaringType;
		}

		protected override void FreezeInternal()
		{
			base.FreezeInternal();
			baseTypes = FreezableHelper.FreezeList(baseTypes);
			typeParameters = FreezableHelper.FreezeListAndElements(typeParameters);
			nestedTypes = FreezableHelper.FreezeListAndElements(nestedTypes);
			members = FreezableHelper.FreezeListAndElements(members);
		}

		public override object Clone()
		{
			var copy = (RoslynUnresolvedTypeDefinitionAdapter)base.Clone();
			if (baseTypes != null)
				copy.baseTypes = new List<ITypeReference>(baseTypes);
			if (typeParameters != null)
				copy.typeParameters = new List<IUnresolvedTypeParameter>(typeParameters);
			if (nestedTypes != null)
				copy.nestedTypes = new List<IUnresolvedTypeDefinition>(nestedTypes);
			if (members != null)
				copy.members = new List<IUnresolvedMember>(members);
			return copy;
		}

		public TypeKind Kind {
			get { return kind; }
			set {
				ThrowIfFrozen();
				kind = value;
			}
		}

		public bool AddDefaultConstructorIfRequired {
			get { return kind == TypeKind.Class || kind == TypeKind.Struct; }
			set { }
		}

		public bool? HasExtensionMethods {
			get { return null; }
			set { }
		}

		public bool IsPartial { get; set; }

		public override string Namespace {
			get { return namespaceName; }
			set {
				if (value == null)
					throw new ArgumentNullException(nameof(value));
				ThrowIfFrozen();
				namespaceName = value;
			}
		}

		public override string ReflectionName {
			get { return FullTypeName.ReflectionName; }
		}

		public FullTypeName FullTypeName {
			get {
				IUnresolvedTypeDefinition declaringTypeDef = this.DeclaringTypeDefinition;
				if (declaringTypeDef != null) {
					return declaringTypeDef.FullTypeName.NestedType(this.Name, this.TypeParameters.Count - declaringTypeDef.TypeParameters.Count);
				} else {
					return new TopLevelTypeName(namespaceName, this.Name, this.TypeParameters.Count);
				}
			}
		}

		public IList<ITypeReference> BaseTypes {
			get {
				if (baseTypes == null)
					baseTypes = new List<ITypeReference>();
				return baseTypes;
			}
		}

		public IList<IUnresolvedTypeParameter> TypeParameters {
			get {
				if (typeParameters == null)
					typeParameters = new List<IUnresolvedTypeParameter>();
				return typeParameters;
			}
		}

		public IList<IUnresolvedTypeDefinition> NestedTypes {
			get {
				if (nestedTypes == null)
					nestedTypes = new List<IUnresolvedTypeDefinition>();
				return nestedTypes;
			}
		}

		public IList<IUnresolvedMember> Members {
			get {
				if (members == null)
					members = new List<IUnresolvedMember>();
				return members;
			}
		}

		public IEnumerable<IUnresolvedMethod> Methods {
			get { return Members.OfType<IUnresolvedMethod>(); }
		}

		public IEnumerable<IUnresolvedProperty> Properties {
			get { return Members.OfType<IUnresolvedProperty>(); }
		}

		public IEnumerable<IUnresolvedField> Fields {
			get { return Members.OfType<IUnresolvedField>(); }
		}

		public IEnumerable<IUnresolvedEvent> Events {
			get { return Members.OfType<IUnresolvedEvent>(); }
		}

		public IType Resolve(ITypeResolveContext context)
		{
			return new DefaultResolvedTypeDefinition(context, this);
		}

		public virtual ITypeResolveContext CreateResolveContext(ITypeResolveContext parentContext)
		{
			return parentContext;
		}
	}
}
