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

namespace ICSharpCode.LanguageServerClient.Protocol
{
	/// <summary>
	/// 服务器能力声明
	/// </summary>
	public class ServerCapabilities
	{
		/// <summary>
		/// 文本文档同步方式
		/// </summary>
		[JsonProperty("textDocumentSync", NullValueHandling = NullValueHandling.Ignore)]
		public TextDocumentSyncOptions TextDocumentSync { get; set; }

		/// <summary>
		/// 补全提供者
		/// </summary>
		[JsonProperty("completionProvider", NullValueHandling = NullValueHandling.Ignore)]
		public CompletionOptions CompletionProvider { get; set; }

		/// <summary>
		/// 悬停提示提供者
		/// </summary>
		[JsonProperty("hoverProvider", NullValueHandling = NullValueHandling.Ignore)]
		public bool? HoverProvider { get; set; }

		/// <summary>
		/// 签名帮助提供者
		/// </summary>
		[JsonProperty("signatureHelpProvider", NullValueHandling = NullValueHandling.Ignore)]
		public SignatureHelpOptions SignatureHelpProvider { get; set; }

		/// <summary>
		/// 定义跳转提供者
		/// </summary>
		[JsonProperty("definitionProvider", NullValueHandling = NullValueHandling.Ignore)]
		public bool? DefinitionProvider { get; set; }

		/// <summary>
		/// 类型定义跳转提供者
		/// </summary>
		[JsonProperty("typeDefinitionProvider", NullValueHandling = NullValueHandling.Ignore)]
		public bool? TypeDefinitionProvider { get; set; }

		/// <summary>
		/// 引用查找提供者
		/// </summary>
		[JsonProperty("referencesProvider", NullValueHandling = NullValueHandling.Ignore)]
		public bool? ReferencesProvider { get; set; }

		/// <summary>
		/// 文档高亮提供者
		/// </summary>
		[JsonProperty("documentHighlightProvider", NullValueHandling = NullValueHandling.Ignore)]
		public bool? DocumentHighlightProvider { get; set; }

		/// <summary>
		/// 文档符号提供者
		/// </summary>
		[JsonProperty("documentSymbolProvider", NullValueHandling = NullValueHandling.Ignore)]
		public bool? DocumentSymbolProvider { get; set; }

		/// <summary>
		/// 工作区符号提供者
		/// </summary>
		[JsonProperty("workspaceSymbolProvider", NullValueHandling = NullValueHandling.Ignore)]
		public bool? WorkspaceSymbolProvider { get; set; }

		/// <summary>
		/// 代码操作提供者
		/// </summary>
		[JsonProperty("codeActionProvider", NullValueHandling = NullValueHandling.Ignore)]
		public JToken CodeActionProvider { get; set; }

		/// <summary>
		/// 文档格式化提供者
		/// </summary>
		[JsonProperty("documentFormattingProvider", NullValueHandling = NullValueHandling.Ignore)]
		public bool? DocumentFormattingProvider { get; set; }

		/// <summary>
		/// 文档范围格式化提供者
		/// </summary>
		[JsonProperty("documentRangeFormattingProvider", NullValueHandling = NullValueHandling.Ignore)]
		public bool? DocumentRangeFormattingProvider { get; set; }

		/// <summary>
		/// 文档格式化按类型提供者
		/// </summary>
		[JsonProperty("documentOnTypeFormattingProvider", NullValueHandling = NullValueHandling.Ignore)]
		public DocumentOnTypeFormattingOptions DocumentOnTypeFormattingProvider { get; set; }

		/// <summary>
		/// 重命名提供者
		/// </summary>
		[JsonProperty("renameProvider", NullValueHandling = NullValueHandling.Ignore)]
		public JToken RenameProvider { get; set; }

		/// <summary>
		/// 折叠范围提供者
		/// </summary>
		[JsonProperty("foldingRangeProvider", NullValueHandling = NullValueHandling.Ignore)]
		public JToken FoldingRangeProvider { get; set; }

		/// <summary>
		/// 执行命令提供者
		/// </summary>
		[JsonProperty("executeCommandProvider", NullValueHandling = NullValueHandling.Ignore)]
		public ExecuteCommandOptions ExecuteCommandProvider { get; set; }
	}

	/// <summary>
	/// 文本文档同步选项
	/// </summary>
	public class TextDocumentSyncOptions
	{
		/// <summary>
		/// 是否在打开文档时发送通知
		/// </summary>
		[JsonProperty("openClose", NullValueHandling = NullValueHandling.Ignore)]
		public bool? OpenClose { get; set; }

		/// <summary>
		/// 同步方式：0=无，1=全量，2=增量
		/// </summary>
		[JsonProperty("change", NullValueHandling = NullValueHandling.Ignore)]
		public int? Change { get; set; }

		/// <summary>
		/// 是否在保存前发送通知
		/// </summary>
		[JsonProperty("willSave", NullValueHandling = NullValueHandling.Ignore)]
		public bool? WillSave { get; set; }

		/// <summary>
		/// 是否在保存前等待
		/// </summary>
		[JsonProperty("willSaveWaitUntil", NullValueHandling = NullValueHandling.Ignore)]
		public bool? WillSaveWaitUntil { get; set; }

		/// <summary>
		/// 是否在保存后发送通知
		/// </summary>
		[JsonProperty("save", NullValueHandling = NullValueHandling.Ignore)]
		public JToken Save { get; set; }
	}

	/// <summary>
	/// 文档同步方式
	/// </summary>
	public static class TextDocumentSyncKind
	{
		/// <summary>不同步</summary>
		public const int None = 0;
		/// <summary>全量同步</summary>
		public const int Full = 1;
		/// <summary>增量同步</summary>
		public const int Incremental = 2;
	}

	/// <summary>
	/// 补全选项
	/// </summary>
	public class CompletionOptions
	{
		/// <summary>
		/// 触发补全的字符列表
		/// </summary>
		[JsonProperty("triggerCharacters", NullValueHandling = NullValueHandling.Ignore)]
		public List<string> TriggerCharacters { get; set; }

		/// <summary>
		/// 是否支持补全项解析
		/// </summary>
		[JsonProperty("resolveProvider", NullValueHandling = NullValueHandling.Ignore)]
		public bool? ResolveProvider { get; set; }
	}

	/// <summary>
	/// 签名帮助选项
	/// </summary>
	public class SignatureHelpOptions
	{
		/// <summary>
		/// 触发签名帮助的字符列表
		/// </summary>
		[JsonProperty("triggerCharacters", NullValueHandling = NullValueHandling.Ignore)]
		public List<string> TriggerCharacters { get; set; }
	}

	/// <summary>
	/// 按类型格式化选项
	/// </summary>
	public class DocumentOnTypeFormattingOptions
	{
		/// <summary>
		/// 触发格式化的字符
		/// </summary>
		[JsonProperty("firstTriggerCharacter")]
		public string FirstTriggerCharacter { get; set; }

		/// <summary>
		/// 更多触发字符
		/// </summary>
		[JsonProperty("moreTriggerCharacter", NullValueHandling = NullValueHandling.Ignore)]
		public List<string> MoreTriggerCharacter { get; set; }
	}

	/// <summary>
	/// 执行命令选项
	/// </summary>
	public class ExecuteCommandOptions
	{
		/// <summary>
		/// 支持的命令列表
		/// </summary>
		[JsonProperty("commands")]
		public List<string> Commands { get; set; }
	}
}
