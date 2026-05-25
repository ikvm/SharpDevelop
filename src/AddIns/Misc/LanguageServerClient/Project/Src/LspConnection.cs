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
using ICSharpCode.LanguageServerClient.Protocol.Models;
using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace ICSharpCode.LanguageServerClient
{
	/// <summary>
	/// 管理与语言服务器的连接，包括进程生命周期和自动重连
	/// </summary>
	public class LspConnection : IDisposable
	{
		/// <summary>
		/// 语言服务器可执行文件路径
		/// </summary>
		private readonly string serverPath;

		/// <summary>
		/// 语言服务器启动参数
		/// </summary>
		private readonly string serverArguments;

		/// <summary>
		/// 工作区根目录路径
		/// </summary>
		private readonly string rootPath;

		/// <summary>
		/// 语言服务器进程
		/// </summary>
		private Process serverProcess;

		/// <summary>
		/// JSON-RPC 客户端
		/// </summary>
		private JsonRpcClient rpcClient;

		/// <summary>
		/// LSP 客户端
		/// </summary>
		private LspClient lspClient;

		/// <summary>
		/// 是否已释放
		/// </summary>
		private bool disposed;

		/// <summary>
		/// 是否正在重连
		/// </summary>
		private bool reconnecting;

		/// <summary>
		/// 最大重连次数
		/// </summary>
		private const int MaxReconnectAttempts = 3;

		/// <summary>
		/// 重连间隔（毫秒）
		/// </summary>
		private const int ReconnectDelayMs = 2000;

		/// <summary>
		/// 是否启用自动重连
		/// </summary>
		private readonly bool autoReconnect;

		/// <summary>
		/// LSP 客户端实例
		/// </summary>
		public LspClient Client => lspClient;

		/// <summary>
		/// 是否已连接
		/// </summary>
		public bool IsConnected => lspClient != null && lspClient.IsConnected;

		/// <summary>
		/// 是否已初始化
		/// </summary>
		public bool IsInitialized => lspClient != null && lspClient.IsInitialized;

		/// <summary>
		/// 连接状态变更时触发
		/// </summary>
		public event EventHandler<ConnectionStateChangedEventArgs> ConnectionStateChanged;

		/// <summary>
		/// 收到诊断信息时触发
		/// </summary>
		public event EventHandler<PublishDiagnosticsParams> DiagnosticsReceived;

		/// <summary>
		/// 创建 LSP 连接
		/// </summary>
		/// <param name="serverPath">语言服务器可执行文件路径</param>
		/// <param name="serverArguments">启动参数</param>
		/// <param name="rootPath">工作区根目录路径</param>
		/// <param name="autoReconnect">是否启用自动重连</param>
		public LspConnection(string serverPath, string serverArguments, string rootPath, bool autoReconnect = true)
		{
			if (string.IsNullOrEmpty(serverPath))
				throw new ArgumentNullException(nameof(serverPath));
			if (!File.Exists(serverPath))
				throw new FileNotFoundException($"找不到语言服务器: {serverPath}", serverPath);

			this.serverPath = serverPath;
			this.serverArguments = serverArguments ?? string.Empty;
			this.rootPath = rootPath;
			this.autoReconnect = autoReconnect;
		}

		/// <summary>
		/// 启动语言服务器并建立连接
		/// </summary>
		/// <param name="cancellationToken">取消令牌</param>
		public async Task StartAsync(CancellationToken cancellationToken = default)
		{
			ThrowIfDisposed();

			LoggingService.Info($"[LSP] 正在启动语言服务器: {serverPath} {serverArguments}");

			// 启动语言服务器进程
			serverProcess = CreateServerProcess();

			try
			{
				serverProcess.Start();
				LoggingService.Info($"[LSP] 语言服务器进程已启动 (PID: {serverProcess.Id})");
			}
			catch (Exception ex)
			{
				LoggingService.Error($"[LSP] 启动语言服务器失败: {ex.Message}", ex);
				OnConnectionStateChanged(ConnectionState.Failed, $"启动失败: {ex.Message}");
				throw;
			}

			// 创建 JSON-RPC 客户端并绑定到进程
			rpcClient = new JsonRpcClient();
			rpcClient.Attach(serverProcess);

			// 创建 LSP 客户端
			lspClient = new LspClient(rpcClient);
			lspClient.DiagnosticsReceived += OnDiagnosticsReceived;
			lspClient.ConnectionClosed += OnConnectionClosed;

			// 监听进程退出事件
			serverProcess.EnableRaisingEvents = true;
			serverProcess.Exited += OnServerProcessExited;

			OnConnectionStateChanged(ConnectionState.Connecting, "正在连接语言服务器...");

			// 执行初始化握手
			try
			{
				var rootUri = FilePathToUri(rootPath);
				var result = await lspClient.InitializeAsync(rootUri, null, cancellationToken).ConfigureAwait(false);

				if (result != null)
				{
					OnConnectionStateChanged(ConnectionState.Connected, $"已连接到 {result.ServerInfo?.Name ?? "语言服务器"}");
				}
				else
				{
					OnConnectionStateChanged(ConnectionState.Connected, "已连接到语言服务器");
				}
			}
			catch (OperationCanceledException)
			{
				LoggingService.Info("[LSP] 初始化被取消");
				OnConnectionStateChanged(ConnectionState.Failed, "初始化被取消");
				throw;
			}
			catch (Exception ex)
			{
				LoggingService.Error($"[LSP] 初始化失败: {ex.Message}", ex);
				OnConnectionStateChanged(ConnectionState.Failed, $"初始化失败: {ex.Message}");
				throw;
			}
		}

		/// <summary>
		/// 关闭连接并停止语言服务器
		/// </summary>
		public async Task StopAsync()
		{
			if (lspClient != null && lspClient.IsInitialized)
			{
				try
				{
					await lspClient.ShutdownAsync().ConfigureAwait(false);
				}
				catch (Exception ex)
				{
					LoggingService.Error($"[LSP] 关闭语言服务器时出错: {ex.Message}", ex);
				}
			}

			KillServerProcess();
			OnConnectionStateChanged(ConnectionState.Disconnected, "已断开连接");
		}

		/// <summary>
		/// 创建语言服务器进程
		/// </summary>
		private Process CreateServerProcess()
		{
			var startInfo = new ProcessStartInfo
			{
				FileName = serverPath,
				Arguments = serverArguments,
				UseShellExecute = false,
				RedirectStandardInput = true,
				RedirectStandardOutput = true,
				RedirectStandardError = true,
				CreateNoWindow = true,
				StandardOutputEncoding = System.Text.Encoding.UTF8
			};

			// 设置工作目录
			if (!string.IsNullOrEmpty(rootPath) && Directory.Exists(rootPath))
			{
				startInfo.WorkingDirectory = rootPath;
			}

			var process = new Process
			{
				StartInfo = startInfo,
				EnableRaisingEvents = true
			};

			// 捕获标准错误输出
			process.ErrorDataReceived += (sender, e) =>
			{
				if (!string.IsNullOrEmpty(e.Data))
				{
					LoggingService.Debug($"[LSP Server stderr] {e.Data}");
				}
			};

			return process;
		}

		/// <summary>
		/// 语言服务器进程退出处理
		/// </summary>
		private void OnServerProcessExited(object sender, EventArgs e)
		{
			var exitCode = serverProcess?.ExitCode ?? -1;
			LoggingService.Info($"[LSP] 语言服务器进程已退出 (退出码: {exitCode})");

			if (!disposed && autoReconnect && !reconnecting)
			{
				TryReconnect();
			}
			else
			{
				OnConnectionStateChanged(ConnectionState.Disconnected, $"语言服务器已退出 (退出码: {exitCode})");
			}
		}

		/// <summary>
		/// 连接断开处理
		/// </summary>
		private void OnConnectionClosed(object sender, EventArgs e)
		{
			if (!disposed && autoReconnect && !reconnecting)
			{
				TryReconnect();
			}
			else
			{
				OnConnectionStateChanged(ConnectionState.Disconnected, "连接已断开");
			}
		}

		/// <summary>
		/// 尝试自动重连
		/// </summary>
		private async void TryReconnect()
		{
			if (reconnecting || disposed)
				return;

			reconnecting = true;
			OnConnectionStateChanged(ConnectionState.Reconnecting, "正在尝试重新连接...");

			for (int attempt = 1; attempt <= MaxReconnectAttempts; attempt++)
			{
				if (disposed)
					break;

				LoggingService.Info($"[LSP] 重连尝试 {attempt}/{MaxReconnectAttempts}...");

				try
				{
					// 清理旧连接
					CleanupOldConnection();

					// 等待一段时间后重试
					await Task.Delay(ReconnectDelayMs).ConfigureAwait(false);

					if (disposed)
						break;

					// 重新启动
					await StartAsync().ConfigureAwait(false);

					LoggingService.Info("[LSP] 重连成功");
					reconnecting = false;
					return;
				}
				catch (Exception ex)
				{
					LoggingService.Error($"[LSP] 重连尝试 {attempt} 失败: {ex.Message}", ex);
				}
			}

			reconnecting = false;
			LoggingService.Error("[LSP] 重连失败，已达到最大重试次数");
			OnConnectionStateChanged(ConnectionState.Failed, "重连失败");
		}

		/// <summary>
		/// 清理旧连接
		/// </summary>
		private void CleanupOldConnection()
		{
			if (lspClient != null)
			{
				lspClient.DiagnosticsReceived -= OnDiagnosticsReceived;
				lspClient.ConnectionClosed -= OnConnectionClosed;
				lspClient.Dispose();
				lspClient = null;
			}

			if (rpcClient != null)
			{
				rpcClient.Dispose();
				rpcClient = null;
			}

			KillServerProcess();
		}

		/// <summary>
		/// 终止语言服务器进程
		/// </summary>
		private void KillServerProcess()
		{
			if (serverProcess != null && !serverProcess.HasExited)
			{
				try
				{
					serverProcess.Kill();
					LoggingService.Info("[LSP] 语言服务器进程已终止");
				}
				catch (Exception ex)
				{
					LoggingService.Error($"[LSP] 终止语言服务器进程时出错: {ex.Message}", ex);
				}
			}

			if (serverProcess != null)
			{
				serverProcess.Exited -= OnServerProcessExited;
				serverProcess.ErrorDataReceived -= null;
				serverProcess.Dispose();
				serverProcess = null;
			}
		}

		/// <summary>
		/// 转发诊断信息事件
		/// </summary>
		private void OnDiagnosticsReceived(object sender, PublishDiagnosticsParams e)
		{
			DiagnosticsReceived?.Invoke(this, e);
		}

		/// <summary>
		/// 触发连接状态变更事件
		/// </summary>
		private void OnConnectionStateChanged(ConnectionState state, string message)
		{
			ConnectionStateChanged?.Invoke(this, new ConnectionStateChangedEventArgs(state, message));
		}

		/// <summary>
		/// 将文件路径转换为 file:/// URI
		/// </summary>
		public static string FilePathToUri(string filePath)
		{
			if (string.IsNullOrEmpty(filePath))
				return null;

			var fullPath = Path.GetFullPath(filePath);
			var uri = new Uri(fullPath);
			return uri.AbsoluteUri;
		}

		private void ThrowIfDisposed()
		{
			if (disposed)
				throw new ObjectDisposedException(nameof(LspConnection));
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
				CleanupOldConnection();
			}
		}
	}

	/// <summary>
	/// 连接状态
	/// </summary>
	public enum ConnectionState
	{
		/// <summary>未连接</summary>
		Disconnected,
		/// <summary>正在连接</summary>
		Connecting,
		/// <summary>已连接</summary>
		Connected,
		/// <summary>正在重连</summary>
		Reconnecting,
		/// <summary>连接失败</summary>
		Failed
	}

	/// <summary>
	/// 连接状态变更事件参数
	/// </summary>
	public class ConnectionStateChangedEventArgs : EventArgs
	{
		/// <summary>
		/// 新的连接状态
		/// </summary>
		public ConnectionState State { get; }

		/// <summary>
		/// 状态描述消息
		/// </summary>
		public string Message { get; }

		public ConnectionStateChangedEventArgs(ConnectionState state, string message)
		{
			State = state;
			Message = message;
		}
	}
}
