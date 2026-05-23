using ICSharpCode.Core;
using ICSharpCode.SharpDevelop;
using ICSharpCode.SharpDevelop.Gui;
using ICSharpCode.SharpDevelop.Editor;
using ICSharpCode.SharpDevelop.Workbench;
using System;
using System.Windows.Forms;

namespace ICSharpCode.AiAgent
{
    public class ShowAiAgentCommand : AbstractMenuCommand
    {
        public override void Run()
        {
            var pad = SD.Workbench.GetPad(typeof(AiAgentPad));
            if (pad != null)
                pad.BringPadToFront();
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
            Form resultForm = new Form();
            resultForm.Text = title;
            resultForm.Size = new System.Drawing.Size(800, 600);
            
            TextBox textBox = new TextBox();
            textBox.Dock = DockStyle.Fill;
            textBox.Multiline = true;
            textBox.ReadOnly = true;
            textBox.ScrollBars = ScrollBars.Both;
            textBox.Font = new System.Drawing.Font("Consolas", 10);
            textBox.Text = content;
            
            resultForm.Controls.Add(textBox);
            resultForm.ShowDialog();
        }
    }
}
