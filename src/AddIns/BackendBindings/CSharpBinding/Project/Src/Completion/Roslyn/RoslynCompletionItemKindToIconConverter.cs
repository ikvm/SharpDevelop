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
using System.Linq;

using ICSharpCode.SharpDevelop;

using Microsoft.CodeAnalysis.Completion;

namespace CSharpBinding.Completion.Roslyn
{
	/// <summary>
	/// 将 Roslyn CompletionItem 的 Tags 映射为 SharpDevelop 的图标。
	/// Roslyn 使用标签（如 "Class"、"Method"、"Property" 等）来标识补全项的类型，
	/// 本类将这些标签映射到 ClassBrowserIconService 提供的图标。
	/// </summary>
	public static class RoslynCompletionItemKindToIconConverter
	{
		/// <summary>
		/// 根据 Roslyn CompletionItem 的 Tags 获取对应的 SD 图标。
		/// Roslyn 的 CompletionItem.Tags 包含如 "Class"、"Method"、"Property" 等标签，
		/// 用于标识补全项的符号类型。
		/// </summary>
		public static IImage GetIcon(CompletionItem item)
		{
			if (item == null)
				return ClassBrowserIconService.Keyword;

			var tags = item.Tags;
			if (tags == null)
				return ClassBrowserIconService.Keyword;

			// Roslyn CompletionItem 的 Tags 可能包含以下标签：
			// Class, Struct, Interface, Enum, Delegate, Method, Property, Field,
			// Event, Namespace, Keyword, Snippet, Parameter, Local, Label, Constant,
			// EnumMember, ExtensionMethod, Module, TypeParameter, etc.

			// 优先匹配更具体的标签
			if (tags.Contains("ExtensionMethod"))
				return ClassBrowserIconService.Method;

			if (tags.Contains("EnumMember"))
				return ClassBrowserIconService.Const;

			if (tags.Contains("Constant"))
				return ClassBrowserIconService.Const;

			// 匹配类型标签
			if (tags.Contains("Class"))
				return ClassBrowserIconService.Class;

			if (tags.Contains("Struct"))
				return ClassBrowserIconService.Struct;

			if (tags.Contains("Interface"))
				return ClassBrowserIconService.Interface;

			if (tags.Contains("Enum"))
				return ClassBrowserIconService.Enum;

			if (tags.Contains("Delegate"))
				return ClassBrowserIconService.Delegate;

			// 匹配成员标签
			if (tags.Contains("Method"))
				return ClassBrowserIconService.Method;

			if (tags.Contains("Property"))
				return ClassBrowserIconService.Property;

			if (tags.Contains("Field"))
				return ClassBrowserIconService.Field;

			if (tags.Contains("Event"))
				return ClassBrowserIconService.Event;

			if (tags.Contains("Indexer"))
				return ClassBrowserIconService.Indexer;

			// 匹配其他标签
			if (tags.Contains("Namespace"))
				return ClassBrowserIconService.Namespace;

			if (tags.Contains("Parameter"))
				return ClassBrowserIconService.Parameter;

			if (tags.Contains("Local"))
				return ClassBrowserIconService.LocalVariable;

			if (tags.Contains("Keyword"))
				return ClassBrowserIconService.Keyword;

			if (tags.Contains("Snippet"))
				return ClassBrowserIconService.CodeTemplate;

			if (tags.Contains("TypeParameter"))
				return ClassBrowserIconService.Class;

			// 默认图标
			return ClassBrowserIconService.Keyword;
		}
	}
}
