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
using System.Diagnostics;
using System.Linq;
using ICSharpCode.NRefactory.Semantics;

namespace ICSharpCode.NRefactory.TypeSystem.Implementation
{
	[Serializable]
	public sealed class UnresolvedSecurityDeclarationBlob
	{
		static readonly ITypeReference securityActionTypeReference = typeof(System.Security.Permissions.SecurityAction).ToTypeReference();
		static readonly ITypeReference permissionSetAttributeTypeReference = typeof(System.Security.Permissions.PermissionSetAttribute).ToTypeReference();
		
		readonly IConstantValue securityAction;
		readonly byte[] blob;
		readonly IList<IUnresolvedAttribute> unresolvedAttributes = new List<IUnresolvedAttribute>();
		
		public UnresolvedSecurityDeclarationBlob(int securityAction, byte[] blob)
		{
			BlobReader reader = new BlobReader(blob, null);
			this.securityAction = new SimpleConstantValue(securityActionTypeReference, securityAction);
			this.blob = blob;
			if (reader.ReadByte() == '.') {
				// binary attribute
				uint attributeCount = reader.ReadCompressedUInt32();
				for (uint i = 0; i < attributeCount; i++) {
					unresolvedAttributes.Add(new UnresolvedSecurityAttribute(this, (int)i));
				}
			} else {
				// for backward compatibility with .NET 1.0: XML-encoded attribute
				var attr = new DefaultUnresolvedAttribute(permissionSetAttributeTypeReference);
				attr.ConstructorParameterTypes.Add(securityActionTypeReference);
				attr.PositionalArguments.Add(this.securityAction);
				string xml = System.Text.Encoding.Unicode.GetString(blob);
				attr.AddNamedPropertyArgument("XML", new SimpleConstantValue(KnownTypeReference.String, xml));
				unresolvedAttributes.Add(attr);
			}
		}
		
		public IList<IUnresolvedAttribute> UnresolvedAttributes {
			get { return unresolvedAttributes; }
		}
		
		public IList<IAttribute> Resolve(IAssembly currentAssembly)
		{
			// TODO: make this a per-assembly cache
//				CacheManager cache = currentAssembly.Compilation.CacheManager;
//				IList<IAttribute> result = (IList<IAttribute>)cache.GetShared(this);
//				if (result != null)
//					return result;
			
			ITypeResolveContext context = new SimpleTypeResolveContext(currentAssembly);
			BlobReader reader = new BlobReader(blob, currentAssembly);
			if (reader.ReadByte() != '.') {
				// should not use UnresolvedSecurityDeclaration for XML secdecls
				throw new InvalidOperationException();
			}
			ResolveResult securityActionRR = securityAction.Resolve(context);
			uint attributeCount = reader.ReadCompressedUInt32();
			IAttribute[] attributes = new IAttribute[attributeCount];
			try {
				ReadSecurityBlob(reader, attributes, context, securityActionRR);
			} catch (NotSupportedException ex) {
				// ignore invalid blobs
				Debug.WriteLine(ex.ToString());
			}
			for (int i = 0; i < attributes.Length; i++) {
				if (attributes[i] == null)
					attributes[i] = new CecilResolvedAttribute(context, SpecialType.UnknownType);
			}
			return attributes;
//				return (IList<IAttribute>)cache.GetOrAddShared(this, attributes);
		}
		
		void ReadSecurityBlob(BlobReader reader, IAttribute[] attributes, ITypeResolveContext context, ResolveResult securityActionRR)
		{
			for (int i = 0; i < attributes.Length; i++) {
				string attributeTypeName = reader.ReadSerString();
				ITypeReference attributeTypeRef = ReflectionHelper.ParseReflectionName(attributeTypeName);
				IType attributeType = attributeTypeRef.Resolve(context);
				
				reader.ReadCompressedUInt32(); // ??
				// The specification seems to be incorrect here, so I'm using the logic from Cecil instead.
				uint numNamed = reader.ReadCompressedUInt32();
				
				var namedArgs = new List<KeyValuePair<IMember, ResolveResult>>((int)numNamed);
				for (uint j = 0; j < numNamed; j++) {
					var namedArg = reader.ReadNamedArg(attributeType);
					if (namedArg.Key != null)
						namedArgs.Add(namedArg);
					
				}
				attributes[i] = new DefaultAttribute(
					attributeType,
					positionalArguments: new ResolveResult[] { securityActionRR },
					namedArguments: namedArgs.Select(p => new KeyValuePair<string, ResolveResult>(p.Key.FullName, p.Value)).ToList());
			}
		}
	}
	
	[Serializable]
	sealed class UnresolvedSecurityAttribute : IUnresolvedAttribute, ISupportsInterning
	{
		readonly UnresolvedSecurityDeclarationBlob secDecl;
		readonly int index;
		
		public UnresolvedSecurityAttribute(UnresolvedSecurityDeclarationBlob secDecl, int index)
		{
			Debug.Assert(secDecl != null);
			this.secDecl = secDecl;
			this.index = index;
		}
		
		ICSharpCode.TypeSystem.DomRegion ICSharpCode.TypeSystem.IUnresolvedAttribute.Region => ICSharpCode.TypeSystem.DomRegion.Empty;

		IAttribute IUnresolvedAttribute.CreateResolvedAttribute(ITypeResolveContext context)
		{
			return secDecl.Resolve(context.CurrentAssembly)[index];
		}

		int ISupportsInterning.GetHashCodeForInterning()
		{
			return index ^ secDecl.GetHashCode();
		}

		bool ISupportsInterning.EqualsForInterning(ISupportsInterning other)
		{
			UnresolvedSecurityAttribute attr = other as UnresolvedSecurityAttribute;
			return attr != null && index == attr.index && secDecl == attr.secDecl;
		}

		#region 显式实现 Abstractions 接口成员
		ITypeReference IUnresolvedAttribute.AttributeType => null;
		ICSharpCode.TypeSystem.IAttribute ICSharpCode.TypeSystem.IUnresolvedAttribute.CreateResolvedAttribute(ICSharpCode.TypeSystem.ITypeResolveContext context) => ((IUnresolvedAttribute)this).CreateResolvedAttribute((ITypeResolveContext)context);
		#endregion
	}
}
