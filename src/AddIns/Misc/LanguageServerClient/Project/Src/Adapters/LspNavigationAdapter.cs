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
using ICSharpCode.LanguageServerClient.JsonRpc;
using ICSharpCode.LanguageServerClient.Protocol.Models;
using ICSharpCode.NRefactory.TypeSystem;
using ICSharpCode.SharpDevelop;
using ICSharpCode.SharpDevelop.Editor;
using ICSharpCode.SharpDevelop.Project;

namespace ICSharpCode.LanguageServerClient.Adapters
{
	/// <summary>
	/// LSP 导航适配器，提供代码导航功能：
	/// - 跳转到定义 (textDocument/definition)
	/// - 查找引用 (textDocument/references)
	/// - 跳转到实现 (textDocument/implementation)
	/// </summary>
	public class LspNavigationAdapter
	{
		/// <summary>
		/// 跳转到定义
		/// </summary>
		/// <param name="editor">当前编辑器</param>
		/// <returns>是否成功跳转</returns>
		public bool GoToDefinition(ITextEditor editor)
		{
			var lspClient = LspService.Instance.GetClient();
			if (lspClient == null || !lspClient.IsInitialized)
				return false;

			// 检查服务器是否支持定义跳转
			if (lspClient.ServerCapabilities?.DefinitionProvider != true)
				return false;

			try {
				var uri = LspConnection.FilePathToUri(editor.FileName);
				if (uri == null)
					return false;

				// 将编辑器位置转换为 LSP Position（0-based）
				var position = new Position(editor.Caret.Line - 1, editor.Caret.Column - 1);

				// 请求定义位置
				var locations = lspClient.GotoDefinitionAsync(uri, position).GetAwaiter().GetResult();

				if (locations == null || locations.Count == 0)
					return false;

				// 跳转到第一个定义位置
				return NavigateToLocation(locations[0]);
			} catch (Exception ex) {
				LoggingService.Error($"[LSP] 跳转到定义失败: {ex.Message}", ex);
				return false;
			}
		}

		/// <summary>
		/// 查找引用
		/// </summary>
		/// <param name="editor">当前编辑器</param>
		/// <returns>找到的引用位置列表</returns>
		public List<ReferenceLocation> FindReferences(ITextEditor editor)
		{
			var lspClient = LspService.Instance.GetClient();
			if (lspClient == null || !lspClient.IsInitialized)
				return null;

			// 检查服务器是否支持引用查找
			if (lspClient.ServerCapabilities?.ReferencesProvider != true)
				return null;

			try {
				var uri = LspConnection.FilePathToUri(editor.FileName);
				if (uri == null)
					return null;

				// 将编辑器位置转换为 LSP Position（0-based）
				var position = new Position(editor.Caret.Line - 1, editor.Caret.Column - 1);

				// 请求引用位置
				var locations = lspClient.FindReferencesAsync(uri, position, true).GetAwaiter().GetResult();

				if (locations == null || locations.Count == 0)
					return new List<ReferenceLocation>();

				// 转换为引用位置列表
				return locations.Select(ConvertToReferenceLocation).Where(loc => loc != null).ToList();
			} catch (Exception ex) {
				LoggingService.Error($"[LSP] 查找引用失败: {ex.Message}", ex);
				return null;
			}
		}

		/// <summary>
		/// 跳转到实现
		/// </summary>
		/// <param name="editor">当前编辑器</param>
		/// <returns>是否成功跳转</returns>
		public bool GoToImplementation(ITextEditor editor)
		{
			var lspClient = LspService.Instance.GetClient();
			if (lspClient == null || !lspClient.IsInitialized)
				return false;

			// 检查服务器是否支持实现跳转
			// ImplementationProvider 在 ServerCapabilities 中可能不存在
			// 尝试使用 textDocument/implementation，如果不支持则回退到定义跳转
			try {
				var uri = LspConnection.FilePathToUri(editor.FileName);
				if (uri == null)
					return false;

				var position = new Position(editor.Caret.Line - 1, editor.Caret.Column - 1);

				// 尝试通过 JSON-RPC 直接请求实现位置
				var rpcClient = GetRpcClient(lspClient);
				if (rpcClient == null)
					return GoToDefinition(editor);

				var result = rpcClient.SendRequestAsync("textDocument/implementation", new
				{
					textDocument = new TextDocumentIdentifier(uri),
					position = position
				}).GetAwaiter().GetResult();

				if (result == null || result.Type == Newtonsoft.Json.Linq.JTokenType.Null)
					return GoToDefinition(editor);

				// 解析结果（可能是 Location、Location[] 或 Location[][]）
				var locations = ParseLocationResult(result);
				if (locations == null || locations.Count == 0)
					return GoToDefinition(editor);

				return NavigateToLocation(locations[0]);
			} catch (Exception ex) {
				LoggingService.Debug($"[LSP] 跳转到实现失败，回退到定义跳转: {ex.Message}");
				return GoToDefinition(editor);
			}
		}

		/// <summary>
		/// 获取 LspClient 内部的 JsonRpcClient（通过反射）
		/// </summary>
		private JsonRpcClient GetRpcClient(LspClient client)
		{
			try {
				var field = typeof(LspClient).GetField("rpcClient",
					System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
				return field?.GetValue(client) as JsonRpcClient;
			} catch {
				return null;
			}
		}

		/// <summary>
		/// 导航到指定位置
		/// </summary>
		private bool NavigateToLocation(Location location)
		{
			if (location == null)
				return false;

			var filePath = UriToFilePath(location.Uri);
			if (filePath == null)
				return false;

			// LSP Position 是 0-based，SD 是 1-based
			int line = location.Range.Start.Line + 1;
			int column = location.Range.Start.Character + 1;

			try {
				SD.FileService.JumpToFilePosition(FileName.Create(filePath), line, column);
				return true;
			} catch (Exception ex) {
				LoggingService.Error($"[LSP] 导航到位置失败: {ex.Message}", ex);
				return false;
			}
		}

		/// <summary>
		/// 将 LSP Location 转换为 ReferenceLocation
		/// </summary>
		private ReferenceLocation ConvertToReferenceLocation(Location location)
		{
			if (location == null)
				return null;

			var filePath = UriToFilePath(location.Uri);
			if (filePath == null)
				return null;

			// LSP Position 是 0-based，SD 是 1-based
			int startLine = location.Range.Start.Line + 1;
			int startColumn = location.Range.Start.Character + 1;
			int endLine = location.Range.End.Line + 1;
			int endColumn = location.Range.End.Character + 1;

			return new ReferenceLocation(
				FileName.Create(filePath),
				new DomRegion(filePath, startLine, startColumn, endLine, endColumn)
			);
		}

		/// <summary>
		/// 解析位置结果（可能是 Location、Location[] 或 Location[][]）
		/// </summary>
		private List<Location> ParseLocationResult(Newtonsoft.Json.Linq.JToken result)
		{
			if (result == null || result.Type == Newtonsoft.Json.Linq.JTokenType.Null)
				return new List<Location>();

			// 单个 Location
			if (result.Type == Newtonsoft.Json.Linq.JTokenType.Object && result["uri"] != null) {
				var location = result.ToObject<Location>();
				return location != null ? new List<Location> { location } : new List<Location>();
			}

			// Location 数组
			if (result.Type == Newtonsoft.Json.Linq.JTokenType.Array) {
				// 可能是 Location[][] (用于 Definition 的变体)
				if (result.First != null && result.First.Type == Newtonsoft.Json.Linq.JTokenType.Array) {
					var locations = new List<Location>();
					foreach (var inner in result) {
						if (inner.Type == Newtonsoft.Json.Linq.JTokenType.Array) {
							locations.AddRange(inner.ToObject<List<Location>>() ?? new List<Location>());
						} else {
							var loc = inner.ToObject<Location>();
							if (loc != null)
								locations.Add(loc);
						}
					}
					return locations;
				}

				return result.ToObject<List<Location>>() ?? new List<Location>();
			}

			return new List<Location>();
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
	}

	/// <summary>
	/// 引用位置信息
	/// </summary>
	public class ReferenceLocation
	{
		/// <summary>
		/// 文件名
		/// </summary>
		public FileName FileName { get; }

		/// <summary>
		/// 引用区域
		/// </summary>
		public DomRegion Region { get; }

		public ReferenceLocation(FileName fileName, DomRegion region)
		{
			FileName = fileName;
			Region = region;
		}
	}
}
