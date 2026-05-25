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

using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Folding;
using ICSharpCode.NRefactory.TypeSystem;
using ICSharpCode.SharpDevelop.Parser;
using Microsoft.CodeAnalysis;

namespace CSharpBinding.Parser.Roslyn
{
	/// <summary>
	/// 基于 Roslyn SyntaxTree 的完整解析信息。
	/// 类似于 CSharpFullParseInformation，但包装 Roslyn 语法树。
	/// </summary>
	public class RoslynFullParseInformation : ParseInformation
	{
		readonly SyntaxTree syntaxTree;
		internal List<NewFolding> newFoldings;

		public RoslynFullParseInformation(IUnresolvedFile unresolvedFile, ITextSourceVersion parsedVersion, SyntaxTree syntaxTree)
			: base(unresolvedFile, parsedVersion, isFullParseInformation: true)
		{
			this.syntaxTree = syntaxTree ?? throw new ArgumentNullException(nameof(syntaxTree));
		}

		/// <summary>
		/// 获取 Roslyn 语法树。
		/// </summary>
		public SyntaxTree SyntaxTree {
			get { return syntaxTree; }
		}

		public override IEnumerable<NewFolding> GetFoldings(IDocument document, out int firstErrorOffset)
		{
			firstErrorOffset = -1;
			return newFoldings;
		}
	}
}
