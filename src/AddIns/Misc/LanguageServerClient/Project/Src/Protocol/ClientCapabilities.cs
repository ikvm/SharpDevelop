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

namespace ICSharpCode.LanguageServerClient.Protocol
{
	/// <summary>
	/// 客户端能力声明
	/// </summary>
	public class ClientCapabilities
	{
		/// <summary>
		/// 工作区相关能力
		/// </summary>
		[JsonProperty("workspace", NullValueHandling = NullValueHandling.Ignore)]
		public WorkspaceClientCapabilities Workspace { get; set; }

		/// <summary>
		/// 文本文档相关能力
		/// </summary>
		[JsonProperty("textDocument", NullValueHandling = NullValueHandling.Ignore)]
		public TextDocumentClientCapabilities TextDocument { get; set; }

		/// <summary>
		/// 窗口相关能力
		/// </summary>
		[JsonProperty("window", NullValueHandling = NullValueHandling.Ignore)]
		public WindowClientCapabilities Window { get; set; }
	}

	/// <summary>
	/// 工作区客户端能力
	/// </summary>
	public class WorkspaceClientCapabilities
	{
		/// <summary>
		/// 是否支持 applyEdit
		/// </summary>
		[JsonProperty("applyEdit", NullValueHandling = NullValueHandling.Ignore)]
		public bool? ApplyEdit { get; set; }

		/// <summary>
		/// workspace/edit 能力
		/// </summary>
		[JsonProperty("workspaceEdit", NullValueHandling = NullValueHandling.Ignore)]
		public WorkspaceEditCapability WorkspaceEdit { get; set; }

		/// <summary>
		/// didChangeConfiguration 能力
		/// </summary>
		[JsonProperty("didChangeConfiguration", NullValueHandling = NullValueHandling.Ignore)]
		public DynamicCapability DidChangeConfiguration { get; set; }

		/// <summary>
		/// didChangeWatchedFiles 能力
		/// </summary>
		[JsonProperty("didChangeWatchedFiles", NullValueHandling = NullValueHandling.Ignore)]
		public DynamicCapability DidChangeWatchedFiles { get; set; }

		/// <summary>
		/// symbol 能力
		/// </summary>
		[JsonProperty("symbol", NullValueHandling = NullValueHandling.Ignore)]
		public DynamicCapability Symbol { get; set; }
	}

	/// <summary>
	/// 文本文档客户端能力
	/// </summary>
	public class TextDocumentClientCapabilities
	{
		/// <summary>
		/// 同步能力
		/// </summary>
		[JsonProperty("synchronization", NullValueHandling = NullValueHandling.Ignore)]
		public TextDocumentSyncCapability Synchronization { get; set; }

		/// <summary>
		/// 补全能力
		/// </summary>
		[JsonProperty("completion", NullValueHandling = NullValueHandling.Ignore)]
		public CompletionCapability Completion { get; set; }

		/// <summary>
		/// 悬停提示能力
		/// </summary>
		[JsonProperty("hover", NullValueHandling = NullValueHandling.Ignore)]
		public DynamicCapability Hover { get; set; }

		/// <summary>
		/// 签名帮助能力
		/// </summary>
		[JsonProperty("signatureHelp", NullValueHandling = NullValueHandling.Ignore)]
		public DynamicCapability SignatureHelp { get; set; }

		/// <summary>
		/// 定义跳转能力
		/// </summary>
		[JsonProperty("definition", NullValueHandling = NullValueHandling.Ignore)]
		public DynamicCapability Definition { get; set; }

		/// <summary>
		/// 引用查找能力
		/// </summary>
		[JsonProperty("references", NullValueHandling = NullValueHandling.Ignore)]
		public DynamicCapability References { get; set; }

		/// <summary>
		/// 文档高亮能力
		/// </summary>
		[JsonProperty("documentHighlight", NullValueHandling = NullValueHandling.Ignore)]
		public DynamicCapability DocumentHighlight { get; set; }

		/// <summary>
		/// 文档符号能力
		/// </summary>
		[JsonProperty("documentSymbol", NullValueHandling = NullValueHandling.Ignore)]
		public DynamicCapability DocumentSymbol { get; set; }

		/// <summary>
		/// 格式化能力
		/// </summary>
		[JsonProperty("formatting", NullValueHandling = NullValueHandling.Ignore)]
		public DynamicCapability Formatting { get; set; }

		/// <summary>
		/// 发布诊断能力
		/// </summary>
		[JsonProperty("publishDiagnostics", NullValueHandling = NullValueHandling.Ignore)]
		public PublishDiagnosticsCapability PublishDiagnostics { get; set; }
	}

	/// <summary>
	/// 窗口客户端能力
	/// </summary>
	public class WindowClientCapabilities
	{
		/// <summary>
		/// 是否支持 showMessage 请求
		/// </summary>
		[JsonProperty("showMessage", NullValueHandling = NullValueHandling.Ignore)]
		public DynamicCapability ShowMessage { get; set; }

		/// <summary>
		/// 是否支持 logMessage 通知
		/// </summary>
		[JsonProperty("logMessage", NullValueHandling = NullValueHandling.Ignore)]
		public DynamicCapability LogMessage { get; set; }
	}

	/// <summary>
	/// 动态能力（支持注册/注销）
	/// </summary>
	public class DynamicCapability
	{
		/// <summary>
		/// 是否支持动态注册
		/// </summary>
		[JsonProperty("dynamicRegistration", NullValueHandling = NullValueHandling.Ignore)]
		public bool? DynamicRegistration { get; set; }
	}

	/// <summary>
	/// 工作区编辑能力
	/// </summary>
	public class WorkspaceEditCapability
	{
		[JsonProperty("documentChanges", NullValueHandling = NullValueHandling.Ignore)]
		public bool? DocumentChanges { get; set; }
	}

	/// <summary>
	/// 文档同步能力
	/// </summary>
	public class TextDocumentSyncCapability
	{
		[JsonProperty("dynamicRegistration", NullValueHandling = NullValueHandling.Ignore)]
		public bool? DynamicRegistration { get; set; }

		[JsonProperty("willSave", NullValueHandling = NullValueHandling.Ignore)]
		public bool? WillSave { get; set; }

		[JsonProperty("willSaveWaitUntil", NullValueHandling = NullValueHandling.Ignore)]
		public bool? WillSaveWaitUntil { get; set; }

		[JsonProperty("didSave", NullValueHandling = NullValueHandling.Ignore)]
		public bool? DidSave { get; set; }
	}

	/// <summary>
	/// 补全能力
	/// </summary>
	public class CompletionCapability
	{
		[JsonProperty("dynamicRegistration", NullValueHandling = NullValueHandling.Ignore)]
		public bool? DynamicRegistration { get; set; }

		[JsonProperty("completionItem", NullValueHandling = NullValueHandling.Ignore)]
		public CompletionItemCapability CompletionItem { get; set; }

		[JsonProperty("contextSupport", NullValueHandling = NullValueHandling.Ignore)]
		public bool? ContextSupport { get; set; }
	}

	/// <summary>
	/// 补全项能力
	/// </summary>
	public class CompletionItemCapability
	{
		[JsonProperty("snippetSupport", NullValueHandling = NullValueHandling.Ignore)]
		public bool? SnippetSupport { get; set; }

		[JsonProperty("documentationFormat", NullValueHandling = NullValueHandling.Ignore)]
		public string[] DocumentationFormat { get; set; }
	}

	/// <summary>
	/// 发布诊断能力
	/// </summary>
	public class PublishDiagnosticsCapability
	{
		[JsonProperty("relatedInformation", NullValueHandling = NullValueHandling.Ignore)]
		public bool? RelatedInformation { get; set; }

		[JsonProperty("tagSupport", NullValueHandling = NullValueHandling.Ignore)]
		public DiagnosticTagSupport TagSupport { get; set; }
	}

	/// <summary>
	/// 诊断标签支持
	/// </summary>
	public class DiagnosticTagSupport
	{
		[JsonProperty("valueSet")]
		public int[] ValueSet { get; set; }
	}
}
