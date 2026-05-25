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
using System.Diagnostics;
using System.Linq;

using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Highlighting;
using ICSharpCode.Core;
using ICSharpCode.NRefactory.TypeSystem;
using ICSharpCode.SharpDevelop;
using ICSharpCode.SharpDevelop.Parser;
using ICSharpCode.SharpDevelop.Project;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;

using AvalonEditDocument = ICSharpCode.AvalonEdit.Document;

using CSharpBinding.Parser;
using CSharpBinding.Parser.Roslyn;

namespace CSharpBinding.RoslynHighlighter
{
	/// <summary>
	/// 基于 Roslyn 的 C# 语义高亮器。
	/// 使用 Roslyn SemanticModel 和 Classifier API 对 C# 代码进行语义高亮，
	/// 替代基于 NRefactory 的 CSharpSemanticHighlighter。
	/// </summary>
	public class RoslynSemanticHighlighter : IHighlighter
	{
		readonly IDocument document;
		internal RoslynSemanticClassificationVisitor visitor;

		List<IDocumentLine> invalidLines;
		List<CachedLine> cachedLines;

		bool hasCrashed;
		bool forceParseOnNextRefresh;
		bool eventHandlersAreRegistered;
		bool inHighlightingGroup;

		public RoslynSemanticHighlighter(IDocument document)
		{
			if (document == null)
				throw new ArgumentNullException("document");
			this.document = document;

			this.visitor = new RoslynSemanticClassificationVisitor(document);

			if (document is AvalonEditDocument.TextDocument && SD.MainThread.CheckAccess()) {
				// 仅对实时 AvalonEdit 文档使用缓存
				// 只读文档（如搜索结果）不需要缓存，因为不需要重复高亮同一行
				cachedLines = new List<CachedLine>();
				// 行失效仅对实时 AvalonEdit 文档需要
				invalidLines = new List<IDocumentLine>();
				// 仅对编辑器中的真实文档注册事件处理器
				SD.ParserService.ParseInformationUpdated += ParserService_ParseInformationUpdated;
				SD.ParserService.LoadSolutionProjectsThread.Finished += ParserService_LoadSolutionProjectsThreadEnded;
				eventHandlersAreRegistered = true;
			}
		}

		public void Dispose()
		{
			if (eventHandlersAreRegistered) {
				SD.ParserService.ParseInformationUpdated -= ParserService_ParseInformationUpdated;
				SD.ParserService.LoadSolutionProjectsThread.Finished -= ParserService_LoadSolutionProjectsThreadEnded;
				eventHandlersAreRegistered = false;
			}
			this.visitor.Dispose();
		}

		public event HighlightingStateChangedEventHandler HighlightingStateChanged;

		protected virtual void OnHighlightingStateChanged(int fromLineNumber, int toLineNumber)
		{
			if (HighlightingStateChanged != null) {
				HighlightingStateChanged(fromLineNumber, toLineNumber);
			}
		}

		IDocument IHighlighter.Document {
			get { return document; }
		}

		IEnumerable<HighlightingColor> IHighlighter.GetColorStack(int lineNumber)
		{
			return null;
		}

		void IHighlighter.UpdateHighlightingState(int lineNumber)
		{
		}

		public HighlightedLine HighlightLine(int lineNumber)
		{
			IDocumentLine documentLine = document.GetLineByNumber(lineNumber);
			if (hasCrashed) {
				// 崩溃后不再高亮
				return new HighlightedLine(document, documentLine);
			}
			ITextSourceVersion newVersion = document.Version;
			CachedLine cachedLine = null;
			if (cachedLines != null) {
				for (int i = 0; i < cachedLines.Count; i++) {
					if (cachedLines[i].DocumentLine == documentLine) {
						if (newVersion == null || !newVersion.BelongsToSameDocumentAs(cachedLines[i].OldVersion)) {
							// 无法列出从旧版本到新版本的变更：无法更新缓存，因此移除
							cachedLines.RemoveAt(i);
						} else {
							cachedLine = cachedLines[i];
						}
						break;
					}
				}

				if (cachedLine != null && cachedLine.IsValid && newVersion.CompareAge(cachedLine.OldVersion) == 0) {
					// 文件自缓存创建以来未更改，直接复用旧的高亮行
					#if DEBUG
					cachedLine.HighlightedLine.ValidateInvariants();
					#endif
					return cachedLine.HighlightedLine;
				}
			}

			bool wasInHighlightingGroup = inHighlightingGroup;
			if (!inHighlightingGroup) {
				BeginHighlighting();
			}
			try {
				return DoHighlightLine(lineNumber, documentLine, cachedLine, newVersion);
			} finally {
				if (!wasInHighlightingGroup)
					EndHighlighting();
			}
		}

		HighlightedLine DoHighlightLine(int lineNumber, IDocumentLine documentLine, CachedLine cachedLine, ITextSourceVersion newVersion)
		{
			// 获取 SemanticModel
			SemanticModel semanticModel = GetSemanticModel();
			if (semanticModel == null) {
				if (invalidLines != null && !invalidLines.Contains(documentLine)) {
					invalidLines.Add(documentLine);
				}

				if (cachedLine != null) {
					// 如果有缓存版本，调整到最新文档变更并返回
					// 避免包含语义高亮的行闪烁
					cachedLine.Update(newVersion);
					#if DEBUG
					cachedLine.HighlightedLine.ValidateInvariants();
					#endif
					return cachedLine.HighlightedLine;
				} else {
					return null;
				}
			}

			// 设置 visitor 的 SemanticModel
			visitor.SemanticModel = semanticModel;

			var line = new HighlightedLine(document, documentLine);

			if (Debugger.IsAttached) {
				visitor.ClassifyLine(lineNumber, line);
				#if DEBUG
				line.ValidateInvariants();
				#endif
			} else {
				try {
					visitor.ClassifyLine(lineNumber, line);
					#if DEBUG
					line.ValidateInvariants();
					#endif
				} catch (Exception ex) {
					hasCrashed = true;
					throw new ApplicationException("Error highlighting line " + lineNumber, ex);
				}
			}

			if (cachedLines != null && document.Version != null) {
				cachedLines.Add(new CachedLine(line, document.Version));
			}
			return line;
		}

		/// <summary>
		/// 获取当前文档的 Roslyn SemanticModel。
		/// 优先从项目的 RoslynCompilationManager 获取，
		/// 如果文件不属于任何项目，则创建单文件 Compilation。
		/// </summary>
		SemanticModel GetSemanticModel()
		{
			var fileName = FileName.Create(document.FileName);

			// 尝试从解析服务获取 Roslyn 解析信息
			var parseInfo = SD.ParserService.GetCachedParseInformation(fileName) as RoslynFullParseInformation;
			if (parseInfo == null) {
				if (forceParseOnNextRefresh) {
					forceParseOnNextRefresh = false;
					parseInfo = SD.ParserService.Parse(fileName, document) as RoslynFullParseInformation;
				}
			}

			if (parseInfo == null) {
				// 尝试回退到 NRefactory 解析信息，从中获取 SyntaxTree
				var nrParseInfo = SD.ParserService.GetCachedParseInformation(fileName) as CSharpFullParseInformation;
				if (nrParseInfo != null) {
					// NRefactory 解析信息无法直接用于 Roslyn，返回 null
					return null;
				}
				return null;
			}

			// 获取项目
			IProject project = SD.ProjectService.FindProjectContainingFile(fileName);
			if (project != null) {
				try {
					var compilation = RoslynCompilationManager.GetOrCreateCompilation(project);
					if (compilation != null) {
						// 查找匹配的 SyntaxTree
						var matchingTree = compilation.SyntaxTrees
							.FirstOrDefault(t => string.Equals(t.FilePath, fileName, StringComparison.OrdinalIgnoreCase));

						if (matchingTree != null) {
							return compilation.GetSemanticModel(matchingTree);
						} else {
							// 使用当前文档的 SyntaxTree 更新 Compilation
							var updatedCompilation = compilation.AddSyntaxTrees(parseInfo.SyntaxTree);
							return updatedCompilation.GetSemanticModel(parseInfo.SyntaxTree);
						}
					}
				} catch (Exception ex) {
					LoggingService.Warn("Roslyn 语义高亮器获取 SemanticModel 失败", ex);
				}
			}

			// 单文件模式：创建最小 Compilation
			try {
				var sourceText = SourceText.From(document.Text);
				var syntaxTree = CSharpSyntaxTree.ParseText(sourceText, path: fileName);
				var compilation = RoslynCompilationManager.CreateCompilationForSingleFile(fileName, document.Text);
				return compilation.GetSemanticModel(syntaxTree);
			} catch (Exception ex) {
				LoggingService.Warn("Roslyn 语义高亮器创建单文件 SemanticModel 失败", ex);
				return null;
			}
		}

		HighlightingColor IHighlighter.DefaultTextColor {
			get {
				return null;
			}
		}

		public void BeginHighlighting()
		{
			if (inHighlightingGroup)
				throw new InvalidOperationException();
			inHighlightingGroup = true;
			if (invalidLines == null) {
				// 如果行失效不可用，强制立即解析文件
				forceParseOnNextRefresh = true;
			}
		}

		public void EndHighlighting()
		{
			inHighlightingGroup = false;
			visitor.SemanticModel = null;
		}

		public HighlightingColor GetNamedColor(string name)
		{
			return null;
		}

		#region Caching
		// 如果行被编辑且在解析信息准备好之前需要显示，
		// 行会闪烁（语义高亮临时消失）。
		// 通过存储语义高亮并在文档变更时更新（使用锚点移动）来避免此问题。

		class CachedLine
		{
			public readonly HighlightedLine HighlightedLine;
			public ITextSourceVersion OldVersion;

			/// <summary>
			/// 缓存行是否有效（自创建以来无文档变更）。
			/// 当 Update() 被调用时设为 false。
			/// </summary>
			public bool IsValid;

			public IDocumentLine DocumentLine { get { return HighlightedLine.DocumentLine; } }

			public CachedLine(HighlightedLine highlightedLine, ITextSourceVersion fileVersion)
			{
				if (highlightedLine == null)
					throw new ArgumentNullException("highlightedLine");
				if (fileVersion == null)
					throw new ArgumentNullException("fileVersion");

				this.HighlightedLine = highlightedLine;
				this.OldVersion = fileVersion;
				this.IsValid = true;
			}

			public void Update(ITextSourceVersion newVersion)
			{
				// 对所有高亮段应用文档变更
				foreach (AvalonEditDocument.TextChangeEventArgs change in OldVersion.GetChangesTo(newVersion)) {
					foreach (HighlightedSection section in HighlightedLine.Sections) {
						int endOffset = section.Offset + section.Length;
						section.Offset = change.GetNewOffset(section.Offset);
						endOffset = change.GetNewOffset(endOffset);
						section.Length = endOffset - section.Offset;
					}
				}
				// 结果段可能已失效：
				// - 零长度（段被删除）
				// - 段可能移出了文档行范围（插入了换行符 = 行被拆分）
				// 因此移除所有失效的高亮段
				int lineStart = HighlightedLine.DocumentLine.Offset;
				int lineEnd = lineStart + HighlightedLine.DocumentLine.Length;
				for (int i = 0; i < HighlightedLine.Sections.Count; i++) {
					HighlightedSection section = HighlightedLine.Sections[i];
					if (section.Offset < lineStart || section.Offset + section.Length > lineEnd || section.Length <= 0)
						HighlightedLine.Sections.RemoveAt(i--);
				}

				this.OldVersion = newVersion;
				this.IsValid = false;
			}
		}

		void InvalidateAll()
		{
			cachedLines.Clear();
			invalidLines.Clear();
			forceParseOnNextRefresh = true;
			OnHighlightingStateChanged(1, document.LineCount);
		}
		#endregion

		#region Event Handlers
		void ParserService_LoadSolutionProjectsThreadEnded(object sender, EventArgs e)
		{
			InvalidateAll();
		}

		void ParserService_ParseInformationUpdated(object sender, ParseInformationEventArgs e)
		{
			if (FileUtility.IsEqualFileName(e.FileName, document.FileName) && invalidLines.Count > 0) {
				cachedLines.Clear();
				foreach (IDocumentLine line in invalidLines) {
					if (!line.IsDeleted) {
						OnHighlightingStateChanged(line.LineNumber, line.LineNumber);
					}
				}
				invalidLines.Clear();
			}
		}
		#endregion
	}
}
