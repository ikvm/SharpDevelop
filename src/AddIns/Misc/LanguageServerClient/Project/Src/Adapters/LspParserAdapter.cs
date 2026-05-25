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
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;

using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Folding;
using ICSharpCode.Core;
using ICSharpCode.NRefactory.CSharp;
using ICSharpCode.NRefactory.CSharp.Resolver;
using ICSharpCode.NRefactory.CSharp.TypeSystem;
using ICSharpCode.NRefactory.Semantics;
using ICSharpCode.NRefactory.TypeSystem;
using ICSharpCode.SharpDevelop;
using ICSharpCode.SharpDevelop.Editor.Search;
using ICSharpCode.SharpDevelop.Parser;
using ICSharpCode.SharpDevelop.Project;

namespace ICSharpCode.LanguageServerClient.Adapters
{
	/// <summary>
	/// LSP 解析器适配器，将 LSP 语言服务器功能适配到 SharpDevelop 的 IParser 接口。
	/// Parse() 方法使用 NRefactory CSharpParser 进行本地语法树解析（LSP 不提供语法树），
	/// Resolve() 方法优先使用 LSP hover 获取语义信息，不可用时回退到 NRefactory。
	/// </summary>
	public class LspParserAdapter : IParser
	{
		/// <summary>
		/// 任务列表标记词
		/// </summary>
		public IReadOnlyList<string> TaskListTokens { get; set; }

		/// <summary>
		/// 判断是否能解析指定文件（仅支持 .cs 文件）
		/// </summary>
		public bool CanParse(string fileName)
		{
			return Path.GetExtension(fileName).Equals(".CS", StringComparison.OrdinalIgnoreCase);
		}

		/// <summary>
		/// 获取文件内容
		/// </summary>
		public ITextSource GetFileContent(FileName fileName)
		{
			return SD.FileService.GetFileContent(fileName);
		}

		/// <summary>
		/// 解析文件。使用 NRefactory CSharpParser 进行本地语法树解析，
		/// 因为 LSP 不提供语法树。语法树用于代码折叠、导航等功能。
		/// </summary>
		public ParseInformation Parse(FileName fileName, ITextSource fileContent, bool fullParseInformationRequested,
		                              IProject parentProject, CancellationToken cancellationToken)
		{
			// 使用 NRefactory CSharpParser 进行本地解析（不依赖 CSharpBinding 的 CSharpProject）
			CSharpParser parser = new CSharpParser();
			SyntaxTree cu = parser.Parse(fileContent.Text, fileName);
			cu.Freeze();

			CSharpUnresolvedFile file = cu.ToTypeSystem();
			ParseInformation parseInfo;

			if (fullParseInformationRequested)
				parseInfo = new LspFullParseInformation(file, fileContent.Version, cu);
			else
				parseInfo = new ParseInformation(file, fileContent.Version, fullParseInformationRequested);

			// 添加注释标签
			IDocument document = fileContent as IDocument;
			AddCommentTags(cu, parseInfo.TagComments, fileContent, parseInfo.FileName, ref document);

			if (fullParseInformationRequested) {
				if (document == null)
					document = new ReadOnlyDocument(fileContent, parseInfo.FileName);
				((LspFullParseInformation)parseInfo).newFoldings = CreateNewFoldings(cu, document);
			}

			return parseInfo;
		}

		/// <summary>
		/// 解析指定位置的符号。优先使用 LSP hover 获取语义信息，
		/// 如果 LSP 不可用则回退到 NRefactory 本地解析。
		/// </summary>
		public ResolveResult Resolve(ParseInformation parseInfo, TextLocation location, ICompilation compilation, CancellationToken cancellationToken)
		{
			// 尝试使用 LSP hover 获取语义信息
			var lspClient = LspService.Instance.GetClient();
			if (lspClient != null && lspClient.IsInitialized) {
				try {
					var resolveResult = ResolveViaLspHover(parseInfo, location, lspClient, compilation, cancellationToken);
					if (resolveResult != null)
						return resolveResult;
				} catch (Exception ex) {
					LoggingService.Debug($"[LSP] 通过 LSP hover 解析失败，回退到 NRefactory: {ex.Message}");
				}
			}

			// 回退到 NRefactory 本地解析
			return ResolveViaNRefactory(parseInfo, location, compilation, cancellationToken);
		}

		/// <summary>
		/// 解析上下文。LSP 不支持此功能，回退到 NRefactory。
		/// </summary>
		public ICodeContext ResolveContext(ParseInformation parseInfo, TextLocation location, ICompilation compilation, CancellationToken cancellationToken)
		{
			var lspParseInfo = parseInfo as LspFullParseInformation;
			if (lspParseInfo == null)
				throw new ArgumentException("Parse info does not have SyntaxTree");

			CSharpUnresolvedFile unresolvedFile = lspParseInfo.UnresolvedFile;
			var projectContents = compilation.Assemblies.Select(asm => asm.UnresolvedAssembly).OfType<IProjectContent>().ToList();
			if (projectContents.All(pc => pc.GetFile(unresolvedFile.FileName) != unresolvedFile))
				unresolvedFile = null;
			var syntaxTree = lspParseInfo.SyntaxTree;
			var node = syntaxTree.GetNodeAt(location.ToNRefactory());
			if (node == null)
				return null;
			var resolver = new CSharpAstResolver(compilation, syntaxTree, unresolvedFile);
			return resolver.GetResolverStateBefore(node);
		}

		/// <summary>
		/// 解析代码片段。LSP 不支持此功能，回退到 NRefactory。
		/// </summary>
		public ResolveResult ResolveSnippet(ParseInformation parseInfo, TextLocation location, string codeSnippet, ICompilation compilation, CancellationToken cancellationToken)
		{
			var lspParseInfo = parseInfo as LspFullParseInformation;
			if (lspParseInfo == null)
				throw new ArgumentException("Parse info does not have SyntaxTree");
			CSharpAstResolver contextResolver = new CSharpAstResolver(compilation, lspParseInfo.SyntaxTree, lspParseInfo.UnresolvedFile);
			var node = lspParseInfo.SyntaxTree.GetNodeAt(location.ToNRefactory());
			CSharpResolver context;
			if (node != null)
				context = contextResolver.GetResolverStateAfter(node, cancellationToken);
			else
				context = new CSharpResolver(compilation);
			CSharpParser parser = new CSharpParser();
			var expr = parser.ParseExpression(codeSnippet);
			if (parser.HasErrors)
				return new ErrorResolveResult(SpecialType.UnknownType, PrintErrorsAsString(parser.Errors), TextLocation.Empty.ToNRefactory());
			CSharpAstResolver snippetResolver = new CSharpAstResolver(context, expr);
			return snippetResolver.Resolve(expr, cancellationToken);
		}

		/// <summary>
		/// 查找局部引用。LSP 不直接支持此功能，回退到 NRefactory。
		/// </summary>
		public void FindLocalReferences(ParseInformation parseInfo, ITextSource fileContent, IVariable variable, ICompilation compilation, Action<SearchResultMatch> callback, CancellationToken cancellationToken)
		{
			var lspParseInfo = parseInfo as LspFullParseInformation;
			if (lspParseInfo == null)
				throw new ArgumentException("Parse info does not have SyntaxTree");

			ReadOnlyDocument document = null;
			ICSharpCode.AvalonEdit.Highlighting.IHighlighter highlighter = null;

			new FindReferences().FindLocalReferences(
				variable, lspParseInfo.UnresolvedFile, lspParseInfo.SyntaxTree, compilation,
				delegate (AstNode node, ResolveResult result) {
					if (document == null) {
						document = new ReadOnlyDocument(fileContent, parseInfo.FileName);
						highlighter = SD.EditorControlService.CreateHighlighter(document);
						highlighter.BeginHighlighting();
					}
					var region = new DomRegion(parseInfo.FileName, node.StartLocation, node.EndLocation);
					int offset = document.GetOffset(node.StartLocation.ToAvalonEdit());
					int length = document.GetOffset(node.EndLocation.ToAvalonEdit()) - offset;
					var builder = SearchResultsPad.CreateInlineBuilder(node.StartLocation.ToAvalonEdit(), node.EndLocation.ToAvalonEdit(), document, highlighter);
					var defaultTextColor = highlighter != null ? highlighter.DefaultTextColor : null;
					callback(new SearchResultMatch(parseInfo.FileName, node.StartLocation.ToAvalonEdit(), node.EndLocation.ToAvalonEdit(), offset, length, builder, defaultTextColor));
				}, cancellationToken);

			if (highlighter != null) {
				highlighter.Dispose();
			}
		}

		/// <summary>
		/// 为不属于任何项目的单文件创建编译。回退到 NRefactory。
		/// </summary>
		public ICompilation CreateCompilationForSingleFile(FileName fileName, IUnresolvedFile unresolvedFile)
		{
			return new CSharpProjectContent()
				.AddAssemblyReferences(defaultReferences.Value)
				.AddOrUpdateFiles(unresolvedFile)
				.CreateCompilation();
		}

		#region 私有辅助方法

		/// <summary>
		/// 通过 LSP hover 请求获取语义信息
		/// </summary>
		private ResolveResult ResolveViaLspHover(ParseInformation parseInfo, TextLocation location, LspClient lspClient, ICompilation compilation, CancellationToken cancellationToken)
		{
			var filePath = parseInfo.FileName;
			var uri = LspConnection.FilePathToUri(filePath);
			if (uri == null)
				return null;

			// 将 SD 的 TextLocation（1-based）转换为 LSP 的 Position（0-based）
			var position = new Protocol.Models.Position(location.Line - 1, location.Column - 1);

			// 异步调用 hover，同步等待结果
			var hover = lspClient.HoverAsync(uri, position, cancellationToken).GetAwaiter().GetResult();

			if (hover == null)
				return null;

			// 从 hover 结果提取类型信息
			string hoverContent = ExtractHoverContent(hover);
			if (string.IsNullOrEmpty(hoverContent))
				return null;

			// 尝试从 hover 内容解析类型并创建 ResolveResult
			return TryCreateResolveResultFromHover(hoverContent, compilation);
		}

		/// <summary>
		/// 从 Hover 结果中提取文本内容
		/// </summary>
		private string ExtractHoverContent(Protocol.Models.Hover hover)
		{
			if (hover.Contents == null)
				return null;

			// 处理 MarkupContent 格式
			if (hover.Contents.Type == Newtonsoft.Json.Linq.JTokenType.Object) {
				var valueProp = hover.Contents["value"];
				if (valueProp != null)
					return valueProp.ToString();
			}

			// 处理字符串格式
			if (hover.Contents.Type == Newtonsoft.Json.Linq.JTokenType.String) {
				return hover.Contents.ToString();
			}

			// 处理 MarkedString 数组格式
			if (hover.Contents.Type == Newtonsoft.Json.Linq.JTokenType.Array) {
				var parts = new List<string>();
				foreach (var item in hover.Contents) {
					if (item.Type == Newtonsoft.Json.Linq.JTokenType.String) {
						parts.Add(item.ToString());
					} else if (item.Type == Newtonsoft.Json.Linq.JTokenType.Object) {
						var valueProp = item["value"];
						if (valueProp != null)
							parts.Add(valueProp.ToString());
					}
				}
				return string.Join(Environment.NewLine, parts);
			}

			return null;
		}

		/// <summary>
		/// 尝试从 hover 内容创建 ResolveResult
		/// </summary>
		private ResolveResult TryCreateResolveResultFromHover(string hoverContent, ICompilation compilation)
		{
			if (string.IsNullOrWhiteSpace(hoverContent))
				return null;

			// 尝试从 hover 内容中查找已知类型
			var typeNames = ExtractTypeNames(hoverContent);
			foreach (var typeName in typeNames) {
				var type = compilation.FindType(new FullTypeName(typeName));
				if (type.Kind != TypeKind.Unknown) {
					return new TypeResolveResult(type);
				}
			}

			return null;
		}

		/// <summary>
		/// 从 hover 文本中提取可能的类型名称
		/// </summary>
		private List<string> ExtractTypeNames(string hoverContent)
		{
			var names = new List<string>();

			foreach (var line in hoverContent.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)) {
				var trimmed = line.Trim();
				// 去除 markdown 代码标记
				trimmed = trimmed.TrimStart('`').TrimEnd('`');

				// 如果包含点号，可能是完全限定名
				if (trimmed.Contains(".") && !trimmed.Contains(" ") && trimmed.Length < 200) {
					names.Add(trimmed);
				}
			}

			return names;
		}

		/// <summary>
		/// 使用 NRefactory 本地解析
		/// </summary>
		private ResolveResult ResolveViaNRefactory(ParseInformation parseInfo, TextLocation location, ICompilation compilation, CancellationToken cancellationToken)
		{
			var lspParseInfo = parseInfo as LspFullParseInformation;
			if (lspParseInfo == null)
				throw new ArgumentException("Parse info does not have SyntaxTree");

			CSharpUnresolvedFile unresolvedFile = lspParseInfo.UnresolvedFile;
			var projectContents = compilation.Assemblies.Select(asm => asm.UnresolvedAssembly).OfType<IProjectContent>().ToList();
			if (projectContents.All(pc => pc.GetFile(unresolvedFile.FileName) != unresolvedFile))
				unresolvedFile = null;
			return ResolveAtLocation.Resolve(compilation, unresolvedFile, lspParseInfo.SyntaxTree, location.ToNRefactory(), cancellationToken);
		}

		/// <summary>
		/// 添加注释标签
		/// </summary>
		void AddCommentTags(SyntaxTree cu, IList<TagComment> tagComments, ITextSource fileContent, FileName fileName, ref IDocument document)
		{
			foreach (var comment in cu.Descendants.OfType<Comment>()) {
				if (comment.CommentType == CommentType.InactiveCode)
					continue;
				string match;
				if (comment.Content.ContainsAny(TaskListTokens, 0, out match)) {
					if (document == null)
						document = new ReadOnlyDocument(fileContent, fileName);
					int commentSignLength = comment.CommentType == CommentType.Documentation || comment.CommentType == CommentType.MultiLineDocumentation ? 3 : 2;
					int commentEndSignLength = comment.CommentType == CommentType.MultiLine || comment.CommentType == CommentType.MultiLineDocumentation ? 2 : 0;
					int commentStartOffset = document.GetOffset(comment.StartLocation.ToAvalonEdit()) + commentSignLength;
					int commentEndOffset = document.GetOffset(comment.EndLocation.ToAvalonEdit()) - commentEndSignLength;
					int endOffset;
					int searchOffset = 0;
					string commentContent = document.GetText(commentStartOffset, Math.Min(commentEndOffset - commentStartOffset + 1, commentEndOffset - commentStartOffset));
					do {
						int start = commentStartOffset + searchOffset;
						int absoluteOffset = document.IndexOf(match, start, document.TextLength - start, StringComparison.Ordinal);
						var startLocation = document.GetLocation(absoluteOffset);
						endOffset = Math.Min(document.GetLineByNumber(startLocation.Line).EndOffset, commentEndOffset);
						string content = document.GetText(absoluteOffset, endOffset - absoluteOffset);
						if (content.Length < match.Length) {
							break;
						}
						tagComments.Add(new TagComment(content.Substring(0, match.Length), new DomRegion(cu.FileName, startLocation.Line, startLocation.Column), content.Substring(match.Length)));
						searchOffset = endOffset - commentStartOffset;
					} while (commentContent.ContainsAny(TaskListTokens, searchOffset, out match));
				}
			}
		}

		/// <summary>
		/// 创建代码折叠
		/// </summary>
		List<NewFolding> CreateNewFoldings(SyntaxTree syntaxTree, IDocument document)
		{
			var visitor = new LspFoldingVisitor { document = document };
			syntaxTree.AcceptVisitor(visitor);
			return visitor.foldings;
		}

		string PrintErrorsAsString(IEnumerable<Error> errors)
		{
			var builder = new System.Text.StringBuilder();
			foreach (var error in errors)
				builder.AppendLine(error.Message);
			return builder.ToString();
		}

		/// <summary>
		/// 默认程序集引用（用于单文件编译）
		/// </summary>
		static readonly Lazy<IAssemblyReference[]> defaultReferences = new Lazy<IAssemblyReference[]>(
			delegate {
				Assembly[] assemblies = {
					typeof(object).Assembly,
					typeof(Uri).Assembly,
					typeof(Enumerable).Assembly
				};
				return assemblies.Select(asm => SD.AssemblyParserService.GetAssembly(FileName.Create(asm.Location))).ToArray();
			});

		#endregion
	}

	/// <summary>
	/// LSP 适配器的完整解析信息，包含语法树和折叠信息。
	/// 类似于 CSharpBinding 中的 CSharpFullParseInformation，但独立于 CSharpBinding 项目。
	/// </summary>
	public class LspFullParseInformation : ParseInformation
	{
		readonly SyntaxTree syntaxTree;
		internal List<NewFolding> newFoldings;

		public LspFullParseInformation(CSharpUnresolvedFile unresolvedFile, ITextSourceVersion parsedVersion, SyntaxTree compilationUnit)
			: base(unresolvedFile, parsedVersion, isFullParseInformation: true)
		{
			if (unresolvedFile == null)
				throw new ArgumentNullException("unresolvedFile");
			if (compilationUnit == null)
				throw new ArgumentNullException("compilationUnit");
			this.syntaxTree = compilationUnit;
		}

		public new CSharpUnresolvedFile UnresolvedFile {
			get { return (CSharpUnresolvedFile)base.UnresolvedFile; }
		}

		public SyntaxTree SyntaxTree {
			get { return syntaxTree; }
		}

		static readonly object ResolverCacheKey = new object();

		public CSharpAstResolver GetResolver(ICompilation compilation)
		{
			var resolver = compilation.CacheManager.GetShared(ResolverCacheKey) as CSharpAstResolver;
			if (resolver == null || resolver.RootNode != syntaxTree || resolver.UnresolvedFile != UnresolvedFile) {
				resolver = new CSharpAstResolver(compilation, syntaxTree, UnresolvedFile);
				compilation.CacheManager.SetShared(ResolverCacheKey, resolver);
			}
			return resolver;
		}

		public override IEnumerable<NewFolding> GetFoldings(IDocument document, out int firstErrorOffset)
		{
			firstErrorOffset = -1;
			return newFoldings;
		}
	}

	/// <summary>
	/// 代码折叠访问器，从语法树中提取折叠区域。
	/// 独立于 CSharpBinding 的 FoldingVisitor，避免跨项目依赖。
	/// </summary>
	class LspFoldingVisitor : DepthFirstAstVisitor
	{
		internal List<NewFolding> foldings = new List<NewFolding>();
		internal IDocument document;

		int GetOffset(TextLocation location)
		{
			return document.GetOffset(location);
		}

		NewFolding AddFolding(TextLocation start, TextLocation end, bool isDefinition = false)
		{
			if (end.Line <= start.Line || start.IsEmpty || end.IsEmpty)
				return null;
			NewFolding folding = new NewFolding(GetOffset(start), GetOffset(end));
			folding.IsDefinition = isDefinition;
			foldings.Add(folding);
			return folding;
		}

		TextLocation GetEndOfPrev(AstNode node)
		{
			do {
				node = node.GetPrevNode();
			} while (node.NodeType == NodeType.Whitespace);
			return node.EndLocation.ToAvalonEdit();
		}

		#region using 声明折叠
		void AddUsings(AstNode parent)
		{
			var firstChild = parent.Children.FirstOrDefault(child => child is UsingDeclaration || child is UsingAliasDeclaration);
			var node = firstChild;
			while (node != null) {
				var next = node.GetNextNode();
				if (next is UsingDeclaration || next is UsingAliasDeclaration) {
					node = next;
				} else {
					break;
				}
			}
			if (firstChild != node) {
				NewFolding folding = AddFolding(firstChild.StartLocation.ToAvalonEdit(), node.EndLocation.ToAvalonEdit());
				if (folding != null) {
					folding.Name = "using...";
					folding.DefaultClosed = true;
				}
			}
		}

		public override void VisitSyntaxTree(SyntaxTree unit)
		{
			AddUsings(unit);
			base.VisitSyntaxTree(unit);
		}

		public override void VisitNamespaceDeclaration(NamespaceDeclaration namespaceDeclaration)
		{
			AddUsings(namespaceDeclaration);
			if (!namespaceDeclaration.RBraceToken.IsNull)
				AddFolding(namespaceDeclaration.LBraceToken.GetPrevNode().EndLocation.ToAvalonEdit(), namespaceDeclaration.RBraceToken.EndLocation.ToAvalonEdit());
			base.VisitNamespaceDeclaration(namespaceDeclaration);
		}
		#endregion

		#region 类型声明折叠
		public override void VisitTypeDeclaration(TypeDeclaration typeDeclaration)
		{
			if (!typeDeclaration.RBraceToken.IsNull)
				AddFolding(GetEndOfPrev(typeDeclaration.LBraceToken), typeDeclaration.RBraceToken.EndLocation.ToAvalonEdit());
			base.VisitTypeDeclaration(typeDeclaration);
		}

		public override void VisitMethodDeclaration(MethodDeclaration methodDeclaration)
		{
			if (!methodDeclaration.Body.IsNull) {
				AddFolding(GetEndOfPrev(methodDeclaration.Body.LBraceToken),
				           methodDeclaration.Body.RBraceToken.EndLocation.ToAvalonEdit(), true);
			}
			base.VisitMethodDeclaration(methodDeclaration);
		}

		public override void VisitConstructorDeclaration(ConstructorDeclaration constructorDeclaration)
		{
			if (!constructorDeclaration.Body.IsNull)
				AddFolding(GetEndOfPrev(constructorDeclaration.Body.LBraceToken),
				           constructorDeclaration.Body.RBraceToken.EndLocation.ToAvalonEdit(), true);
			base.VisitConstructorDeclaration(constructorDeclaration);
		}

		public override void VisitDestructorDeclaration(DestructorDeclaration destructorDeclaration)
		{
			if (!destructorDeclaration.Body.IsNull)
				AddFolding(GetEndOfPrev(destructorDeclaration.Body.LBraceToken),
				           destructorDeclaration.Body.RBraceToken.EndLocation.ToAvalonEdit(), true);
			base.VisitDestructorDeclaration(destructorDeclaration);
		}

		public override void VisitOperatorDeclaration(OperatorDeclaration operatorDeclaration)
		{
			if (!operatorDeclaration.Body.IsNull)
				AddFolding(GetEndOfPrev(operatorDeclaration.Body.LBraceToken),
				           operatorDeclaration.Body.RBraceToken.EndLocation.ToAvalonEdit(), true);
			base.VisitOperatorDeclaration(operatorDeclaration);
		}

		public override void VisitPropertyDeclaration(PropertyDeclaration propertyDeclaration)
		{
			if (!propertyDeclaration.LBraceToken.IsNull)
				AddFolding(GetEndOfPrev(propertyDeclaration.LBraceToken),
				           propertyDeclaration.RBraceToken.EndLocation.ToAvalonEdit(), true);
			base.VisitPropertyDeclaration(propertyDeclaration);
		}

		public override void VisitIndexerDeclaration(IndexerDeclaration indexerDeclaration)
		{
			if (!indexerDeclaration.LBraceToken.IsNull)
				AddFolding(GetEndOfPrev(indexerDeclaration.LBraceToken),
				           indexerDeclaration.RBraceToken.EndLocation.ToAvalonEdit(), true);
			base.VisitIndexerDeclaration(indexerDeclaration);
		}

		public override void VisitCustomEventDeclaration(CustomEventDeclaration eventDeclaration)
		{
			if (!eventDeclaration.LBraceToken.IsNull)
				AddFolding(GetEndOfPrev(eventDeclaration.LBraceToken),
				           eventDeclaration.RBraceToken.EndLocation.ToAvalonEdit(), true);
			base.VisitCustomEventDeclaration(eventDeclaration);
		}
		#endregion

		#region 语句折叠
		public override void VisitSwitchStatement(SwitchStatement switchStatement)
		{
			if (!switchStatement.RBraceToken.IsNull)
				AddFolding(GetEndOfPrev(switchStatement.LBraceToken),
				           switchStatement.RBraceToken.EndLocation.ToAvalonEdit());
			base.VisitSwitchStatement(switchStatement);
		}

		public override void VisitBlockStatement(BlockStatement blockStatement)
		{
			if (!(blockStatement.Parent is EntityDeclaration) && blockStatement.EndLocation.Line - blockStatement.StartLocation.Line > 2) {
				AddFolding(GetEndOfPrev(blockStatement), blockStatement.EndLocation.ToAvalonEdit());
			}
			base.VisitBlockStatement(blockStatement);
		}
		#endregion

		#region 预处理指令折叠
		Stack<NewFolding> regions = new Stack<NewFolding>();

		public override void VisitPreProcessorDirective(PreProcessorDirective preProcessorDirective)
		{
			switch (preProcessorDirective.Type) {
				case PreProcessorDirectiveType.Region:
					NewFolding folding = new NewFolding();
					folding.DefaultClosed = true;
					folding.Name = preProcessorDirective.Argument;
					folding.StartOffset = GetOffset(preProcessorDirective.StartLocation.ToAvalonEdit());
					regions.Push(folding);
					break;
				case PreProcessorDirectiveType.Endregion:
					if (regions.Count > 0) {
						folding = regions.Pop();
						folding.EndOffset = GetOffset(preProcessorDirective.EndLocation.ToAvalonEdit());
						foldings.Add(folding);
					}
					break;
			}
		}
		#endregion

		#region 注释折叠
		public override void VisitComment(Comment comment)
		{
			if (comment.CommentType == CommentType.InactiveCode)
				return;
			if (AreTwoSinglelineCommentsInConsecutiveLines(comment.PrevSibling as Comment, comment))
				return;
			Comment lastComment = comment;
			Comment nextComment;
			while (true) {
				nextComment = lastComment.NextSibling as Comment;
				if (!AreTwoSinglelineCommentsInConsecutiveLines(lastComment, nextComment))
					break;
				lastComment = nextComment;
			}
			if (lastComment.EndLocation.Line - comment.StartLocation.Line > 2) {
				var folding = AddFolding(comment.StartLocation.ToAvalonEdit(), lastComment.EndLocation.ToAvalonEdit());
				if (folding != null) {
					switch (comment.CommentType) {
						case CommentType.SingleLine:
							folding.Name = "// ...";
							break;
						case CommentType.MultiLine:
							folding.Name = "/* ... */";
							break;
						case CommentType.Documentation:
							folding.Name = "/// ...";
							break;
						case CommentType.MultiLineDocumentation:
							folding.Name = "/** ... */";
							break;
					}
				}
			}
		}

		bool AreTwoSinglelineCommentsInConsecutiveLines(Comment comment1, Comment comment2)
		{
			if (comment1 == null || comment2 == null)
				return false;
			return comment1.CommentType == comment2.CommentType
				&& comment1.StartLocation.Line == comment1.EndLocation.Line
				&& comment1.EndLocation.Line + 1 == comment2.StartLocation.Line
				&& comment1.StartLocation.Column == comment2.StartLocation.Column
				&& comment2.StartLocation.Line == comment2.EndLocation.Line;
		}
		#endregion
	}
}
