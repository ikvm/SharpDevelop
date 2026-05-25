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
using ICSharpCode.Core;

namespace ICSharpCode.LanguageServerClient.Adapters
{
	/// <summary>
	/// LSP 服务单例，提供对当前活跃 LSP 连接的访问。
	/// 适配器层通过此类获取 LspClient 实例。
	/// </summary>
	public class LspService
	{
		static readonly Lazy<LspService> instance = new Lazy<LspService>(() => new LspService());

		/// <summary>
		/// LSP 服务单例实例
		/// </summary>
		public static LspService Instance => instance.Value;

		/// <summary>
		/// 当前的 LSP 连接
		/// </summary>
		private LspConnection connection;

		/// <summary>
		/// 获取当前的 LSP 客户端
		/// </summary>
		/// <returns>LSP 客户端实例，如果未连接则返回 null</returns>
		public LspClient GetClient()
		{
			return connection?.Client;
		}

		/// <summary>
		/// 设置当前的 LSP 连接
		/// </summary>
		/// <param name="lspConnection">LSP 连接实例</param>
		public void SetConnection(LspConnection lspConnection)
		{
			connection = lspConnection;
			if (lspConnection != null) {
				LoggingService.Info("[LSP] LspService 已设置连接");
			} else {
				LoggingService.Info("[LSP] LspService 连接已清除");
			}
		}

		/// <summary>
		/// 获取当前的 LSP 连接
		/// </summary>
		public LspConnection Connection => connection;

		/// <summary>
		/// 是否已连接到语言服务器
		/// </summary>
		public bool IsConnected => connection != null && connection.IsConnected;
	}
}
