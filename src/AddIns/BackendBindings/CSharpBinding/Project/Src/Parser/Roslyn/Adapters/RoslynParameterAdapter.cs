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

using ICSharpCode.NRefactory;
using ICSharpCode.NRefactory.TypeSystem;
using ICSharpCode.NRefactory.TypeSystem.Implementation;
using ICSharpCode.NRefactory.Utils;

using Microsoft.CodeAnalysis;

using NRSymbolKind = ICSharpCode.NRefactory.TypeSystem.SymbolKind;
using NRSymbolReference = ICSharpCode.NRefactory.TypeSystem.ISymbolReference;

namespace CSharpBinding.Parser.Roslyn.Adapters
{
	/// <summary>
	/// 将 Roslyn IParameterSymbol 适配为 NRefactory IParameter 接口。
	/// </summary>
	public class RoslynParameterAdapter : IParameter
	{
		readonly IParameterSymbol roslynParameter;
		readonly RoslynCompilationAdapter compilation;

		public RoslynParameterAdapter(IParameterSymbol roslynParameter, RoslynCompilationAdapter compilation)
		{
			this.roslynParameter = roslynParameter ?? throw new ArgumentNullException(nameof(roslynParameter));
			this.compilation = compilation ?? throw new ArgumentNullException(nameof(compilation));
		}

		public string Name => roslynParameter.Name;

		public DomRegion Region {
			get {
				var loc = roslynParameter.Locations.Length > 0 ? roslynParameter.Locations[0] : null;
				if (loc == null)
					return DomRegion.Empty;
				var lineSpan = loc.GetLineSpan();
				return new DomRegion(lineSpan.Path, lineSpan.StartLinePosition.Line + 1, lineSpan.StartLinePosition.Character + 1);
			}
		}

		public IType Type => AdapterCache.GetOrCreateTypeAdapter(roslynParameter.Type, compilation);

		public bool IsConst => roslynParameter.HasExplicitDefaultValue;

		public object ConstantValue {
			get { return roslynParameter.HasExplicitDefaultValue ? roslynParameter.ExplicitDefaultValue : null; }
		}

		public NRSymbolKind SymbolKind => NRSymbolKind.Parameter;

		public NRSymbolReference ToReference()
		{
			return new RoslynParameterReference(roslynParameter);
		}

		public IList<IAttribute> Attributes => EmptyList<IAttribute>.Instance;

		public bool IsRef => roslynParameter.RefKind == RefKind.Ref;

		public bool IsOut => roslynParameter.RefKind == RefKind.Out;

		public bool IsParams => roslynParameter.IsParams;

		public bool IsOptional => roslynParameter.IsOptional;

		public IParameterizedMember Owner {
			get {
				// 返回 null，因为从 Roslyn 参数获取所有者比较复杂
				return null;
			}
		}
	}

	/// <summary>
	/// 参数引用的简单实现。
	/// </summary>
	class RoslynParameterReference : NRSymbolReference
	{
		readonly string name;
		readonly int ordinal;

		public RoslynParameterReference(IParameterSymbol parameter)
		{
			this.name = parameter?.Name ?? string.Empty;
			this.ordinal = parameter?.Ordinal ?? 0;
		}

		public ICSharpCode.NRefactory.TypeSystem.ISymbol Resolve(ITypeResolveContext context)
		{
			// 参数引用的解析比较复杂，当前返回 null
			return null;
		}
	}
}
