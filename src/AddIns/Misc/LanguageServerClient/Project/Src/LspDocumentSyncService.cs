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

using ICSharpCode.Core;
using ICSharpCode.LanguageServerClient.Protocol;
using ICSharpCode.LanguageServerClient.Protocol.Models;
using System;
using System.Collections.Concurrent;
using System.IO;

namespace ICSharpCode.LanguageServerClient
{
	/// <summary>
	/// 文档同步服务，跟踪打开的文档状态并将变更同步到语言服务器
	/// </summary>
	public class LspDocumentSyncService : IDisposable
	{
		/// <summary>
		/// LSP 客户端
		/// </summary>
		private readonly LspClient lspClient;

		/// <summary>
		/// 打开的文档字典（URI -> 文档状态）
		/// </summary>
		private readonly ConcurrentDictionary<string, DocumentState> openDocuments = new ConcurrentDictionary<string, DocumentState>();

		/// <summary>
		/// 服务器支持的文档同步方式
		/// </summary>
		private int syncKind = TextDocumentSyncKind.Full;

		/// <summary>
		/// 是否已释放
		/// </summary>
		private bool disposed;

		/// <summary>
		/// 创建文档同步服务
		/// </summary>
		/// <param name="lspClient">LSP 客户端</param>
		public LspDocumentSyncService(LspClient lspClient)
		{
			this.lspClient = lspClient ?? throw new ArgumentNullException(nameof(lspClient));
		}

		/// <summary>
		/// 更新同步方式（应在初始化后调用）
		/// </summary>
		public void UpdateSyncKind()
		{
			if (lspClient.ServerCapabilities?.TextDocumentSync != null)
			{
				syncKind = lspClient.ServerCapabilities.TextDocumentSync.Change ?? TextDocumentSyncKind.Full;
				LoggingService.Info($"[LSP] 文档同步方式: {(syncKind == TextDocumentSyncKind.Full ? "全量" : syncKind == TextDocumentSyncKind.Incremental ? "增量" : "无")}");
			}
		}

		/// <summary>
		/// 打开文档
		/// </summary>
		/// <param name="filePath">文件路径</param>
		/// <param name="languageId">语言标识符（如 "csharp"）</param>
		/// <param name="text">文档文本内容</param>
		public void OpenDocument(string filePath, string languageId, string text)
		{
			ThrowIfDisposed();

			var uri = LspConnection.FilePathToUri(filePath);
			if (uri == null)
				return;

			var state = new DocumentState
			{
				Uri = uri,
				LanguageId = languageId,
				Version = 1,
				Text = text
			};

			openDocuments[uri] = state;

			var documentItem = new TextDocumentItem(uri, languageId, 1, text);
			lspClient.DidOpen(documentItem);

			LoggingService.Debug($"[LSP] 文档已打开: {filePath} (语言: {languageId})");
		}

		/// <summary>
		/// 文档内容变更（全量更新）
		/// </summary>
		/// <param name="filePath">文件路径</param>
		/// <param name="newText">新的完整文本</param>
		public void DocumentChanged(string filePath, string newText)
		{
			ThrowIfDisposed();

			var uri = LspConnection.FilePathToUri(filePath);
			if (uri == null || !openDocuments.TryGetValue(uri, out var state))
				return;

			// 递增版本号
			state.Version++;
			state.Text = newText;

			if (syncKind == TextDocumentSyncKind.Full)
			{
				// 全量同步：发送完整文本
				var changes = new System.Collections.Generic.List<TextDocumentContentChangeEvent>
				{
					TextDocumentContentChangeEvent.FullChange(newText)
				};
				lspClient.DidChange(uri, state.Version, changes);
			}
			else if (syncKind == TextDocumentSyncKind.Incremental)
			{
				// 增量同步：由于无法精确获取变更范围，回退到全量同步
				var changes = new System.Collections.Generic.List<TextDocumentContentChangeEvent>
				{
					TextDocumentContentChangeEvent.FullChange(newText)
				};
				lspClient.DidChange(uri, state.Version, changes);
			}
		}

		/// <summary>
		/// 文档内容变更（增量更新）
		/// </summary>
		/// <param name="filePath">文件路径</param>
		/// <param name="changeRange">变更的范围</param>
		/// <param name="newText">替换的文本</param>
		/// <param name="fullText">变更后的完整文本</param>
		public void DocumentChangedIncremental(string filePath, Range changeRange, string newText, string fullText)
		{
			ThrowIfDisposed();

			var uri = LspConnection.FilePathToUri(filePath);
			if (uri == null || !openDocuments.TryGetValue(uri, out var state))
				return;

			state.Version++;
			state.Text = fullText;

			if (syncKind == TextDocumentSyncKind.Incremental)
			{
				// 计算被替换文本的长度
				int rangeLength = CalculateRangeLength(state.Text, changeRange);

				var changes = new System.Collections.Generic.List<TextDocumentContentChangeEvent>
				{
					TextDocumentContentChangeEvent.IncrementalChange(changeRange, rangeLength, newText)
				};
				lspClient.DidChange(uri, state.Version, changes);
			}
			else
			{
				// 全量同步
				var changes = new System.Collections.Generic.List<TextDocumentContentChangeEvent>
				{
					TextDocumentContentChangeEvent.FullChange(fullText)
				};
				lspClient.DidChange(uri, state.Version, changes);
			}
		}

		/// <summary>
		/// 关闭文档
		/// </summary>
		/// <param name="filePath">文件路径</param>
		public void CloseDocument(string filePath)
		{
			ThrowIfDisposed();

			var uri = LspConnection.FilePathToUri(filePath);
			if (uri == null)
				return;

			if (openDocuments.TryRemove(uri, out var state))
			{
				lspClient.DidClose(uri);
				LoggingService.Debug($"[LSP] 文档已关闭: {filePath}");
			}
		}

		/// <summary>
		/// 保存文档
		/// </summary>
		/// <param name="filePath">文件路径</param>
		/// <param name="text">保存时的文档文本（可选）</param>
		public void SaveDocument(string filePath, string text = null)
		{
			ThrowIfDisposed();

			var uri = LspConnection.FilePathToUri(filePath);
			if (uri == null)
				return;

			lspClient.DidSave(uri, text);
		}

		/// <summary>
		/// 检查文档是否已打开
		/// </summary>
		/// <param name="filePath">文件路径</param>
		/// <returns>是否已打开</returns>
		public bool IsDocumentOpen(string filePath)
		{
			var uri = LspConnection.FilePathToUri(filePath);
			return uri != null && openDocuments.ContainsKey(uri);
		}

		/// <summary>
		/// 获取文档的当前版本号
		/// </summary>
		/// <param name="filePath">文件路径</param>
		/// <returns>版本号，如果文档未打开则返回 -1</returns>
		public int GetDocumentVersion(string filePath)
		{
			var uri = LspConnection.FilePathToUri(filePath);
			if (uri != null && openDocuments.TryGetValue(uri, out var state))
				return state.Version;
			return -1;
		}

		/// <summary>
		/// 获取文档的 URI
		/// </summary>
		/// <param name="filePath">文件路径</param>
		/// <returns>文档 URI，如果文档未打开则返回 null</returns>
		public string GetDocumentUri(string filePath)
		{
			var uri = LspConnection.FilePathToUri(filePath);
			if (uri != null && openDocuments.ContainsKey(uri))
				return uri;
			return null;
		}

		/// <summary>
		/// 获取语言标识符
		/// </summary>
		/// <param name="filePath">文件路径</param>
		/// <returns>语言标识符</returns>
		public static string GetLanguageId(string filePath)
		{
			var extension = Path.GetExtension(filePath).ToLowerInvariant();
			switch (extension)
			{
				case ".cs": return "csharp";
				case ".vb": return "vb";
				case ".fs": return "fsharp";
				case ".py": return "python";
				case ".js": return "javascript";
				case ".ts": return "typescript";
				case ".json": return "json";
				case ".xml": return "xml";
				case ".html": return "html";
				case ".css": return "css";
				case ".xaml": return "xml";
				case ".java": return "java";
				case ".cpp":
				case ".c":
				case ".h":
				case ".hpp": return "cpp";
				case ".rs": return "rust";
				case ".go": return "go";
				default: return extension.TrimStart('.');
			}
		}

		/// <summary>
		/// 计算范围内文本的长度
		/// </summary>
		private int CalculateRangeLength(string text, Range range)
		{
			if (range == null || string.IsNullOrEmpty(text))
				return 0;

			var lines = text.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);

			if (range.Start.Line >= lines.Length || range.End.Line >= lines.Length)
				return 0;

			// 计算起始偏移
			int startOffset = 0;
			for (int i = 0; i < range.Start.Line; i++)
			{
				startOffset += lines[i].Length + 1; // +1 for newline
			}
			startOffset += Math.Min(range.Start.Character, lines[range.Start.Line].Length);

			// 计算结束偏移
			int endOffset = 0;
			for (int i = 0; i < range.End.Line; i++)
			{
				endOffset += lines[i].Length + 1;
			}
			endOffset += Math.Min(range.End.Character, lines[range.End.Line].Length);

			return Math.Max(0, endOffset - startOffset);
		}

		private void ThrowIfDisposed()
		{
			if (disposed)
				throw new ObjectDisposedException(nameof(LspDocumentSyncService));
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

			if (disposing)
			{
				disposed = true;

				// 关闭所有打开的文档
				foreach (var kvp in openDocuments)
				{
					try
					{
						lspClient.DidClose(kvp.Key);
					}
					catch
					{
						// 忽略关闭时的错误
					}
				}

				openDocuments.Clear();
			}
		}

		/// <summary>
		/// 文档状态
		/// </summary>
		private class DocumentState
		{
			/// <summary>
			/// 文档 URI
			/// </summary>
			public string Uri { get; set; }

			/// <summary>
			/// 语言标识符
			/// </summary>
			public string LanguageId { get; set; }

			/// <summary>
			/// 文档版本号
			/// </summary>
			public int Version { get; set; }

			/// <summary>
			/// 文档文本内容
			/// </summary>
			public string Text { get; set; }
		}
	}
}
