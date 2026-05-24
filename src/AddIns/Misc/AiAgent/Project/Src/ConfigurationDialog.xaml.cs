using System;
using System.Windows;
using System.Windows.Controls;

namespace ICSharpCode.AiAgent
{
    public partial class ConfigurationDialog : Window
    {
        public ConfigurationDialog()
        {
            InitializeComponent();
            LoadCurrentConfiguration();
        }

        private void LoadCurrentConfiguration()
        {
            foreach (ComboBoxItem item in ProviderComboBox.Items)
            {
                string tag = item.Tag?.ToString();
                if (tag != null && Enum.TryParse(tag, out AiProvider provider) && provider == AiService.Instance.Provider)
                {
                    ProviderComboBox.SelectedItem = item;
                    break;
                }
            }

            RefreshModelList();
            SelectCurrentModel();

            ApiKeyBox.Password = "";
        }

        private void RefreshModelList()
        {
            ModelComboBox.Items.Clear();
            ComboBoxItem selectedItem = ProviderComboBox.SelectedItem as ComboBoxItem;
            if (selectedItem?.Tag == null || !Enum.TryParse(selectedItem.Tag.ToString(), out AiProvider provider))
                provider = AiProvider.OpenAI;

            if (OpenAiClient.AvailableModels.TryGetValue(provider, out string[] models))
            {
                foreach (string model in models)
                {
                    ModelComboBox.Items.Add(new ComboBoxItem { Content = model, Tag = model });
                }
            }
        }

        private void SelectCurrentModel()
        {
            string currentModel = AiService.Instance.SelectedModel;
            foreach (ComboBoxItem item in ModelComboBox.Items)
            {
                if (item.Tag?.ToString() == currentModel)
                {
                    ModelComboBox.SelectedItem = item;
                    return;
                }
            }
            if (ModelComboBox.Items.Count > 0)
                ModelComboBox.SelectedIndex = 0;
        }

        private void ProviderComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            RefreshModelList();
            if (ModelComboBox.Items.Count > 0)
                ModelComboBox.SelectedIndex = 0;
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            string apiKey = ApiKeyBox.Password;
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                StatusMessage.Text = "Please enter an API key";
                StatusMessage.Foreground = System.Windows.Media.Brushes.Red;
                return;
            }

            ComboBoxItem providerItem = ProviderComboBox.SelectedItem as ComboBoxItem;
            AiProvider provider = AiProvider.OpenAI;
            if (providerItem?.Tag != null && Enum.TryParse(providerItem.Tag.ToString(), out AiProvider parsed))
                provider = parsed;

            string model = (ModelComboBox.SelectedItem as ComboBoxItem)?.Tag?.ToString();

            AiService.Instance.Configure(apiKey, null, provider, model);
            DialogResult = true;
            Close();
        }
    }
}