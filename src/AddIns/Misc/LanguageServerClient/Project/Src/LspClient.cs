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
using ICSharpCode.LanguageServerClient.JsonRpc;
using ICSharpCode.LanguageServerClient.Protocol;
using ICSharpCode.LanguageServerClient.Protocol.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace ICSharpCode.LanguageServerClient
{
	/// <summary>
	/// LSP 客户端，封装 JSON-RPC 通信并提供 LSP 特定的功能
	/// </summary>
	public class LspClient : IDisposable
	{
		/// <summary>
		/// JSON-RPC 客户端
		/// </summary>
		private JsonRpcClient rpcClient;

		/// <summary>
		/// 服务器能力（初始化后设置）
		/// </summary>
		private ServerCapabilities serverCapabilities;

		/// <summary>
		/// 是否已完成初始化握手
		/// </summary>
		private bool initialized;

		/// <summary>
		/// 是否已释放
		/// </summary>
		private bool disposed;

		/// <summary>
		/// 服务器能力
		/// </summary>
		public ServerCapabilities ServerCapabilities => serverCapabilities;

		/// <summary>
		/// 是否已初始化
		/// </summary>
		public bool IsInitialized => initialized;

		/// <summary>
		/// 是否已连接
		/// </summary>
		public bool IsConnected => rpcClient != null && rpcClient.IsConnected;

		/// <summary>
		/// 收到诊断信息时触发
		/// </summary>
		public event EventHandler<PublishDiagnosticsParams> DiagnosticsReceived;

		/// <summary>
		/// 收到日志消息时触发
		/// </summary>
		public event EventHandler<LogMessageParams> LogMessageReceived;

		/// <summary>
		/// 连接断开时触发
		/// </summary>
		public event EventHandler<EventArgs> ConnectionClosed;

		/// <summary>
		/// 使用已有的 JSON-RPC 客户端创建 LSP 客户端
		/// </summary>
		public LspClient(JsonRpcClient rpcClient)
		{
			this.rpcClient = rpcClient ?? throw new ArgumentNullException(nameof(rpcClient));
			this.rpcClient.NotificationReceived += OnNotificationReceived;
			this.rpcClient.ConnectionClosed += OnConnectionClosed;
		}

		/// <summary>
		/// 执行 LSP 初始化握手
		/// </summary>
		/// <param name="rootUri">工作区根目录 URI</param>
		/// <param name="initializationOptions">初始化选项</param>
		/// <param name="cancellationToken">取消令牌</param>
		/// <returns>初始化结果</returns>
		public async Task<InitializeResult> InitializeAsync(string rootUri, object initializationOptions = null, CancellationToken cancellationToken = default)
		{
			ThrowIfDisposed();

			var capabilities = CreateClientCapabilities();

			var initParams = new InitializeParams
			{
				ProcessId = Process.GetCurrentProcess().Id,
				RootUri = rootUri,
				RootPath = rootUri != null ? UriToFilePath(rootUri) : null,
				Capabilities = capabilities,
				InitializationOptions = initializationOptions,
				Trace = "off"
			};

			LoggingService.Info($"[LSP] 正在初始化语言服务器，根目录: {rootUri}");

			var result = await rpcClient.SendRequestAsync("initialize", initParams, cancellationToken).ConfigureAwait(false);

			var initializeResult = result.ToObject<InitializeResult>();
			if (initializeResult != null)
			{
				serverCapabilities = initializeResult.Capabilities;
				LoggingService.Info($"[LSP] 语言服务器已响应初始化: {initializeResult.ServerInfo?.Name} v{initializeResult.ServerInfo?.Version}");
			}

			// 发送 initialized 通知
			rpcClient.SendNotification("initialized", new { capabilities = new { } });
			initialized = true;

			LoggingService.Info("[LSP] 初始化握手完成");
			return initializeResult;
		}

		/// <summary>
		/// 发送 textDocument/didOpen 通知
		/// </summary>
		/// <param name="documentItem">文档项</param>
		public void DidOpen(TextDocumentItem documentItem)
		{
			ThrowIfDisposed();
			ThrowIfNotInitialized();

			rpcClient.SendNotification("textDocument/didOpen", new
			{
				textDocument = documentItem
			});

			LoggingService.Debug($"[LSP] 文档已打开: {documentItem.Uri}");
		}

		/// <summary>
		/// 发送 textDocument/didChange 通知
		/// </summary>
		/// <param name="uri">文档 URI</param>
		/// <param name="version">文档版本</param>
		/// <param name="changes">变更事件列表</param>
		public void DidChange(string uri, int version, List<TextDocumentContentChangeEvent> changes)
		{
			ThrowIfDisposed();
			ThrowIfNotInitialized();

			rpcClient.SendNotification("textDocument/didChange", new
			{
				textDocument = new VersionedTextDocumentIdentifier(uri, version),
				contentChanges = changes
			});
		}

		/// <summary>
		/// 发送 textDocument/didClose 通知
		/// </summary>
		/// <param name="uri">文档 URI</param>
		public void DidClose(string uri)
		{
			ThrowIfDisposed();
			ThrowIfNotInitialized();

			rpcClient.SendNotification("textDocument/didClose", new
			{
				textDocument = new TextDocumentIdentifier(uri)
			});

			LoggingService.Debug($"[LSP] 文档已关闭: {uri}");
		}

		/// <summary>
		/// 发送 textDocument/didSave 通知
		/// </summary>
		/// <param name="uri">文档 URI</param>
		/// <param name="text">保存时的文档文本（可选）</param>
		public void DidSave(string uri, string text = null)
		{
			ThrowIfDisposed();
			ThrowIfNotInitialized();

			var param = new
			{
				textDocument = new TextDocumentIdentifier(uri),
				text
			};

			rpcClient.SendNotification("textDocument/didSave", param);
		}

		/// <summary>
		/// 请求代码补全
		/// </summary>
		/// <param name="uri">文档 URI</param>
		/// <param name="position">请求补全的位置</param>
		/// <param name="triggerKind">触发方式</param>
		/// <param name="triggerCharacter">触发字符</param>
		/// <param name="cancellationToken">取消令牌</param>
		/// <returns>补全列表</returns>
		public async Task<CompletionList> CompletionAsync(string uri, Position position, int triggerKind = CompletionTriggerKind.Invoked, string triggerCharacter = null, CancellationToken cancellationToken = default)
		{
			ThrowIfDisposed();
			ThrowIfNotInitialized();

			var completionParams = new CompletionParams
			{
				TextDocument = new TextDocumentIdentifier(uri),
				Position = position,
				Context = new CompletionContext
				{
					TriggerKind = triggerKind,
					TriggerCharacter = triggerCharacter
				}
			};

			var result = await rpcClient.SendRequestAsync("textDocument/completion", completionParams, cancellationToken).ConfigureAwait(false);

			// 结果可能是 CompletionList 或 CompletionItem[]
			if (result == null)
				return new CompletionList();

			if (result.Type == JTokenType.Array)
			{
				var items = result.ToObject<List<CompletionItem>>();
				return new CompletionList
				{
					IsIncomplete = false,
					Items = items ?? new List<CompletionItem>()
				};
			}

			return result.ToObject<CompletionList>() ?? new CompletionList();
		}

		/// <summary>
		/// 请求悬停提示
		/// </summary>
		/// <param name="uri">文档 URI</param>
		/// <param name="position">请求悬停提示的位置</param>
		/// <param name="cancellationToken">取消令牌</param>
		/// <returns>悬停提示结果，如果无信息则为 null</returns>
		public async Task<Hover> HoverAsync(string uri, Position position, CancellationToken cancellationToken = default)
		{
			ThrowIfDisposed();
			ThrowIfNotInitialized();

			var hoverParams = new HoverParams
			{
				TextDocument = new TextDocumentIdentifier(uri),
				Position = position
			};

			var result = await rpcClient.SendRequestAsync("textDocument/hover", hoverParams, cancellationToken).ConfigureAwait(false);

			if (result == null || result.Type == JTokenType.Null)
				return null;

			return result.ToObject<Hover>();
		}

		/// <summary>
		/// 请求跳转到定义
		/// </summary>
		/// <param name="uri">文档 URI</param>
		/// <param name="position">请求定义的位置</param>
		/// <param name="cancellationToken">取消令牌</param>
		/// <returns>定义位置列表</returns>
		public async Task<List<Location>> GotoDefinitionAsync(string uri, Position position, CancellationToken cancellationToken = default)
		{
			ThrowIfDisposed();
			ThrowIfNotInitialized();

			var definitionParams = new DefinitionParams
			{
				TextDocument = new TextDocumentIdentifier(uri),
				Position = position
			};

			var result = await rpcClient.SendRequestAsync("textDocument/definition", definitionParams, cancellationToken).ConfigureAwait(false);

			return ParseLocationResult(result);
		}

		/// <summary>
		/// 请求查找引用
		/// </summary>
		/// <param name="uri">文档 URI</param>
		/// <param name="position">请求引用的位置</param>
		/// <param name="includeDeclaration">是否包含声明</param>
		/// <param name="cancellationToken">取消令牌</param>
		/// <returns>引用位置列表</returns>
		public async Task<List<Location>> FindReferencesAsync(string uri, Position position, bool includeDeclaration = true, CancellationToken cancellationToken = default)
		{
			ThrowIfDisposed();
			ThrowIfNotInitialized();

			var referenceParams = new ReferenceParams
			{
				TextDocument = new TextDocumentIdentifier(uri),
				Position = position,
				Context = new ReferenceContext
				{
					IncludeDeclaration = includeDeclaration
				}
			};

			var result = await rpcClient.SendRequestAsync("textDocument/references", referenceParams, cancellationToken).ConfigureAwait(false);

			return ParseLocationResult(result);
		}

		/// <summary>
		/// 请求关闭语言服务器
		/// </summary>
		public async Task ShutdownAsync()
		{
			ThrowIfDisposed();

			try
			{
				// 发送 shutdown 请求
				await rpcClient.SendRequestAsync("shutdown").ConfigureAwait(false);

				// 发送 exit 通知
				rpcClient.SendNotification("exit");

				LoggingService.Info("[LSP] 语言服务器已关闭");
			}
			catch (Exception ex)
			{
				LoggingService.Error($"[LSP] 关闭语言服务器时出错: {ex.Message}", ex);
			}
		}

		/// <summary>
		/// 解析位置结果（可能是 Location、Location[] 或 Location[][]）
		/// </summary>
		private List<Location> ParseLocationResult(JToken result)
		{
			if (result == null || result.Type == JTokenType.Null)
				return new List<Location>();

			// 单个 Location
			if (result.Type == JTokenType.Object && result["uri"] != null)
			{
				var location = result.ToObject<Location>();
				return location != null ? new List<Location> { location } : new List<Location>();
			}

			// Location 数组
			if (result.Type == JTokenType.Array)
			{
				// 可能是 Location[][] (用于 Definition 的变体)
				if (result.First != null && result.First.Type == JTokenType.Array)
				{
					var locations = new List<Location>();
					foreach (var inner in result)
					{
						if (inner.Type == JTokenType.Array)
						{
							locations.AddRange(inner.ToObject<List<Location>>() ?? new List<Location>());
						}
						else
						{
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
		/// 处理收到的通知
		/// </summary>
		private void OnNotificationReceived(object sender, JsonRpcNotification notification)
		{
			try
			{
				switch (notification.Method)
				{
					case "textDocument/publishDiagnostics":
						HandlePublishDiagnostics(notification.Params);
						break;

					case "window/logMessage":
						HandleLogMessage(notification.Params);
						break;

					case "window/showMessage":
						HandleShowMessage(notification.Params);
						break;

					case "window/showMessageRequest":
						HandleShowMessageRequest(notification.Params);
						break;

					default:
						LoggingService.Debug($"[LSP] 收到未处理的通知: {notification.Method}");
						break;
				}
			}
			catch (Exception ex)
			{
				LoggingService.Error($"[LSP] 处理通知 '{notification.Method}' 时出错: {ex.Message}", ex);
			}
		}

		/// <summary>
		/// 处理诊断信息通知
		/// </summary>
		private void HandlePublishDiagnostics(JToken parameters)
		{
			var diagnosticsParams = parameters.ToObject<PublishDiagnosticsParams>();
			if (diagnosticsParams != null)
			{
				LoggingService.Debug($"[LSP] 收到诊断信息: {diagnosticsParams.Uri} ({diagnosticsParams.Diagnostics.Count} 项)");
				DiagnosticsReceived?.Invoke(this, diagnosticsParams);
			}
		}

		/// <summary>
		/// 处理日志消息通知
		/// </summary>
		private void HandleLogMessage(JToken parameters)
		{
			var logParams = parameters.ToObject<LogMessageParams>();
			if (logParams != null)
			{
				LogMessageReceived?.Invoke(this, logParams);

				// 根据类型级别记录日志
				switch (logParams.Type)
				{
					case 1: LoggingService.Error($"[LSP Server] {logParams.Message}"); break;
					case 2: LoggingService.Warn($"[LSP Server] {logParams.Message}"); break;
					case 3: LoggingService.Info($"[LSP Server] {logParams.Message}"); break;
					case 4: LoggingService.Debug($"[LSP Server] {logParams.Message}"); break;
				}
			}
		}

		/// <summary>
		/// 处理显示消息通知
		/// </summary>
		private void HandleShowMessage(JToken parameters)
		{
			var msgParams = parameters.ToObject<ShowMessageParams>();
			if (msgParams != null)
			{
				LoggingService.Info($"[LSP Server Message] {msgParams.Message}");
			}
		}

		/// <summary>
		/// 处理显示消息请求
		/// </summary>
		private void HandleShowMessageRequest(JToken parameters)
		{
			var msgParams = parameters.ToObject<ShowMessageRequestParams>();
			if (msgParams != null)
			{
				LoggingService.Info($"[LSP Server Message Request] {msgParams.Message}");
			}
		}

		/// <summary>
		/// 连接断开处理
		/// </summary>
		private void OnConnectionClosed(object sender, EventArgs e)
		{
			initialized = false;
			ConnectionClosed?.Invoke(this, EventArgs.Empty);
		}

		/// <summary>
		/// 创建客户端能力声明
		/// </summary>
		private ClientCapabilities CreateClientCapabilities()
		{
			return new ClientCapabilities
			{
				Workspace = new WorkspaceClientCapabilities
				{
					ApplyEdit = true,
					DidChangeConfiguration = new DynamicCapability { DynamicRegistration = false },
					DidChangeWatchedFiles = new DynamicCapability { DynamicRegistration = false },
					Symbol = new DynamicCapability { DynamicRegistration = false },
					WorkspaceEdit = new WorkspaceEditCapability { DocumentChanges = true }
				},
				TextDocument = new TextDocumentClientCapabilities
				{
					Synchronization = new TextDocumentSyncCapability
					{
						DynamicRegistration = false,
						WillSave = true,
						WillSaveWaitUntil = false,
						DidSave = true
					},
					Completion = new CompletionCapability
					{
						DynamicRegistration = false,
						CompletionItem = new CompletionItemCapability
						{
							SnippetSupport = false,
							DocumentationFormat = new[] { "plaintext", "markdown" }
						},
						ContextSupport = true
					},
					Hover = new DynamicCapability { DynamicRegistration = false },
					SignatureHelp = new DynamicCapability { DynamicRegistration = false },
					Definition = new DynamicCapability { DynamicRegistration = false },
					References = new DynamicCapability { DynamicRegistration = false },
					DocumentHighlight = new DynamicCapability { DynamicRegistration = false },
					DocumentSymbol = new DynamicCapability { DynamicRegistration = false },
					Formatting = new DynamicCapability { DynamicRegistration = false },
					PublishDiagnostics = new PublishDiagnosticsCapability
					{
						RelatedInformation = true,
						TagSupport = new DiagnosticTagSupport
						{
							ValueSet = new[] { 1, 2 }
						}
					}
				},
				Window = new WindowClientCapabilities
				{
					ShowMessage = new DynamicCapability { DynamicRegistration = false },
					LogMessage = new DynamicCapability { DynamicRegistration = false }
				}
			};
		}

		/// <summary>
		/// 将 file:/// URI 转换为本地文件路径
		/// </summary>
		private string UriToFilePath(string uri)
		{
			if (string.IsNullOrEmpty(uri))
				return null;

			try
			{
				var uriObj = new Uri(uri);
				return uriObj.LocalPath;
			}
			catch
			{
				return uri;
			}
		}

		private void ThrowIfDisposed()
		{
			if (disposed)
				throw new ObjectDisposedException(nameof(LspClient));
		}

		private void ThrowIfNotInitialized()
		{
			if (!initialized)
				throw new InvalidOperationException("LSP 客户端尚未初始化");
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

				if (rpcClient != null)
				{
					rpcClient.NotificationReceived -= OnNotificationReceived;
					rpcClient.ConnectionClosed -= OnConnectionClosed;
					rpcClient.Dispose();
					rpcClient = null;
				}

				initialized = false;
			}
		}
	}

	/// <summary>
	/// 日志消息参数
	/// </summary>
	public class LogMessageParams
	{
		[JsonProperty("type")]
		public int Type { get; set; }

		[JsonProperty("message")]
		public string Message { get; set; }
	}

	/// <summary>
	/// 显示消息参数
	/// </summary>
	public class ShowMessageParams
	{
		[JsonProperty("type")]
		public int Type { get; set; }

		[JsonProperty("message")]
		public string Message { get; set; }
	}

	/// <summary>
	/// 显示消息请求参数
	/// </summary>
	public class ShowMessageRequestParams
	{
		[JsonProperty("type")]
		public int Type { get; set; }

		[JsonProperty("message")]
		public string Message { get; set; }

		[JsonProperty("actions", NullValueHandling = NullValueHandling.Ignore)]
		public List<MessageActionItem> Actions { get; set; }
	}

	/// <summary>
	/// 消息操作项
	/// </summary>
	public class MessageActionItem
	{
		[JsonProperty("title")]
		public string Title { get; set; }
	}
}
