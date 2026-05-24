using ICSharpCode.SharpDevelop.Workbench;
using System;
using System.Windows;
using ICSharpCode.SharpDevelop;

namespace ICSharpCode.AiAgent
{
    public class AiAgentPad : AbstractPadContent
    {
        private readonly AiAgentControl _control;
        private string _selectedAction;

        public override object Control => _control;

        public AiAgentPad()
        {
            _control = new AiAgentControl();
            _control.BtnGenerate.Click += BtnGenerate_Click;
            _control.BtnExplain.Click += BtnExplain_Click;
            _control.BtnOptimize.Click += BtnOptimize_Click;
            _control.BtnRefactor.Click += BtnRefactor_Click;
            _control.BtnDebug.Click += BtnDebug_Click;
            _control.BtnExecute.Click += BtnExecute_Click;
            _control.SettingsButton.Click += SettingsButton_Click;

            UpdateStatus();

            AiService.Instance.PropertyChanged += (sender, e) =>
            {
                if (e.PropertyName == nameof(AiService.StatusMessage))
                {
                    UpdateStatus();
                }
                else if (e.PropertyName == nameof(AiService.IsConfigured))
                {
                    UpdateStatus();
                    _control.BtnExecute.IsEnabled = AiService.Instance.IsConfigured;
                }
            };
        }

        private void UpdateStatus()
        {
            _control.StatusText.Text = AiService.Instance.StatusMessage;
            _control.StatusText.Foreground = AiService.Instance.IsConfigured
                ? System.Windows.Media.Brushes.Green
                : System.Windows.Media.Brushes.Orange;
        }

        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new ConfigurationDialog();
            dialog.Owner = Window.GetWindow(_control);
            dialog.ShowDialog();
            UpdateStatus();
            _control.BtnExecute.IsEnabled = AiService.Instance.IsConfigured;
        }

        private void BtnGenerate_Click(object sender, RoutedEventArgs e)
        {
            _selectedAction = "generate";
            _control.PromptTextBox.Text = "Generate a C# class for...";
            _control.BtnExecute.IsEnabled = AiService.Instance.IsConfigured;
        }

        private void BtnExplain_Click(object sender, RoutedEventArgs e)
        {
            _selectedAction = "explain";
            string selectedCode = CommandHelper.GetSelectedCode();
            _control.PromptTextBox.Text = string.IsNullOrEmpty(selectedCode)
                ? "Explain this code..."
                : selectedCode;
            _control.BtnExecute.IsEnabled = AiService.Instance.IsConfigured;
        }

        private void BtnOptimize_Click(object sender, RoutedEventArgs e)
        {
            _selectedAction = "optimize";
            string selectedCode = CommandHelper.GetSelectedCode();
            _control.PromptTextBox.Text = string.IsNullOrEmpty(selectedCode)
                ? "Optimize this code..."
                : selectedCode;
            _control.BtnExecute.IsEnabled = AiService.Instance.IsConfigured;
        }

        private void BtnRefactor_Click(object sender, RoutedEventArgs e)
        {
            _selectedAction = "refactor";
            string selectedCode = CommandHelper.GetSelectedCode();
            _control.PromptTextBox.Text = string.IsNullOrEmpty(selectedCode)
                ? "Refactor this code to..."
                : selectedCode;
            _control.BtnExecute.IsEnabled = AiService.Instance.IsConfigured;
        }

        private void BtnDebug_Click(object sender, RoutedEventArgs e)
        {
            _selectedAction = "debug";
            string selectedCode = CommandHelper.GetSelectedCode();
            _control.PromptTextBox.Text = string.IsNullOrEmpty(selectedCode)
                ? "Debug this code with error..."
                : selectedCode;
            _control.BtnExecute.IsEnabled = AiService.Instance.IsConfigured;
        }

        private async void BtnExecute_Click(object sender, RoutedEventArgs e)
        {
            if (!AiService.Instance.IsConfigured)
            {
                MessageBox.Show("Please configure the API key first");
                return;
            }

            string prompt = _control.PromptTextBox.Text;
            if (string.IsNullOrWhiteSpace(prompt))
            {
                MessageBox.Show("Please enter a prompt");
                return;
            }

            _control.BtnExecute.IsEnabled = false;

            string systemMessage = null;
            string actionName = null;

            switch (_selectedAction)
            {
                case "generate":
                    actionName = "Generate Code";
                    systemMessage = "You are an expert software developer. Generate clean, efficient, and well-documented code. Respond only with the requested code.";
                    break;
                case "explain":
                    actionName = "Explain Code";
                    systemMessage = "You are an expert software developer. Explain the provided code in detail, including its purpose, key algorithms, and potential improvements.";
                    prompt = $"Explain this code:\n\n{prompt}";
                    break;
                case "optimize":
                    actionName = "Optimize Code";
                    systemMessage = "You are an expert software developer. Optimize the provided code for performance, readability, and best practices. Explain the changes made.";
                    prompt = $"Optimize this code and explain the changes:\n\n{prompt}";
                    break;
                case "refactor":
                    actionName = "Refactor Code";
                    systemMessage = "You are an expert software developer. Refactor the provided code according to the specified goal. Explain the refactoring approach.";
                    prompt = $"Refactor this code:\n\n{prompt}";
                    break;
                case "debug":
                    actionName = "Debug Code";
                    systemMessage = "You are an expert debugger. Analyze the provided code and error description to identify and fix bugs.";
                    prompt = $"Debug this code:\n\n{prompt}";
                    break;
            }

            try
            {
                _control.StreamOutput.Clear();
                await AiService.Instance.StreamActionAsync(prompt, systemMessage, actionName, _control.StreamOutput);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error: {ex.Message}");
            }
            finally
            {
                _control.BtnExecute.IsEnabled = AiService.Instance.IsConfigured;
            }
        }
    }
}