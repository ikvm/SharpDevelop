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

using ICSharpCode.Core;
using ICSharpCode.LanguageServerClient.Protocol.Models;
using ICSharpCode.SharpDevelop;
using ICSharpCode.SharpDevelop.Project;

namespace ICSharpCode.LanguageServerClient.Adapters
{
	/// <summary>
	/// LSP 诊断信息适配器，将 LSP 诊断通知转换为 SharpDevelop 的任务列表项。
	/// 订阅 LspClient 的 DiagnosticsReceived 事件，将 Diagnostic 对象转换为 SDTask。
	/// </summary>
	public class LspDiagnosticsAdapter : IDisposable
	{
		/// <summary>
		/// LSP 客户端
		/// </summary>
		private LspClient lspClient;

		/// <summary>
		/// 当前文件的诊断任务缓存（文件路径 -> 任务列表）
		/// 用于在收到新诊断时移除旧任务
		/// </summary>
		private readonly Dictionary<string, List<SDTask>> fileTasks = new Dictionary<string, List<SDTask>>();

		/// <summary>
		/// 是否已释放
		/// </summary>
		private bool disposed;

		/// <summary>
		/// 创建诊断信息适配器
		/// </summary>
		/// <param name="lspClient">LSP 客户端实例</param>
		public LspDiagnosticsAdapter(LspClient lspClient)
		{
			this.lspClient = lspClient ?? throw new ArgumentNullException(nameof(lspClient));
			this.lspClient.DiagnosticsReceived += OnDiagnosticsReceived;
		}

		/// <summary>
		/// 处理收到的诊断信息
		/// </summary>
		private void OnDiagnosticsReceived(object sender, PublishDiagnosticsParams e)
		{
			try {
				// 将 LSP URI 转换为本地文件路径
				var filePath = UriToFilePath(e.Uri);
				if (filePath == null) {
					LoggingService.Debug($"[LSP] 无法转换诊断 URI 为文件路径: {e.Uri}");
					return;
				}

				var fileName = FileName.Create(filePath);

				// 移除该文件的旧诊断任务
				RemoveTasksForFile(fileName);

				// 添加新的诊断任务
				var newTasks = new List<SDTask>();
				foreach (var diagnostic in e.Diagnostics) {
					var task = ConvertDiagnosticToTask(diagnostic, fileName);
					if (task != null) {
						newTasks.Add(task);
						TaskService.Add(task);
					}
				}

				// 缓存新任务
				fileTasks[filePath] = newTasks;

				LoggingService.Debug($"[LSP] 已更新 {filePath} 的诊断信息 ({e.Diagnostics.Count} 项)");
			} catch (Exception ex) {
				LoggingService.Error($"[LSP] 处理诊断信息时出错: {ex.Message}", ex);
			}
		}

		/// <summary>
		/// 移除指定文件的旧诊断任务
		/// </summary>
		private void RemoveTasksForFile(FileName fileName)
		{
			var filePath = fileName.ToString();
			if (fileTasks.TryGetValue(filePath, out var oldTasks)) {
				foreach (var task in oldTasks) {
					TaskService.Remove(task);
				}
				fileTasks.Remove(filePath);
			}
		}

		/// <summary>
		/// 将 LSP Diagnostic 转换为 SharpDevelop SDTask
		/// </summary>
		private SDTask ConvertDiagnosticToTask(Diagnostic diagnostic, FileName fileName)
		{
			if (diagnostic == null)
				return null;

			// 确定任务类型
			TaskType taskType = ConvertSeverityToTaskType(diagnostic.Severity);

			// 确定位置（LSP 是 0-based，SD 是 1-based）
			int line = 0;
			int column = 0;
			if (diagnostic.Range?.Start != null) {
				line = diagnostic.Range.Start.Line + 1;
				column = diagnostic.Range.Start.Character + 1;
			}

			// 构建描述信息
			string description = BuildDescription(diagnostic);

			return new SDTask(fileName, description, column, line, taskType);
		}

		/// <summary>
		/// 将 LSP DiagnosticSeverity 转换为 SharpDevelop TaskType
		/// </summary>
		private TaskType ConvertSeverityToTaskType(DiagnosticSeverity? severity)
		{
			switch (severity) {
				case DiagnosticSeverity.Error:
					return TaskType.Error;
				case DiagnosticSeverity.Warning:
					return TaskType.Warning;
				case DiagnosticSeverity.Information:
				case DiagnosticSeverity.Hint:
					return TaskType.Message;
				default:
					// 默认为错误
					return TaskType.Error;
			}
		}

		/// <summary>
		/// 构建诊断描述信息
		/// </summary>
		private string BuildDescription(Diagnostic diagnostic)
		{
			var parts = new List<string>();

			// 添加来源前缀
			if (!string.IsNullOrEmpty(diagnostic.Source)) {
				parts.Add($"[{diagnostic.Source}]");
			}

			// 添加消息
			if (!string.IsNullOrEmpty(diagnostic.Message)) {
				parts.Add(diagnostic.Message);
			}

			// 添加错误代码
			if (diagnostic.Code != null) {
				var codeStr = diagnostic.Code.Type == Newtonsoft.Json.Linq.JTokenType.String
					? diagnostic.Code.ToString()
					: diagnostic.Code.ToString();
				if (!string.IsNullOrEmpty(codeStr)) {
					parts.Add($"({codeStr})");
				}
			}

			return parts.Count > 0 ? string.Join(" ", parts) : "Unknown diagnostic";
		}

		/// <summary>
		/// 将 file:/// URI 转换为本地文件路径
		/// </summary>
		private string UriToFilePath(string uri)
		{
			if (string.IsNullOrEmpty(uri))
				return null;

			try {
				var uriObj = new Uri(uri);
				return uriObj.LocalPath;
			} catch {
				return uri;
			}
		}

		/// <summary>
		/// 清除所有 LSP 诊断任务
		/// </summary>
		public void ClearAllTasks()
		{
			foreach (var kvp in fileTasks) {
				foreach (var task in kvp.Value) {
					TaskService.Remove(task);
				}
			}
			fileTasks.Clear();
		}

		public void Dispose()
		{
			Dispose(true);
			GC.SuppressFinalize(this);
		}

		protected virtual void Dispose(bool disposing)
		{
			if (disposed)
				return;

			if (disposing) {
				disposed = true;

				// 清除所有诊断任务
				ClearAllTasks();

				// 取消订阅事件
				if (lspClient != null) {
					lspClient.DiagnosticsReceived -= OnDiagnosticsReceived;
					lspClient = null;
				}
			}
		}
	}
}
