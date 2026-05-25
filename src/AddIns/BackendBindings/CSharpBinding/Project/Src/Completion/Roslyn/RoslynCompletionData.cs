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
using System.Linq;
using System.Threading;

using ICSharpCode.Core;
using ICSharpCode.SharpDevelop;
using ICSharpCode.SharpDevelop.Editor;
using ICSharpCode.SharpDevelop.Editor.CodeCompletion;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Completion;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;

using SDCompletionContext = ICSharpCode.SharpDevelop.Editor.CodeCompletion.CompletionContext;

namespace CSharpBinding.Completion.Roslyn
{
	/// <summary>
	/// Roslyn 补全项适配器，将 Roslyn CompletionItem 适配为 SD ICompletionItem。
	/// 实现 ICompletionItem 接口，用于在 SharpDevelop 补全窗口中显示 Roslyn 的补全建议。
	/// </summary>
	public class RoslynCompletionData : ICompletionItem
	{
		readonly CompletionItem roslynItem;
		readonly CSharpCompilation compilation;
		readonly ITextEditor editor;

		/// <summary>
		/// 缓存的描述文本
		/// </summary>
		string description;

		public RoslynCompletionData(CompletionItem roslynItem, CSharpCompilation compilation, ITextEditor editor)
		{
			this.roslynItem = roslynItem ?? throw new ArgumentNullException(nameof(roslynItem));
			this.compilation = compilation ?? throw new ArgumentNullException(nameof(compilation));
			this.editor = editor ?? throw new ArgumentNullException(nameof(editor));
		}

		/// <summary>
		/// 补全项的显示文本
		/// </summary>
		public string Text {
			get { return roslynItem.DisplayText; }
		}

		/// <summary>
		/// 补全项的描述信息。
		/// 使用 Roslyn 的 Description 属性获取详细描述。
		/// </summary>
		public string Description {
			get {
				if (description == null) {
					description = GetDescription();
				}
				return description;
			}
		}

		/// <summary>
		/// 补全项的图标。
		/// 根据 Roslyn CompletionItem 的 Tags 映射到 SD 图标。
		/// </summary>
		public IImage Image {
			get { return RoslynCompletionItemKindToIconConverter.GetIcon(roslynItem); }
		}

		/// <summary>
		/// 优先级（用于补全列表排序）
		/// </summary>
		public double Priority {
			get { return CodeCompletionDataUsageCache.GetPriority(DisplayText, true); }
		}

		/// <summary>
		/// 用于 CodeCompletionDataUsageCache 的显示文本
		/// </summary>
		string DisplayText {
			get { return roslynItem.DisplayText; }
		}

		/// <summary>
		/// 执行补全操作。
		/// 使用 Roslyn 的 CompletionService.GetChangeAsync 获取补全变更，
		/// 然后应用到编辑器文档中。
		/// </summary>
		public void Complete(SDCompletionContext context)
		{
			try {
				// 创建 AdhocWorkspace 和 Document 来获取 CompletionChange
				var workspace = new AdhocWorkspace();
				var projectId = ProjectId.CreateNewId();
				var versionStamp = VersionStamp.Create();

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
					compilation.ExternalReferences,
					null, // analyzerReferences
					null, // additionalDocuments
					false, // isSubmission
					null  // hostObjectType
				);

				workspace.AddProject(projectInfo);
				var sourceText = SourceText.From(context.Editor.Document.Text);
				// AdhocWorkspace.AddDocument 返回 Document
				var document = workspace.AddDocument(projectId, context.Editor.FileName, sourceText);
				if (document == null) {
					// 回退：直接使用 DisplayText 作为插入文本
					FallbackComplete(context);
					return;
				}

				var completionService = CompletionService.GetService(document);
				if (completionService == null) {
					FallbackComplete(context);
					return;
				}

				// 获取补全变更（Roslyn 4.8.0 签名：GetChangeAsync(document, item, commitCharacter, cancellationToken)）
				var change = completionService.GetChangeAsync(
					document, roslynItem, null, default(CancellationToken)
				).GetAwaiter().GetResult();

				if (change == null) {
					FallbackComplete(context);
					return;
				}

				// 应用主文本变更
				var textChange = change.TextChange;
				context.Editor.Document.Replace(
					textChange.Span.Start,
					textChange.Span.Length,
					textChange.NewText
				);
				context.StartOffset = textChange.Span.Start;
				context.EndOffset = textChange.Span.Start + textChange.NewText.Length;

				// 应用附加变更（如自动添加 using 指令）
				if (change.IncludesCommitCharacter) {
					// Roslyn 已处理提交字符
				}

				// 更新使用计数
				CodeCompletionDataUsageCache.IncrementUsage(DisplayText);
			} catch (Exception ex) {
				LoggingService.Warn("Roslyn 补全应用失败，使用回退方式", ex);
				FallbackComplete(context);
			}
		}

		/// <summary>
		/// 回退补全方式：直接使用 DisplayText 或 FilterText 作为插入文本
		/// </summary>
		void FallbackComplete(SDCompletionContext context)
		{
			var insertText = roslynItem.DisplayText;
			context.Editor.Document.Replace(context.StartOffset, context.Length, insertText);
			context.EndOffset = context.StartOffset + insertText.Length;
			CodeCompletionDataUsageCache.IncrementUsage(DisplayText);
		}

		/// <summary>
		/// 获取补全项的描述信息。
		/// 使用 Roslyn 的 CompletionItem.Description 属性。
		/// </summary>
		string GetDescription()
		{
			try {
				// 优先使用 InlineDescription
				if (!string.IsNullOrEmpty(roslynItem.InlineDescription)) {
					return roslynItem.InlineDescription;
				}

				// 尝试从 Tags 中获取类型信息
				var tags = roslynItem.Tags;
				if (tags != null) {
					// 如果有 SymbolKind 标签，构造描述
					var kindTag = tags.FirstOrDefault(t =>
						t.StartsWith("SymbolKind:", StringComparison.OrdinalIgnoreCase));
					if (kindTag != null) {
						return kindTag.Substring("SymbolKind:".Length);
					}
				}

				return roslynItem.DisplayText;
			} catch {
				return roslynItem.DisplayText;
			}
		}
	}
}
