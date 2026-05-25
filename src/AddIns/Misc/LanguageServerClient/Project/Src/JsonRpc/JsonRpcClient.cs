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
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ICSharpCode.LanguageServerClient.JsonRpc
{
	/// <summary>
	/// JSON-RPC 2.0 客户端，通过 stdio 与语言服务器通信
	/// 实现 LSP 规范的 Content-Length 帧协议
	/// </summary>
	public class JsonRpcClient : IDisposable
	{
		/// <summary>
		/// Content-Length 头部前缀
		/// </summary>
		private const string ContentLengthHeader = "Content-Length: ";

		/// <summary>
		/// 消息分隔符
		/// </summary>
		private static readonly byte[] HeaderSeparator = Encoding.ASCII.GetBytes("\r\n\r\n");

		/// <summary>
		/// JSON 序列化设置
		/// </summary>
		private static readonly JsonSerializerSettings SerializerSettings = new JsonSerializerSettings
		{
			NullValueHandling = NullValueHandling.Ignore,
			Formatting = Formatting.None
		};

		/// <summary>
		/// 待处理的请求字典（线程安全）
		/// </summary>
		private readonly ConcurrentDictionary<int, PendingRequest> pendingRequests = new ConcurrentDictionary<int, PendingRequest>();

		/// <summary>
		/// 请求 ID 计数器
		/// </summary>
		private int nextRequestId = 0;

		/// <summary>
		/// 语言服务器进程
		/// </summary>
		private Process serverProcess;

		/// <summary>
		/// 消息读取线程
		/// </summary>
		private Thread readerThread;

		/// <summary>
		/// 写锁，保证消息发送的原子性
		/// </summary>
		private readonly object writeLock = new object();

		/// <summary>
		/// 是否已释放
		/// </summary>
		private bool disposed;

		/// <summary>
		/// 是否已连接
		/// </summary>
		public bool IsConnected => serverProcess != null && !serverProcess.HasExited;

		/// <summary>
		/// 收到通知消息时触发
		/// </summary>
		public event EventHandler<JsonRpcNotification> NotificationReceived;

		/// <summary>
		/// 连接断开时触发
		/// </summary>
		public event EventHandler<EventArgs> ConnectionClosed;

		/// <summary>
		/// 日志消息事件
		/// </summary>
		public event EventHandler<string> LogMessage;

		/// <summary>
		/// 将语言服务器进程绑定到此客户端
		/// </summary>
		/// <param name="process">已启动的语言服务器进程</param>
		public void Attach(Process process)
		{
			if (process == null)
				throw new ArgumentNullException(nameof(process));

			serverProcess = process;

			// 启动后台读取线程
			readerThread = new Thread(ReadMessages)
			{
				IsBackground = true,
				Name = "LSP JSON-RPC Reader"
			};
			readerThread.Start();

			Log("已连接到语言服务器进程 (PID: " + process.Id + ")");
		}

		/// <summary>
		/// 发送请求并等待响应
		/// </summary>
		/// <param name="method">方法名称</param>
		/// <param name="parameters">方法参数</param>
		/// <param name="cancellationToken">取消令牌</param>
		/// <returns>响应结果</returns>
		public Task<JToken> SendRequestAsync(string method, object parameters = null, CancellationToken cancellationToken = default)
		{
			ThrowIfDisposed();
			ThrowIfNotConnected();

			int requestId = Interlocked.Increment(ref nextRequestId);
			var request = new JsonRpcRequest
			{
				Id = requestId,
				Method = method,
				Params = parameters != null ? JToken.FromObject(parameters) : null
			};

			var pending = new PendingRequest
			{
				Id = requestId,
				Method = method,
				CompletionSource = new TaskCompletionSource<JToken>()
			};

			pendingRequests[requestId] = pending;

			// 注册取消回调
			if (cancellationToken.CanBeCanceled)
			{
				cancellationToken.Register(() =>
				{
					if (pendingRequests.TryRemove(requestId, out var removed))
					{
						removed.CompletionSource.TrySetCanceled(cancellationToken);
					}
					// 发送取消通知
					TrySendNotification("$/cancelRequest", new { id = requestId });
				});
			}

			try
			{
				SendMessage(request);
				Log($"发送请求: {method} (id={requestId})");
			}
			catch (Exception ex)
			{
				pendingRequests.TryRemove(requestId, out _);
				pending.CompletionSource.TrySetException(ex);
			}

			return pending.CompletionSource.Task;
		}

		/// <summary>
		/// 发送通知（不需要响应）
		/// </summary>
		/// <param name="method">方法名称</param>
		/// <param name="parameters">方法参数</param>
		public void SendNotification(string method, object parameters = null)
		{
			ThrowIfDisposed();
			ThrowIfNotConnected();

			var notification = new JsonRpcNotification
			{
				Method = method,
				Params = parameters != null ? JToken.FromObject(parameters) : null
			};

			SendMessage(notification);
			Log($"发送通知: {method}");
		}

		/// <summary>
		/// 尝试发送通知，忽略异常
		/// </summary>
		private void TrySendNotification(string method, object parameters = null)
		{
			try
			{
				if (IsConnected)
					SendNotification(method, parameters);
			}
			catch
			{
				// 忽略发送失败
			}
		}

		/// <summary>
		/// 发送消息到语言服务器
		/// </summary>
		private void SendMessage(JsonRpcMessage message)
		{
			var json = JsonConvert.SerializeObject(message, SerializerSettings);
			var contentBytes = Encoding.UTF8.GetBytes(json);
			var header = ContentLengthHeader + contentBytes.Length + "\r\n\r\n";
			var headerBytes = Encoding.ASCII.GetBytes(header);

			lock (writeLock)
			{
				var stream = serverProcess.StandardInput.BaseStream;
				stream.Write(headerBytes, 0, headerBytes.Length);
				stream.Write(contentBytes, 0, contentBytes.Length);
				stream.Flush();
			}
		}

		/// <summary>
		/// 后台线程：持续读取来自语言服务器的消息
		/// </summary>
		private void ReadMessages()
		{
			try
			{
				var stream = serverProcess.StandardOutput.BaseStream;

				while (!disposed && IsConnected)
				{
					var message = ReadMessage(stream);
					if (message == null)
						break;

					ProcessMessage(message);
				}
			}
			catch (Exception ex)
			{
				if (!disposed)
				{
					Log($"读取消息异常: {ex.Message}");
				}
			}
			finally
			{
				// 通知所有待处理请求连接已断开
				FailAllPendingRequests(new IOException("与语言服务器的连接已断开"));
				ConnectionClosed?.Invoke(this, EventArgs.Empty);
			}
		}

		/// <summary>
		/// 从流中读取一条完整的 JSON-RPC 消息
		/// </summary>
		private JObject ReadMessage(Stream stream)
		{
			// 读取 Content-Length 头部
			int contentLength = ReadContentLength(stream);
			if (contentLength < 0)
				return null;

			// 读取消息内容
			var contentBuffer = new byte[contentLength];
			int totalRead = 0;
			while (totalRead < contentLength)
			{
				int bytesRead = stream.Read(contentBuffer, totalRead, contentLength - totalRead);
				if (bytesRead == 0)
					return null;
				totalRead += bytesRead;
			}

			var json = Encoding.UTF8.GetString(contentBuffer);
			return JObject.Parse(json);
		}

		/// <summary>
		/// 读取 Content-Length 头部值
		/// </summary>
		private int ReadContentLength(Stream stream)
		{
			// 逐字节读取头部，直到找到 \r\n\r\n 分隔符
			var headerBuilder = new StringBuilder();
			int separatorMatchPos = 0;

			while (separatorMatchPos < 4)
			{
				int b = stream.ReadByte();
				if (b < 0)
					return -1;

				if (b == HeaderSeparator[separatorMatchPos])
				{
					separatorMatchPos++;
				}
				else
				{
					separatorMatchPos = 0;
					headerBuilder.Append((char)b);
				}
			}

			// 解析头部字符串
			var headerString = headerBuilder.ToString();
			foreach (var line in headerString.Split(new[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries))
			{
				if (line.StartsWith(ContentLengthHeader, StringComparison.OrdinalIgnoreCase))
				{
					var lengthStr = line.Substring(ContentLengthHeader.Length);
					if (int.TryParse(lengthStr, out int contentLength))
						return contentLength;
				}
			}

			Log($"无法解析 Content-Length 头部: {headerString}");
			return -1;
		}

		/// <summary>
		/// 处理收到的消息
		/// </summary>
		private void ProcessMessage(JObject message)
		{
			// 判断消息类型：有 id 且有 result/error 的是响应，否则是通知
			if (message.ContainsKey("id") && (message.ContainsKey("result") || message.ContainsKey("error")))
			{
				// 响应消息
				var response = message.ToObject<JsonRpcResponse>();
				if (response != null)
				{
					HandleResponse(response);
				}
			}
			else if (message.ContainsKey("method"))
			{
				// 通知消息
				var notification = message.ToObject<JsonRpcNotification>();
				if (notification != null)
				{
					Log($"收到通知: {notification.Method}");
					NotificationReceived?.Invoke(this, notification);
				}
			}
			else
			{
				Log($"收到未知消息类型: {message}");
			}
		}

		/// <summary>
		/// 处理响应消息
		/// </summary>
		private void HandleResponse(JsonRpcResponse response)
		{
			if (pendingRequests.TryRemove(response.Id, out var pending))
			{
				if (response.Error != null)
				{
					var errorMessage = $"请求 '{pending.Method}' 失败: [{response.Error.Code}] {response.Error.Message}";
					Log(errorMessage);
					pending.CompletionSource.TrySetException(new JsonRpcException(response.Error.Code, response.Error.Message, response.Error.Data));
				}
				else
				{
					Log($"收到响应: {pending.Method} (id={response.Id})");
					pending.CompletionSource.TrySetResult(response.Result);
				}
			}
			else
			{
				Log($"收到未知请求 ID 的响应: {response.Id}");
			}
		}

		/// <summary>
		/// 使所有待处理请求失败
		/// </summary>
		private void FailAllPendingRequests(Exception exception)
		{
			foreach (var kvp in pendingRequests)
			{
				if (pendingRequests.TryRemove(kvp.Key, out var pending))
				{
					pending.CompletionSource.TrySetException(exception);
				}
			}
		}

		/// <summary>
		/// 记录日志
		/// </summary>
		private void Log(string message)
		{
			LoggingService.Debug($"[LSP JSON-RPC] {message}");
			LogMessage?.Invoke(this, message);
		}

		private void ThrowIfDisposed()
		{
			if (disposed)
				throw new ObjectDisposedException(nameof(JsonRpcClient));
		}

		private void ThrowIfNotConnected()
		{
			if (!IsConnected)
				throw new InvalidOperationException("未连接到语言服务器");
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

				// 使所有待处理请求失败
				FailAllPendingRequests(new ObjectDisposedException(nameof(JsonRpcClient)));

				// 关闭服务器进程的输入流
				try
				{
					if (serverProcess != null && !serverProcess.HasExited)
					{
						serverProcess.StandardInput.Close();
					}
				}
				catch { }

				// 等待读取线程结束
				if (readerThread != null && readerThread.IsAlive)
				{
					if (!readerThread.Join(3000))
					{
						Log("读取线程未能在超时内结束");
					}
				}
			}
		}

		/// <summary>
		/// 待处理请求信息
		/// </summary>
		private class PendingRequest
		{
			public int Id { get; set; }
			public string Method { get; set; }
			public TaskCompletionSource<JToken> CompletionSource { get; set; }
		}
	}

	/// <summary>
	/// JSON-RPC 异常
	/// </summary>
	public class JsonRpcException : Exception
	{
		/// <summary>
		/// JSON-RPC 错误代码
		/// </summary>
		public int Code { get; }

		/// <summary>
		/// 附加错误数据
		/// </summary>
		public new object Data { get; }

		public JsonRpcException(int code, string message, object data = null)
			: base(message)
		{
			Code = code;
			Data = data;
		}
	}
}
