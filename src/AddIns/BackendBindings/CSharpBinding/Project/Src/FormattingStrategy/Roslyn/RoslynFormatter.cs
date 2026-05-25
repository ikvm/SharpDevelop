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
using System.Threading;
using System.Threading.Tasks;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.Text;
using Microsoft.CodeAnalysis.Options;

namespace CSharpBinding.FormattingStrategy.Roslyn
{
	/// <summary>
	/// 基于 Roslyn 的 C# 代码格式化器
	/// 使用 Microsoft.CodeAnalysis.Formatting.Formatter 格式化代码
	/// </summary>
	public static class RoslynFormatter
	{
		/// <summary>
		/// 格式化整个文档
		/// </summary>
		/// <param name="source">源代码文本</param>
		/// <param name="fileName">文件名，用于 SyntaxTree 的路径标识</param>
		/// <param name="cancellationToken">取消令牌</param>
		/// <returns>格式化后的源代码文本</returns>
		public static async Task<string> FormatDocumentAsync(string source, string fileName, CancellationToken cancellationToken)
		{
			if (string.IsNullOrEmpty(source))
				return source;

			// 创建 AdhocWorkspace 用于格式化
			using (var workspace = new AdhocWorkspace()) {
				// 解析源代码为 SyntaxTree
				var syntaxTree = CSharpSyntaxTree.ParseText(source, path: fileName);

				// 检查是否有语法错误，如果有则不格式化
				var diagnostics = syntaxTree.GetDiagnostics(cancellationToken);
				bool hasError = false;
				foreach (var diag in diagnostics) {
					if (diag.Severity == DiagnosticSeverity.Error) {
						hasError = true;
						break;
					}
				}
				if (hasError)
					return source;

				// 创建项目和文档
				var projectId = ProjectId.CreateNewId();
				var documentId = DocumentId.CreateNewId(projectId);

				var solution = workspace.CurrentSolution
					.AddProject(projectId, "FormatProject", "FormatProject", LanguageNames.CSharp)
					.AddDocument(documentId, fileName, syntaxTree.GetText(cancellationToken));

				var document = solution.GetDocument(documentId);

				// 获取格式化选项（当前使用默认选项，后续可从 SD 设置映射）
				var formattingOptions = ConvertFormattingOptions();

				// 执行格式化
				var formattedDocument = await Formatter.FormatAsync(document, formattingOptions, cancellationToken);
				var formattedRoot = await formattedDocument.GetSyntaxRootAsync(cancellationToken);
				var formattedText = formattedRoot.GetText();

				return formattedText.ToString();
			}
		}

		/// <summary>
		/// 格式化指定范围
		/// </summary>
		/// <param name="source">源代码文本</param>
		/// <param name="startOffset">起始偏移量</param>
		/// <param name="endOffset">结束偏移量</param>
		/// <param name="fileName">文件名</param>
		/// <param name="cancellationToken">取消令牌</param>
		/// <returns>格式化后的源代码文本</returns>
		public static async Task<string> FormatRangeAsync(string source, int startOffset, int endOffset, string fileName, CancellationToken cancellationToken)
		{
			if (string.IsNullOrEmpty(source))
				return source;

			if (startOffset < 0) startOffset = 0;
			if (endOffset > source.Length) endOffset = source.Length;
			if (startOffset >= endOffset)
				return source;

			// 创建 AdhocWorkspace 用于格式化
			using (var workspace = new AdhocWorkspace()) {
				// 解析源代码为 SyntaxTree
				var syntaxTree = CSharpSyntaxTree.ParseText(source, path: fileName);

				// 检查是否有语法错误，如果有则不格式化
				var diagnostics = syntaxTree.GetDiagnostics(cancellationToken);
				bool hasError = false;
				foreach (var diag in diagnostics) {
					if (diag.Severity == DiagnosticSeverity.Error) {
						hasError = true;
						break;
					}
				}
				if (hasError)
					return source;

				// 创建项目和文档
				var projectId = ProjectId.CreateNewId();
				var documentId = DocumentId.CreateNewId(projectId);

				var solution = workspace.CurrentSolution
					.AddProject(projectId, "FormatProject", "FormatProject", LanguageNames.CSharp)
					.AddDocument(documentId, fileName, syntaxTree.GetText(cancellationToken));

				var document = solution.GetDocument(documentId);

				// 创建要格式化的文本范围
				var textSpan = new TextSpan(startOffset, endOffset - startOffset);

				// 获取格式化选项
				var formattingOptions = ConvertFormattingOptions();

				// 执行范围格式化
				var formattedDocument = await Formatter.FormatAsync(document, textSpan, formattingOptions, cancellationToken);
				var formattedRoot = await formattedDocument.GetSyntaxRootAsync(cancellationToken);
				var formattedText = formattedRoot.GetText();

				return formattedText.ToString();
			}
		}

		/// <summary>
		/// 将 SD 格式化选项转换为 Roslyn FormattingOptions。
		/// 当前实现使用 Roslyn 默认格式化选项。
		/// 后续可从 SD 的 CSharpFormattingOptionsContainer 映射具体选项。
		/// </summary>
		static OptionSet ConvertFormattingOptions()
		{
			// 使用 Roslyn 的默认格式化选项
			// TODO: 后续实现从 SD 格式化设置到 Roslyn 选项的完整映射
			// - 缩进：空格/制表符、缩进大小、缩进大括号等
			// - 间距：关键字后空格、括号前后空格等
			// - 换行：大括号换行、子句换行等
			// - 换行：参数换行、参数换行等
			return null;
		}
	}
}
