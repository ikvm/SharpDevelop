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
	/// 带版本号的文本文档标识符
	/// </summary>
	public class VersionedTextDocumentIdentifier
	{
		/// <summary>
		/// 文档的 URI（file:/// 格式）
		/// </summary>
		[JsonProperty("uri")]
		public string Uri { get; set; }

		/// <summary>
		/// 文档版本号，从 1 开始递增
		/// </summary>
		[JsonProperty("version")]
		public int Version { get; set; }

		public VersionedTextDocumentIdentifier() { }

		public VersionedTextDocumentIdentifier(string uri, int version)
		{
			Uri = uri;
			Version = version;
		}
	}
}
