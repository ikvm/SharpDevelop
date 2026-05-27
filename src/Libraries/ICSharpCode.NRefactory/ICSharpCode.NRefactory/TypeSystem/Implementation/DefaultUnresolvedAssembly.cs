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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.Serialization;
using System.Threading;

using ICSharpCode.NRefactory.Semantics;
using ICSharpCode.NRefactory.Utils;

namespace ICSharpCode.NRefactory.TypeSystem.Implementation
{
	/// <summary>
	/// Default implementation for <see cref="IUnresolvedAssembly"/>.
	/// </summary>
	[Serializable]
	public class DefaultUnresolvedAssembly : AbstractFreezable, IUnresolvedAssembly
	{
		string assemblyName;
		string fullAssemblyName;
		IList<IUnresolvedAttribute> assemblyAttributes;
		IList<IUnresolvedAttribute> moduleAttributes;
		Dictionary<TopLevelTypeName, IUnresolvedTypeDefinition> typeDefinitions = new Dictionary<TopLevelTypeName, IUnresolvedTypeDefinition>(TopLevelTypeNameComparer.Ordinal);
		Dictionary<TopLevelTypeName, ITypeReference> typeForwarders = new Dictionary<TopLevelTypeName, ITypeReference>(TopLevelTypeNameComparer.Ordinal);
		
		protected override void FreezeInternal()
		{
			base.FreezeInternal();
			assemblyAttributes = FreezableHelper.FreezeListAndElements(assemblyAttributes);
			moduleAttributes = FreezableHelper.FreezeListAndElements(moduleAttributes);
			foreach (var type in typeDefinitions.Values) {
				FreezableHelper.Freeze(type);
			}
		}
		
		/// <summary>
		/// Creates a new unresolved assembly.
		/// </summary>
		/// <param name="assemblyName">Full assembly name</param>
		public DefaultUnresolvedAssembly(string assemblyName)
		{
			if (assemblyName == null)
				throw new ArgumentNullException("assemblyName");
			this.fullAssemblyName = assemblyName;
			int pos = assemblyName != null ? assemblyName.IndexOf(',') : -1;
			this.assemblyName = pos < 0 ? assemblyName : assemblyName.Substring(0, pos);
			this.assemblyAttributes = new List<IUnresolvedAttribute>();
			this.moduleAttributes = new List<IUnresolvedAttribute>();
		}
		
		/// <summary>
		/// Gets/Sets the short assembly name.
		/// </summary>
		/// <remarks>
		/// This class handles the short and the full name independently;
		/// if you change the short name, you should also change the full name.
		/// </remarks>
		public string AssemblyName {
			get { return assemblyName; }
			set {
				if (value == null)
					throw new ArgumentNullException("value");
				FreezableHelper.ThrowIfFrozen(this);
				assemblyName = value;
			}
		}
		
		/// <summary>
		/// Gets/Sets the full assembly name.
		/// </summary>
		/// <remarks>
		/// This class handles the short and the full name independently;
		/// if you change the full name, you should also change the short name.
		/// </remarks>
		public string FullAssemblyName {
			get { return fullAssemblyName; }
			set {
				if (value == null)
					throw new ArgumentNullException("value");
				FreezableHelper.ThrowIfFrozen(this);
				fullAssemblyName = value;
			}
		}
		
		string location;
		public string Location {
			get {
				return location;
			}
			set {
				FreezableHelper.ThrowIfFrozen(this);
				location = value;
			}
		}

		public IList<IUnresolvedAttribute> AssemblyAttributes {
			get { return assemblyAttributes; }
		}
		
		public IList<IUnresolvedAttribute> ModuleAttributes {
			get { return moduleAttributes; }
		}
		
		public IList<IUnresolvedTypeDefinition> TopLevelTypeDefinitions {
			get { return new List<IUnresolvedTypeDefinition>(typeDefinitions.Values); }
		}
		
		/// <summary>
		/// Adds a new top-level type definition to this assembly.
		/// </summary>
		/// <remarks>DefaultUnresolvedAssembly does not support partial classes.
		/// Adding more than one part of a type will cause an ArgumentException.</remarks>
		public void AddTypeDefinition(IUnresolvedTypeDefinition typeDefinition)
		{
			if (typeDefinition == null)
				throw new ArgumentNullException("typeDefinition");
			if (typeDefinition.DeclaringTypeDefinition != null)
				throw new ArgumentException("Cannot add nested types.");
			FreezableHelper.ThrowIfFrozen(this);
			var key = new TopLevelTypeName(typeDefinition.Namespace, typeDefinition.Name, typeDefinition.TypeParameters.Count);
			typeDefinitions.Add(key, typeDefinition);
		}
		
		static readonly ITypeReference typeForwardedToAttributeTypeRef = typeof(System.Runtime.CompilerServices.TypeForwardedToAttribute).ToTypeReference();
		
		/// <summary>
		/// Adds a type forwarder.
		/// This adds both an assembly attribute and an internal forwarder entry, which will be used
		/// by the resolved assembly to provide the forwarded types.
		/// </summary>
		/// <param name="typeName">The name of the type.</param>
		/// <param name="referencedType">The reference used to look up the type in the target assembly.</param>
		public void AddTypeForwarder(TopLevelTypeName typeName, ITypeReference referencedType)
		{
			if (referencedType == null)
				throw new ArgumentNullException("referencedType");
			FreezableHelper.ThrowIfFrozen(this);
			var attribute = new DefaultUnresolvedAttribute(typeForwardedToAttributeTypeRef, new[] { KnownTypeReference.Type });
			attribute.PositionalArguments.Add(new TypeOfConstantValue(referencedType));
			assemblyAttributes.Add(attribute);
			
			typeForwarders[typeName] = referencedType;
		}
		
		[Serializable]
		sealed class TypeOfConstantValue : IConstantValue
		{
			readonly ITypeReference typeRef;
			
			public TypeOfConstantValue(ITypeReference typeRef)
			{
				this.typeRef = typeRef;
			}
			
			public ResolveResult Resolve(ITypeResolveContext context)
			{
				return new TypeOfResolveResult(context.Compilation.FindType(KnownTypeCode.Type), typeRef.Resolve(context));
			}
			
			ICSharpCode.TypeSystem.ResolveResult ICSharpCode.TypeSystem.IConstantValue.Resolve(ICSharpCode.TypeSystem.ITypeResolveContext context) => Resolve((ITypeResolveContext)context);
		}
		
		public IUnresolvedTypeDefinition GetTypeDefinition(string ns, string name, int typeParameterCount)
		{
			var key = new TopLevelTypeName(ns ?? string.Empty, name, typeParameterCount);
			IUnresolvedTypeDefinition td;
			if (typeDefinitions.TryGetValue(key, out td))
				return td;
			else
				return null;
		}
		
		public IAssembly Resolve(ITypeResolveContext context)
		{
			if (context == null)
				throw new ArgumentNullException("context");
			Freeze();
			var cache = context.Compilation.CacheManager;
			IAssembly asm = (IAssembly)cache.GetShared(this);
			if (asm != null) {
				return asm;
			} else {
				asm = new DefaultResolvedAssembly(context.Compilation, this);
				return (IAssembly)cache.GetOrAddShared(this, asm);
			}
		}
		
		ICSharpCode.TypeSystem.IAssembly ICSharpCode.TypeSystem.IAssemblyReference.Resolve(ICSharpCode.TypeSystem.ITypeResolveContext context) => Resolve((ITypeResolveContext)context);
		
		public override string ToString()
		{
			return "[" + GetType().Name + " " + assemblyName + "]";
		}
		
		#region 显式实现 Abstractions IUnresolvedAssembly 接口成员
		System.Collections.Generic.IEnumerable<ICSharpCode.TypeSystem.IUnresolvedAttribute> ICSharpCode.TypeSystem.IUnresolvedAssembly.AssemblyAttributes => AssemblyAttributes.Cast<ICSharpCode.TypeSystem.IUnresolvedAttribute>();
		System.Collections.Generic.IEnumerable<ICSharpCode.TypeSystem.IUnresolvedAttribute> ICSharpCode.TypeSystem.IUnresolvedAssembly.ModuleAttributes => ModuleAttributes.Cast<ICSharpCode.TypeSystem.IUnresolvedAttribute>();
		System.Collections.Generic.IEnumerable<ICSharpCode.TypeSystem.IUnresolvedTypeDefinition> ICSharpCode.TypeSystem.IUnresolvedAssembly.TopLevelTypeDefinitions => TopLevelTypeDefinitions.Cast<ICSharpCode.TypeSystem.IUnresolvedTypeDefinition>();
		#endregion
		
		//[NonSerialized]
		//List<Dictionary<TopLevelTypeName, IUnresolvedTypeDefinition>> cachedTypeDictionariesPerNameComparer;
		
		Dictionary<TopLevelTypeName, IUnresolvedTypeDefinition> GetTypeDictionary(StringComparer nameComparer)
		{
			Debug.Assert(IsFrozen);
			if (nameComparer == StringComparer.Ordinal)
				return typeDefinitions;
			else
				throw new NotImplementedException();
		}
		
		#region UnresolvedNamespace
		sealed class UnresolvedNamespace
		{
			internal readonly string FullName;
			internal readonly string Name;
			internal readonly List<UnresolvedNamespace> Children = new List<UnresolvedNamespace>();
			
			public UnresolvedNamespace(string fullName, string name)
			{
				this.FullName = fullName;
				this.Name = name;
			}
		}
		
		[NonSerialized]
		List<KeyValuePair<StringComparer, UnresolvedNamespace>> unresolvedNamespacesPerNameComparer;
		
		UnresolvedNamespace GetUnresolvedRootNamespace(StringComparer nameComparer)
		{
			Debug.Assert(IsFrozen);
			LazyInitializer.EnsureInitialized(ref unresolvedNamespacesPerNameComparer);
			lock (unresolvedNamespacesPerNameComparer) {
				foreach (var pair in unresolvedNamespacesPerNameComparer) {
					if (pair.Key == nameComparer)
						return pair.Value;
				}
				var root = new UnresolvedNamespace(string.Empty, string.Empty);
				var dict = new Dictionary<string, UnresolvedNamespace>(nameComparer);
				dict.Add(root.FullName, root);
				foreach (var typeName in typeDefinitions.Keys) {
					GetOrAddNamespace(dict, typeName.Namespace);
				}
				unresolvedNamespacesPerNameComparer.Add(new KeyValuePair<StringComparer, UnresolvedNamespace>(nameComparer, root));
				return root;
			}
		}
		
		static UnresolvedNamespace GetOrAddNamespace(Dictionary<string, UnresolvedNamespace> dict, string fullName)
		{
			UnresolvedNamespace ns;
			if (dict.TryGetValue(fullName, out ns))
				return ns;
			int pos = fullName.LastIndexOf('.');
			UnresolvedNamespace parent;
			string name;
			if (pos < 0) {
				parent = dict[string.Empty]; // root
				name = fullName;
			} else {
				parent = GetOrAddNamespace(dict, fullName.Substring(0, pos));
				name = fullName.Substring(pos + 1);
			}
			ns = new UnresolvedNamespace(fullName, name);
			parent.Children.Add(ns);
			dict.Add(fullName, ns);
			return ns;
		}
		#endregion
		
		sealed class DefaultResolvedAssembly : IAssembly
		{
			readonly DefaultUnresolvedAssembly unresolvedAssembly;
			readonly ICompilation compilation;
			readonly ITypeResolveContext context;
			readonly Dictionary<TopLevelTypeName, IUnresolvedTypeDefinition> unresolvedTypeDict;
			readonly ConcurrentDictionary<IUnresolvedTypeDefinition, ITypeDefinition> typeDict = new ConcurrentDictionary<IUnresolvedTypeDefinition, ITypeDefinition>();
			readonly INamespace rootNamespace;
			
			public DefaultResolvedAssembly(ICompilation compilation, DefaultUnresolvedAssembly unresolved)
			{
				this.compilation = compilation;
				this.unresolvedAssembly = unresolved;
				this.unresolvedTypeDict = unresolved.GetTypeDictionary(compilation.NameComparer);
				this.rootNamespace = new NS(this, unresolved.GetUnresolvedRootNamespace(compilation.NameComparer), null);
				this.context = new SimpleTypeResolveContext(this);
				this.AssemblyAttributes = unresolved.AssemblyAttributes.CreateResolvedAttributes(context);
				this.ModuleAttributes = unresolved.ModuleAttributes.CreateResolvedAttributes(context);
			}
			
			public IUnresolvedAssembly UnresolvedAssembly {
				get { return unresolvedAssembly; }
			}
			
			public bool IsMainAssembly {
				get { return this.Compilation.MainAssembly == this; }
			}
			
			public string AssemblyName {
				get { return unresolvedAssembly.AssemblyName; }
			}
			
			public string FullAssemblyName {
				get { return unresolvedAssembly.FullAssemblyName; }
			}
			
			public IList<IAttribute> AssemblyAttributes { get; private set; }
			public IList<IAttribute> ModuleAttributes { get; private set; }
			
			public INamespace RootNamespace {
				get { return rootNamespace; }
			}
			
			public ICompilation Compilation {
				get { return compilation; }
			}
	
			public ITypeResolveContext TypeResolveContext {
				get { return context; }
			}
	
			public bool InternalsVisibleTo(IAssembly assembly)
			{
				if (this == assembly)
					return true;
				foreach (string shortName in GetInternalsVisibleTo()) {
					if (assembly.AssemblyName == shortName)
						return true;
				}
				return false;
			}
			
			public ITypeDefinition GetTypeDefinition(string ns, string name, int typeParameterCount = 0)
			{
				return GetTypeDefinition(new TopLevelTypeName(ns ?? string.Empty, name, typeParameterCount));
			}

			volatile string[] internalsVisibleTo;

			string[] GetInternalsVisibleTo()
			{
				var result = this.internalsVisibleTo;
				if (result != null) {
					return result;
				} else {
					using (var busyLock = BusyManager.Enter(this)) {
						Debug.Assert(busyLock.Success);
						if (!busyLock.Success) {
							return new string[0];
						}
						internalsVisibleTo = (
							from attr in this.AssemblyAttributes
							where attr.AttributeType.Name == "InternalsVisibleToAttribute"
							&& attr.AttributeType.Namespace == "System.Runtime.CompilerServices"
							&& attr.PositionalArguments.Count == 1
							select GetShortName(attr.PositionalArguments.Single().ConstantValue as string)
						).ToArray();
					}
					return internalsVisibleTo;
				}
			}

			static string GetShortName(string fullAssemblyName)
			{
				if (fullAssemblyName == null)
					return null;
				int pos = fullAssemblyName.IndexOf(',');
				if (pos < 0)
					return fullAssemblyName;
				else
					return fullAssemblyName.Substring(0, pos);
			}

			public ITypeDefinition GetTypeDefinition(TopLevelTypeName topLevelTypeName)
			{
				IUnresolvedTypeDefinition td;
				ITypeReference typeRef;
				if (unresolvedAssembly.typeDefinitions.TryGetValue(topLevelTypeName, out td))
					return GetTypeDefinition(td);
				if (unresolvedAssembly.typeForwarders.TryGetValue(topLevelTypeName, out typeRef)) {
					// Protect against cyclic type forwarders:
					using (var busyLock = BusyManager.Enter(typeRef)) {
						if (busyLock.Success)
							return typeRef.Resolve(compilation.TypeResolveContext).GetDefinition();
					}
				}
				return null;
			}
			
			ITypeDefinition GetTypeDefinition(IUnresolvedTypeDefinition unresolved)
			{
				return typeDict.GetOrAdd(unresolved, t => CreateTypeDefinition(t));
			}
			
			ITypeDefinition CreateTypeDefinition(IUnresolvedTypeDefinition unresolved)
			{
				if (unresolved.DeclaringTypeDefinition != null) {
					ITypeDefinition declaringType = GetTypeDefinition(unresolved.DeclaringTypeDefinition);
					return new DefaultResolvedTypeDefinition(context.WithCurrentTypeDefinition(declaringType), unresolved);
				} else if (unresolved.Name == "Void" && unresolved.Namespace == "System" && unresolved.TypeParameters.Count == 0) {
					return new VoidTypeDefinition(context, unresolved);
				} else {
					return new DefaultResolvedTypeDefinition(context, unresolved);
				}
			}
			
			public IEnumerable<ITypeDefinition> TopLevelTypeDefinitions {
				get {
					return unresolvedAssembly.TopLevelTypeDefinitions.Select(t => GetTypeDefinition(t));
				}
			}
			
			public override string ToString()
			{
				return "[DefaultResolvedAssembly " + AssemblyName + "]";
			}
			
			#region 显式实现 Abstractions 接口成员
			ICSharpCode.TypeSystem.IUnresolvedAssembly ICSharpCode.TypeSystem.IAssembly.UnresolvedAssembly => UnresolvedAssembly;
			System.Collections.Generic.IList<ICSharpCode.TypeSystem.IAttribute> ICSharpCode.TypeSystem.IAssembly.AssemblyAttributes => new CastList<IAttribute, ICSharpCode.TypeSystem.IAttribute>(AssemblyAttributes);
			System.Collections.Generic.IList<ICSharpCode.TypeSystem.IAttribute> ICSharpCode.TypeSystem.IAssembly.ModuleAttributes => new CastList<IAttribute, ICSharpCode.TypeSystem.IAttribute>(ModuleAttributes);
			ICSharpCode.TypeSystem.INamespace ICSharpCode.TypeSystem.IAssembly.RootNamespace => RootNamespace;
			ICSharpCode.TypeSystem.ICompilation ICSharpCode.TypeSystem.ICompilationProvider.Compilation => Compilation;
			ICSharpCode.TypeSystem.ITypeDefinition ICSharpCode.TypeSystem.IAssembly.GetTypeDefinition(ICSharpCode.TypeSystem.TopLevelTypeName topLevelTypeName) => GetTypeDefinition(new TopLevelTypeName(topLevelTypeName.Namespace, topLevelTypeName.Name, topLevelTypeName.TypeParameterCount));
			System.Collections.Generic.IEnumerable<ICSharpCode.TypeSystem.ITypeDefinition> ICSharpCode.TypeSystem.IAssembly.TopLevelTypeDefinitions => TopLevelTypeDefinitions.Cast<ICSharpCode.TypeSystem.ITypeDefinition>();
			bool ICSharpCode.TypeSystem.IAssembly.InternalsVisibleTo(ICSharpCode.TypeSystem.IAssembly assembly) => InternalsVisibleTo(assembly as IAssembly);
			#endregion
			
			sealed class NS : INamespace
			{
				readonly DefaultResolvedAssembly assembly;
				readonly UnresolvedNamespace ns;
				readonly INamespace parentNamespace;
				readonly IList<NS> childNamespaces;
				IEnumerable<ITypeDefinition> types;
				
				public NS(DefaultResolvedAssembly assembly, UnresolvedNamespace ns, INamespace parentNamespace)
				{
					this.assembly = assembly;
					this.ns = ns;
					this.parentNamespace = parentNamespace;
					this.childNamespaces = new ProjectedList<NS, UnresolvedNamespace, NS>(
						this, ns.Children, (self, c) => new NS(self.assembly, c, self));
				}
				
				string ICSharpCode.TypeSystem.INamespace.ExternAlias {
					get { return null; }
				}
				
				string ICSharpCode.TypeSystem.INamespace.FullName {
					get { return ns.FullName; }
				}
				
				SymbolKind ISymbol.SymbolKind {
					get { return SymbolKind.Namespace; }
				}
				
				public string Name {
					get { return ns.Name; }
				}
				
				INamespace INamespace.ParentNamespace {
					get { return parentNamespace; }
				}
				
				IEnumerable<IAssembly> INamespace.ContributingAssemblies {
					get { return new [] { assembly }; }
				}
				
				IEnumerable<INamespace> INamespace.ChildNamespaces {
					get { return childNamespaces; }
				}
				
				ICSharpCode.TypeSystem.INamespace ICSharpCode.TypeSystem.INamespace.GetChildNamespace(string name)
				{
					var nameComparer = assembly.compilation.NameComparer;
					for (int i = 0; i < childNamespaces.Count; i++) {
						if (nameComparer.Equals(name, ns.Children[i].Name))
							return childNamespaces[i];
					}
					return null;
				}
				
				ICompilation ICompilationProvider.Compilation {
					get { return assembly.compilation; }
				}
				
				IEnumerable<ITypeDefinition> INamespace.Types {
					get {
						var result = LazyInit.VolatileRead(ref this.types);
						if (result != null) {
							return result;
						} else {
							var hashSet = new HashSet<ITypeDefinition>();
							foreach (IUnresolvedTypeDefinition typeDef in assembly.UnresolvedAssembly.TopLevelTypeDefinitions) {
								if (typeDef.Namespace == ns.FullName)
									hashSet.Add(assembly.GetTypeDefinition(typeDef));
							}
							return LazyInit.GetOrSet(ref this.types, hashSet.ToArray());
						}
					}
				}
				
				ITypeDefinition INamespace.GetTypeDefinition(string name, int typeParameterCount)
				{
					var key = new TopLevelTypeName(ns.FullName, name, typeParameterCount);
					IUnresolvedTypeDefinition unresolvedTypeDef;
					if (assembly.unresolvedTypeDict.TryGetValue(key, out unresolvedTypeDef))
						return assembly.GetTypeDefinition(unresolvedTypeDef);
					else
						return null;
				}
				
				public ISymbolReference ToReference()
				{
					return new NamespaceReference(new DefaultAssemblyReference(assembly.AssemblyName), ns.FullName);
				}
				
				#region 显式实现 Abstractions 接口成员
				ICSharpCode.TypeSystem.SymbolKind ICSharpCode.TypeSystem.ISymbol.SymbolKind => (ICSharpCode.TypeSystem.SymbolKind)(byte)SymbolKind.Namespace;
				ICSharpCode.TypeSystem.INamespace ICSharpCode.TypeSystem.INamespace.ParentNamespace => parentNamespace;
				System.Collections.Generic.IEnumerable<ICSharpCode.TypeSystem.INamespace> ICSharpCode.TypeSystem.INamespace.ChildNamespaces => childNamespaces.Cast<ICSharpCode.TypeSystem.INamespace>();
				System.Collections.Generic.IEnumerable<ICSharpCode.TypeSystem.ITypeDefinition> ICSharpCode.TypeSystem.INamespace.Types => ((INamespace)this).Types.Cast<ICSharpCode.TypeSystem.ITypeDefinition>();
				ICSharpCode.TypeSystem.ITypeDefinition ICSharpCode.TypeSystem.INamespace.GetTypeDefinition(string name, int typeParameterCount) => ((INamespace)this).GetTypeDefinition(name, typeParameterCount);
				ICSharpCode.TypeSystem.ISymbolReference ICSharpCode.TypeSystem.ISymbol.ToReference() => ToReference();
				System.Collections.Generic.IEnumerable<ICSharpCode.TypeSystem.IAssembly> ICSharpCode.TypeSystem.INamespace.ContributingAssemblies => ((INamespace)this).ContributingAssemblies.Cast<ICSharpCode.TypeSystem.IAssembly>();
				ICSharpCode.TypeSystem.ICompilation ICSharpCode.TypeSystem.ICompilationProvider.Compilation => assembly.compilation;
				#endregion
			}
		}
	}
	
	public sealed class NamespaceReference : ISymbolReference
	{
		IAssemblyReference assemblyReference;
		string fullName;
		
		public NamespaceReference(IAssemblyReference assemblyReference, string fullName)
		{
			if (assemblyReference == null)
				throw new ArgumentNullException("assemblyReference");
			this.assemblyReference = assemblyReference;
			this.fullName = fullName;
		}
		
		public ISymbol Resolve(ITypeResolveContext context)
		{
			IAssembly assembly = assemblyReference.Resolve(context);
			INamespace parent = assembly.RootNamespace;
			
			string[] parts = fullName.Split('.');
			
			int i = 0;
			while (i < parts.Length && parent != null) {
				parent = (INamespace)parent.GetChildNamespace(parts[i]);
				i++;
			}
			
			return parent;
		}
		
		// 显式实现 Abstractions 接口
		ICSharpCode.TypeSystem.ISymbol ICSharpCode.TypeSystem.ISymbolReference.Resolve(ICSharpCode.TypeSystem.ITypeResolveContext context) => Resolve((ITypeResolveContext)context) as ICSharpCode.TypeSystem.ISymbol;
	}
	
	public sealed class MergedNamespaceReference : ISymbolReference
	{
		string externAlias;
		string fullName;
		
		public MergedNamespaceReference(string externAlias, string fullName)
		{
			this.externAlias = externAlias;
			this.fullName = fullName;
		}
		
		public ISymbol Resolve(ITypeResolveContext context)
		{
			string[] parts = fullName.Split('.');
			INamespace parent = context.Compilation.GetNamespaceForExternAlias(externAlias);
			
			int i = 0;
			while (i < parts.Length && parent != null) {
				parent = (INamespace)parent.GetChildNamespace(parts[i]);
				i++;
			}
			
			return parent;
		}
		
		// 显式实现 Abstractions 接口
		ICSharpCode.TypeSystem.ISymbol ICSharpCode.TypeSystem.ISymbolReference.Resolve(ICSharpCode.TypeSystem.ITypeResolveContext context) => Resolve((ITypeResolveContext)context) as ICSharpCode.TypeSystem.ISymbol;
	}
}
