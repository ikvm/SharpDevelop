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

using Newtonsoft.Json;

namespace ICSharpCode.LanguageServerClient.Protocol.Models
{
	/// <summary>
	/// 查找引用请求参数
	/// </summary>
	public class ReferenceParams
	{
		/// <summary>
		/// 文本文档标识符
		/// </summary>
		[JsonProperty("textDocument")]
		public TextDocumentIdentifier TextDocument { get; set; }

		/// <summary>
		/// 请求引用的位置
		/// </summary>
		[JsonProperty("position")]
		public Position Position { get; set; }

		/// <summary>
		/// 引用上下文
		/// </summary>
		[JsonProperty("context")]
		public ReferenceContext Context { get; set; }
	}

	/// <summary>
	/// 引用查找上下文
	/// </summary>
	public class ReferenceContext
	{
		/// <summary>
		/// 是否包含声明位置的引用
		/// </summary>
		[JsonProperty("includeDeclaration")]
		public bool IncludeDeclaration { get; set; } = true;
	}
}
