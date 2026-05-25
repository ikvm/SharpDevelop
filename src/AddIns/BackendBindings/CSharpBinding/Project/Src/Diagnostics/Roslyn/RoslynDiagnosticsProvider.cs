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
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using ICSharpCode.Core;
using ICSharpCode.SharpDevelop;
using ICSharpCode.SharpDevelop.Project;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

using CSharpBinding.Parser.Roslyn;

namespace CSharpBinding.Diagnostics.Roslyn
{
	/// <summary>
	/// 基于 Roslyn 的代码诊断提供者。
	/// 使用 Roslyn DiagnosticAnalyzer 基础设施为 SharpDevelop 提供代码诊断。
	/// 替代原有的 NRefactory CodeIssue 系统。
	/// </summary>
	public class RoslynDiagnosticsProvider
	{
		/// <summary>
		/// 对指定文件运行 Roslyn 诊断分析。
		/// 从 RoslynCompilationManager 获取 CSharpCompilation，
		/// 运行语法和语义诊断，并转换为 SDTask 列表。
		/// </summary>
		/// <param name="fileName">要分析的文件名</param>
		/// <param name="project">文件所属的项目</param>
		/// <param name="cancellationToken">取消令牌</param>
		/// <returns>诊断结果列表</returns>
		public static async Task<List<SDTask>> AnalyzeFileAsync(FileName fileName, IProject project, CancellationToken cancellationToken)
		{
			if (fileName == null)
				throw new ArgumentNullException(nameof(fileName));
			if (project == null)
				throw new ArgumentNullException(nameof(project));

			var tasks = new List<SDTask>();

			try {
				// 1. 从 RoslynCompilationManager 获取 CSharpCompilation
				var compilation = RoslynCompilationManager.GetOrCreateCompilation(project);
				if (compilation == null)
					return tasks;

				cancellationToken.ThrowIfCancellationRequested();

				// 2. 查找文件对应的 SyntaxTree
				string filePath = fileName.ToString();
				var syntaxTree = compilation.SyntaxTrees.FirstOrDefault(t => t.FilePath == filePath);

				if (syntaxTree != null) {
					// 3. 获取该文件的诊断（语法+语义），筛选该文件的结果
					var diagnostics = await Task.Run(
						() => compilation.GetDiagnostics(cancellationToken),
						cancellationToken);
					foreach (var diagnostic in diagnostics) {
						cancellationToken.ThrowIfCancellationRequested();
						// 仅筛选属于当前文件的诊断
						if (diagnostic.Location != null && diagnostic.Location.SourceTree == syntaxTree) {
							var task = ConvertDiagnostic(diagnostic, fileName);
							if (task != null)
								tasks.Add(task);
						}
					}
				} else {
					// 文件不在编译中，尝试获取整个编译的诊断（仅筛选该文件的）
					var allDiagnostics = await Task.Run(
						() => compilation.GetDiagnostics(cancellationToken),
						cancellationToken);
					foreach (var diagnostic in allDiagnostics) {
						cancellationToken.ThrowIfCancellationRequested();
						if (diagnostic.Location != null && diagnostic.Location.SourceTree != null
						    && diagnostic.Location.SourceTree.FilePath == filePath) {
							var task = ConvertDiagnostic(diagnostic, fileName);
							if (task != null)
								tasks.Add(task);
						}
					}
				}
			} catch (OperationCanceledException) {
				// 取消时返回已收集的结果
			} catch (Exception ex) {
				LoggingService.Warn("RoslynDiagnosticsProvider 分析文件时出错: " + fileName, ex);
			}

			return tasks;
		}

		/// <summary>
		/// 对整个项目运行 Roslyn 诊断分析。
		/// 包括语法错误、语义错误，以及可选的自定义 DiagnosticAnalyzer。
		/// </summary>
		/// <param name="project">要分析的项目</param>
		/// <param name="cancellationToken">取消令牌</param>
		/// <returns>诊断结果列表</returns>
		public static async Task<List<SDTask>> AnalyzeProjectAsync(IProject project, CancellationToken cancellationToken)
		{
			if (project == null)
				throw new ArgumentNullException(nameof(project));

			var tasks = new List<SDTask>();

			try {
				var compilation = RoslynCompilationManager.GetOrCreateCompilation(project);
				if (compilation == null)
					return tasks;

				cancellationToken.ThrowIfCancellationRequested();

				// 获取整个编译的所有诊断
				var diagnostics = await Task.Run(
					() => compilation.GetDiagnostics(cancellationToken),
					cancellationToken);

				foreach (var diagnostic in diagnostics) {
					cancellationToken.ThrowIfCancellationRequested();

					// 确定诊断所属文件
					FileName fileName = null;
					if (diagnostic.Location != null && diagnostic.Location.SourceTree != null) {
						fileName = FileName.Create(diagnostic.Location.SourceTree.FilePath);
					}

					var task = ConvertDiagnostic(diagnostic, fileName);
					if (task != null)
						tasks.Add(task);
				}
			} catch (OperationCanceledException) {
				// 取消时返回已收集的结果
			} catch (Exception ex) {
				LoggingService.Warn("RoslynDiagnosticsProvider 分析项目时出错: " + project.Name, ex);
			}

			return tasks;
		}

		/// <summary>
		/// 使用自定义 DiagnosticAnalyzer 对项目运行诊断分析。
		/// 通过 CompilationWithAnalyzers 执行分析器。
		/// </summary>
		/// <param name="project">要分析的项目</param>
		/// <param name="analyzers">要运行的诊断分析器列表</param>
		/// <param name="cancellationToken">取消令牌</param>
		/// <returns>诊断结果列表</returns>
		public static async Task<List<SDTask>> AnalyzeWithAnalyzersAsync(
			IProject project,
			IEnumerable<DiagnosticAnalyzer> analyzers,
			CancellationToken cancellationToken)
		{
			if (project == null)
				throw new ArgumentNullException(nameof(project));
			if (analyzers == null)
				throw new ArgumentNullException(nameof(analyzers));

			var tasks = new List<SDTask>();

			try {
				var compilation = RoslynCompilationManager.GetOrCreateCompilation(project);
				if (compilation == null)
					return tasks;

				cancellationToken.ThrowIfCancellationRequested();

				// 创建 CompilationWithAnalyzers 以运行自定义分析器
				var analyzerOptions = new AnalyzerOptions(ImmutableArray<AdditionalText>.Empty);
				var compilationWithAnalyzers = compilation.WithAnalyzers(
					analyzers.ToImmutableArray(),
					analyzerOptions,
					cancellationToken);

				// 获取所有诊断结果
				var diagnostics = await compilationWithAnalyzers.GetAllDiagnosticsAsync(cancellationToken);

				foreach (var diagnostic in diagnostics) {
					cancellationToken.ThrowIfCancellationRequested();

					FileName fileName = null;
					if (diagnostic.Location != null && diagnostic.Location.SourceTree != null) {
						fileName = FileName.Create(diagnostic.Location.SourceTree.FilePath);
					}

					var task = ConvertDiagnostic(diagnostic, fileName);
					if (task != null)
						tasks.Add(task);
				}
			} catch (OperationCanceledException) {
				// 取消时返回已收集的结果
			} catch (Exception ex) {
				LoggingService.Warn("RoslynDiagnosticsProvider 运行分析器时出错: " + project.Name, ex);
			}

			return tasks;
		}

		/// <summary>
		/// 将 Roslyn Diagnostic 转换为 SD SDTask。
		/// 映射 DiagnosticSeverity 到 TaskType，
		/// 并从 diagnostic.Location 提取行列信息。
		/// </summary>
		/// <param name="diagnostic">Roslyn 诊断对象</param>
		/// <param name="fileName">文件名，如果诊断没有位置信息则使用此值</param>
		/// <returns>转换后的 SDTask，如果诊断被隐藏则返回 null</returns>
		static SDTask ConvertDiagnostic(Diagnostic diagnostic, FileName fileName)
		{
			if (diagnostic == null)
				return null;

			// Hidden 严重级别的诊断不显示在任务列表中
			if (diagnostic.Severity == DiagnosticSeverity.Hidden)
				return null;

			// 映射严重级别
			TaskType taskType = MapSeverity(diagnostic.Severity);

			// 获取位置信息
			int line = 0;
			int column = 0;

			if (diagnostic.Location != null && diagnostic.Location != Location.None) {
				var lineSpan = diagnostic.Location.GetLineSpan();
				if (lineSpan.IsValid) {
					// Roslyn 行列从 0 开始，SDTask 需要 1 开始
					line = lineSpan.StartLinePosition.Line + 1;
					column = lineSpan.StartLinePosition.Character + 1;

					// 如果诊断没有关联文件名，从位置信息中获取
					if (fileName == null && lineSpan.Path != null) {
						fileName = FileName.Create(lineSpan.Path);
					}
				}
			}

			// 构建描述信息，包含诊断代码
			string description = diagnostic.GetMessage();
			if (diagnostic.Id != null) {
				description = string.Format("{0} ({1})", description, diagnostic.Id);
			}

			return new SDTask(fileName, description, column, line, taskType);
		}

		/// <summary>
		/// 将 Roslyn DiagnosticSeverity 映射为 SD TaskType。
		/// Error -> Error, Warning -> Warning, Info -> Message, Hidden -> Message
		/// </summary>
		static TaskType MapSeverity(DiagnosticSeverity severity)
		{
			switch (severity) {
				case DiagnosticSeverity.Error:
					return TaskType.Error;
				case DiagnosticSeverity.Warning:
					return TaskType.Warning;
				case DiagnosticSeverity.Info:
					return TaskType.Message;
				default:
					return TaskType.Message;
			}
		}
	}
}
