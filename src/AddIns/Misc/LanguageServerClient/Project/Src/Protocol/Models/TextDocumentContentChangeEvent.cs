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
	/// 文档内容变更事件
	/// 支持增量同步（带 range）和全量同步（不带 range）
	/// </summary>
	public class TextDocumentContentChangeEvent
	{
		/// <summary>
		/// 变更的范围。为 null 时表示全量替换
		/// </summary>
		[JsonProperty("range", NullValueHandling = NullValueHandling.Ignore)]
		public Range Range { get; set; }

		/// <summary>
		/// 被替换文本的长度（仅增量模式使用）
		/// </summary>
		[JsonProperty("rangeLength", NullValueHandling = NullValueHandling.Ignore)]
		public int? RangeLength { get; set; }

		/// <summary>
		/// 新的文本内容
		/// </summary>
		[JsonProperty("text")]
		public string Text { get; set; }

		/// <summary>
		/// 创建全量同步变更事件
		/// </summary>
		public static TextDocumentContentChangeEvent FullChange(string text)
		{
			return new TextDocumentContentChangeEvent
			{
				Range = null,
				RangeLength = null,
				Text = text
			};
		}

		/// <summary>
		/// 创建增量同步变更事件
		/// </summary>
		public static TextDocumentContentChangeEvent IncrementalChange(Range range, int rangeLength, string text)
		{
			return new TextDocumentContentChangeEvent
			{
				Range = range,
				RangeLength = rangeLength,
				Text = text
			};
		}
	}
}
