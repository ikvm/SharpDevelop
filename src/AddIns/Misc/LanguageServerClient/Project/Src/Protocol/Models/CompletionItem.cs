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

using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ICSharpCode.LanguageServerClient.Protocol.Models
{
	/// <summary>
	/// 代码补全项
	/// </summary>
	public class CompletionItem
	{
		/// <summary>
		/// 补全项的标签（显示文本）
		/// </summary>
		[JsonProperty("label")]
		public string Label { get; set; }

		/// <summary>
		/// 补全项的类型
		/// </summary>
		[JsonProperty("kind", NullValueHandling = NullValueHandling.Ignore)]
		public int? Kind { get; set; }

		/// <summary>
		/// 补全项的详细信息（如类型签名）
		/// </summary>
		[JsonProperty("detail", NullValueHandling = NullValueHandling.Ignore)]
		public string Detail { get; set; }

		/// <summary>
		/// 补全项的文档说明
		/// </summary>
		[JsonProperty("documentation", NullValueHandling = NullValueHandling.Ignore)]
		public JToken Documentation { get; set; }

		/// <summary>
		/// 是否已弃用
		/// </summary>
		[JsonProperty("deprecated", NullValueHandling = NullValueHandling.Ignore)]
		public bool? Deprecated { get; set; }

		/// <summary>
		/// 预选此补全项
		/// </summary>
		[JsonProperty("preselect", NullValueHandling = NullValueHandling.Ignore)]
		public bool? Preselect { get; set; }

		/// <summary>
		/// 排序文本（用于自定义排序）
		/// </summary>
		[JsonProperty("sortText", NullValueHandling = NullValueHandling.Ignore)]
		public string SortText { get; set; }

		/// <summary>
		/// 过滤文本（用于自定义过滤）
		/// </summary>
		[JsonProperty("filterText", NullValueHandling = NullValueHandling.Ignore)]
		public string FilterText { get; set; }

		/// <summary>
		/// 插入文本（如果不同于 label）
		/// </summary>
		[JsonProperty("insertText", NullValueHandling = NullValueHandling.Ignore)]
		public string InsertText { get; set; }

		/// <summary>
		/// 插入文本的格式（1=纯文本，2=代码片段）
		/// </summary>
		[JsonProperty("insertTextFormat", NullValueHandling = NullValueHandling.Ignore)]
		public int? InsertTextFormat { get; set; }

		/// <summary>
		/// 文本编辑（替代 insertText）
		/// </summary>
		[JsonProperty("textEdit", NullValueHandling = NullValueHandling.Ignore)]
		public TextEdit TextEdit { get; set; }

		/// <summary>
		/// 附加的文本编辑（如添加 using 语句）
		/// </summary>
		[JsonProperty("additionalTextEdits", NullValueHandling = NullValueHandling.Ignore)]
		public List<TextEdit> AdditionalTextEdits { get; set; }

		/// <summary>
		/// 提交字符列表
		/// </summary>
		[JsonProperty("commitCharacters", NullValueHandling = NullValueHandling.Ignore)]
		public List<string> CommitCharacters { get; set; }

		/// <summary>
		/// 补全项的数据（在 resolve 请求时原样返回）
		/// </summary>
		[JsonProperty("data", NullValueHandling = NullValueHandling.Ignore)]
		public JToken Data { get; set; }
	}

	/// <summary>
	/// 文本编辑
	/// </summary>
	public class TextEdit
	{
		/// <summary>
		/// 要替换的范围
		/// </summary>
		[JsonProperty("range")]
		public Range Range { get; set; }

		/// <summary>
		/// 替换的文本
		/// </summary>
		[JsonProperty("newText")]
		public string NewText { get; set; }
	}

	/// <summary>
	/// 补全项类型枚举
	/// </summary>
	public static class CompletionItemKind
	{
		public const int Text = 1;
		public const int Method = 2;
		public const int Function = 3;
		public const int Constructor = 4;
		public const int Field = 5;
		public const int Variable = 6;
		public const int Class = 7;
		public const int Interface = 8;
		public const int Module = 9;
		public const int Property = 10;
		public const int Unit = 11;
		public const int Value = 12;
		public const int Enum = 13;
		public const int Keyword = 14;
		public const int Snippet = 15;
		public const int Color = 16;
		public const int File = 17;
		public const int Reference = 18;
		public const int Folder = 19;
		public const int EnumMember = 20;
		public const int Constant = 21;
		public const int Struct = 22;
		public const int Event = 23;
		public const int Operator = 24;
		public const int TypeParameter = 25;
	}
}
