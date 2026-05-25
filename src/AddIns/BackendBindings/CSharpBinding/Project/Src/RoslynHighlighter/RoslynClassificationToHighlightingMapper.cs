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
using ICSharpCode.AvalonEdit.Highlighting;
using Microsoft.CodeAnalysis.Classification;

namespace CSharpBinding.RoslynHighlighter
{
	/// <summary>
	/// 将 Roslyn 语义分类类型映射到 SharpDevelop 高亮颜色名称。
	/// Roslyn 使用 ClassificationTypeNames 中定义的字符串常量，
	/// SharpDevelop 使用 CSharp-Semantic.xshd 中定义的颜色名称。
	/// </summary>
	public static class RoslynClassificationToHighlightingMapper
	{
		/// <summary>
		/// 缓存高亮定义，避免重复查找
		/// </summary>
		static readonly IHighlightingDefinition csharpHighlighting;

		static RoslynClassificationToHighlightingMapper()
		{
			csharpHighlighting = HighlightingManager.Instance.GetDefinition("C#");
		}

		/// <summary>
		/// 将 Roslyn 分类类型名称映射为 SharpDevelop 的 HighlightingColor。
		/// 返回 null 表示该分类不需要语义高亮（由基础语法高亮处理）。
		/// </summary>
		public static HighlightingColor MapClassificationToColor(string classificationType)
		{
			if (classificationType == null)
				return null;

			string colorName = MapClassificationToColorName(classificationType);
			if (colorName == null)
				return null;

			if (csharpHighlighting != null) {
				var color = csharpHighlighting.GetNamedColor(colorName);
				if (color != null)
					return color;
			}

			return null;
		}

		/// <summary>
		/// 将 Roslyn 分类类型名称映射为 SharpDevelop 高亮颜色名称。
		/// 返回 null 表示该分类不需要语义高亮。
		/// </summary>
		static string MapClassificationToColorName(string classificationType)
		{
			switch (classificationType) {
				// 类型名称 → ReferenceTypes / ValueTypes / InterfaceTypes / EnumTypes / TypeParameters / DelegateTypes
				case ClassificationTypeNames.ClassName:
					return "ReferenceTypes";
				case ClassificationTypeNames.StructName:
					return "ValueTypes";
				case ClassificationTypeNames.InterfaceName:
					return "InterfaceTypes";
				case ClassificationTypeNames.EnumName:
					return "EnumTypes";
				case ClassificationTypeNames.TypeParameterName:
					return "TypeParameters";
				case ClassificationTypeNames.DelegateName:
					return "DelegateTypes";

				// 成员名称 → MethodCall / FieldAccess
				case ClassificationTypeNames.MethodName:
					return "MethodCall";
				case ClassificationTypeNames.PropertyName:
					// SharpDevelop 没有专门的属性颜色，使用默认
					return null;
				case ClassificationTypeNames.FieldName:
					return "FieldAccess";
				case ClassificationTypeNames.EventName:
					// SharpDevelop 没有专门的事件颜色，使用默认
					return null;

				// 变量和参数 → 使用默认（SharpDevelop 的 xshd 中没有专门的颜色）
				case ClassificationTypeNames.LocalName:
				case ClassificationTypeNames.ParameterName:
					return null;

				// 命名空间
				case ClassificationTypeNames.NamespaceName:
					return null;

				// 关键字 → 由基础语法高亮处理，不需要语义高亮
				case ClassificationTypeNames.Keyword:
				case ClassificationTypeNames.ControlKeyword:
					return null;

				// 字符串 → 由基础语法高亮处理
				case ClassificationTypeNames.StringLiteral:
				case ClassificationTypeNames.VerbatimStringLiteral:
					return null;

				// 数字 → 由基础语法高亮处理
				case ClassificationTypeNames.NumericLiteral:
					return null;

				// 注释 → 由基础语法高亮处理
				case ClassificationTypeNames.Comment:
					return null;

				// 预处理器 → 由基础语法高亮处理
				case ClassificationTypeNames.PreprocessorKeyword:
				case ClassificationTypeNames.PreprocessorText:
					return null;

				// 运算符和标点 → 由基础语法高亮处理
				case ClassificationTypeNames.Operator:
				case ClassificationTypeNames.OperatorOverloaded:
				case ClassificationTypeNames.Punctuation:
					return null;

				// 标识符 → 默认
				case ClassificationTypeNames.Identifier:
					return null;

				// 记录类型和模块（Roslyn 新增分类）
				case ClassificationTypeNames.RecordClassName:
					return "ReferenceTypes";
				case ClassificationTypeNames.RecordStructName:
					return "ValueTypes";

				// 扩展方法
				case ClassificationTypeNames.ExtensionMethodName:
					return "MethodCall";

				// 常量
				case ClassificationTypeNames.ConstantName:
					return "FieldAccess";

				// 枚举成员
				case ClassificationTypeNames.EnumMemberName:
					return "FieldAccess";

				// 静态符号
				case ClassificationTypeNames.StaticSymbol:
					return null;

				default:
					return null;
			}
		}
	}
}
