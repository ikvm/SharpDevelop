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

            ApiKeyBox.Password = "";
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

            ComboBoxItem selectedItem = ProviderComboBox.SelectedItem as ComboBoxItem;
            AiProvider provider = AiProvider.OpenAI;

            if (selectedItem?.Tag != null && Enum.TryParse(selectedItem.Tag.ToString(), out AiProvider parsed))
            {
                provider = parsed;
            }

            AiService.Instance.Configure(apiKey, null, provider);
            DialogResult = true;
            Close();
        }
    }
}