using ICSharpCode.SharpDevelop.Workbench;
using System;
using System.Windows;
using System.Windows.Controls;
using ICSharpCode.SharpDevelop;

namespace ICSharpCode.AiAgent
{
    public class AiAgentPad : AbstractPadContent
    {
        private readonly AiAgentControl _control;
        private IAiSkill _currentSkill;

        public override object Control => _control;

        public AiAgentPad()
        {
            _control = new AiAgentControl();
            _control.SkillCombo.SelectionChanged += SkillCombo_SelectionChanged;
            _control.ModelCombo.SelectionChanged += ModelCombo_SelectionChanged;
            _control.ActionCombo.SelectionChanged += ActionCombo_SelectionChanged;
            _control.BtnExecute.Click += BtnExecute_Click;
            _control.SettingsButton.Click += SettingsButton_Click;

            PopulateModelList();
            PopulateSkillList();
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
            PopulateModelList();
        }

        private void PopulateModelList()
        {
            _control.ModelCombo.Items.Clear();
            if (OpenAiClient.AvailableModels.TryGetValue(AiService.Instance.Provider, out string[] models))
            {
                foreach (string model in models)
                {
                    var item = new ComboBoxItem { Content = model, Tag = model };
                    item.IsSelected = (model == AiService.Instance.SelectedModel);
                    _control.ModelCombo.Items.Add(item);
                }
            }
        }

        private void PopulateSkillList()
        {
            _control.SkillCombo.Items.Clear();
            foreach (var skill in SkillManager.Instance.Skills)
            {
                var item = new ComboBoxItem { Content = skill.Name, Tag = skill.Id };
                _control.SkillCombo.Items.Add(item);
            }
            if (_control.SkillCombo.Items.Count > 0)
                _control.SkillCombo.SelectedIndex = 0;
        }

        private void ModelCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var item = _control.ModelCombo.SelectedItem as ComboBoxItem;
            if (item?.Tag != null)
            {
                AiService.Instance.SelectedModel = item.Tag.ToString();
            }
        }

        private void SkillCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var item = _control.SkillCombo.SelectedItem as ComboBoxItem;
            if (item?.Tag == null) return;

            _currentSkill = SkillManager.Instance.GetSkill(item.Tag.ToString());
            if (_currentSkill == null) return;

            UpdatePromptForSkill();
            _control.BtnExecute.IsEnabled = true;
        }

        private void UpdatePromptForSkill()
        {
            if (_currentSkill == null) return;
            string selectedCode = CommandHelper.GetSelectedCode();

            if (_currentSkill is ExplainCodeSkill || _currentSkill is OptimizeCodeSkill ||
                _currentSkill is RefactorCodeSkill || _currentSkill is DebugCodeSkill)
            {
                _control.PromptTextBox.Text = string.IsNullOrEmpty(selectedCode)
                    ? "请先在编辑器中选中代码..."
                    : selectedCode;
            }
            else
            {
                _control.PromptTextBox.Text = _control.PromptTextBox.Text.Length < 5
                    ? "输入您的需求..."
                    : _control.PromptTextBox.Text;
            }
        }

        private void ActionCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var item = _control.ActionCombo.SelectedItem as ComboBoxItem;
            if (item == null) return;

            string action = item.Tag.ToString();

            if (action == "execute" && _currentSkill != null)
            {
                UpdatePromptForSkill();
            }
            else if (action == "chat")
            {
                _control.PromptTextBox.Text = "自由对话模式：输入任意问题...";
            }
        }

        private async void BtnExecute_Click(object sender, RoutedEventArgs e)
        {
            if (!AiService.Instance.IsConfigured)
            {
                MessageBox.Show("请先配置 API Key");
                return;
            }

            string prompt = _control.PromptTextBox.Text;
            if (string.IsNullOrWhiteSpace(prompt))
            {
                MessageBox.Show("请输入提示内容");
                return;
            }

            var actionItem = _control.ActionCombo.SelectedItem as ComboBoxItem;
            string action = actionItem?.Tag?.ToString() ?? "execute";

            _control.BtnExecute.IsEnabled = false;

            try
            {
                string systemMessage;
                string actionName;
                string finalPrompt;

                if (action == "execute" && _currentSkill != null)
                {
                    string selectedCode = CommandHelper.GetSelectedCode();
                    systemMessage = _currentSkill.SystemMessage;
                    actionName = _currentSkill.Name;
                    finalPrompt = _currentSkill.BuildPrompt(prompt, selectedCode);
                }
                else
                {
                    systemMessage = "你是一位有用的AI助手。请根据用户的输入提供有帮助的回复。";
                    actionName = "自由对话";
                    finalPrompt = prompt;
                }

                _control.StreamOutput.Clear();

                if (AiService.Instance.ToolExecutor.ExecutedCount > 0)
                {
                    var confirmRollback = MessageBox.Show(
                        "上一次操作有未回滚的本地文件更改。是否先回滚上一次的更改？\n\n选择\"是\"：回滚后继续\n选择\"否\"：保留更改并继续",
                        "本地更改提醒",
                        MessageBoxButton.YesNoCancel,
                        MessageBoxImage.Question);

                    if (confirmRollback == MessageBoxResult.Yes)
                    {
                        await AiService.Instance.RollbackAsync();
                        _control.StreamOutput.AppendToolStatus("✅ 已回滚上一次的更改");
                    }
                    else if (confirmRollback == MessageBoxResult.Cancel)
                    {
                        return;
                    }
                }

                AiService.Instance.ToolParser.Reset();
                AiService.Instance.ToolExecutor.ClearRollbackStack();

                await AiService.Instance.StreamActionAsync(finalPrompt, systemMessage, actionName, _control.StreamOutput);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"错误：{ex.Message}");
            }
            finally
            {
                _control.BtnExecute.IsEnabled = AiService.Instance.IsConfigured;
            }
        }
    }
}