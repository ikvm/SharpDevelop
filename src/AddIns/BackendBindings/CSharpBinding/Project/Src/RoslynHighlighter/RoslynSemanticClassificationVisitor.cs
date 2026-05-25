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
using System.Threading;

using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Highlighting;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Classification;
using Microsoft.CodeAnalysis.Text;

using System.Linq;

namespace CSharpBinding.RoslynHighlighter
{
	/// <summary>
	/// 使用 Roslyn Classifier API 对语法节点进行语义分类。
	/// 利用 Roslyn 内置的 Classifier.GetClassifiedSpansAsync() 方法，
	/// 获取指定文本范围内的语义分类结果，并映射为 SharpDevelop 高亮颜色。
	/// </summary>
	public class RoslynSemanticClassificationVisitor : IDisposable
	{
		SemanticModel semanticModel;
		readonly IDocument document;

		/// <summary>
		/// 当前正在高亮的行号
		/// </summary>
		int lineNumber;

		/// <summary>
		/// 当前正在构建的高亮行
		/// </summary>
		HighlightedLine highlightedLine;

		public RoslynSemanticClassificationVisitor(IDocument document)
		{
			if (document == null)
				throw new ArgumentNullException("document");
			this.document = document;
		}

		public void Dispose()
		{
			semanticModel = null;
		}

		/// <summary>
		/// 设置当前 SemanticModel。
		/// 在每次高亮会话开始时设置，结束时清除。
		/// </summary>
		internal SemanticModel SemanticModel {
			get { return semanticModel; }
			set { semanticModel = value; }
		}

		/// <summary>
		/// 对指定行进行语义分类并应用高亮。
		/// 使用 Roslyn Classifier.GetClassifiedSpansAsync() 获取分类结果，
		/// 然后通过 RoslynClassificationToHighlightingMapper 映射为 SD 高亮颜色。
		/// 注意：由于 IHighlighter.HighlightLine 是同步方法，
		/// 这里使用 GetAwaiter().GetResult() 同步等待异步结果。
		/// </summary>
		internal void ClassifyLine(int lineNumber, HighlightedLine line)
		{
			this.lineNumber = lineNumber;
			this.highlightedLine = line;

			if (semanticModel == null)
				return;

			try {
				var documentLine = line.DocumentLine;
				int startOffset = documentLine.Offset;

				// 创建 Roslyn TextSpan 覆盖当前行
				var textSpan = new TextSpan(startOffset, documentLine.Length);

				// 使用 Roslyn Classifier API 获取分类结果
				// GetClassifiedSpansAsync 需要 Document 对象，因此从 AdhocWorkspace 创建
				var classifiedSpans = GetClassifiedSpans(textSpan);

				int endOffset = startOffset + documentLine.Length;
				foreach (var classifiedSpan in classifiedSpans) {
					ProcessClassifiedSpan(classifiedSpan, startOffset, endOffset);
				}
			} catch (Exception ex) {
				// 分类失败时静默处理，不影响编辑器稳定性
				System.Diagnostics.Debug.WriteLine("Roslyn 语义分类失败: " + ex.Message);
			}
		}

		/// <summary>
		/// 使用 AdhocWorkspace 从当前 SemanticModel 创建 Document，
		/// 然后调用 Classifier.GetClassifiedSpansAsync 获取分类结果。
		/// </summary>
		IEnumerable<ClassifiedSpan> GetClassifiedSpans(TextSpan textSpan)
		{
			var syntaxTree = semanticModel.SyntaxTree;
			var compilation = semanticModel.Compilation;

			using (var workspace = new AdhocWorkspace()) {
				var projectId = ProjectId.CreateNewId();
				var documentId = DocumentId.CreateNewId(projectId);

				var projectInfo = ProjectInfo.Create(
					projectId,
					VersionStamp.Create(),
					"TempClassificationProject",
					"TempClassificationAssembly",
					LanguageNames.CSharp,
					documents: new[] {
						DocumentInfo.Create(
							documentId,
							syntaxTree.FilePath ?? "TempFile.cs",
							sourceCodeKind: SourceCodeKind.Regular,
							filePath: syntaxTree.FilePath)
					},
					metadataReferences: compilation.References);

				var roslynProject = workspace.AddProject(projectInfo);

				// 添加语法树内容到文档
				var sourceText = syntaxTree.GetText();
				var solution = workspace.CurrentSolution.WithDocumentText(documentId, sourceText);
				var document = solution.GetDocument(documentId);

				if (document == null)
					return Enumerable.Empty<ClassifiedSpan>();

				return Classifier.GetClassifiedSpansAsync(document, textSpan, CancellationToken.None)
					.GetAwaiter().GetResult();
			}
		}

		/// <summary>
		/// 处理单个分类结果，将其映射为高亮段并添加到当前行。
		/// </summary>
		void ProcessClassifiedSpan(ClassifiedSpan classifiedSpan, int lineStartOffset, int lineEndOffset)
		{
			var classificationType = classifiedSpan.ClassificationType;
			var textSpan = classifiedSpan.TextSpan;

			// 将分类类型映射为 SD 高亮颜色
			var highlightingColor = RoslynClassificationToHighlightingMapper.MapClassificationToColor(classificationType);
			if (highlightingColor == null)
				return;

			// 计算分类范围在当前行内的偏移
			int spanStart = textSpan.Start;
			int spanEnd = textSpan.End;

			// 将范围限制在当前行内
			int startOffset = Math.Max(spanStart, lineStartOffset);
			int endOffset = Math.Min(spanEnd, lineEndOffset);

			if (startOffset >= endOffset)
				return;

			// 添加高亮段
			AddHighlightSection(startOffset, endOffset - startOffset, highlightingColor);
		}

		/// <summary>
		/// 向当前高亮行添加一个高亮段。
		/// 确保高亮段按偏移量排序，并处理重叠情况。
		/// </summary>
		void AddHighlightSection(int offset, int length, HighlightingColor color)
		{
			if (color == null || length <= 0)
				return;

			// 确保高亮段不超出当前行范围
			int lineStartOffset = highlightedLine.DocumentLine.Offset;
			int lineEndOffset = lineStartOffset + highlightedLine.DocumentLine.Length;

			offset = Math.Max(offset, lineStartOffset);
			int endOffset = Math.Min(offset + length, lineEndOffset);
			length = endOffset - offset;

			if (length <= 0)
				return;

			// 检查与已有段的重叠
			if (highlightedLine.Sections.Count > 0) {
				HighlightedSection prevSection = highlightedLine.Sections[highlightedLine.Sections.Count - 1];
				if (offset < prevSection.Offset + prevSection.Length) {
					// 重叠：跳过（与现有 CSharpSemanticHighlighter 行为一致）
					return;
				}
			}

			highlightedLine.Sections.Add(new HighlightedSection {
				Offset = offset,
				Length = length,
				Color = color
			});
		}
	}
}
