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
using System.Threading.Tasks;

using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.Core;
using ICSharpCode.SharpDevelop;
using ICSharpCode.SharpDevelop.Editor;
using ICSharpCode.SharpDevelop.Project;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;

using CSharpBinding.Parser.Roslyn;

namespace CSharpBinding.Diagnostics.Roslyn
{
	/// <summary>
	/// Roslyn 代码修复服务。
	/// 提供基于 Roslyn CodeFixProvider 的代码修复功能。
	/// 初始实现提供基本结构，完整的代码修复工作流将在后续迭代中完善。
	/// </summary>
	public class RoslynCodeFixService
	{
		/// <summary>
		/// 获取指定文件和位置的可用代码修复。
		/// 查找该位置的诊断，然后通过 CodeFixProvider 获取可用的修复操作。
		/// </summary>
		/// <param name="fileName">文件名</param>
		/// <param name="location">文本位置</param>
		/// <param name="project">文件所属的项目</param>
		/// <param name="cancellationToken">取消令牌</param>
		/// <returns>可用的代码修复列表</returns>
		public static async Task<List<CodeFix>> GetCodeFixesAsync(
			FileName fileName,
			TextLocation location,
			IProject project,
			CancellationToken cancellationToken)
		{
			if (fileName == null)
				throw new ArgumentNullException(nameof(fileName));
			if (project == null)
				throw new ArgumentNullException(nameof(project));

			var codeFixes = new List<CodeFix>();

			try {
				// 1. 从 RoslynCompilationManager 获取 CSharpCompilation
				var compilation = RoslynCompilationManager.GetOrCreateCompilation(project);
				if (compilation == null)
					return codeFixes;

				cancellationToken.ThrowIfCancellationRequested();

				// 2. 查找文件对应的 SyntaxTree
				string filePath = fileName.ToString();
				var syntaxTree = compilation.SyntaxTrees.FirstOrDefault(t => t.FilePath == filePath);
				if (syntaxTree == null)
					return codeFixes;

				// 3. 获取 SemanticModel
				var semanticModel = compilation.GetSemanticModel(syntaxTree);
				if (semanticModel == null)
					return codeFixes;

				// 4. 将 TextLocation 转换为 Roslyn 行列位置
				// TextLocation 的 Line/Column 从 1 开始，Roslyn 从 0 开始
				var linePosition = new LinePosition(location.Line - 1, location.Column - 1);
				var linePositionSpan = new LinePositionSpan(linePosition, linePosition);

				// 5. 获取该位置的诊断
				var diagnostics = await Task.Run(
					() => compilation.GetDiagnostics(cancellationToken),
					cancellationToken);

				var targetDiagnostics = diagnostics
					.Where(d => {
						if (d.Location == null || d.Location == Location.None)
							return false;
						var span = d.Location.GetLineSpan();
						return span.StartLinePosition <= linePosition && linePosition <= span.EndLinePosition;
					})
					.ToList();

				if (targetDiagnostics.Count == 0)
					return codeFixes;

				// 6. 为每个诊断查找可用的 CodeFixProvider
				// 注意：完整的实现需要通过 MEF 或注册机制发现 CodeFixProvider
				// 当前为初始结构，后续迭代中完善
				foreach (var diagnostic in targetDiagnostics) {
					cancellationToken.ThrowIfCancellationRequested();

					// TODO: 通过 CodeFixProvider 注册机制查找可用的 Provider
					// var providers = GetCodeFixProviders(diagnostic.Id);
					// foreach (var provider in providers) {
					//     await CollectCodeFixesAsync(provider, syntaxTree, semanticModel, diagnostic, codeFixes, cancellationToken);
					// }
				}
			} catch (OperationCanceledException) {
				// 取消时返回已收集的结果
			} catch (Exception ex) {
				LoggingService.Warn("RoslynCodeFixService 获取代码修复时出错: " + fileName, ex);
			}

			return codeFixes;
		}

		/// <summary>
		/// 使用指定的 CodeFixProvider 收集代码修复。
		/// </summary>
		/// <param name="provider">代码修复提供者</param>
		/// <param name="syntaxTree">语法树</param>
		/// <param name="semanticModel">语义模型</param>
		/// <param name="diagnostic">目标诊断</param>
		/// <param name="codeFixes">收集代码修复的列表</param>
		/// <param name="cancellationToken">取消令牌</param>
		static async Task CollectCodeFixesAsync(
			CodeFixProvider provider,
			SyntaxTree syntaxTree,
			SemanticModel semanticModel,
			Diagnostic diagnostic,
			List<CodeFix> codeFixes,
			CancellationToken cancellationToken)
		{
			if (provider == null || diagnostic == null)
				return;

			var document = CreateDocument(syntaxTree, semanticModel);
			if (document == null)
				return;

			var context = new CodeFixContext(
				document,
				diagnostic,
				(action, __) => {
					codeFixes.Add(new CodeFix {
						Title = action.Title,
						GetAction = _ => Task.FromResult(action)
					});
				},
				cancellationToken);

			await provider.RegisterCodeFixesAsync(context);
		}

		/// <summary>
		/// 应用代码修复到编辑器。
		/// 执行 CodeAction 并将变更应用到文本编辑器。
		/// </summary>
		/// <param name="fix">要应用的代码修复</param>
		/// <param name="editor">文本编辑器</param>
		/// <param name="cancellationToken">取消令牌</param>
		public static async Task ApplyCodeFixAsync(CodeFix fix, ITextEditor editor, CancellationToken cancellationToken)
		{
			if (fix == null)
				throw new ArgumentNullException(nameof(fix));
			if (editor == null)
				throw new ArgumentNullException(nameof(editor));

			try {
				// 获取 CodeAction
				var action = await fix.GetAction(cancellationToken);
				if (action == null)
					return;

				// TODO: 完整的 CodeAction 应用需要以下步骤：
				// 1. 创建包含当前文档的 Workspace（AdHocWorkspace）
				// 2. 获取操作的计算结果
				// 3. 将变更应用到编辑器
				// 当前为初始结构，后续迭代中完善

				LoggingService.Info("RoslynCodeFixService: 代码修复 '" + fix.Title + "' 已请求，但完整应用逻辑尚未实现");
			} catch (OperationCanceledException) {
				// 取消时不做任何操作
			} catch (Exception ex) {
				LoggingService.Warn("RoslynCodeFixService 应用代码修复时出错: " + fix.Title, ex);
			}
		}

		/// <summary>
		/// 从 SyntaxTree 和 SemanticModel 创建 Roslyn Document。
		/// 使用 AdHocWorkspace 创建临时的 Document 实例。
		/// </summary>
		static Document CreateDocument(SyntaxTree syntaxTree, SemanticModel semanticModel)
		{
			if (syntaxTree == null || semanticModel == null)
				return null;

			try {
				var workspace = new AdhocWorkspace();
				var projectId = ProjectId.CreateNewId();
				var documentId = DocumentId.CreateNewId(projectId);

				var projectInfo = ProjectInfo.Create(
					projectId,
					VersionStamp.Create(),
					"TempProject",
					"TempAssembly",
					LanguageNames.CSharp,
					documents: new[] {
						DocumentInfo.Create(
							documentId,
							syntaxTree.FilePath ?? "TempFile.cs",
							sourceCodeKind: SourceCodeKind.Regular,
							filePath: syntaxTree.FilePath)
					});

				var roslynProject = workspace.AddProject(projectInfo);
				var document = workspace.CurrentSolution.GetDocument(documentId);

				return document;
			} catch (Exception ex) {
				LoggingService.Warn("RoslynCodeFixService 创建临时 Document 时出错", ex);
				return null;
			}
		}
	}

	/// <summary>
	/// 表示一个可用的代码修复。
	/// 封装了 Roslyn CodeAction 的标题和延迟加载的操作。
	/// </summary>
	public class CodeFix
	{
		/// <summary>
		/// 代码修复的标题/描述
		/// </summary>
		public string Title { get; set; }

		/// <summary>
		/// 延迟获取 CodeAction 的委托。
		/// 允许在实际需要时才计算代码修复操作。
		/// </summary>
		public Func<CancellationToken, Task<CodeAction>> GetAction { get; set; }
	}
}
