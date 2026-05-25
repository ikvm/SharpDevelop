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
using System.Threading;
using System.Threading.Tasks;

using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.Core;
using ICSharpCode.NRefactory;
using SDTextLocation = ICSharpCode.AvalonEdit.Document.TextLocation;
using ICSharpCode.SharpDevelop;
using ICSharpCode.SharpDevelop.Editor.Search;
using ICSharpCode.SharpDevelop.Parser;
using ICSharpCode.SharpDevelop.Project;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.FindSymbols;
using Microsoft.CodeAnalysis.Text;

using CSharpBinding.Parser.Roslyn;

namespace CSharpBinding.Navigation.Roslyn
{
	/// <summary>
	/// 基于 Roslyn 的代码导航服务。
	/// 提供跳转到定义、查找引用、跳转到实现等功能。
	/// </summary>
	public static class RoslynNavigationService
	{
		/// <summary>
		/// 跳转到定义。
		/// 使用 Roslyn SemanticModel 获取符号的定义位置。
		/// </summary>
		public static async Task GoToDefinitionAsync(FileName fileName, SDTextLocation location, IProject project, CancellationToken cancellationToken)
		{
			if (fileName == null)
				throw new ArgumentNullException(nameof(fileName));

			// 1. 获取 CSharpCompilation
			var compilation = GetCompilation(fileName, project);
			if (compilation == null) {
				MessageService.ShowMessage("无法获取编译上下文，请确保项目已加载。");
				return;
			}

			// 2. 查找文件对应的 SyntaxTree
			var syntaxTree = compilation.SyntaxTrees
				.FirstOrDefault(t => string.Equals(t.FilePath, fileName, StringComparison.OrdinalIgnoreCase));

			if (syntaxTree == null) {
				// 如果 Compilation 中没有该文件，尝试解析并添加
				var sourceText = GetFileSourceText(fileName);
				if (sourceText == null) {
					MessageService.ShowMessage("无法读取文件内容: " + fileName);
					return;
				}
				syntaxTree = CSharpSyntaxTree.ParseText(sourceText, path: fileName, cancellationToken: cancellationToken);
				compilation = compilation.AddSyntaxTrees(syntaxTree);
			}

			// 3. 获取 SemanticModel
			var semanticModel = compilation.GetSemanticModel(syntaxTree);

			// 4. 将 SD TextLocation（1-based）转换为 Roslyn 位置（0-based）
			var linePosition = new LinePosition(location.Line - 1, location.Column - 1);
			var position = syntaxTree.GetText(cancellationToken).Lines.GetPosition(linePosition);

			// 5. 获取指定位置的符号
			var symbol = await GetSymbolAtLocationAsync(compilation, syntaxTree, position, cancellationToken);
			if (symbol == null) {
				MessageService.ShowMessage("无法解析当前位置的符号。");
				return;
			}

			// 6. 获取符号的声明位置
			var declarations = symbol.DeclaringSyntaxReferences;
			if (declarations.Length == 0) {
				MessageService.ShowMessage("未找到符号的定义位置。");
				return;
			}

			// 7. 跳转到第一个声明位置
			var firstDecl = declarations[0];
			var declSyntax = await firstDecl.GetSyntaxAsync(cancellationToken);
			var lineSpan = declSyntax.GetLocation().GetLineSpan();

			if (!string.IsNullOrEmpty(lineSpan.Path)) {
				var targetFileName = FileName.Create(lineSpan.Path);
				var targetLocation = ToTextLocation(lineSpan.StartLinePosition);
				SD.FileService.JumpToFilePosition(targetFileName, targetLocation.Line, targetLocation.Column);
			}
		}

		/// <summary>
		/// 查找所有引用。
		/// 使用 Roslyn SymbolFinder.FindReferencesAsync。
		/// 需要创建 AdhocWorkspace 来构建 Solution。
		/// </summary>
		public static async Task FindReferencesAsync(FileName fileName, SDTextLocation location, IProject project, CancellationToken cancellationToken)
		{
			if (fileName == null)
				throw new ArgumentNullException(nameof(fileName));

			// 1. 获取 CSharpCompilation
			var compilation = GetCompilation(fileName, project);
			if (compilation == null) {
				MessageService.ShowMessage("无法获取编译上下文，请确保项目已加载。");
				return;
			}

			// 2. 查找文件对应的 SyntaxTree
			var syntaxTree = compilation.SyntaxTrees
				.FirstOrDefault(t => string.Equals(t.FilePath, fileName, StringComparison.OrdinalIgnoreCase));

			if (syntaxTree == null) {
				var sourceText = GetFileSourceText(fileName);
				if (sourceText == null) {
					MessageService.ShowMessage("无法读取文件内容: " + fileName);
					return;
				}
				syntaxTree = CSharpSyntaxTree.ParseText(sourceText, path: fileName, cancellationToken: cancellationToken);
				compilation = compilation.AddSyntaxTrees(syntaxTree);
			}

			var semanticModel = compilation.GetSemanticModel(syntaxTree);

			// 3. 将 SD TextLocation（1-based）转换为 Roslyn 位置（0-based）
			var linePosition = new LinePosition(location.Line - 1, location.Column - 1);
			var position = syntaxTree.GetText(cancellationToken).Lines.GetPosition(linePosition);

			// 4. 获取指定位置的符号
			var symbol = await GetSymbolAtLocationAsync(compilation, syntaxTree, position, cancellationToken);
			if (symbol == null) {
				MessageService.ShowMessage("无法解析当前位置的符号。");
				return;
			}

			// 5. 使用 AdhocWorkspace 创建 Solution 以支持 SymbolFinder
			var results = new List<SearchResultMatch>();

			using (var workspace = new AdhocWorkspace()) {
				var projectId = ProjectId.CreateNewId();
				var solution = workspace.CurrentSolution
					.AddProject(projectId, project?.Name ?? "Project", project?.AssemblyName ?? "Project", LanguageNames.CSharp);

				// 添加元数据引用
				foreach (var reference in compilation.References) {
					solution = solution.AddMetadataReference(projectId, reference);
				}

				// 添加所有 SyntaxTree 作为文档
				foreach (var tree in compilation.SyntaxTrees) {
					var docId = DocumentId.CreateNewId(projectId);
					var sourceText = await tree.GetTextAsync(cancellationToken);
					solution = solution.AddDocument(docId, Path.GetFileName(tree.FilePath), sourceText, filePath: tree.FilePath);
				}

				// 6. 使用 SymbolFinder 查找引用
				var referencedSymbols = await SymbolFinder.FindReferencesAsync(symbol, solution, cancellationToken);

				foreach (var referencedSymbol in referencedSymbols) {
					foreach (var loc in referencedSymbol.Locations) {
						cancellationToken.ThrowIfCancellationRequested();

						var locLineSpan = loc.Location.GetLineSpan();
						if (string.IsNullOrEmpty(locLineSpan.Path))
							continue;

						var refFileName = FileName.Create(locLineSpan.Path);
						var startLocation = ToTextLocation(locLineSpan.StartLinePosition);
						var endLocation = ToTextLocation(locLineSpan.EndLinePosition);

						// 创建简单的 SearchResultMatch（不含语法高亮）
						results.Add(new SearchResultMatch(
							refFileName,
							startLocation,
							endLocation,
							0,  // offset 在没有文档时无法精确计算
							0,  // length 同理
							null,
							null
						));
					}
				}
			}

			// 7. 显示搜索结果
			if (results.Count > 0) {
				var symbolName = symbol.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
				var searchedFiles = GroupResultsByFile(results);
				SearchResultsPad.Instance.ShowSearchResults(
					StringParser.Parse("${res:SharpDevelop.Refactoring.ReferencesTo}", new StringTagPair("Name", symbolName)),
					searchedFiles.SelectMany(f => f.Matches)
				);
				SearchResultsPad.Instance.BringToFront();
			} else {
				MessageService.ShowMessage("未找到任何引用。");
			}
		}

		/// <summary>
		/// 跳转到实现。
		/// 使用 Roslyn SymbolFinder.FindImplementationsAsync。
		/// </summary>
		public static async Task GoToImplementationAsync(FileName fileName, SDTextLocation location, IProject project, CancellationToken cancellationToken)
		{
			if (fileName == null)
				throw new ArgumentNullException(nameof(fileName));

			// 1. 获取 CSharpCompilation
			var compilation = GetCompilation(fileName, project);
			if (compilation == null) {
				MessageService.ShowMessage("无法获取编译上下文，请确保项目已加载。");
				return;
			}

			// 2. 查找文件对应的 SyntaxTree
			var syntaxTree = compilation.SyntaxTrees
				.FirstOrDefault(t => string.Equals(t.FilePath, fileName, StringComparison.OrdinalIgnoreCase));

			if (syntaxTree == null) {
				var sourceText = GetFileSourceText(fileName);
				if (sourceText == null) {
					MessageService.ShowMessage("无法读取文件内容: " + fileName);
					return;
				}
				syntaxTree = CSharpSyntaxTree.ParseText(sourceText, path: fileName, cancellationToken: cancellationToken);
				compilation = compilation.AddSyntaxTrees(syntaxTree);
			}

			var semanticModel = compilation.GetSemanticModel(syntaxTree);

			// 3. 将 SD TextLocation（1-based）转换为 Roslyn 位置（0-based）
			var linePosition = new LinePosition(location.Line - 1, location.Column - 1);
			var position = syntaxTree.GetText(cancellationToken).Lines.GetPosition(linePosition);

			// 4. 获取指定位置的符号
			var symbol = await GetSymbolAtLocationAsync(compilation, syntaxTree, position, cancellationToken);
			if (symbol == null) {
				MessageService.ShowMessage("无法解析当前位置的符号。");
				return;
			}

			// 5. 使用 AdhocWorkspace 创建 Solution 以支持 SymbolFinder
			using (var workspace = new AdhocWorkspace()) {
				var projectId = ProjectId.CreateNewId();
				var solution = workspace.CurrentSolution
					.AddProject(projectId, project?.Name ?? "Project", project?.AssemblyName ?? "Project", LanguageNames.CSharp);

				foreach (var reference in compilation.References) {
					solution = solution.AddMetadataReference(projectId, reference);
				}

				foreach (var tree in compilation.SyntaxTrees) {
					var docId = DocumentId.CreateNewId(projectId);
					var sourceText = await tree.GetTextAsync(cancellationToken);
					solution = solution.AddDocument(docId, Path.GetFileName(tree.FilePath), sourceText, filePath: tree.FilePath);
				}

				// 6. 查找实现（Roslyn 4.8.0 返回 IEnumerable<ISymbol>）
				var implementations = await SymbolFinder.FindImplementationsAsync(symbol, solution, null, cancellationToken);
				var implList = implementations.ToList();

				if (implList.Count == 0) {
					// 没有找到实现，回退到跳转到定义
					await GoToDefinitionAsync(fileName, location, project, cancellationToken);
					return;
				}

				// 7. 如果只有一个实现，直接跳转
				if (implList.Count == 1) {
					var impl = implList[0];
					var declRef = impl.DeclaringSyntaxReferences.FirstOrDefault();
					if (declRef != null) {
						var implSyntax = await declRef.GetSyntaxAsync(cancellationToken);
						var implLineSpan = implSyntax.GetLocation().GetLineSpan();
						if (!string.IsNullOrEmpty(implLineSpan.Path)) {
							var targetFileName = FileName.Create(implLineSpan.Path);
							var targetLocation = ToTextLocation(implLineSpan.StartLinePosition);
							SD.FileService.JumpToFilePosition(targetFileName, targetLocation.Line, targetLocation.Column);
						}
					}
					return;
				}

				// 8. 多个实现时，在搜索结果窗口中显示
				var results = new List<SearchResultMatch>();
				foreach (var impl in implList) {
					foreach (var declRef in impl.DeclaringSyntaxReferences) {
						var implSyntax = await declRef.GetSyntaxAsync(cancellationToken);
						var implLineSpan = implSyntax.GetLocation().GetLineSpan();
						if (string.IsNullOrEmpty(implLineSpan.Path))
							continue;

						var refFileName = FileName.Create(implLineSpan.Path);
						var startLocation = ToTextLocation(implLineSpan.StartLinePosition);
						var endLocation = ToTextLocation(implLineSpan.EndLinePosition);

						results.Add(new SearchResultMatch(
							refFileName,
							startLocation,
							endLocation,
							0,
							0,
							null,
							null
						));
					}
				}

				if (results.Count > 0) {
					var symbolName = symbol.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
					SearchResultsPad.Instance.ShowSearchResults(
						"实现 " + symbolName,
						results
					);
					SearchResultsPad.Instance.BringToFront();
				}
			}
		}

		/// <summary>
		/// 获取指定位置的 Roslyn ISymbol。
		/// 首先尝试通过 SemanticModel.GetSymbolInfo 获取引用的符号，
		/// 如果失败则尝试 SemanticModel.GetDeclaredSymbol 获取声明符号。
		/// </summary>
		static async Task<ISymbol> GetSymbolAtLocationAsync(CSharpCompilation compilation, SyntaxTree syntaxTree, int position, CancellationToken cancellationToken)
		{
			var semanticModel = compilation.GetSemanticModel(syntaxTree);
			var root = await syntaxTree.GetRootAsync(cancellationToken);

			// 首先尝试从 trivia 位置获取 token
			var token = root.FindTrivia(position).Token;
			SyntaxNode node = token.Parent;

			// 如果 trivia 位置没找到有效节点，尝试从位置直接查找
			if (node == null || node.IsKind(SyntaxKind.None)) {
				node = root.FindNode(new TextSpan(position, 0));
			}

			if (node == null)
				return null;

			// 尝试获取符号信息（用于引用位置）
			var symbolInfo = semanticModel.GetSymbolInfo(node, cancellationToken);
			var symbol = symbolInfo.Symbol ?? symbolInfo.CandidateSymbols.FirstOrDefault();

			if (symbol == null) {
				// 尝试获取声明符号（用于类型声明、方法声明等定义位置）
				symbol = semanticModel.GetDeclaredSymbol(node, cancellationToken);
			}

			// 如果是方法组符号，尝试获取候选方法中的第一个
			// Roslyn 4.8.0 中 IMethodGroupSymbol 不再公开，MethodKind.MethodGroup 也已移除
			// 通过 CandidateSymbols 处理已在上方 GetSymbolInfo 中完成

			return symbol;
		}

		/// <summary>
		/// 将 Roslyn LinePosition（0-based）转换为 SD TextLocation（1-based）。
		/// </summary>
		static SDTextLocation ToTextLocation(LinePosition position)
		{
			return new SDTextLocation(position.Line + 1, position.Character + 1);
		}

		/// <summary>
		/// 获取指定文件的 CSharpCompilation。
		/// 如果有项目，使用 RoslynCompilationManager 获取；
		/// 否则创建单文件 Compilation。
		/// </summary>
		static CSharpCompilation GetCompilation(FileName fileName, IProject project)
		{
			try {
				if (project != null) {
					return RoslynCompilationManager.GetOrCreateCompilation(project);
				}

				// 非项目文件，创建单文件 Compilation
				if (fileName != null && File.Exists(fileName)) {
					var source = File.ReadAllText(fileName);
					return RoslynCompilationManager.CreateCompilationForSingleFile(fileName, source);
				}
			} catch (Exception ex) {
				LoggingService.Warn("获取 Roslyn Compilation 失败: " + fileName, ex);
			}

			return null;
		}

		/// <summary>
		/// 读取文件内容为 SourceText
		/// </summary>
		static SourceText GetFileSourceText(FileName fileName)
		{
			try {
				if (fileName != null && File.Exists(fileName)) {
					return SourceText.From(File.ReadAllText(fileName));
				}
			} catch (Exception ex) {
				LoggingService.Warn("读取文件内容失败: " + fileName, ex);
			}

			return null;
		}

		/// <summary>
		/// 将搜索结果按文件分组为 SearchedFile 列表。
		/// </summary>
		static List<SearchedFile> GroupResultsByFile(List<SearchResultMatch> results)
		{
			var grouped = new List<SearchedFile>();

			foreach (var group in results.GroupBy(r => r.FileName)) {
				grouped.Add(new SearchedFile(group.Key, group.ToList()));
			}

			return grouped;
		}
	}
}
