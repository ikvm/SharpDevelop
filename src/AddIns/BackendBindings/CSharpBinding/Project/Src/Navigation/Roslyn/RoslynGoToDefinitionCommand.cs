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
using System.Threading;

using ICSharpCode.Core;
using ICSharpCode.NRefactory;
using ICSharpCode.SharpDevelop;
using ICSharpCode.SharpDevelop.Editor;
using ICSharpCode.SharpDevelop.Project;

namespace CSharpBinding.Navigation.Roslyn
{
	/// <summary>
	/// Roslyn 版"跳转到定义"命令。
	/// 在编辑器右键菜单中使用，跳转到光标位置符号的定义。
	/// </summary>
	public class RoslynGoToDefinitionCommand : AbstractMenuCommand
	{
		public override void Run()
		{
			var editor = SD.GetActiveViewContentService<ITextEditor>();
			if (editor == null)
				return;

			var fileName = editor.FileName;
			if (fileName == null || !fileName.HasExtension(".cs"))
				return;

			var location = editor.Caret.Location;
			var project = GetProject(fileName);

			try {
				RoslynNavigationService.GoToDefinitionAsync(
					fileName,
					location,
					project,
					CancellationToken.None
				).Wait();
			} catch (AggregateException ex) {
				LoggingService.Warn("Roslyn 跳转到定义失败", ex.InnerException ?? ex);
			} catch (Exception ex) {
				LoggingService.Warn("Roslyn 跳转到定义失败", ex);
			}
		}

		/// <summary>
		/// 获取包含指定文件的项目
		/// </summary>
		static IProject GetProject(FileName fileName)
		{
			if (SD.ProjectService.CurrentSolution != null) {
				return SD.ProjectService.FindProjectContainingFile(fileName);
			}
			return null;
		}
	}

	/// <summary>
	/// Roslyn 版"查找引用"命令。
	/// 在编辑器右键菜单中使用，查找光标位置符号的所有引用。
	/// </summary>
	public class RoslynFindReferencesCommand : AbstractMenuCommand
	{
		public override void Run()
		{
			var editor = SD.GetActiveViewContentService<ITextEditor>();
			if (editor == null)
				return;

			var fileName = editor.FileName;
			if (fileName == null || !fileName.HasExtension(".cs"))
				return;

			var location = editor.Caret.Location;
			var project = GetProject(fileName);

			try {
				RoslynNavigationService.FindReferencesAsync(
					fileName,
					location,
					project,
					CancellationToken.None
				).Wait();
			} catch (AggregateException ex) {
				LoggingService.Warn("Roslyn 查找引用失败", ex.InnerException ?? ex);
			} catch (Exception ex) {
				LoggingService.Warn("Roslyn 查找引用失败", ex);
			}
		}

		static IProject GetProject(FileName fileName)
		{
			if (SD.ProjectService.CurrentSolution != null) {
				return SD.ProjectService.FindProjectContainingFile(fileName);
			}
			return null;
		}
	}

	/// <summary>
	/// Roslyn 版"跳转到实现"命令。
	/// 在编辑器右键菜单中使用，跳转到光标位置符号的实现。
	/// </summary>
	public class RoslynGoToImplementationCommand : AbstractMenuCommand
	{
		public override void Run()
		{
			var editor = SD.GetActiveViewContentService<ITextEditor>();
			if (editor == null)
				return;

			var fileName = editor.FileName;
			if (fileName == null || !fileName.HasExtension(".cs"))
				return;

			var location = editor.Caret.Location;
			var project = GetProject(fileName);

			try {
				RoslynNavigationService.GoToImplementationAsync(
					fileName,
					location,
					project,
					CancellationToken.None
				).Wait();
			} catch (AggregateException ex) {
				LoggingService.Warn("Roslyn 跳转到实现失败", ex.InnerException ?? ex);
			} catch (Exception ex) {
				LoggingService.Warn("Roslyn 跳转到实现失败", ex);
			}
		}

		static IProject GetProject(FileName fileName)
		{
			if (SD.ProjectService.CurrentSolution != null) {
				return SD.ProjectService.FindProjectContainingFile(fileName);
			}
			return null;
		}
	}
}
