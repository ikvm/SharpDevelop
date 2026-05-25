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
	/// 文档中的位置（0-based 行和字符）
	/// </summary>
	public class Position
	{
		/// <summary>
		/// 行号，从 0 开始
		/// </summary>
		[JsonProperty("line")]
		public int Line { get; set; }

		/// <summary>
		/// 字符偏移，从 0 开始（按 UTF-16 代码单元计算）
		/// </summary>
		[JsonProperty("character")]
		public int Character { get; set; }

		public Position() { }

		public Position(int line, int character)
		{
			Line = line;
			Character = character;
		}

		public override string ToString() => $"({Line},{Character})";
	}
}
