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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;

using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Folding;
using ICSharpCode.Core;
using ICSharpCode.NRefactory.Semantics;
using ICSharpCode.NRefactory.TypeSystem;
using ICSharpCode.NRefactory.TypeSystem.Implementation;
using ICSharpCode.NRefactory.Documentation;
using ICSharpCode.SharpDevelop;
using ICSharpCode.SharpDevelop.Editor.Search;
using ICSharpCode.SharpDevelop.Parser;
using ICSharpCode.SharpDevelop.Project;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

using NRSpecialType = ICSharpCode.NRefactory.TypeSystem.SpecialType;
using NRAccessibility = ICSharpCode.NRefactory.TypeSystem.Accessibility;
using NRSymbolKind = ICSharpCode.NRefactory.TypeSystem.SymbolKind;
using NRSymbolReference = ICSharpCode.NRefactory.TypeSystem.ISymbolReference;
using NRTypeKind = ICSharpCode.NRefactory.TypeSystem.TypeKind;
using NRUnknownType = ICSharpCode.NRefactory.TypeSystem.Implementation.UnknownType;
using RoslynISymbol = Microsoft.CodeAnalysis.ISymbol;
using RoslynSymbolKind = Microsoft.CodeAnalysis.SymbolKind;
using RoslynTypeKind = Microsoft.CodeAnalysis.TypeKind;
using RoslynSpecialType = Microsoft.CodeAnalysis.SpecialType;

using CSharpBinding.Parser.Roslyn.Adapters;

namespace CSharpBinding.Parser.Roslyn
{
	/// <summary>
	/// 基于 Roslyn 的 IParser 实现。
	/// 使用 Microsoft.CodeAnalysis.CSharp 作为解析后端，
	/// 将 Roslyn 语法树适配为 SharpDevelop 的类型系统接口。
	/// </summary>
	public class RoslynParserAdapter : IParser
	{
		public IReadOnlyList<string> TaskListTokens { get; set; }

		public bool CanParse(string fileName)
		{
			return Path.GetExtension(fileName).Equals(".CS", StringComparison.OrdinalIgnoreCase);
		}

		public ITextSource GetFileContent(FileName fileName)
		{
			return SD.FileService.GetFileContent(fileName);
		}

		public ParseInformation Parse(FileName fileName, ITextSource fileContent, bool fullParseInformationRequested,
		                              IProject parentProject, CancellationToken cancellationToken)
		{
			// 使用 Roslyn 解析源代码
			var sourceText = SourceText.From(fileContent.Text);
			var syntaxTree = CSharpSyntaxTree.ParseText(sourceText, path: fileName, cancellationToken: cancellationToken);

			// 从 Roslyn 语法树创建 IUnresolvedFile 适配器
			var unresolvedFile = new RoslynUnresolvedFileAdapter(syntaxTree, fileName);

			ParseInformation parseInfo;
			if (fullParseInformationRequested)
				parseInfo = new RoslynFullParseInformation(unresolvedFile, fileContent.Version, syntaxTree);
			else
				parseInfo = new ParseInformation(unresolvedFile, fileContent.Version, fullParseInformationRequested);

			// 添加注释标签
			IDocument document = fileContent as IDocument;
			AddCommentTags(syntaxTree, parseInfo.TagComments, fileContent, parseInfo.FileName, ref document);

			if (fullParseInformationRequested) {
				if (document == null)
					document = new ReadOnlyDocument(fileContent, parseInfo.FileName);
				((RoslynFullParseInformation)parseInfo).newFoldings = CreateNewFoldings(syntaxTree, document);
			}

			return parseInfo;
		}

		#region AddCommentTags

		/// <summary>
		/// 从 Roslyn trivia 中提取包含任务列表标记的注释。
		/// </summary>
		void AddCommentTags(SyntaxTree syntaxTree, IList<TagComment> tagComments, ITextSource fileContent, FileName fileName, ref IDocument document)
		{
			if (TaskListTokens == null || TaskListTokens.Count == 0)
				return;

			var root = syntaxTree.GetRoot();

			foreach (var trivia in root.DescendantTrivia()) {
				// 只处理注释类型的 trivia
				if (!trivia.IsKind(SyntaxKind.SingleLineCommentTrivia) &&
				    !trivia.IsKind(SyntaxKind.MultiLineCommentTrivia) &&
				    !trivia.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia) &&
				    !trivia.IsKind(SyntaxKind.MultiLineDocumentationCommentTrivia))
					continue;

				string triviaText = trivia.ToString();
				string match;
				if (!triviaText.ContainsAny(TaskListTokens, 0, out match))
					continue;

				if (document == null)
					document = new ReadOnlyDocument(fileContent, fileName);

				var lineSpan = trivia.GetLocation().GetLineSpan();
				var startLinePos = lineSpan.StartLinePosition;
				var endLinePos = lineSpan.EndLinePosition;

				// 计算注释符号长度
				int commentSignLength;
				if (trivia.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia) ||
				    trivia.IsKind(SyntaxKind.MultiLineDocumentationCommentTrivia)) {
					commentSignLength = 3; // /// 或 /**
				} else if (trivia.IsKind(SyntaxKind.MultiLineCommentTrivia)) {
					commentSignLength = 2; // /*
				} else {
					commentSignLength = 2; // //
				}

				int commentEndSignLength;
				if (trivia.IsKind(SyntaxKind.MultiLineCommentTrivia) ||
				    trivia.IsKind(SyntaxKind.MultiLineDocumentationCommentTrivia)) {
					commentEndSignLength = 2; // */
				} else {
					commentEndSignLength = 0;
				}

				int commentStartOffset = document.GetOffset(new TextLocation(startLinePos.Line + 1, startLinePos.Character + 1)) + commentSignLength;
				int commentEndOffset = document.GetOffset(new TextLocation(endLinePos.Line + 1, endLinePos.Character + 1)) - commentEndSignLength;

				int searchOffset = 0;
				string commentContent = document.GetText(commentStartOffset, Math.Max(0, commentEndOffset - commentStartOffset));

				do {
					int start = commentStartOffset + searchOffset;
					if (start >= document.TextLength)
						break;
					int absoluteOffset = document.IndexOf(match, start, document.TextLength - start, StringComparison.Ordinal);
					if (absoluteOffset < 0 || absoluteOffset > commentEndOffset)
						break;

					var startLocation = document.GetLocation(absoluteOffset);
					int endOffset = Math.Min(document.GetLineByNumber(startLocation.Line).EndOffset, commentEndOffset);
					string content = document.GetText(absoluteOffset, endOffset - absoluteOffset);
					if (content.Length < match.Length)
						break;

					tagComments.Add(new TagComment(
						content.Substring(0, match.Length),
						new DomRegion(fileName, startLocation.Line, startLocation.Column),
						content.Substring(match.Length)));

					searchOffset = endOffset - commentStartOffset;
				} while (commentContent.ContainsAny(TaskListTokens, searchOffset, out match));
			}
		}

		#endregion

		#region CreateNewFoldings

		/// <summary>
		/// 从 Roslyn 语法树创建折叠区域。
		/// 遍历类型声明和方法声明，生成对应的折叠标记。
		/// </summary>
		List<NewFolding> CreateNewFoldings(SyntaxTree syntaxTree, IDocument document)
		{
			var foldings = new List<NewFolding>();
			var root = syntaxTree.GetRoot();

			// 收集 using 折叠
			AddUsingFoldings(root, document, foldings);

			// 递归遍历语法节点
			CollectFoldings(root, document, foldings);

			// 收集 region 折叠
			AddRegionFoldings(root, document, foldings);

			// 收集多行注释折叠
			AddCommentFoldings(root, document, foldings);

			foldings.Sort((a, b) => a.StartOffset.CompareTo(b.StartOffset));
			return foldings;
		}

		/// <summary>
		/// 收集 using 指令的折叠区域。
		/// </summary>
		void AddUsingFoldings(SyntaxNode root, IDocument document, List<NewFolding> foldings)
		{
			var usings = root.DescendantNodes().OfType<UsingDirectiveSyntax>().ToList();
			if (usings.Count < 2)
				return;

			var firstUsing = usings.First();
			var lastUsing = usings.Last();

			// 检查 using 之间是否连续
			bool isContiguous = true;
			for (int i = 1; i < usings.Count; i++) {
				var prevEnd = usings[i - 1].GetLocation().GetLineSpan().EndLinePosition.Line;
				var curStart = usings[i].GetLocation().GetLineSpan().StartLinePosition.Line;
				if (curStart > prevEnd + 1) {
					isContiguous = false;
					break;
				}
			}

			if (isContiguous) {
				var startOffset = GetOffsetFromLinePosition(document, firstUsing.GetLocation().GetLineSpan().StartLinePosition);
				var endOffset = GetOffsetFromLinePosition(document, lastUsing.GetLocation().GetLineSpan().EndLinePosition);
				if (endOffset > startOffset) {
					var folding = new NewFolding(startOffset, endOffset);
					folding.Name = "using...";
					folding.DefaultClosed = true;
					foldings.Add(folding);
				}
			}
		}

		/// <summary>
		/// 递归收集类型声明和成员声明的折叠区域。
		/// </summary>
		void CollectFoldings(SyntaxNode node, IDocument document, List<NewFolding> foldings)
		{
			// 命名空间折叠
			var nsDecl = node as NamespaceDeclarationSyntax;
			if (nsDecl != null) {
				AddBraceFolding(nsDecl.OpenBraceToken, nsDecl.CloseBraceToken, document, foldings);
			}

			// 类型声明折叠
			var typeDecl = node as TypeDeclarationSyntax;
			if (typeDecl != null) {
				AddBraceFolding(typeDecl.OpenBraceToken, typeDecl.CloseBraceToken, document, foldings);
			}

			// 枚举声明折叠
			var enumDecl = node as EnumDeclarationSyntax;
			if (enumDecl != null) {
				AddBraceFolding(enumDecl.OpenBraceToken, enumDecl.CloseBraceToken, document, foldings);
			}

			// 方法声明折叠
			var methodDecl = node as MethodDeclarationSyntax;
			if (methodDecl != null && methodDecl.Body != null) {
				AddBlockFolding(methodDecl.Body, document, foldings, isDefinition: true);
			}

			// 构造函数折叠
			var ctorDecl = node as ConstructorDeclarationSyntax;
			if (ctorDecl != null && ctorDecl.Body != null) {
				AddBlockFolding(ctorDecl.Body, document, foldings, isDefinition: true);
			}

			// 析构函数折叠
			var dtorDecl = node as DestructorDeclarationSyntax;
			if (dtorDecl != null && dtorDecl.Body != null) {
				AddBlockFolding(dtorDecl.Body, document, foldings, isDefinition: true);
			}

			// 运算符折叠
			var opDecl = node as OperatorDeclarationSyntax;
			if (opDecl != null && opDecl.Body != null) {
				AddBlockFolding(opDecl.Body, document, foldings, isDefinition: true);
			}

			// 属性折叠
			var propDecl = node as PropertyDeclarationSyntax;
			if (propDecl != null && propDecl.AccessorList != null) {
				AddBraceFolding(propDecl.AccessorList.OpenBraceToken, propDecl.AccessorList.CloseBraceToken, document, foldings, isDefinition: true);
			}

			// 索引器折叠
			var indexerDecl = node as IndexerDeclarationSyntax;
			if (indexerDecl != null && indexerDecl.AccessorList != null) {
				AddBraceFolding(indexerDecl.AccessorList.OpenBraceToken, indexerDecl.AccessorList.CloseBraceToken, document, foldings, isDefinition: true);
			}

			// 事件折叠
			var eventDecl = node as EventDeclarationSyntax;
			if (eventDecl != null && eventDecl.AccessorList != null) {
				AddBraceFolding(eventDecl.AccessorList.OpenBraceToken, eventDecl.AccessorList.CloseBraceToken, document, foldings, isDefinition: true);
			}

			// switch 语句折叠
			var switchDecl = node as SwitchStatementSyntax;
			if (switchDecl != null) {
				AddBraceFolding(switchDecl.OpenBraceToken, switchDecl.CloseBraceToken, document, foldings);
			}

			// 块语句折叠（非成员声明）
			var block = node as BlockSyntax;
			if (block != null && !(block.Parent is MemberDeclarationSyntax) && !(block.Parent is AccessorDeclarationSyntax)) {
				var startLine = block.GetLocation().GetLineSpan().StartLinePosition.Line;
				var endLine = block.GetLocation().GetLineSpan().EndLinePosition.Line;
				if (endLine - startLine > 2) {
					var startOffset = GetOffsetFromLinePosition(document, block.GetLocation().GetLineSpan().StartLinePosition);
					var endOffset = GetOffsetFromLinePosition(document, block.GetLocation().GetLineSpan().EndLinePosition);
					if (endOffset > startOffset) {
						foldings.Add(new NewFolding(startOffset, endOffset));
					}
				}
			}

			foreach (var child in node.ChildNodes()) {
				CollectFoldings(child, document, foldings);
			}
		}

		/// <summary>
		/// 添加花括号之间的折叠区域。
		/// </summary>
		void AddBraceFolding(SyntaxToken openBrace, SyntaxToken closeBrace, IDocument document, List<NewFolding> foldings, bool isDefinition = false)
		{
			if (openBrace.IsKind(SyntaxKind.None) || closeBrace.IsKind(SyntaxKind.None))
				return;

			var openBraceLine = openBrace.GetLocation().GetLineSpan().StartLinePosition.Line;
			var closeBraceLine = closeBrace.GetLocation().GetLineSpan().EndLinePosition.Line;

			if (closeBraceLine <= openBraceLine)
				return;

			int startOffset = GetOffsetFromLinePosition(document, openBrace.GetLocation().GetLineSpan().StartLinePosition);
			int endOffset = GetOffsetFromLinePosition(document, closeBrace.GetLocation().GetLineSpan().EndLinePosition);

			if (endOffset > startOffset) {
				var folding = new NewFolding(startOffset, endOffset);
				folding.IsDefinition = isDefinition;
				foldings.Add(folding);
			}
		}

		/// <summary>
		/// 添加方法体的折叠区域。
		/// </summary>
		void AddBlockFolding(BlockSyntax block, IDocument document, List<NewFolding> foldings, bool isDefinition = false)
		{
			if (block == null)
				return;

			var openBraceLine = block.OpenBraceToken.GetLocation().GetLineSpan().StartLinePosition.Line;
			var closeBraceLine = block.CloseBraceToken.GetLocation().GetLineSpan().EndLinePosition.Line;

			if (closeBraceLine <= openBraceLine)
				return;

			int startOffset = GetOffsetFromLinePosition(document, block.OpenBraceToken.GetLocation().GetLineSpan().StartLinePosition);
			int endOffset = GetOffsetFromLinePosition(document, block.CloseBraceToken.GetLocation().GetLineSpan().EndLinePosition);

			if (endOffset > startOffset) {
				var folding = new NewFolding(startOffset, endOffset);
				folding.IsDefinition = isDefinition;
				foldings.Add(folding);
			}
		}

		/// <summary>
		/// 收集 #region 折叠区域。
		/// </summary>
		void AddRegionFoldings(SyntaxNode root, IDocument document, List<NewFolding> foldings)
		{
			var regionDirectives = root.DescendantTrivia()
				.Where(t => t.IsKind(SyntaxKind.RegionDirectiveTrivia))
				.ToList();

			var endRegionDirectives = root.DescendantTrivia()
				.Where(t => t.IsKind(SyntaxKind.EndRegionDirectiveTrivia))
				.ToList();

			// 简单配对：按顺序匹配 region 和 endregion
			int endIndex = 0;
			foreach (var region in regionDirectives) {
				// 查找对应的 endregion
				while (endIndex < endRegionDirectives.Count) {
					var endRegion = endRegionDirectives[endIndex];
					var regionLine = region.GetLocation().GetLineSpan().StartLinePosition.Line;
					var endRegionLine = endRegion.GetLocation().GetLineSpan().StartLinePosition.Line;

					if (endRegionLine >= regionLine) {
						int startOffset = GetOffsetFromLinePosition(document, region.GetLocation().GetLineSpan().StartLinePosition);
						int endOffset = GetOffsetFromLinePosition(document, endRegion.GetLocation().GetLineSpan().EndLinePosition);

						if (endOffset > startOffset) {
							var folding = new NewFolding(startOffset, endOffset);
							folding.DefaultClosed = true;
							// 提取 region 名称
							var regionText = region.ToString();
							var nameStart = regionText.IndexOf(' ');
							folding.Name = nameStart >= 0 ? regionText.Substring(nameStart + 1).Trim() : "...";
							foldings.Add(folding);
						}
						endIndex++;
						break;
					}
					endIndex++;
				}
			}
		}

		/// <summary>
		/// 收集多行注释的折叠区域。
		/// </summary>
		void AddCommentFoldings(SyntaxNode root, IDocument document, List<NewFolding> foldings)
		{
			var comments = root.DescendantTrivia()
				.Where(t => t.IsKind(SyntaxKind.SingleLineCommentTrivia) ||
				            t.IsKind(SyntaxKind.MultiLineCommentTrivia) ||
				            t.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia) ||
				            t.IsKind(SyntaxKind.MultiLineDocumentationCommentTrivia))
				.ToList();

			// 对连续的单行注释进行分组折叠
			List<SyntaxTrivia> currentGroup = new List<SyntaxTrivia>();
			foreach (var comment in comments) {
				if (currentGroup.Count == 0) {
					currentGroup.Add(comment);
					continue;
				}

				var prevComment = currentGroup[currentGroup.Count - 1];
				var prevEndLine = prevComment.GetLocation().GetLineSpan().EndLinePosition.Line;
				var curStartLine = comment.GetLocation().GetLineSpan().StartLinePosition.Line;

				// 检查是否为连续的单行注释
				if (comment.IsKind(SyntaxKind.SingleLineCommentTrivia) &&
				    prevComment.IsKind(SyntaxKind.SingleLineCommentTrivia) &&
				    curStartLine == prevEndLine + 1) {
					currentGroup.Add(comment);
				} else {
					// 处理当前分组
					ProcessCommentGroup(currentGroup, document, foldings);
					currentGroup.Clear();
					currentGroup.Add(comment);
				}
			}
			ProcessCommentGroup(currentGroup, document, foldings);
		}

		/// <summary>
		/// 处理一组连续注释的折叠。
		/// </summary>
		void ProcessCommentGroup(List<SyntaxTrivia> group, IDocument document, List<NewFolding> foldings)
		{
			if (group.Count == 0)
				return;

			var firstComment = group[0];
			var lastComment = group[group.Count - 1];

			var startLine = firstComment.GetLocation().GetLineSpan().StartLinePosition.Line;
			var endLine = lastComment.GetLocation().GetLineSpan().EndLinePosition.Line;

			// 多行注释或超过 3 行的连续单行注释才折叠
			bool isMultiLine = firstComment.IsKind(SyntaxKind.MultiLineCommentTrivia) ||
			                   firstComment.IsKind(SyntaxKind.MultiLineDocumentationCommentTrivia);

			if (isMultiLine || endLine - startLine > 2) {
				int startOffset = GetOffsetFromLinePosition(document, firstComment.GetLocation().GetLineSpan().StartLinePosition);
				int endOffset = GetOffsetFromLinePosition(document, lastComment.GetLocation().GetLineSpan().EndLinePosition);

				if (endOffset > startOffset) {
					var folding = new NewFolding(startOffset, endOffset);
					if (firstComment.IsKind(SyntaxKind.SingleLineCommentTrivia))
						folding.Name = "// ...";
					else if (firstComment.IsKind(SyntaxKind.MultiLineCommentTrivia))
						folding.Name = "/* ... */";
					else if (firstComment.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia))
						folding.Name = "/// ...";
					else if (firstComment.IsKind(SyntaxKind.MultiLineDocumentationCommentTrivia))
						folding.Name = "/** ... */";
					foldings.Add(folding);
				}
			}
		}

		/// <summary>
		/// 将 Roslyn LinePosition 转换为文档偏移量。
		/// </summary>
		static int GetOffsetFromLinePosition(IDocument document, LinePosition linePosition)
		{
			// Roslyn 的行号从 0 开始，AvalonEdit 的行号从 1 开始
			int line = linePosition.Line + 1;
			int column = linePosition.Character + 1;
			if (line < 1) line = 1;
			if (line > document.LineCount) return document.TextLength;
			return document.GetOffset(new TextLocation(line, column));
		}

		#endregion

		#region Resolve / ResolveContext

		/// <summary>
		/// 在指定位置解析符号。
		/// 使用 RoslynCompilationManager 获取 SemanticModel，然后在指定位置解析符号。
		/// </summary>
		public ResolveResult Resolve(ParseInformation parseInfo, TextLocation location, ICompilation compilation, CancellationToken cancellationToken)
		{
			var roslynParseInfo = parseInfo as RoslynFullParseInformation;
			if (roslynParseInfo == null)
				return ErrorResolveResult.UnknownError;

			// 尝试获取 SemanticModel
			var semanticModel = GetSemanticModel(roslynParseInfo.SyntaxTree, compilation, cancellationToken);
			if (semanticModel == null)
				return new ErrorResolveResult(NRSpecialType.UnknownType);

			try {
				// 将 NRefactory 位置（1-based）转换为 Roslyn 位置（0-based）
				var linePosition = new LinePosition(location.Line - 1, location.Column - 1);
				var position = roslynParseInfo.SyntaxTree.GetText(cancellationToken).Lines.GetPosition(linePosition);

				// 在指定位置查找符号
				// 首先尝试获取 trivia 位置的 token，然后获取其父节点的符号
				var root = roslynParseInfo.SyntaxTree.GetRoot(cancellationToken);
				var node = root.FindTrivia(position).Token.Parent;
				if (node == null) {
					// 如果 trivia 位置没找到，尝试直接查找节点
					node = root.FindNode(new Microsoft.CodeAnalysis.Text.TextSpan(position, 0));
				}

				if (node == null)
					return new ErrorResolveResult(NRSpecialType.UnknownType);

				// 获取符号信息
				var symbolInfo = semanticModel.GetSymbolInfo(node, cancellationToken);
				var symbol = symbolInfo.Symbol ?? symbolInfo.CandidateSymbols.FirstOrDefault();

				if (symbol == null) {
					// 尝试获取声明符号（用于类型声明等）
					var declaredSymbol = semanticModel.GetDeclaredSymbol(node, cancellationToken);
					if (declaredSymbol != null)
						symbol = declaredSymbol;
				}

				if (symbol == null)
					return new ErrorResolveResult(NRSpecialType.UnknownType);

				// 将 Roslyn ISymbol 转换为 NRefactory ResolveResult
				return ConvertToResolveResult(symbol, semanticModel, position, cancellationToken);
			} catch (Exception ex) {
				LoggingService.Warn("Roslyn 解析符号时出错", ex);
				return new ErrorResolveResult(NRSpecialType.UnknownType);
			}
		}

		/// <summary>
		/// 在指定位置解析代码上下文。
		/// 使用 Roslyn SemanticModel 获取当前位置的类型和成员上下文。
		/// </summary>
		public ICodeContext ResolveContext(ParseInformation parseInfo, TextLocation location, ICompilation compilation, CancellationToken cancellationToken)
		{
			// 当前尚未实现完整的 Roslyn 代码上下文解析
			// 返回 null 让调用者使用默认上下文
			return null;
		}

		/// <summary>
		/// 获取指定 SyntaxTree 的 SemanticModel。
		/// 优先从项目创建 Compilation，否则使用传入的 ICompilation。
		/// </summary>
		SemanticModel GetSemanticModel(SyntaxTree syntaxTree, ICompilation compilation, CancellationToken cancellationToken)
		{
			// 如果传入的 ICompilation 已经是 RoslynCompilationAdapter，直接使用
			var roslynCompAdapter = compilation as RoslynCompilationAdapter;
			if (roslynCompAdapter != null) {
				try {
					var roslynCompilation = roslynCompAdapter.RoslynCompilation;
					var matchingTree = roslynCompilation.SyntaxTrees
						.FirstOrDefault(t => string.Equals(t.FilePath, syntaxTree.FilePath, StringComparison.OrdinalIgnoreCase));

					if (matchingTree != null) {
						return roslynCompilation.GetSemanticModel(matchingTree);
					} else {
						var updatedCompilation = roslynCompilation.AddSyntaxTrees(syntaxTree);
						return updatedCompilation.GetSemanticModel(syntaxTree);
					}
				} catch (Exception ex) {
					LoggingService.Warn("从 RoslynCompilationAdapter 获取 SemanticModel 时出错", ex);
				}
			}

			// 尝试从项目创建 Roslyn Compilation
			var project = compilation?.GetProject();
			if (project != null) {
				try {
					var roslynCompilation = RoslynCompilationManager.GetOrCreateCompilation(project);
					if (roslynCompilation != null) {
						// 查找匹配的 SyntaxTree（可能文件路径不同，需要按路径匹配）
						var matchingTree = roslynCompilation.SyntaxTrees
							.FirstOrDefault(t => string.Equals(t.FilePath, syntaxTree.FilePath, StringComparison.OrdinalIgnoreCase));

						if (matchingTree != null) {
							return roslynCompilation.GetSemanticModel(matchingTree);
						} else {
							// 如果 Compilation 中没有该文件，添加当前 SyntaxTree 并获取 SemanticModel
							var updatedCompilation = roslynCompilation.AddSyntaxTrees(syntaxTree);
							return updatedCompilation.GetSemanticModel(syntaxTree);
						}
					}
				} catch (Exception ex) {
					LoggingService.Warn("获取 Roslyn SemanticModel 时出错", ex);
				}
			}

			return null;
		}

		/// <summary>
		/// 获取与 SemanticModel 关联的 RoslynCompilationAdapter。
		/// 用于将 Roslyn ISymbol 转换为 NRefactory 类型系统对象。
		/// </summary>
		RoslynCompilationAdapter GetCompilationAdapter(SemanticModel semanticModel)
		{
			if (semanticModel == null)
				return null;

			// 从缓存中获取或创建 RoslynCompilationAdapter
			var roslynCompilation = semanticModel.Compilation as CSharpCompilation;
			if (roslynCompilation == null)
				return null;

			return compilationAdapterCache.GetOrAdd(
				roslynCompilation,
				comp => new RoslynCompilationAdapter(comp)
			);
		}

		/// <summary>
		/// RoslynCompilationAdapter 缓存，避免为同一个 CSharpCompilation 创建多个适配器
		/// </summary>
		static readonly ConcurrentDictionary<CSharpCompilation, RoslynCompilationAdapter> compilationAdapterCache =
			new ConcurrentDictionary<CSharpCompilation, RoslynCompilationAdapter>();

		#endregion

		#region FindLocalReferences

		/// <summary>
		/// 查找局部变量的引用。
		/// 当前尚未实现，需要 Roslyn SemanticModel 支持。
		/// </summary>
		public void FindLocalReferences(ParseInformation parseInfo, ITextSource fileContent, IVariable variable, ICompilation compilation, Action<SearchResultMatch> callback, CancellationToken cancellationToken)
		{
			throw new NotSupportedException("FindLocalReferences 尚未在 Roslyn 适配器中实现");
		}

		#endregion

		#region CreateCompilationForSingleFile

		static readonly Lazy<IAssemblyReference[]> defaultReferences = new Lazy<IAssemblyReference[]>(
			delegate {
				Assembly[] assemblies = {
					typeof(object).Assembly,
					typeof(Uri).Assembly,
					typeof(Enumerable).Assembly
				};
				return assemblies.Select(asm => SD.AssemblyParserService.GetAssembly(FileName.Create(asm.Location))).ToArray();
			});

		/// <summary>
		/// 为不属于任何项目的单个文件创建编译上下文。
		/// 使用 RoslynCompilationManager 创建 Roslyn Compilation，
		/// 并包装为 RoslynCompilationAdapter 返回。
		/// </summary>
		public ICompilation CreateCompilationForSingleFile(FileName fileName, IUnresolvedFile unresolvedFile)
		{
			// 尝试使用 Roslyn 创建编译上下文
			try {
				var source = unresolvedFile != null && File.Exists(unresolvedFile.FileName)
					? File.ReadAllText(unresolvedFile.FileName)
					: string.Empty;
				var roslynCompilation = RoslynCompilationManager.CreateCompilationForSingleFile(
					fileName,
					source
				);
				if (roslynCompilation != null) {
					return new RoslynCompilationAdapter(roslynCompilation);
				}
			} catch (Exception ex) {
				LoggingService.Warn("使用 Roslyn 创建单文件编译上下文失败，回退到 NRefactory", ex);
			}

			// 回退到 NRefactory CSharpProjectContent
			return new ICSharpCode.NRefactory.CSharp.CSharpProjectContent()
				.AddAssemblyReferences(defaultReferences.Value)
				.AddOrUpdateFiles(unresolvedFile)
				.CreateCompilation();
		}

		#endregion

		#region ResolveSnippet

		/// <summary>
		/// 解析代码片段。
		/// 使用 Roslyn SemanticModel 进行语义解析。
		/// </summary>
		public ResolveResult ResolveSnippet(ParseInformation parseInfo, TextLocation location, string codeSnippet, ICompilation compilation, CancellationToken cancellationToken)
		{
			// 使用 Roslyn 解析代码片段以检查语法错误
			var snippetTree = CSharpSyntaxTree.ParseText(codeSnippet);
			var snippetDiagnostics = snippetTree.GetDiagnostics();
			if (snippetDiagnostics.Any(d => d.Severity == DiagnosticSeverity.Error)) {
				var errorMessages = string.Join(Environment.NewLine, snippetDiagnostics.Select(d => d.GetMessage()));
				return new ErrorResolveResult(NRSpecialType.UnknownType, errorMessages, ICSharpCode.NRefactory.TextLocation.Empty);
			}

			// 尝试使用 Roslyn SemanticModel 进行语义解析
			var roslynParseInfo = parseInfo as RoslynFullParseInformation;
			if (roslynParseInfo != null) {
				var semanticModel = GetSemanticModel(roslynParseInfo.SyntaxTree, compilation, cancellationToken);
				if (semanticModel != null) {
					try {
						// 在代码片段中查找指定位置的符号
						var linePosition = new LinePosition(location.Line - 1, location.Column - 1);
						var position = snippetTree.GetText(cancellationToken).Lines.GetPosition(linePosition);

						var root = snippetTree.GetRoot(cancellationToken);
						var node = root.FindNode(new Microsoft.CodeAnalysis.Text.TextSpan(position, 0));

						if (node != null) {
							// 为代码片段创建临时 Compilation 以获取 SemanticModel
							var snippetCompilation = semanticModel.Compilation.AddSyntaxTrees(snippetTree);
							var snippetSemanticModel = snippetCompilation.GetSemanticModel(snippetTree);

							var symbolInfo = snippetSemanticModel.GetSymbolInfo(node, cancellationToken);
							var symbol = symbolInfo.Symbol ?? symbolInfo.CandidateSymbols.FirstOrDefault();

							if (symbol != null) {
								return ConvertToResolveResult(symbol, snippetSemanticModel, position, cancellationToken);
							}
						}
					} catch (Exception ex) {
						LoggingService.Warn("Roslyn 解析代码片段时出错", ex);
					}
				}
			}

			return new ErrorResolveResult(NRSpecialType.UnknownType);
		}

		#endregion

		#region ConvertToResolveResult

		/// <summary>
		/// 将 Roslyn ISymbol 转换为 NRefactory ResolveResult。
		/// 使用 Task 2 的适配器层（RoslynTypeAdapter、RoslynMemberAdapter 等）
		/// 将 Roslyn 符号转换为 NRefactory 类型系统对象。
		/// </summary>
		ResolveResult ConvertToResolveResult(RoslynISymbol symbol, SemanticModel semanticModel, int position, CancellationToken cancellationToken)
		{
			if (symbol == null)
				return new ErrorResolveResult(NRSpecialType.UnknownType);

			var compilationAdapter = GetCompilationAdapter(semanticModel);
			if (compilationAdapter == null)
				return new ErrorResolveResult(NRSpecialType.UnknownType);

			switch (symbol.Kind) {
				case RoslynSymbolKind.NamedType: {
					var typeSymbol = (INamedTypeSymbol)symbol;
					var nrType = AdapterCache.GetOrCreateTypeAdapter(typeSymbol, compilationAdapter);
					return new TypeResolveResult(nrType);
				}

				case RoslynSymbolKind.Method:
				case RoslynSymbolKind.Property:
				case RoslynSymbolKind.Field:
				case RoslynSymbolKind.Event: {
					var nrMember = new RoslynMemberAdapter(symbol, compilationAdapter);
					var targetResult = nrMember.DeclaringType != null
						? new TypeResolveResult(nrMember.DeclaringType)
						: null;
					return new MemberResolveResult(targetResult, nrMember);
				}

				case RoslynSymbolKind.Local: {
					var localSymbol = (ILocalSymbol)symbol;
					var nrType = AdapterCache.GetOrCreateTypeAdapter(localSymbol.Type, compilationAdapter);
					var variable = new DefaultVariable(nrType, localSymbol.Name);
					return new LocalResolveResult(variable);
				}

				case RoslynSymbolKind.Parameter: {
					var paramSymbol = (IParameterSymbol)symbol;
					var nrType = AdapterCache.GetOrCreateTypeAdapter(paramSymbol.Type, compilationAdapter);
					var parameter = new DefaultParameter(nrType, paramSymbol.Name);
					return new LocalResolveResult(parameter);
				}

				case RoslynSymbolKind.Namespace: {
					var namespaceSymbol = (INamespaceSymbol)symbol;
					var ns = AdapterCache.GetOrCreateNamespaceAdapter(namespaceSymbol, compilationAdapter);
					return new NamespaceResolveResult(ns);
				}

				case RoslynSymbolKind.TypeParameter: {
					var typeParamSymbol = (ITypeParameterSymbol)symbol;
					var nrType = AdapterCache.GetOrCreateTypeAdapter(typeParamSymbol, compilationAdapter);
					return new TypeResolveResult(nrType);
				}

				case RoslynSymbolKind.Label:
					return new ErrorResolveResult(NRSpecialType.UnknownType);

				default:
					return new ErrorResolveResult(NRSpecialType.UnknownType);
			}
		}

		#endregion
	}
}