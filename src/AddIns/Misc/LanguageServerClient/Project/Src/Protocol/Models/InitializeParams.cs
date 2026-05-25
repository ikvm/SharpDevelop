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

using Newtonsoft.Json;

namespace ICSharpCode.LanguageServerClient.Protocol.Models
{
	/// <summary>
	/// LSP 初始化请求参数
	/// </summary>
	public class InitializeParams
	{
		/// <summary>
		/// 客户端进程 ID
		/// </summary>
		[JsonProperty("processId")]
		public int? ProcessId { get; set; }

		/// <summary>
		/// 客户端支持的工作区文件夹
		/// </summary>
		[JsonProperty("rootUri", NullValueHandling = NullValueHandling.Include)]
		public string RootUri { get; set; }

		/// <summary>
		/// 客户端能力
		/// </summary>
		[JsonProperty("capabilities")]
		public ClientCapabilities Capabilities { get; set; }

		/// <summary>
		/// 工作区根路径（已弃用，保留兼容性）
		/// </summary>
		[JsonProperty("rootPath", NullValueHandling = NullValueHandling.Ignore)]
		public string RootPath { get; set; }

		/// <summary>
		/// 初始化选项，由语言服务器定义
		/// </summary>
		[JsonProperty("initializationOptions", NullValueHandling = NullValueHandling.Ignore)]
		public object InitializationOptions { get; set; }

		/// <summary>
		/// 客户端追踪设置
		/// </summary>
		[JsonProperty("trace", NullValueHandling = NullValueHandling.Ignore)]
		public string Trace { get; set; }
	}
}
