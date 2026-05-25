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
using System.Linq;
using System.Threading;

using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.Core;
using ICSharpCode.LanguageServerClient.Protocol.Models;
using ICSharpCode.SharpDevelop;
using ICSharpCode.SharpDevelop.Editor;
using ICSharpCode.SharpDevelop.Editor.CodeCompletion;

using SDCompletionContext = ICSharpCode.SharpDevelop.Editor.CodeCompletion.CompletionContext;
using SDCompletionItem = ICSharpCode.SharpDevelop.Editor.CodeCompletion.ICompletionItem;
using SDCompletionItemList = ICSharpCode.SharpDevelop.Editor.CodeCompletion.DefaultCompletionItemList;

namespace ICSharpCode.LanguageServerClient.Adapters
{
	/// <summary>
	/// LSP 代码补全适配器，将 LSP 补全结果转换为 SharpDevelop 的 ICompletionItem。
	/// 实现 ICodeCompletionBinding 接口，在编辑器中触发补全时通过 LSP 获取补全列表。
	/// </summary>
	public class LspCompletionAdapter : ICodeCompletionBinding, IInsightCodeCompletionBinding
	{
		/// <summary>
		/// 按键前触发（在字符插入前调用）
		/// </summary>
		public CodeCompletionKeyPressResult HandleKeyPress(ITextEditor editor, char ch)
		{
			// 使用 HandleKeyPressed 代替
			return CodeCompletionKeyPressResult.None;
		}

		/// <summary>
		/// 按键后触发（在字符插入后调用）
		/// </summary>
		public bool HandleKeyPressed(ITextEditor editor, char ch)
		{
			if (editor.ActiveCompletionWindow != null)
				return false;
			return ShowCompletion(editor, ch, false);
		}

		/// <summary>
		/// Ctrl+Space 触发补全
		/// </summary>
		public bool CtrlSpace(ITextEditor editor)
		{
			return ShowCompletion(editor, '\0', true);
		}

		/// <summary>
		/// Ctrl+Shift+Space 触发方法提示（暂不支持）
		/// </summary>
		public bool CtrlShiftSpace(ITextEditor editor)
		{
			// LSP 签名帮助暂不实现
			return false;
		}

		/// <summary>
		/// 显示补全列表
		/// </summary>
		private bool ShowCompletion(ITextEditor editor, char completionChar, bool ctrlSpace)
		{
			var lspClient = LspService.Instance.GetClient();
			if (lspClient == null || !lspClient.IsInitialized)
				return false;

			// 检查服务器是否支持补全
			if (lspClient.ServerCapabilities?.CompletionProvider == null)
				return false;

			try {
				var uri = LspConnection.FilePathToUri(editor.FileName);
				if (uri == null)
					return false;

				// 将编辑器位置转换为 LSP Position（0-based）
				var position = new Position(editor.Caret.Line - 1, editor.Caret.Column - 1);

				// 确定触发方式
				int triggerKind = ctrlSpace
					? CompletionTriggerKind.Invoked
					: CompletionTriggerKind.TriggerCharacter;

				// 获取补全列表
				var completionList = lspClient.CompletionAsync(
					uri, position, triggerKind,
					completionChar != '\0' ? completionChar.ToString() : null
				).GetAwaiter().GetResult();

				if (completionList == null || completionList.Items == null || completionList.Items.Count == 0)
					return false;

				// 转换为 SharpDevelop 补全列表
				var itemList = ConvertToCompletionItemList(completionList, editor, ctrlSpace, completionChar);
				if (itemList.Items.Count == 0)
					return false;

				itemList.SortItems();
				editor.ShowCompletionWindow(itemList);
				return true;
			} catch (Exception ex) {
				LoggingService.Error($"[LSP] 获取补全列表失败: {ex.Message}", ex);
				return false;
			}
		}

		/// <summary>
		/// 将 LSP CompletionList 转换为 SharpDevelop DefaultCompletionItemList
		/// </summary>
		private DefaultCompletionItemList ConvertToCompletionItemList(CompletionList completionList, ITextEditor editor, bool ctrlSpace, char completionChar)
		{
			var itemList = new DefaultCompletionItemList();
			int caretOffset = editor.Caret.Offset;

			foreach (var item in completionList.Items) {
				// 过滤掉不合适的项
				if (string.IsNullOrEmpty(item.Label))
					continue;

				var completionData = new LspCompletionItem(item, editor);
				itemList.Items.Add(completionData);
			}

			// 设置预选长度
			if (ctrlSpace) {
				// Ctrl+Space：尝试确定补全词的起始位置
				itemList.PreselectionLength = 0;
			} else {
				// 触发字符补全
				if (char.IsLetterOrDigit(completionChar) || completionChar == '_') {
					itemList.PreselectionLength = 1;
				} else {
					itemList.PreselectionLength = 0;
				}
			}

			itemList.ContainsAllAvailableItems = !completionList.IsIncomplete;

			return itemList;
		}
	}

	/// <summary>
	/// LSP 补全项适配器，将 LSP CompletionItem 转换为 SharpDevelop 的 ICompletionItem
	/// </summary>
	public class LspCompletionItem : ICompletionItem
	{
		readonly CompletionItem lspItem;
		readonly ITextEditor editor;

		public LspCompletionItem(CompletionItem lspItem, ITextEditor editor)
		{
			this.lspItem = lspItem;
			this.editor = editor;
		}

		/// <summary>
		/// 补全项的文本（用于搜索和显示）
		/// </summary>
		public string Text {
			get { return lspItem.InsertText ?? lspItem.Label; }
		}

		/// <summary>
		/// 补全项的描述
		/// </summary>
		public string Description {
			get {
				var parts = new List<string>();
				if (!string.IsNullOrEmpty(lspItem.Detail))
					parts.Add(lspItem.Detail);
				var docText = GetDocumentationText();
				if (!string.IsNullOrEmpty(docText))
					parts.Add(docText);
				return parts.Count > 0 ? string.Join(Environment.NewLine + Environment.NewLine, parts) : lspItem.Label;
			}
		}

		/// <summary>
		/// 补全项的图标
		/// </summary>
		public IImage Image {
			get { return GetImageForCompletionKind(lspItem.Kind); }
		}

		/// <summary>
		/// 优先级
		/// </summary>
		public double Priority { get; set; }

		/// <summary>
		/// 执行补全
		/// </summary>
		public void Complete(SDCompletionContext context)
		{
			if (lspItem.TextEdit != null) {
				// 使用 LSP 提供的 TextEdit
				var startOffset = PositionToOffset(context.Editor, lspItem.TextEdit.Range.Start);
				var endOffset = PositionToOffset(context.Editor, lspItem.TextEdit.Range.End);
				context.Editor.Document.Replace(startOffset, endOffset - startOffset, lspItem.TextEdit.NewText);
				context.EndOffset = startOffset + lspItem.TextEdit.NewText.Length;
				context.StartOffset = startOffset;
			} else {
				// 使用 InsertText 或 Label
				var insertText = lspItem.InsertText ?? lspItem.Label;
				context.Editor.Document.Replace(context.StartOffset, context.Length, insertText);
				context.EndOffset = context.StartOffset + insertText.Length;
			}

			// 处理附加的文本编辑（如自动添加 using 语句）
			if (lspItem.AdditionalTextEdits != null) {
				foreach (var edit in lspItem.AdditionalTextEdits) {
					var startOffset = PositionToOffset(context.Editor, edit.Range.Start);
					var endOffset = PositionToOffset(context.Editor, edit.Range.End);
					context.Editor.Document.Replace(startOffset, endOffset - startOffset, edit.NewText);
				}
			}
		}

		/// <summary>
		/// 获取文档文本
		/// </summary>
		private string GetDocumentationText()
		{
			if (lspItem.Documentation == null)
				return null;

			// 字符串格式的文档
			if (lspItem.Documentation.Type == Newtonsoft.Json.Linq.JTokenType.String) {
				return lspItem.Documentation.ToString();
			}

			// MarkupContent 格式
			if (lspItem.Documentation.Type == Newtonsoft.Json.Linq.JTokenType.Object) {
				var valueProp = lspItem.Documentation["value"];
				if (valueProp != null)
					return valueProp.ToString();
			}

			return null;
		}

		/// <summary>
		/// 将 LSP Position 转换为文档偏移量
		/// </summary>
		private int PositionToOffset(ITextEditor textEditor, Position position)
		{
			// LSP Position 是 0-based，SD 是 1-based
			int line = position.Line + 1;
			int column = position.Character + 1;
			return textEditor.Document.GetOffset(new TextLocation(line, column));
		}

		/// <summary>
		/// 根据 LSP CompletionItemKind 获取对应的图标
		/// </summary>
		private static IImage GetImageForCompletionKind(int? kind)
		{
			if (kind == null)
				return ClassBrowserIconService.Keyword;

			switch (kind.Value) {
				case CompletionItemKind.Class:
					return ClassBrowserIconService.Class;
				case CompletionItemKind.Interface:
					return ClassBrowserIconService.Interface;
				case CompletionItemKind.Enum:
					return ClassBrowserIconService.Enum;
				case CompletionItemKind.Struct:
					return ClassBrowserIconService.Struct;
				case CompletionItemKind.Method:
				case CompletionItemKind.Function:
				case CompletionItemKind.Constructor:
					return ClassBrowserIconService.Method;
				case CompletionItemKind.Property:
					return ClassBrowserIconService.Property;
				case CompletionItemKind.Field:
					return ClassBrowserIconService.Field;
				case CompletionItemKind.Event:
					return ClassBrowserIconService.Event;
				case CompletionItemKind.Variable:
					return ClassBrowserIconService.LocalVariable;
				case CompletionItemKind.Constant:
					return ClassBrowserIconService.Const;
				case CompletionItemKind.Keyword:
					return ClassBrowserIconService.Keyword;
				case CompletionItemKind.Snippet:
					return ClassBrowserIconService.CodeTemplate;
				case CompletionItemKind.Module:
					return ClassBrowserIconService.Namespace;
				case CompletionItemKind.EnumMember:
				case CompletionItemKind.Value:
					return ClassBrowserIconService.Const;
				case CompletionItemKind.TypeParameter:
					return ClassBrowserIconService.Class;
				default:
					return ClassBrowserIconService.Keyword;
			}
		}
	}
}
