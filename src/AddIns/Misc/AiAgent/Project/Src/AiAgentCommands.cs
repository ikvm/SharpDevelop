using ICSharpCode.Core;
using ICSharpCode.SharpDevelop;
using ICSharpCode.SharpDevelop.Gui;
using ICSharpCode.SharpDevelop.Editor;
using ICSharpCode.SharpDevelop.Workbench;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ICSharpCode.AiAgent
{
    public class ShowAiAgentCommand : AbstractMenuCommand
    {
        public override void Run()
        {
            // 首先尝试通过GetPad获取
            var pad = SD.Workbench.GetPad(typeof(AiAgentPad));
            if (pad != null)
            {
                pad.BringPadToFront();
                return;
            }
            
            // 如果GetPad返回null，遍历PadContentCollection查找
            foreach (var p in SD.Workbench.PadContentCollection)
            {
                if (p.Class == typeof(AiAgentPad).FullName)
                {
                    p.BringPadToFront();
                    return;
                }
            }
            
            // 最后尝试通过AddInTree查找
            var pads = ICSharpCode.Core.AddInTree.BuildItems<PadDescriptor>("/SharpDevelop/Workbench/Pads", null, false);
            foreach (var p in pads)
            {
                if (p.Class == typeof(AiAgentPad).FullName)
                {
                    // 使用ActivatePad而不是ShowPad
                    SD.Workbench.ActivatePad(p);
                    return;
                }
            }
        }
    }

    public class GenerateCodeCommand : AbstractMenuCommand
    {
        public override async void Run()
        {
            try
            {
                string prompt = SD.MessageService.ShowInputBox(
                    "AI Code Generation",
                    "Enter your code generation request:",
                    "Generate a C# class for...");
                
                if (!string.IsNullOrEmpty(prompt))
                {
                    var aiService = AiService.Instance;
                    string result = await aiService.GenerateCodeAsync(prompt);
                    CommandHelper.InsertCode(result);
                }
            }
            catch (Exception ex)
            {
                MessageService.ShowError(ex.Message);
            }
        }
    }

    public class ExplainCodeCommand : AbstractMenuCommand
    {
        public override async void Run()
        {
            try
            {
                string selectedCode = CommandHelper.GetSelectedCode();
                
                if (string.IsNullOrEmpty(selectedCode))
                {
                    MessageService.ShowMessage("Please select code to explain");
                    return;
                }

                var aiService = AiService.Instance;
                string result = await aiService.ExplainCodeAsync(selectedCode);
                CommandHelper.ShowResult("Code Explanation", result);
            }
            catch (Exception ex)
            {
                MessageService.ShowError(ex.Message);
            }
        }
    }

    public class OptimizeCodeCommand : AbstractMenuCommand
    {
        public override async void Run()
        {
            try
            {
                string selectedCode = CommandHelper.GetSelectedCode();
                
                if (string.IsNullOrEmpty(selectedCode))
                {
                    MessageService.ShowMessage("Please select code to optimize");
                    return;
                }

                var aiService = AiService.Instance;
                string result = await aiService.OptimizeCodeAsync(selectedCode);
                CommandHelper.ShowResult("Code Optimization", result);
            }
            catch (Exception ex)
            {
                MessageService.ShowError(ex.Message);
            }
        }
    }

    public class RefactorCodeCommand : AbstractMenuCommand
    {
        public override async void Run()
        {
            try
            {
                string selectedCode = CommandHelper.GetSelectedCode();
                
                if (string.IsNullOrEmpty(selectedCode))
                {
                    MessageService.ShowMessage("Please select code to refactor");
                    return;
                }

                string goal = SD.MessageService.ShowInputBox(
                    "AI Code Refactoring",
                    "Enter refactoring goal:",
                    "make this code more readable");
                
                if (!string.IsNullOrEmpty(goal))
                {
                    var aiService = AiService.Instance;
                    string result = await aiService.RefactorCodeAsync(selectedCode, goal);
                    CommandHelper.ShowResult("Code Refactoring", result);
                }
            }
            catch (Exception ex)
            {
                MessageService.ShowError(ex.Message);
            }
        }
    }

    public class DebugCodeCommand : AbstractMenuCommand
    {
        public override async void Run()
        {
            try
            {
                string selectedCode = CommandHelper.GetSelectedCode();
                
                if (string.IsNullOrEmpty(selectedCode))
                {
                    MessageService.ShowMessage("Please select code to debug");
                    return;
                }

                string error = SD.MessageService.ShowInputBox(
                    "AI Debugging",
                    "Enter error description:",
                    "NullReferenceException at line...");
                
                if (!string.IsNullOrEmpty(error))
                {
                    var aiService = AiService.Instance;
                    string result = await aiService.DebugCodeAsync(selectedCode, error);
                    CommandHelper.ShowResult("Debugging Analysis", result);
                }
            }
            catch (Exception ex)
            {
                MessageService.ShowError(ex.Message);
            }
        }
    }

    internal static class CommandHelper
    {
        public static string GetSelectedCode()
        {
            IViewContent view = SD.Workbench.ActiveViewContent;
            if (view != null)
            {
                ITextEditor textEditor = view as ITextEditor;
                if (textEditor != null)
                {
                    return textEditor.SelectedText;
                }
                // Try to get ITextEditor from IServiceProvider
                if (view is IServiceProvider serviceProvider)
                {
                    ITextEditor editor = serviceProvider.GetService(typeof(ITextEditor)) as ITextEditor;
                    if (editor != null)
                    {
                        return editor.SelectedText;
                    }
                }
            }
            return string.Empty;
        }

        public static void InsertCode(string code)
        {
            IViewContent view = SD.Workbench.ActiveViewContent;
            if (view != null)
            {
                ITextEditor textEditor = view as ITextEditor;
                if (textEditor != null)
                {
                    textEditor.SelectedText = code;
                    return;
                }
                // Try to get ITextEditor from IServiceProvider
                if (view is IServiceProvider serviceProvider)
                {
                    ITextEditor editor = serviceProvider.GetService(typeof(ITextEditor)) as ITextEditor;
                    if (editor != null)
                    {
                        editor.SelectedText = code;
                    }
                }
            }
        }

        public static void ShowResult(string title, string content)
        {
            var window = new TypewriterResultWindow(title, content);
            window.ShowDialog();
        }
    }
}
