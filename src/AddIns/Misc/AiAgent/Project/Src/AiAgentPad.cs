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
            _control.ConfigureButton.Click += ConfigureButton_Click;
            
            UpdateStatus();
            
            AiService.Instance.PropertyChanged += (sender, e) =>
            {
                if (e.PropertyName == nameof(AiService.StatusMessage))
                {
                    UpdateStatus();
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

        private void ConfigureButton_Click(object sender, RoutedEventArgs e)
        {
            string apiKey = _control.ApiKeyTextBox.Password;
            
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                MessageBox.Show("Please enter an API key");
                return;
            }
            
            AiService.Instance.Configure(apiKey);
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
            _control.PromptTextBox.Text = "Explain this code...";
            _control.BtnExecute.IsEnabled = AiService.Instance.IsConfigured;
        }

        private void BtnOptimize_Click(object sender, RoutedEventArgs e)
        {
            _selectedAction = "optimize";
            _control.PromptTextBox.Text = "Optimize this code...";
            _control.BtnExecute.IsEnabled = AiService.Instance.IsConfigured;
        }

        private void BtnRefactor_Click(object sender, RoutedEventArgs e)
        {
            _selectedAction = "refactor";
            _control.PromptTextBox.Text = "Refactor this code to...";
            _control.BtnExecute.IsEnabled = AiService.Instance.IsConfigured;
        }

        private void BtnDebug_Click(object sender, RoutedEventArgs e)
        {
            _selectedAction = "debug";
            _control.PromptTextBox.Text = "Debug this code with error...";
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

            try
            {
                string result = string.Empty;
                
                switch (_selectedAction)
                {
                    case "generate":
                        result = await AiService.Instance.GenerateCodeAsync(prompt);
                        break;
                    case "explain":
                        result = await AiService.Instance.ExplainCodeAsync(prompt);
                        break;
                    case "optimize":
                        result = await AiService.Instance.OptimizeCodeAsync(prompt);
                        break;
                    case "refactor":
                        result = await AiService.Instance.RefactorCodeAsync(prompt, "improve code quality");
                        break;
                    case "debug":
                        result = await AiService.Instance.DebugCodeAsync(prompt, "Unknown error");
                        break;
                }

                ShowResult(result);
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

        private void ShowResult(string content)
        {
            var resultWindow = new Window();
            resultWindow.Title = "AI Agent Result";
            resultWindow.SizeToContent = SizeToContent.WidthAndHeight;
            resultWindow.MinWidth = 600;
            resultWindow.MinHeight = 400;
            
            var textBox = new System.Windows.Controls.TextBox();
            textBox.Text = content;
            textBox.IsReadOnly = true;
            textBox.AcceptsReturn = true;
            textBox.VerticalScrollBarVisibility = System.Windows.Controls.ScrollBarVisibility.Auto;
            textBox.HorizontalScrollBarVisibility = System.Windows.Controls.ScrollBarVisibility.Auto;
            textBox.FontFamily = new System.Windows.Media.FontFamily("Consolas");
            textBox.FontSize = 12;
            textBox.MinWidth = 580;
            textBox.MinHeight = 380;
            
            resultWindow.Content = textBox;
            resultWindow.ShowDialog();
        }
    }
}