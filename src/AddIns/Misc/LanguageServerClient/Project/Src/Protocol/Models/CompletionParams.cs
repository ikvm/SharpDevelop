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
	/// 代码补全请求参数
	/// </summary>
	public class CompletionParams
	{
		/// <summary>
		/// 文本文档标识符
		/// </summary>
		[JsonProperty("textDocument")]
		public TextDocumentIdentifier TextDocument { get; set; }

		/// <summary>
		/// 请求补全的位置
		/// </summary>
		[JsonProperty("position")]
		public Position Position { get; set; }

		/// <summary>
		/// 补全上下文（可选）
		/// </summary>
		[JsonProperty("context", NullValueHandling = NullValueHandling.Ignore)]
		public CompletionContext Context { get; set; }
	}

	/// <summary>
	/// 代码补全上下文
	/// </summary>
	public class CompletionContext
	{
		/// <summary>
		/// 触发补全的方式
		/// </summary>
		[JsonProperty("triggerKind")]
		public int TriggerKind { get; set; }

		/// <summary>
		/// 触发补全的字符
		/// </summary>
		[JsonProperty("triggerCharacter", NullValueHandling = NullValueHandling.Ignore)]
		public string TriggerCharacter { get; set; }
	}

	/// <summary>
	/// 补全触发方式
	/// </summary>
	public static class CompletionTriggerKind
	{
		/// <summary>由用户交互触发（如按 Ctrl+Space）</summary>
		public const int Invoked = 1;
		/// <summary>由触发字符触发（如输入 '.' 或 '('）</summary>
		public const int TriggerCharacter = 2;
		/// <summary>由触发字符触发，但本次补全请求后不应再触发</summary>
		public const int TriggerForIncompleteCompletions = 3;
	}
}
