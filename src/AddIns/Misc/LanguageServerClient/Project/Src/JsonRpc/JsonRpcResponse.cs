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
using Newtonsoft.Json.Linq;

namespace ICSharpCode.LanguageServerClient.JsonRpc
{
	/// <summary>
	/// JSON-RPC 2.0 响应消息
	/// </summary>
	public class JsonRpcResponse : JsonRpcMessage
	{
		/// <summary>
		/// 对应请求的标识符
		/// </summary>
		[JsonProperty("id")]
		public int Id { get; set; }

		/// <summary>
		/// 请求的结果，与 error 互斥
		/// </summary>
		[JsonProperty("result", NullValueHandling = NullValueHandling.Include)]
		public JToken Result { get; set; }

		/// <summary>
		/// 错误信息，与 result 互斥
		/// </summary>
		[JsonProperty("error", NullValueHandling = NullValueHandling.Ignore)]
		public JsonRpcError Error { get; set; }
	}
}
