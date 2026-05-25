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

using ICSharpCode.Core;
using ICSharpCode.NRefactory.TypeSystem;
using ICSharpCode.SharpDevelop;
using ICSharpCode.SharpDevelop.Editor;
using ICSharpCode.SharpDevelop.Editor.CodeCompletion;
using ICSharpCode.SharpDevelop.Project;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Completion;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;

using CSharpBinding.Parser.Roslyn;

namespace CSharpBinding.Completion.Roslyn
{
	/// <summary>
	/// 基于 Roslyn 的 C# 代码补全绑定。
	/// 使用 Microsoft.CodeAnalysis.CSharp.Workspaces 的 CompletionService 提供代码补全。
	/// </summary>
	public class RoslynCompletionBinding : ICodeCompletionBinding, IInsightCodeCompletionBinding
	{
		public CodeCompletionKeyPressResult HandleKeyPress(ITextEditor editor, char ch)
		{
			// 使用 HandleKeyPressed 代替
			return CodeCompletionKeyPressResult.None;
		}

		public bool HandleKeyPressed(ITextEditor editor, char ch)
		{
			if (editor.ActiveCompletionWindow != null)
				return false;
			return ShowCompletion(editor, ch, false);
		}

		public bool CtrlSpace(ITextEditor editor)
		{
			return ShowCompletion(editor, '\0', true);
		}

		public bool CtrlShiftSpace(ITextEditor editor)
		{
			// Roslyn 签名帮助暂不实现，由 NRefactory 绑定处理
			return false;
		}

		/// <summary>
		/// 显示补全列表的核心方法。
		/// 1. 获取或创建 Roslyn Document
		/// 2. 调用 CompletionService.GetCompletionsAsync
		/// 3. 将 Roslyn CompletionItem 转换为 SD ICompletionItem
		/// 4. 显示补全窗口
		/// </summary>
		bool ShowCompletion(ITextEditor editor, char completionChar, bool ctrlSpace)
		{
			try {
				// 获取项目
				var project = SD.ProjectService.FindProjectContainingFile(editor.FileName);
				if (project == null)
					return false;

				// 获取 Roslyn CSharpCompilation
				var compilation = RoslynCompilationManager.GetOrCreateCompilation(project);
				if (compilation == null)
					return false;

				// 使用当前编辑器内容更新 Compilation 中的 SyntaxTree
				var sourceText = SourceText.From(editor.Document.Text);
				var updatedCompilation = RoslynCompilationManager.UpdateCompilation(
					compilation, editor.FileName, sourceText);

				// 查找当前文件的 SyntaxTree
				var syntaxTree = updatedCompilation.SyntaxTrees
					.FirstOrDefault(t => string.Equals(t.FilePath, editor.FileName, StringComparison.OrdinalIgnoreCase));
				if (syntaxTree == null)
					return false;

				// 创建 AdhocWorkspace → Project → Document 以使用 CompletionService
				var workspace = new AdhocWorkspace();
				var projectId = ProjectId.CreateNewId();
				var versionStamp = VersionStamp.Create();

				// ProjectInfo.Create 位置参数顺序：
				// (projectId, versionStamp, name, assemblyName, language, filePath, outputFilePath,
				//  compilationOptions, parseOptions, documents, projectReferences, metadataReferences,
				//  analyzerReferences, additionalDocuments, isSubmission, hostObjectType)
				var projectInfo = ProjectInfo.Create(
					projectId,
					versionStamp,
					"CompletionProject",
					"CompletionAssembly",
					LanguageNames.CSharp,
					null, // filePath
					null, // outputFilePath
					null, // compilationOptions
					null, // parseOptions
					null, // documents
					null, // projectReferences
					updatedCompilation.ExternalReferences,
					null, // analyzerReferences
					null, // additionalDocuments
					false, // isSubmission
					null  // hostObjectType
				);

				workspace.AddProject(projectInfo);
				// AdhocWorkspace.AddDocument 返回 Document，从中获取 Id
				var document = workspace.AddDocument(projectId, editor.FileName, sourceText);
				if (document == null)
					return false;

				// 计算光标位置（Roslyn 使用 0-based 偏移量）
				int caretPosition = editor.Caret.Offset;

				// 确定补全触发方式（Roslyn 4.8.0 使用静态工厂方法创建 CompletionTrigger）
				CompletionTrigger trigger;
				if (ctrlSpace) {
					trigger = CompletionTrigger.Invoke;
				} else if (completionChar != '\0') {
					trigger = CompletionTrigger.CreateInsertionTrigger(completionChar);
				} else {
					trigger = CompletionTrigger.Invoke;
				}

				// 获取补全列表
				var completionService = CompletionService.GetService(document);
				if (completionService == null)
					return false;

				var completionList = completionService.GetCompletionsAsync(
					document, caretPosition, trigger
				).GetAwaiter().GetResult();

				if (completionList == null || completionList.ItemsList.Count == 0)
					return false;

				// 转换为 SharpDevelop 补全列表
				var itemList = ConvertToCompletionItemList(
					completionList, editor, ctrlSpace, completionChar, updatedCompilation);
				if (itemList.Items.Count == 0)
					return false;

				itemList.SortItems();
				editor.ShowCompletionWindow(itemList);
				return true;
			} catch (Exception ex) {
				LoggingService.Warn("Roslyn 代码补全失败", ex);
				return false;
			}
		}

		/// <summary>
		/// 将 Roslyn CompletionList 转换为 SharpDevelop DefaultCompletionItemList
		/// </summary>
		DefaultCompletionItemList ConvertToCompletionItemList(
			CompletionList completionList,
			ITextEditor editor,
			bool ctrlSpace,
			char completionChar,
			CSharpCompilation compilation)
		{
			var itemList = new DefaultCompletionItemList();
			int caretOffset = editor.Caret.Offset;

			foreach (var item in completionList.ItemsList) {
				// 过滤掉空项
				if (string.IsNullOrEmpty(item.DisplayText))
					continue;

				var completionData = new RoslynCompletionData(item, compilation, editor);
				itemList.Items.Add(completionData);
			}

			// 添加代码片段
			var snippets = editor.GetSnippets().ToList();
			foreach (var snippet in snippets) {
				itemList.Items.Add(snippet);
			}

			// 设置预选长度
			if (ctrlSpace) {
				// Ctrl+Space：尝试确定补全词的起始位置
				itemList.PreselectionLength = GetPreselectionLength(editor);
			} else {
				// 触发字符补全
				if (char.IsLetterOrDigit(completionChar) || completionChar == '_') {
					itemList.PreselectionLength = 1;
				} else {
					itemList.PreselectionLength = 0;
				}
			}

			// Roslyn 4.8.0 中 CompletionList 不再有 IsIncomplete/IsExclusive 属性，
			// 默认设置为 true（假设包含所有可用项）
			itemList.ContainsAllAvailableItems = true;

			return itemList;
		}

		/// <summary>
		/// 获取 Ctrl+Space 时的预选长度。
		/// 向前查找标识符字符，确定补全词的起始位置。
		/// </summary>
		static int GetPreselectionLength(ITextEditor editor)
		{
			int offset = editor.Caret.Offset;
			int start = offset;
			while (start > 0 && IsIdentifierChar(editor.Document.GetCharAt(start - 1))) {
				start--;
			}
			return offset - start;
		}

		/// <summary>
		/// 判断字符是否为标识符字符（字母、数字、下划线）
		/// </summary>
		static bool IsIdentifierChar(char c)
		{
			return char.IsLetterOrDigit(c) || c == '_';
		}
	}
}
