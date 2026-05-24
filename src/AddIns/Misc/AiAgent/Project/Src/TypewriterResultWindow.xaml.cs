using System;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Threading;

namespace ICSharpCode.AiAgent
{
    public partial class TypewriterResultWindow : Window
    {
        private string _fullContent;
        private int _currentIndex;
        private DispatcherTimer _typewriterTimer;
        private const int CharactersPerTick = 3;
        private const int TickIntervalMs = 20;

        public TypewriterResultWindow(string title, string content)
        {
            InitializeComponent();
            TitleText.Text = title;
            Title = title;
            _fullContent = content ?? string.Empty;
            OutputTextBox.Text = string.Empty;
            StartTypewriter();
        }

        private void StartTypewriter()
        {
            _currentIndex = 0;

            _typewriterTimer = new DispatcherTimer();
            _typewriterTimer.Interval = TimeSpan.FromMilliseconds(TickIntervalMs);
            _typewriterTimer.Tick += TypewriterTick;
            _typewriterTimer.Start();
        }

        private void TypewriterTick(object sender, EventArgs e)
        {
            int charsToAppend = Math.Min(CharactersPerTick, _fullContent.Length - _currentIndex);
            if (charsToAppend <= 0)
            {
                _typewriterTimer.Stop();
                ProgressText.Text = $"Complete ({_fullContent.Length} chars)";
                TypewriterStatus.Text = "Done";
                InsertButton.IsEnabled = true;
                return;
            }

            string chunk = _fullContent.Substring(_currentIndex, charsToAppend);
            _currentIndex += charsToAppend;
            OutputTextBox.Text += chunk;

            int progressPercent = (int)((double)_currentIndex / _fullContent.Length * 100);
            ProgressText.Text = $"{progressPercent}% ({_currentIndex}/{_fullContent.Length})";

            ScrollViewer.ScrollToBottom();
        }

        protected override void OnClosed(EventArgs e)
        {
            if (_typewriterTimer != null && _typewriterTimer.IsEnabled)
            {
                _typewriterTimer.Stop();
            }
            base.OnClosed(e);
        }

        public void SkipToEnd()
        {
            if (_typewriterTimer != null && _typewriterTimer.IsEnabled)
            {
                _typewriterTimer.Stop();
            }
            OutputTextBox.Text = _fullContent;
            _currentIndex = _fullContent.Length;
            ProgressText.Text = $"Complete ({_fullContent.Length} chars)";
            TypewriterStatus.Text = "Done";
            InsertButton.IsEnabled = true;
            ScrollViewer.ScrollToBottom();
        }

        private void CopyButton_Click(object sender, RoutedEventArgs e)
        {
            SkipToEnd();
            try
            {
                Clipboard.SetText(OutputTextBox.Text);
                CopyButton.Content = "Copied!";
            }
            catch
            {
                CopyButton.Content = "Failed";
            }
        }

        private void InsertButton_Click(object sender, RoutedEventArgs e)
        {
            SkipToEnd();
            try
            {
                string code = CommandHelper.GetSelectedCode();
                if (!string.IsNullOrEmpty(code))
                {
                    CommandHelper.InsertCode(OutputTextBox.Text);
                    InsertButton.Content = "Inserted!";
                }
                else
                {
                    InsertButton.Content = "No editor";
                }
            }
            catch
            {
                InsertButton.Content = "Failed";
            }
        }
    }
}