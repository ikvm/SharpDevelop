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

namespace ICSharpCode.LanguageServerClient.JsonRpc
{
	/// <summary>
	/// JSON-RPC 2.0 错误对象
	/// </summary>
	public class JsonRpcError
	{
		/// <summary>
		/// 错误代码
		/// </summary>
		[JsonProperty("code")]
		public int Code { get; set; }

		/// <summary>
		/// 错误消息
		/// </summary>
		[JsonProperty("message")]
		public string Message { get; set; }

		/// <summary>
		/// 附加错误数据
		/// </summary>
		[JsonProperty("data", NullValueHandling = NullValueHandling.Ignore)]
		public object Data { get; set; }
	}

	/// <summary>
	/// JSON-RPC 标准错误代码
	/// </summary>
	public static class JsonRpcErrorCodes
	{
		/// <summary>解析错误：服务端接收到无效的 JSON</summary>
		public const int ParseError = -32700;
		/// <summary>无效请求：发送的 JSON 不是一个有效的请求对象</summary>
		public const int InvalidRequest = -32600;
		/// <summary>方法未找到：该方法不存在或不可用</summary>
		public const int MethodNotFound = -32601;
		/// <summary>无效参数：无效的方法参数</summary>
		public const int InvalidParams = -32602;
		/// <summary>内部错误：JSON-RPC 内部错误</summary>
		public const int InternalError = -32603;

		/// <summary>请求被取消</summary>
		public const int ServerCancelled = -32800;
		/// <summary>内容已修改</summary>
		public const int ContentModified = -32801;
		/// <summary>请求失败</summary>
		public const int RequestFailed = -32803;
		/// <summary>服务端取消</summary>
		public const int ServerEnd = -32802;
	}
}
