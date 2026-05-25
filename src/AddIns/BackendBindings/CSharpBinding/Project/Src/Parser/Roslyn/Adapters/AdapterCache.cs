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
using System.Runtime.CompilerServices;

using ICSharpCode.NRefactory.TypeSystem;
using ICSharpCode.NRefactory.Utils;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

using NRTypeKind = ICSharpCode.NRefactory.TypeSystem.TypeKind;
using NRAccessibility = ICSharpCode.NRefactory.TypeSystem.Accessibility;
using NRSpecialType = ICSharpCode.NRefactory.TypeSystem.SpecialType;
using NRSymbolKind = ICSharpCode.NRefactory.TypeSystem.SymbolKind;
using RoslynTypeKind = Microsoft.CodeAnalysis.TypeKind;
using RoslynAccessibility = Microsoft.CodeAnalysis.Accessibility;
using RoslynSpecialType = Microsoft.CodeAnalysis.SpecialType;

namespace CSharpBinding.Parser.Roslyn.Adapters
{
	/// <summary>
	/// 适配器缓存，用于避免为同一个 Roslyn 符号创建重复的适配器对象。
	/// 使用 ConditionalWeakTable 确保不会阻止垃圾回收。
	/// </summary>
	internal static class AdapterCache
	{
		// 缓存 ITypeSymbol -> IType 适配器
		static readonly ConditionalWeakTable<ITypeSymbol, RoslynTypeAdapter> typeCache =
			new ConditionalWeakTable<ITypeSymbol, RoslynTypeAdapter>();

		// 缓存 INamedTypeSymbol -> ITypeDefinition 适配器
		static readonly ConditionalWeakTable<INamedTypeSymbol, RoslynTypeDefinitionAdapter> typeDefCache =
			new ConditionalWeakTable<INamedTypeSymbol, RoslynTypeDefinitionAdapter>();

		// 缓存 INamespaceSymbol -> INamespace 适配器
		static readonly ConditionalWeakTable<INamespaceSymbol, RoslynNamespaceAdapter> namespaceCache =
			new ConditionalWeakTable<INamespaceSymbol, RoslynNamespaceAdapter>();

		// 缓存 IAssemblySymbol -> IAssembly 适配器
		static readonly ConditionalWeakTable<IAssemblySymbol, RoslynAssemblyAdapter> assemblyCache =
			new ConditionalWeakTable<IAssemblySymbol, RoslynAssemblyAdapter>();

		/// <summary>
		/// 获取或创建 ITypeSymbol 的适配器。
		/// </summary>
		public static RoslynTypeAdapter GetOrCreateTypeAdapter(ITypeSymbol roslynType, RoslynCompilationAdapter compilation)
		{
			if (roslynType == null)
				return null;
			return typeCache.GetValue(roslynType, _ => new RoslynTypeAdapter(roslynType, compilation));
		}

		/// <summary>
		/// 获取或创建 INamedTypeSymbol 的 ITypeDefinition 适配器。
		/// </summary>
		public static RoslynTypeDefinitionAdapter GetOrCreateTypeDefinitionAdapter(INamedTypeSymbol roslynType, RoslynCompilationAdapter compilation)
		{
			if (roslynType == null)
				return null;
			return typeDefCache.GetValue(roslynType, _ => new RoslynTypeDefinitionAdapter(roslynType, compilation));
		}

		/// <summary>
		/// 获取或创建 INamespaceSymbol 的适配器。
		/// </summary>
		public static RoslynNamespaceAdapter GetOrCreateNamespaceAdapter(INamespaceSymbol roslynNamespace, RoslynCompilationAdapter compilation)
		{
			if (roslynNamespace == null)
				return null;
			return namespaceCache.GetValue(roslynNamespace, _ => new RoslynNamespaceAdapter(roslynNamespace, compilation));
		}

		/// <summary>
		/// 获取或创建 IAssemblySymbol 的适配器。
		/// </summary>
		public static RoslynAssemblyAdapter GetOrCreateAssemblyAdapter(IAssemblySymbol roslynAssembly, RoslynCompilationAdapter compilation)
		{
			if (roslynAssembly == null)
				return null;
			return assemblyCache.GetValue(roslynAssembly, _ => new RoslynAssemblyAdapter(roslynAssembly, compilation));
		}
	}

	/// <summary>
	/// Roslyn 类型系统到 NRefactory 类型系统的映射工具类。
	/// 提供 TypeKind、Accessibility、SymbolKind 等枚举值的转换。
	/// </summary>
	internal static class RoslynTypeSystemMapper
	{
		/// <summary>
		/// 将 Roslyn TypeKind 映射为 NRefactory TypeKind。
		/// </summary>
		public static NRTypeKind MapTypeKind(RoslynTypeKind roslynKind)
		{
			switch (roslynKind) {
				case RoslynTypeKind.Class:
					return NRTypeKind.Class;
				case RoslynTypeKind.Struct:
					return NRTypeKind.Struct;
				case RoslynTypeKind.Interface:
					return NRTypeKind.Interface;
				case RoslynTypeKind.Enum:
					return NRTypeKind.Enum;
				case RoslynTypeKind.Delegate:
					return NRTypeKind.Delegate;
				case RoslynTypeKind.Module:
					return NRTypeKind.Module;
				case RoslynTypeKind.TypeParameter:
					return NRTypeKind.TypeParameter;
				case RoslynTypeKind.Array:
					return NRTypeKind.Array;
				case RoslynTypeKind.Pointer:
					return NRTypeKind.Pointer;
				case RoslynTypeKind.Dynamic:
					return NRTypeKind.Dynamic;
				case RoslynTypeKind.Submission:
					return NRTypeKind.Other;
				default:
					return NRTypeKind.Unknown;
			}
		}

		/// <summary>
		/// 将 Roslyn Accessibility 映射为 NRefactory Accessibility。
		/// </summary>
		public static NRAccessibility MapAccessibility(RoslynAccessibility roslynAccessibility)
		{
			switch (roslynAccessibility) {
				case RoslynAccessibility.NotApplicable:
					return NRAccessibility.None;
				case RoslynAccessibility.Private:
					return NRAccessibility.Private;
				case RoslynAccessibility.Public:
					return NRAccessibility.Public;
				case RoslynAccessibility.Protected:
					return NRAccessibility.Protected;
				case RoslynAccessibility.Internal:
					return NRAccessibility.Internal;
				case RoslynAccessibility.ProtectedAndInternal:
					return NRAccessibility.ProtectedAndInternal;
				case RoslynAccessibility.ProtectedOrInternal:
					return NRAccessibility.ProtectedOrInternal;
				default:
					return NRAccessibility.None;
			}
		}

		/// <summary>
		/// 将 NRefactory KnownTypeCode 映射为 Roslyn SpecialType。
		/// </summary>
		public static RoslynSpecialType MapKnownTypeCodeToSpecialType(KnownTypeCode typeCode)
		{
			switch (typeCode) {
				case KnownTypeCode.Object:
					return RoslynSpecialType.System_Object;
				case KnownTypeCode.Boolean:
					return RoslynSpecialType.System_Boolean;
				case KnownTypeCode.Char:
					return RoslynSpecialType.System_Char;
				case KnownTypeCode.SByte:
					return RoslynSpecialType.System_SByte;
				case KnownTypeCode.Byte:
					return RoslynSpecialType.System_Byte;
				case KnownTypeCode.Int16:
					return RoslynSpecialType.System_Int16;
				case KnownTypeCode.UInt16:
					return RoslynSpecialType.System_UInt16;
				case KnownTypeCode.Int32:
					return RoslynSpecialType.System_Int32;
				case KnownTypeCode.UInt32:
					return RoslynSpecialType.System_UInt32;
				case KnownTypeCode.Int64:
					return RoslynSpecialType.System_Int64;
				case KnownTypeCode.UInt64:
					return RoslynSpecialType.System_UInt64;
				case KnownTypeCode.Single:
					return RoslynSpecialType.System_Single;
				case KnownTypeCode.Double:
					return RoslynSpecialType.System_Double;
				case KnownTypeCode.Decimal:
					return RoslynSpecialType.System_Decimal;
				case KnownTypeCode.DateTime:
					return RoslynSpecialType.System_DateTime;
				case KnownTypeCode.String:
					return RoslynSpecialType.System_String;
				case KnownTypeCode.Void:
					return RoslynSpecialType.System_Void;
				case KnownTypeCode.Array:
					return RoslynSpecialType.System_Array;
				case KnownTypeCode.ValueType:
					return RoslynSpecialType.System_ValueType;
				case KnownTypeCode.Enum:
					return RoslynSpecialType.System_Enum;
				case KnownTypeCode.Delegate:
					return RoslynSpecialType.System_Delegate;
				case KnownTypeCode.MulticastDelegate:
					return RoslynSpecialType.System_MulticastDelegate;
				case KnownTypeCode.IntPtr:
					return RoslynSpecialType.System_IntPtr;
				case KnownTypeCode.UIntPtr:
					return RoslynSpecialType.System_UIntPtr;
				case KnownTypeCode.IEnumerable:
					return RoslynSpecialType.System_Collections_IEnumerable;
				case KnownTypeCode.IEnumerator:
					return RoslynSpecialType.System_Collections_IEnumerator;
				case KnownTypeCode.IEnumerableOfT:
					return RoslynSpecialType.System_Collections_Generic_IEnumerable_T;
				case KnownTypeCode.IEnumeratorOfT:
					return RoslynSpecialType.System_Collections_Generic_IEnumerator_T;
				case KnownTypeCode.ICollectionOfT:
					return RoslynSpecialType.System_Collections_Generic_ICollection_T;
				case KnownTypeCode.IListOfT:
					return RoslynSpecialType.System_Collections_Generic_IList_T;
				case KnownTypeCode.IReadOnlyCollectionOfT:
					return RoslynSpecialType.System_Collections_Generic_IReadOnlyCollection_T;
				case KnownTypeCode.IReadOnlyListOfT:
					return RoslynSpecialType.System_Collections_Generic_IReadOnlyList_T;
				case KnownTypeCode.IDisposable:
					return RoslynSpecialType.System_IDisposable;
				case KnownTypeCode.NullableOfT:
					return RoslynSpecialType.System_Nullable_T;
				default:
					// 以下 KnownTypeCode 在当前版本的 Roslyn SpecialType 中没有对应值：
					// Type, Attribute, Exception, ICollection, IList
					return RoslynSpecialType.None;
			}
		}

		/// <summary>
		/// 将 Roslyn SymbolKind 映射为 NRefactory SymbolKind。
		/// </summary>
		public static NRSymbolKind MapSymbolKind(Microsoft.CodeAnalysis.SymbolKind roslynKind, Microsoft.CodeAnalysis.ISymbol symbol)
		{
			switch (roslynKind) {
				case Microsoft.CodeAnalysis.SymbolKind.Field:
					return NRSymbolKind.Field;
				case Microsoft.CodeAnalysis.SymbolKind.Property:
					var prop = symbol as IPropertySymbol;
					return (prop != null && prop.IsIndexer) ? NRSymbolKind.Indexer : NRSymbolKind.Property;
				case Microsoft.CodeAnalysis.SymbolKind.Event:
					return NRSymbolKind.Event;
				case Microsoft.CodeAnalysis.SymbolKind.Method:
					var method = symbol as IMethodSymbol;
					if (method == null)
						return NRSymbolKind.Method;
					switch (method.MethodKind) {
						case MethodKind.Constructor:
						case MethodKind.StaticConstructor:
							return NRSymbolKind.Constructor;
						case MethodKind.Destructor:
							return NRSymbolKind.Destructor;
						case MethodKind.UserDefinedOperator:
						case MethodKind.Conversion:
							return NRSymbolKind.Operator;
						case MethodKind.PropertyGet:
						case MethodKind.PropertySet:
						case MethodKind.EventAdd:
						case MethodKind.EventRemove:
						case MethodKind.EventRaise:
							return NRSymbolKind.Accessor;
						default:
							return NRSymbolKind.Method;
					}
				case Microsoft.CodeAnalysis.SymbolKind.NamedType:
					return NRSymbolKind.TypeDefinition;
				case Microsoft.CodeAnalysis.SymbolKind.Namespace:
					return NRSymbolKind.Namespace;
				case Microsoft.CodeAnalysis.SymbolKind.TypeParameter:
					return NRSymbolKind.TypeParameter;
				case Microsoft.CodeAnalysis.SymbolKind.Parameter:
					return NRSymbolKind.Parameter;
				case Microsoft.CodeAnalysis.SymbolKind.Local:
					return NRSymbolKind.Variable;
				default:
					return NRSymbolKind.None;
			}
		}
	}
}
