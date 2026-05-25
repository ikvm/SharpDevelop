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
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Highlighting;
using ICSharpCode.AvalonEdit.Indentation.CSharp;
using ICSharpCode.Core;
using ICSharpCode.NRefactory.TypeSystem;
using ICSharpCode.SharpDevelop;
using ICSharpCode.SharpDevelop.Editor;
using ICSharpCode.SharpDevelop.Project;

namespace CSharpBinding.FormattingStrategy.Roslyn
{
	/// <summary>
	/// 基于 Roslyn 的 C# 格式化策略
	/// 使用 Microsoft.CodeAnalysis.Formatting 进行代码格式化，
	/// 同时保留原有的智能缩进和行格式化逻辑。
	/// </summary>
	public class RoslynFormattingStrategy : DefaultFormattingStrategy
	{
		#region 智能缩进

		public override void IndentLine(ITextEditor editor, IDocumentLine line)
		{
			int lineNr = line.LineNumber;
			DocumentAccessor acc = new DocumentAccessor(editor.Document, lineNr, lineNr);

			CSharpIndentationStrategy indentStrategy = new CSharpIndentationStrategy();
			indentStrategy.IndentationString = GetIndentationString(editor);
			indentStrategy.Indent(acc, false);

			string t = acc.Text;
			if (t.Length == 0) {
				// 对于注释或逐字字符串中的新行，使用自动缩进
				base.IndentLine(editor, line);
			}
		}

		public override void IndentLines(ITextEditor editor, int beginLine, int endLine)
		{
			DocumentAccessor acc = new DocumentAccessor(editor.Document, beginLine, endLine);
			CSharpIndentationStrategy indentStrategy = new CSharpIndentationStrategy();
			indentStrategy.IndentationString = GetIndentationString(editor);
			indentStrategy.Indent(acc, true);
		}

		CSharpFormattingOptionsContainer GetOptionsContainerForEditor(ITextEditor editor)
		{
			var currentProject = SD.ProjectService.FindProjectContainingFile(editor.FileName);
			if (currentProject != null) {
				var persistence = CSharpFormattingPolicies.Instance.GetProjectOptions(currentProject);
				if (persistence != null) {
					return persistence.OptionsContainer;
				}
			}

			return null;
		}

		string GetIndentationString(ITextEditor editor)
		{
			// 获取当前缩进选项值
			int indentationSize = editor.Options.IndentationSize;
			bool convertTabsToSpaces = editor.Options.ConvertTabsToSpaces;
			var container = GetOptionsContainerForEditor(editor);
			if (container != null) {
				int? effectiveIndentationSize = container.GetEffectiveIndentationSize();
				if (effectiveIndentationSize.HasValue)
					indentationSize = effectiveIndentationSize.Value;
				bool? effectiveConvertTabsToSpaces = container.GetEffectiveConvertTabsToSpaces();
				if (effectiveConvertTabsToSpaces.HasValue)
					convertTabsToSpaces = effectiveConvertTabsToSpaces.Value;
			}

			if (convertTabsToSpaces)
				return new string(' ', indentationSize);
			else
				return "\t";
		}

		#endregion

		#region 私有辅助函数

		bool NeedCurlyBracket(string text)
		{
			int curlyCounter = 0;

			bool inString = false;
			bool inChar   = false;
			bool verbatim = false;

			bool lineComment  = false;
			bool blockComment = false;

			for (int i = 0; i < text.Length; ++i) {
				switch (text[i]) {
					case '\r':
					case '\n':
						lineComment = false;
						inChar = false;
						if (!verbatim) inString = false;
						break;
					case '/':
						if (blockComment) {
							Debug.Assert(i > 0);
							if (text[i - 1] == '*') {
								blockComment = false;
							}
						}
						if (!inString && !inChar && i + 1 < text.Length) {
							if (!blockComment && text[i + 1] == '/') {
								lineComment = true;
							}
							if (!lineComment && text[i + 1] == '*') {
								blockComment = true;
							}
						}
						break;
					case '"':
						if (!(inChar || lineComment || blockComment)) {
							if (inString && verbatim) {
								if (i + 1 < text.Length && text[i + 1] == '"') {
									++i; // 跳过转义引号
									inString = false; // 让字符串继续
								} else {
									verbatim = false;
								}
							} else if (!inString && i > 0 && text[i - 1] == '@') {
								verbatim = true;
							}
							inString = !inString;
						}
						break;
					case '\'':
						if (!(inString || lineComment || blockComment)) {
							inChar = !inChar;
						}
						break;
					case '{':
						if (!(inString || inChar || lineComment || blockComment)) {
							++curlyCounter;
						}
						break;
					case '}':
						if (!(inString || inChar || lineComment || blockComment)) {
							--curlyCounter;
						}
						break;
					case '\\':
						if ((inString && !verbatim) || inChar)
							++i; // 跳过下一个字符
						break;
				}
			}
			return curlyCounter > 0;
		}

		bool IsInsideStringOrComment(ITextEditor textArea, IDocumentLine curLine, int cursorOffset)
		{
			// 扫描当前行是否在字符串或单行注释（//）内
			bool insideString  = false;
			char stringstart = ' ';
			bool verbatim = false; // 如果当前字符串是逐字字符串（@-string）则为 true
			char c = ' ';
			char lastchar;

			for (int i = curLine.Offset; i < cursorOffset; ++i) {
				lastchar = c;
				c = textArea.Document.GetCharAt(i);
				if (insideString) {
					if (c == stringstart) {
						if (verbatim && i + 1 < cursorOffset && textArea.Document.GetCharAt(i + 1) == '"') {
							++i; // 跳过转义字符
						} else {
							insideString = false;
						}
					} else if (c == '\\' && !verbatim) {
						++i; // 跳过转义字符
					}
				} else if (c == '/' && i + 1 < cursorOffset && textArea.Document.GetCharAt(i + 1) == '/') {
					return true;
				} else if (c == '"' || c == '\'') {
					stringstart = c;
					insideString = true;
					verbatim = (c == '"') && (lastchar == '@');
				}
			}

			return insideString;
		}

		bool IsInsideDocumentationComment(ITextEditor textArea, IDocumentLine curLine, int cursorOffset)
		{
			for (int i = curLine.Offset; i < cursorOffset; ++i) {
				char ch = textArea.Document.GetCharAt(i);
				if (ch == '"') {
					// 正确解析字符串太复杂（见上文），
					// 但不知道任何文档注释在字符串之后的情况...
					return false;
				}
				if (ch == '/' && i + 2 < cursorOffset && textArea.Document.GetCharAt(i + 1) == '/' && textArea.Document.GetCharAt(i + 2) == '/') {
					return true;
				}
			}
			return false;
		}

		/// <summary>
		/// 获取指定插入符位置之后的下一个成员。
		/// </summary>
		IUnresolvedEntity GetMemberAfter(ITextEditor editor, int caretLine)
		{
			FileName fileName = editor.FileName;
			IUnresolvedEntity nextElement = null;
			if (fileName != null) {
				IUnresolvedFile unresolvedFile = SD.ParserService.ParseFile(fileName, editor.Document);
				if (unresolvedFile != null) {
					var currentClass = unresolvedFile.GetInnermostTypeDefinition(caretLine, 0);
					int nextElementLine = int.MaxValue;
					if (currentClass == null) {
						foreach (var c in unresolvedFile.TopLevelTypeDefinitions) {
							if (c.Region.BeginLine < nextElementLine && c.Region.BeginLine > caretLine) {
								nextElementLine = c.Region.BeginLine;
								nextElement = c;
							}
						}
					} else {
						foreach (var c in currentClass.NestedTypes) {
							if (c.Region.BeginLine < nextElementLine && c.Region.BeginLine > caretLine) {
								nextElementLine = c.Region.BeginLine;
								nextElement = c;
							}
						}
						foreach (var m in currentClass.Members) {
							if (m.Region.BeginLine < nextElementLine && m.Region.BeginLine > caretLine) {
								nextElementLine = m.Region.BeginLine;
								nextElement = m;
							}
						}
					}
				}
			}
			return nextElement;
		}

		#endregion

		#region FormatLine

		bool NeedEndregion(IDocument document)
		{
			int regions = 0;
			int endregions = 0;
			for (int i = 1; i <= document.LineCount; i++) {
				string text = document.GetText(document.GetLineByNumber(i)).Trim();
				if (text.StartsWith("#region", StringComparison.Ordinal)) {
					++regions;
				} else if (text.StartsWith("#endregion", StringComparison.Ordinal)) {
					++endregions;
				}
			}
			return regions > endregions;
		}

		public override void FormatLines(ITextEditor textArea)
		{
			// 格式化当前选择或整个文档
			int formattedTextOffset = 0;
			int formattedTextLength = textArea.Document.TextLength;
			if (textArea.SelectionLength != 0) {
				formattedTextOffset = textArea.SelectionStart;
				formattedTextLength = textArea.SelectionLength;
			}
			FormatCode(textArea, formattedTextOffset, formattedTextLength, false);
		}

		/// <summary>
		/// 使用 Roslyn 格式化器根据当前有效的格式化设置格式化代码段。
		/// </summary>
		/// <param name="textArea">要格式化代码的文本编辑器实例。</param>
		/// <param name="offset">格式化代码的起始偏移量。</param>
		/// <param name="length">格式化代码的长度。</param>
		/// <param name="respectAutoFormattingSetting">
		/// 设为 <c>true</c> 时，仅在自动格式化设置激活时执行格式化。
		/// 如果为 <c>false</c>，则在任何情况下都会执行格式化。
		/// </param>
		/// <returns>如果代码已被格式化则返回 <c>true</c>，如果自动格式化当前被禁止则返回 <c>false</c>。</returns>
		private bool FormatCode(ITextEditor textArea, int offset, int length, bool respectAutoFormattingSetting)
		{
			if ((offset > textArea.Document.TextLength) || ((offset + length) > textArea.Document.TextLength))
				return false;
			if (respectAutoFormattingSetting && !CSharpFormattingPolicies.AutoFormatting)
				return false;

			using (textArea.Document.OpenUndoGroup()) {
				try {
					// 使用 Roslyn 格式化器
					string source = textArea.Document.Text;
					int endOffset = offset + length;
					string fileName = textArea.FileName;

					// 根据是否为全文档格式化选择不同的格式化方法
					Task<string> formatTask;
					if (offset == 0 && length == source.Length) {
						formatTask = RoslynFormatter.FormatDocumentAsync(source, fileName, CancellationToken.None);
					} else {
						formatTask = RoslynFormatter.FormatRangeAsync(source, offset, endOffset, fileName, CancellationToken.None);
					}

					// 同步等待异步结果（格式化操作通常很快）
					string formattedText = formatTask.Result;

					if (formattedText != source) {
						textArea.Document.Replace(0, source.Length, formattedText);
					}
				} catch (AggregateException) {
					// 格式化时可能发生异常（如代码包含语法错误），需要捕获
					return false;
				} catch (Exception) {
					return false;
				}
				return true;
			}
		}

		public override void FormatLine(ITextEditor textArea, char ch)
		{
			using (textArea.Document.OpenUndoGroup()) {
				FormatLineInternal(textArea, textArea.Caret.Line, textArea.Caret.Offset, ch);
			}
		}

		bool FormatStatement(ITextEditor textArea, int cursorOffset, int formattingStartOffset)
		{
			var line = textArea.Document.GetLineByOffset(formattingStartOffset);
			int lineOffset = line.Offset;
			// 向上遍历行，直到到达前一个语句、块、注释或预处理指令
			while (line.PreviousLine != null) {
				line = line.PreviousLine;
				string lineText = textArea.Document.GetText(line.Offset, line.Length);
				if (IsLineEndOfStatement(lineText)) {
					// 前一行是另一个语句，不格式化它
					break;
				}
				lineOffset = line.Offset;
			}

			return FormatCode(textArea, lineOffset, cursorOffset - lineOffset, true);
		}

		void FormatLineInternal(ITextEditor textArea, int lineNr, int cursorOffset, char ch)
		{
			IDocumentLine curLine   = textArea.Document.GetLineByNumber(lineNr);
			IDocumentLine lineAbove = lineNr > 1 ? textArea.Document.GetLineByNumber(lineNr - 1) : null;
			string terminator = DocumentUtilities.GetLineTerminator(textArea.Document, lineNr);

			string curLineText;
			// curLine 段的本地字符串
			if (ch == '/') {
				curLineText = textArea.Document.GetText(curLine);
				string lineAboveText = lineAbove == null ? "" : textArea.Document.GetText(lineAbove);
				if (curLineText != null && curLineText.EndsWith("///", StringComparison.Ordinal) && (lineAboveText == null || !lineAboveText.Trim().StartsWith("///", StringComparison.Ordinal))) {
					string indentation = DocumentUtilities.GetWhitespaceAfter(textArea.Document, curLine.Offset);
					IUnresolvedEntity member = GetMemberAfter(textArea, lineNr);
					if (member != null) {
						StringBuilder sb = new StringBuilder();
						sb.Append(" <summary>");
						sb.Append(terminator);
						sb.Append(indentation);
						sb.Append("/// ");
						sb.Append(terminator);
						sb.Append(indentation);
						sb.Append("/// </summary>");

						IUnresolvedMethod method = null;
						if (member is IUnresolvedMethod) {
							method = (IUnresolvedMethod)member;
						} else if (member is IUnresolvedTypeDefinition) {
							IUnresolvedTypeDefinition type = (IUnresolvedTypeDefinition) member;
							if (type.Kind == TypeKind.Delegate) {
								method = type.Methods.FirstOrDefault(m => m.Name == "Invoke");
							}
						}

						if (method != null) {
							for (int i = 0; i < method.Parameters.Count; ++i) {
								sb.Append(terminator);
								sb.Append(indentation);
								sb.Append("/// <param name=\"");
								sb.Append(method.Parameters[i].Name);
								sb.Append("\"></param>");
							}
							if (!method.IsConstructor) {
								KnownTypeReference returnType = method.ReturnType as KnownTypeReference;
								if (returnType == null || returnType.KnownTypeCode != KnownTypeCode.Void) {
									sb.Append(terminator);
									sb.Append(indentation);
									sb.Append("/// <returns></returns>");
								}
							}
						}

						textArea.Document.Insert(cursorOffset, sb.ToString());
						textArea.Caret.Offset = cursorOffset + indentation.Length + "/// ".Length + " <summary>".Length + terminator.Length;
					}
				}
				return;
			}

			if (ch != '\n' && ch != '>') {
				if (IsInsideStringOrComment(textArea, curLine, cursorOffset)) {
					return;
				}
			}
			switch (ch) {
				case '>':
					if (IsInsideDocumentationComment(textArea, curLine, cursorOffset)) {
						curLineText = textArea.Document.GetText(curLine);
						int column = cursorOffset - curLine.Offset;
						int index = Math.Min(column - 1, curLineText.Length - 1);

						while (index >= 0 && curLineText[index] != '<') {
							--index;
							if(curLineText[index] == '/')
								return; // 该标签是结束标签或已有
						}

						if (index > 0) {
							StringBuilder commentBuilder = new StringBuilder("");
							for (int i = index; i < curLineText.Length && i < column && !Char.IsWhiteSpace(curLineText[i]); ++i) {
								commentBuilder.Append(curLineText[ i]);
							}
							string tag = commentBuilder.ToString().Trim();
							if (!tag.EndsWith(">", StringComparison.Ordinal)) {
								tag += ">";
							}
							if (!tag.StartsWith("/", StringComparison.Ordinal)) {
								textArea.Document.Insert(cursorOffset, "</" + tag.Substring(1), AnchorMovementType.BeforeInsertion);
							}
						}
					}
					break;
				case ':':
				case ')':
				case ']':
				case '{':
					IndentLine(textArea, curLine);
					break;
				case '}':
					// 尝试获取对应的块起始大括号
					var bracketSearchResult = textArea.Language.BracketSearcher.SearchBracket(textArea.Document, cursorOffset);
					if (bracketSearchResult != null) {
						// 格式化该块
						if (!FormatStatement(textArea, cursorOffset, bracketSearchResult.OpeningBracketOffset)) {
							// 没有激活自动格式化，至少缩进该行
							IndentLine(textArea, curLine);
						}
					}
					break;
				case ';':
					// 格式化此行
					if (!FormatStatement(textArea, cursorOffset, cursorOffset)) {
						// 没有激活自动格式化，至少缩进该行
						IndentLine(textArea, curLine);
					}
					break;
				case '\n':
					string lineAboveText = lineAbove == null ? "" : textArea.Document.GetText(lineAbove);
					// curLine 可能有需要添加到缩进的文本
					curLineText = textArea.Document.GetText(curLine);

					if (lineAboveText != null && lineAboveText.Trim().StartsWith("#region", StringComparison.Ordinal)
						&& NeedEndregion(textArea.Document))
					{
						textArea.Document.Insert(cursorOffset, "#endregion");
						return;
					}

					IHighlighter highlighter = textArea.GetService(typeof(IHighlighter)) as IHighlighter;
					bool isInMultilineComment = false;
					bool isInMultilineString = false;
					if (highlighter != null && lineAbove != null) {
						var spanStack = highlighter.GetColorStack(lineNr).Select(c => c.Name).ToArray();
						isInMultilineComment = spanStack.Contains(HighlighterKnownSpanNames.Comment);
						isInMultilineString = spanStack.Contains(HighlighterKnownSpanNames.String);
					}
					bool isInNormalCode = !(isInMultilineComment || isInMultilineString);

					if (lineAbove != null && isInMultilineComment) {
						string lineAboveTextTrimmed = lineAboveText.TrimStart();
						if (lineAboveTextTrimmed.StartsWith("/*", StringComparison.Ordinal)) {
							textArea.Document.Insert(cursorOffset, " * ");
							return;
						}

						if (lineAboveTextTrimmed.StartsWith("*", StringComparison.Ordinal)) {
							textArea.Document.Insert(cursorOffset, "* ");
							return;
						}
					}

					if (lineAbove != null && isInNormalCode) {
						IDocumentLine nextLine  = lineNr + 1 <= textArea.Document.LineCount ? textArea.Document.GetLineByNumber(lineNr + 1) : null;
						string nextLineText = (nextLine != null) ? textArea.Document.GetText(nextLine) : "";

						int indexAbove = lineAboveText.IndexOf("///", StringComparison.Ordinal);
						int indexNext = nextLineText.IndexOf("///", StringComparison.Ordinal);
						if (indexAbove > 0 && (indexNext != -1 || indexAbove + 4 < lineAbove.Length)) {
							textArea.Document.Insert(cursorOffset, "/// ");
							return;
						}

						if (IsInNonVerbatimString(lineAboveText, curLineText)) {
							textArea.Document.Insert(cursorOffset, "\"");
							textArea.Document.Insert(lineAbove.Offset + lineAbove.Length,
								"\" +");
						}
					}
					if (textArea.Options.AutoInsertBlockEnd && lineAbove != null && isInNormalCode) {
						string oldLineText = textArea.Document.GetText(lineAbove);
						if (oldLineText.EndsWith("{", StringComparison.Ordinal)) {
							if (NeedCurlyBracket(textArea.Document.Text)) {
								int insertionPoint = curLine.Offset + curLine.Length;
								textArea.Document.Insert(insertionPoint, terminator + "}");
								IndentLine(textArea, textArea.Document.GetLineByNumber(lineNr + 1));
								textArea.Caret.Offset = insertionPoint;
							}
						}
					}
					return;
			}
		}

		bool IsLineEndOfStatement(string lineText)
		{
			string normalizedLine = null;

			// 查看行尾是否有注释
			int indexOfSingleLineComment = lineText.LastIndexOf("//");
			if (indexOfSingleLineComment > -1) {
				normalizedLine = lineText.Substring(0, indexOfSingleLineComment);
			} else {
				normalizedLine = lineText;
			}

			normalizedLine = normalizedLine.Trim(' ', '\t');

			if (normalizedLine.EndsWith("*/")) {
				int indexOfMultiLineCommentStart = normalizedLine.LastIndexOf("/*");
				if (indexOfMultiLineCommentStart > -1) {
					normalizedLine = normalizedLine.Substring(0, indexOfMultiLineCommentStart);
				} else {
					// 似乎是多行注释（此行没有注释开始）
					return true;
				}
			}

			// 常见语句结尾
			if (normalizedLine.StartsWith("#")
				|| normalizedLine.EndsWith(";")
				|| normalizedLine.EndsWith("{")
				|| normalizedLine.EndsWith("}"))
				return true;

			return false;
		}

		/// <summary>
		/// 检查光标是否在非逐字字符串内。
		/// 此方法用于检查是否在字符串中插入了换行符。
		/// 文本编辑器已经为我们断行了，所以只需检查两行。
		/// </summary>
		/// <param name="start">换行符之前的部分</param>
		/// <param name="end">换行符之后的部分</param>
		/// <returns>
		/// 当换行符在非逐字字符串内时返回 true，
		/// 即 start 不包含注释但包含奇数个 "，
		/// 且 end 在第一个注释之前包含奇数个 "。
		/// </returns>
		bool IsInNonVerbatimString(string start, string end)
		{
			bool inString = false;
			bool inChar = false;
			for (int i = 0; i < start.Length; ++i) {
				char c = start[i];
				if (c == '"' && !inChar) {
					if (!inString && i > 0 && start[i - 1] == '@')
						return false; // 逐字字符串不进行字符串换行
					inString = !inString;
				} else if (c == '\'' && !inString) {
					inChar = !inChar;
				}
				if (!inString && i > 0 && start[i - 1] == '/' && (c == '/' || c == '*'))
					return false;
				if (inString && start[i] == '\\')
					++i;
			}
			if (!inString) return false;
			// 可能处于字符串中，或者多行字符串刚刚结束
			// 检查 end 中是否有闭合双引号
			for (int i = 0; i < end.Length; ++i) {
				char c = end[i];
				if (c == '"' && !inChar) {
					if (!inString && i > 0 && end[i - 1] == '@')
						break; // 逐字字符串不进行字符串换行
					inString = !inString;
				} else if (c == '\'' && !inString) {
					inChar = !inChar;
				}
				if (!inString && i > 0 && end[i - 1] == '/' && (c == '/' || c == '*'))
					break;
				if (inString && end[i] == '\\')
					++i;
			}
			// 如果字符串正确关闭则返回 true
			return !inString;
		}

		#endregion

		#region SearchBracket 辅助函数

		static int ScanLineStart(IDocument document, int offset)
		{
			for (int i = offset - 1; i > 0; --i) {
				if (document.GetCharAt(i) == '\n')
					return i + 1;
			}
			return 0;
		}

		/// <summary>
		/// 获取偏移量处的代码类型。<br/>
		/// 0 = 代码，<br/>
		/// 1 = 注释，<br/>
		/// 2 = 字符串<br/>
		/// 不支持块注释和多行字符串。
		/// </summary>
		static int GetStartType(IDocument document, int linestart, int offset)
		{
			bool inString = false;
			bool inChar = false;
			bool verbatim = false;
			for(int i = linestart; i < offset; i++) {
				switch (document.GetCharAt(i)) {
					case '/':
						if (!inString && !inChar && i + 1 < document.TextLength) {
							if (document.GetCharAt(i + 1) == '/') {
								return 1;
							}
						}
						break;
					case '"':
						if (!inChar) {
							if (inString && verbatim) {
								if (i + 1 < document.TextLength && document.GetCharAt(i + 1) == '"') {
									++i; // 跳过转义引号
									inString = false; // 让字符串继续
								} else {
									verbatim = false;
								}
							} else if (!inString && i > 0 && document.GetCharAt(i - 1) == '@') {
								verbatim = true;
							}
							inString = !inString;
						}
						break;
					case '\'':
						if (!inString) inChar = !inChar;
						break;
					case '\\':
						if ((inString && !verbatim) || inChar)
							++i; // 跳过下一个字符
						break;
				}
			}
			return (inString || inChar) ? 2 : 0;
		}

		#endregion

		public override void SurroundSelectionWithComment(ITextEditor editor)
		{
			SurroundSelectionWithSingleLineComment(editor, "//");
		}
	}
}
