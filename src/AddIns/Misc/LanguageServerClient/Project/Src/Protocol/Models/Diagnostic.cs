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
	/// 诊断信息（如编译错误、警告等）
	/// </summary>
	public class Diagnostic
	{
		/// <summary>
		/// 诊断的范围
		/// </summary>
		[JsonProperty("range")]
		public Range Range { get; set; }

		/// <summary>
		/// 诊断的严重程度
		/// </summary>
		[JsonProperty("severity", NullValueHandling = NullValueHandling.Ignore)]
		public DiagnosticSeverity? Severity { get; set; }

		/// <summary>
		/// 诊断代码（可以是数字或字符串）
		/// </summary>
		[JsonProperty("code", NullValueHandling = NullValueHandling.Ignore)]
		public JToken Code { get; set; }

		/// <summary>
		/// 诊断代码的描述
		/// </summary>
		[JsonProperty("codeDescription", NullValueHandling = NullValueHandling.Ignore)]
		public CodeDescription CodeDescription { get; set; }

		/// <summary>
		/// 诊断来源（如 "csharp"、"eslint" 等）
		/// </summary>
		[JsonProperty("source", NullValueHandling = NullValueHandling.Ignore)]
		public string Source { get; set; }

		/// <summary>
		/// 诊断消息
		/// </summary>
		[JsonProperty("message")]
		public string Message { get; set; }

		/// <summary>
		/// 相关标签
		/// </summary>
		[JsonProperty("tags", NullValueHandling = NullValueHandling.Ignore)]
		public List<int> Tags { get; set; }

		/// <summary>
		/// 相关诊断信息
		/// </summary>
		[JsonProperty("relatedInformation", NullValueHandling = NullValueHandling.Ignore)]
		public List<DiagnosticRelatedInformation> RelatedInformation { get; set; }

		/// <summary>
		/// 附加数据
		/// </summary>
		[JsonProperty("data", NullValueHandling = NullValueHandling.Ignore)]
		public JToken Data { get; set; }
	}

	/// <summary>
	/// 诊断代码描述
	/// </summary>
	public class CodeDescription
	{
		/// <summary>
		/// 代码描述的 URI
		/// </summary>
		[JsonProperty("href")]
		public string Href { get; set; }
	}

	/// <summary>
	/// 相关诊断信息
	/// </summary>
	public class DiagnosticRelatedInformation
	{
		/// <summary>
		/// 相关位置
		/// </summary>
		[JsonProperty("location")]
		public Location Location { get; set; }

		/// <summary>
		/// 相关消息
		/// </summary>
		[JsonProperty("message")]
		public string Message { get; set; }
	}

	/// <summary>
	/// 诊断标签
	/// </summary>
	public static class DiagnosticTag
	{
		/// <summary>不必要的代码（如未使用的变量）</summary>
		public const int Unnecessary = 1;
		/// <summary>已弃用的代码</summary>
		public const int Deprecated = 2;
	}
}
