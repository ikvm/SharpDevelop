using Markdig;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace ICSharpCode.AiAgent
{
    public partial class ThinkingStreamControl : UserControl
    {
        private int _totalChars;
        private bool _isComplete;
        private bool _showPreview;
        private DispatcherTimer _thinkingTimer;
        private int _thinkingDotCount;
        private readonly MarkdownPipeline _markdownPipeline;

        public bool IsStreaming { get; private set; }

        public ThinkingStreamControl()
        {
            InitializeComponent();
            InsertButton.IsEnabled = false;
            CopyButton.IsEnabled = false;
            CopyHtmlButton.IsEnabled = false;

            _markdownPipeline = new MarkdownPipelineBuilder()
                .UseAdvancedExtensions()
                .Build();
        }

        public void BeginStream(string title = "Output")
        {
            OutputTextBox.Text = string.Empty;
            _totalChars = 0;
            _isComplete = false;
            _showPreview = false;
            IsStreaming = true;
            TitleText.Text = title;
            ProgressText.Text = "Streaming...";
            StreamStatusText.Text = "Receiving";
            CancelButton.Visibility = Visibility.Visible;
            PreviewToggle.IsEnabled = false;
            InsertButton.IsEnabled = false;
            CopyButton.IsEnabled = false;
            CopyHtmlButton.IsEnabled = false;
            ShowRawView();

            _thinkingTimer = new DispatcherTimer();
            _thinkingTimer.Interval = TimeSpan.FromMilliseconds(500);
            _thinkingTimer.Tick += ThinkingTimerTick;
            _thinkingTimer.Start();
        }

        private void ThinkingTimerTick(object sender, EventArgs e)
        {
            _thinkingDotCount = (_thinkingDotCount % 3) + 1;
            StreamStatusText.Text = $"Receiving{new string('.', _thinkingDotCount)}";
        }

        public void AppendChunk(string chunk)
        {
            if (!IsStreaming || _isComplete)
                return;

            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.BeginInvoke(new Action<string>(InternalAppend), chunk);
                return;
            }
            InternalAppend(chunk);
        }

        private void InternalAppend(string chunk)
        {
            OutputTextBox.Text += chunk;
            _totalChars += chunk.Length;
            ProgressText.Text = $"{_totalChars} chars";

            if (!_showPreview)
                RawScrollViewer.ScrollToBottom();
        }

        public void SetCompleted(string finalStatus = "Complete")
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.BeginInvoke(new Action<string>(SetCompleted), finalStatus);
                return;
            }

            if (_thinkingTimer != null && _thinkingTimer.IsEnabled)
                _thinkingTimer.Stop();

            _isComplete = true;
            IsStreaming = false;
            CancelButton.Visibility = Visibility.Collapsed;
            StreamStatusText.Text = "Done";
            ProgressText.Text = $"Complete ({_totalChars} chars)";
            PreviewToggle.IsEnabled = true;
            InsertButton.IsEnabled = true;
            CopyButton.IsEnabled = true;
            CopyHtmlButton.IsEnabled = true;

            if (_showPreview)
                RefreshPreview();
        }

        public void SetError(string errorMessage)
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.BeginInvoke(new Action<string>(SetError), errorMessage);
                return;
            }

            if (_thinkingTimer != null && _thinkingTimer.IsEnabled)
                _thinkingTimer.Stop();

            _isComplete = true;
            IsStreaming = false;
            CancelButton.Visibility = Visibility.Collapsed;
            StreamStatusText.Text = "Error";
            StreamStatusText.Foreground = System.Windows.Media.Brushes.Red;
            ProgressText.Text = $"Error: {errorMessage}";
            PreviewToggle.IsEnabled = false;
            InsertButton.IsEnabled = true;
            CopyButton.IsEnabled = true;
            CopyHtmlButton.IsEnabled = true;
        }

        public void AppendChunkAndFinalize(string fullContent)
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.BeginInvoke(new Action<string>(AppendChunkAndFinalize), fullContent);
                return;
            }

            BeginStream(TitleText.Text);

            _thinkingTimer?.Stop();
            int batchSize = 3;
            int index = 0;
            var timer = new DispatcherTimer();
            timer.Interval = TimeSpan.FromMilliseconds(20);
            timer.Tick += (s, e) =>
            {
                int charsToAppend = Math.Min(batchSize, fullContent.Length - index);
                if (charsToAppend <= 0)
                {
                    timer.Stop();
                    SetCompleted();
                    return;
                }
                OutputTextBox.Text += fullContent.Substring(index, charsToAppend);
                index += charsToAppend;
                _totalChars = index;
                ProgressText.Text = $"{index}/{fullContent.Length} chars";
                if (!_showPreview)
                    RawScrollViewer.ScrollToBottom();
            };
            timer.Start();
        }

        public void Cancel()
        {
            if (_thinkingTimer != null && _thinkingTimer.IsEnabled)
                _thinkingTimer.Stop();
            _isComplete = true;
            IsStreaming = false;
            CancelButton.Visibility = Visibility.Collapsed;
            StreamStatusText.Text = "Cancelled";
            InsertButton.IsEnabled = true;
            CopyButton.IsEnabled = true;
            CopyHtmlButton.IsEnabled = true;
        }

        public void Clear()
        {
            if (_thinkingTimer != null && _thinkingTimer.IsEnabled)
                _thinkingTimer.Stop();
            OutputTextBox.Text = string.Empty;
            _totalChars = 0;
            _isComplete = false;
            _showPreview = false;
            IsStreaming = false;
            CancelButton.Visibility = Visibility.Collapsed;
            PreviewToggle.IsEnabled = false;
            TitleText.Text = "Output";
            ProgressText.Text = string.Empty;
            StreamStatusText.Text = string.Empty;
            InsertButton.IsEnabled = false;
            CopyButton.IsEnabled = false;
            CopyHtmlButton.IsEnabled = false;
            StreamStatusText.Foreground = System.Windows.Media.Brushes.Gray;
            ShowRawView();
        }

        public string GetFullContent()
        {
            return OutputTextBox.Text;
        }

        private void ShowRawView()
        {
            RawScrollViewer.Visibility = Visibility.Visible;
            PreviewBrowser.Visibility = Visibility.Collapsed;
            PreviewToggle.Content = "Preview";
        }

        private void ShowPreviewView()
        {
            RawScrollViewer.Visibility = Visibility.Collapsed;
            PreviewBrowser.Visibility = Visibility.Visible;
            PreviewToggle.Content = "Raw";
        }

        private void RefreshPreview()
        {
            string markdown = OutputTextBox.Text;
            if (string.IsNullOrEmpty(markdown))
            {
                PreviewBrowser.NavigateToString("<html><body style='font-family:sans-serif;color:#888;padding:20px;'><p>No content</p></body></html>");
                return;
            }

            string html = Markdown.ToHtml(markdown, _markdownPipeline);
            string styledHtml = BuildStyledHtml(html);
            PreviewBrowser.NavigateToString(styledHtml);
        }

        private static string BuildStyledHtml(string bodyHtml)
        {
            return $@"<!DOCTYPE html>
<html>
<head>
<meta http-equiv='Content-Type' content='text/html;charset=UTF-8'>
<style>
  body {{
    font-family: 'Segoe UI', -apple-system, sans-serif;
    font-size: 13px;
    line-height: 1.6;
    color: #1a1a1a;
    padding: 12px 16px;
    margin: 0;
    background: #fff;
  }}
  h1 {{ font-size: 1.4em; border-bottom: 1px solid #eee; padding-bottom: 4px; margin: 12px 0 6px; }}
  h2 {{ font-size: 1.2em; border-bottom: 1px solid #eee; padding-bottom: 3px; margin: 10px 0 5px; }}
  h3 {{ font-size: 1.1em; margin: 8px 0 4px; }}
  h4 {{ font-size: 1.05em; margin: 6px 0 3px; }}
  p {{ margin: 4px 0; }}
  code {{
    font-family: 'Cascadia Code', 'Fira Code', 'Consolas', monospace;
    font-size: 12px;
    background: #f0f0f0;
    padding: 1px 4px;
    border-radius: 3px;
  }}
  pre {{
    background: #1e1e2e;
    color: #cdd6f4;
    padding: 10px 14px;
    border-radius: 6px;
    overflow-x: auto;
    font-size: 12px;
    line-height: 1.5;
  }}
  pre code {{
    background: transparent;
    padding: 0;
    color: inherit;
  }}
  blockquote {{
    border-left: 3px solid #7c3aed;
    margin: 6px 0;
    padding: 4px 12px;
    color: #555;
    background: #f8f6ff;
  }}
  table {{
    border-collapse: collapse;
    width: 100%;
    margin: 6px 0;
  }}
  th, td {{
    border: 1px solid #ddd;
    padding: 5px 8px;
    text-align: left;
  }}
  th {{ background: #f5f5f5; font-weight: 600; }}
  ul, ol {{ margin: 4px 0; padding-left: 22px; }}
  li {{ margin: 2px 0; }}
  hr {{ border: none; border-top: 1px solid #ddd; margin: 10px 0; }}
  a {{ color: #2563eb; text-decoration: none; }}
  a:hover {{ text-decoration: underline; }}
</style>
</head>
<body>{bodyHtml}</body>
</html>";
        }

        private void PreviewToggle_Click(object sender, RoutedEventArgs e)
        {
            _showPreview = !_showPreview;

            if (_showPreview)
            {
                RefreshPreview();
                ShowPreviewView();
            }
            else
            {
                ShowRawView();
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            Cancel();
        }

        private void CopyButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Clipboard.SetText(OutputTextBox.Text);
                FlashButtonText(CopyButton, "Copied!");
            }
            catch
            {
                FlashButtonText(CopyButton, "Failed");
            }
        }

        private void CopyHtmlButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string html = Markdown.ToHtml(OutputTextBox.Text, _markdownPipeline);
                string styledHtml = BuildStyledHtml(html);
                Clipboard.SetText(styledHtml);
                FlashButtonText(CopyHtmlButton, "Copied!");
            }
            catch
            {
                FlashButtonText(CopyHtmlButton, "Failed");
            }
        }

        private void InsertButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                CommandHelper.InsertCode(OutputTextBox.Text);
                FlashButtonText(InsertButton, "Inserted!");
            }
            catch
            {
                FlashButtonText(InsertButton, "Failed");
            }
        }

        private static void FlashButtonText(Button button, string text)
        {
            string original = button.Content.ToString();
            button.Content = text;
            button.Dispatcher.BeginInvoke(new Action(() =>
            {
                button.Content = original;
            }), DispatcherPriority.Background, TimeSpan.FromSeconds(1.5));
        }
    }
}