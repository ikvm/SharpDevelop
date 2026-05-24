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
                    actionName = "生成代码";
                    systemMessage = "你是一位资深的软件开发者。请生成简洁高效且文档完善的代码，直接输出请求的代码即可。";
                    break;
                case "explain":
                    actionName = "解释代码";
                    systemMessage = "你是一位资深的软件开发者。请详细解释所提供的代码，包括其功能、核心算法以及潜在的优化空间。";
                    prompt = $"请解释以下代码：\n\n{prompt}";
                    break;
                case "optimize":
                    actionName = "优化代码";
                    systemMessage = "你是一位资深的软件开发者。请对所提供的代码进行性能、可读性和最佳实践方面的优化，并说明所做的改进。";
                    prompt = $"请优化以下代码并说明改动：\n\n{prompt}";
                    break;
                case "refactor":
                    actionName = "重构代码";
                    systemMessage = "你是一位资深的软件开发者。请根据指定的目标对代码进行重构，并说明重构的思路。";
                    prompt = $"请重构以下代码：\n\n{prompt}";
                    break;
                case "debug":
                    actionName = "调试代码";
                    systemMessage = "你是一位资深的调试专家。请分析所提供的代码和错误描述，找出并修复缺陷。";
                    prompt = $"请调试以下代码：\n\n{prompt}";
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