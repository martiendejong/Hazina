using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace DevGPT.ChatShared
{
    public partial class ChatWindow : Window, INotifyPropertyChanged
    {
        private readonly IChatController _controller;
        private bool _isSending = false;
        private ObservableCollection<ChatDisplayMessage> _messages = new();
        private CancellationTokenSource _cts;
        private bool _isAgentRunning = false;
        public bool IsAgentRunning { get => _isAgentRunning; set { if (_isAgentRunning != value) { _isAgentRunning = value; OnPropertyChanged(nameof(IsAgentRunning)); } } }
        public bool IsSending { get => _isSending; set { if (_isSending != value) { _isSending = value; OnPropertyChanged(nameof(IsSending)); } } }
        public ObservableCollection<ChatDisplayMessage> Messages => _messages;
        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged(string propertyName) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        public string currentMessageId = string.Empty;
        public string AgentOrFlow { get => _agentOrFlow; set { _agentOrFlow = value; OnPropertyChanged(nameof(AgentOrFlow)); } }
        private string _agentOrFlow = string.Empty;
        public ReadOnlyObservableCollection<string> AgentsAndFlows => _controller.AgentsAndFlows;

        public ChatWindow(IChatController controller)
        {
            _controller = controller;
            InitializeComponent();
            DataContext = this;
            ChatMessagesList.ItemsSource = _messages;
            MessageEditor.VerticalAlignment = VerticalAlignment.Center;
            SendButton.VerticalAlignment = VerticalAlignment.Center;
            ChatMessagesList.VerticalAlignment = VerticalAlignment.Stretch;
            MessageEditor.Focus();
            Closed += ChatWindow_Closed;

            _controller.AttachStreaming((id, agent, output) =>
            {
                Dispatcher.Invoke(() =>
                {
                    if (currentMessageId == id)
                    {
                        var message = _messages.LastOrDefault();
                        if (message != null)
                        {
                            _messages.Remove(message);
                            message.Response = output;
                            _messages.Add(message);
                        }
                    }
                    else
                    {
                        if (_messages.Any(m => m.Id == id))
                        {
                            _messages.Add(new ChatDisplayMessage { Id = id, Author = agent, Response = output, IsAsyncOnMessage = true });
                        }
                        _messages.Add(new ChatDisplayMessage { Id = id, Author = agent, Text = output, IsAsyncOnMessage = true });
                        currentMessageId = id;
                    }
                    ScrollToEnd();
                });
            });

            if (AgentsAndFlows.Count > 0 && string.IsNullOrEmpty(AgentOrFlow))
                AgentOrFlow = _controller.DefaultAgentOrFlow;
        }

        private void ChatWindow_Closed(object sender, EventArgs e)
        {
            StopButton_Click(sender, null);
        }

        private void SetSendingState(bool isSending)
        {
            IsSending = isSending;
            SendButton.IsEnabled = !isSending;
            SendingProgress.Visibility = isSending ? Visibility.Visible : Visibility.Collapsed;
            MessageEditor.IsEnabled = !isSending;
            if (!isSending) { IsAgentRunning = false; }
        }

        private void StopButton_Click(object sender, RoutedEventArgs e)
        {
            _cts?.Cancel();
            SetSendingState(false);
            MessageEditor.Focus();
            SendButton.Visibility = Visibility.Visible;
        }

        private void SendButton_Click(object sender, RoutedEventArgs e)
        {
            if (IsSending) return;
            SendMessage();
        }

        private async void SendMessage()
        {
            if (IsSending) return;
            StopButton.Visibility = Visibility.Visible;
            SendButton.Visibility = Visibility.Hidden;

            var text = DateTime.Now.ToString("MMMM d, yyyy h:mm tt", CultureInfo.InvariantCulture) + ": " + MessageEditor.Text?.Trim();
            if (!string.IsNullOrEmpty(text))
            {
                SetSendingState(true);
                IsAgentRunning = true;
                _cts = new CancellationTokenSource();
                var token = _cts.Token;
                _messages.Add(new ChatDisplayMessage { Author = "Gebruiker", Text = text, IsAsyncOnMessage = false });
                MessageEditor.Text = string.Empty;
                ScrollToEnd();
                try
                {
                    var response = await Task.Run(() => _controller.SendMessageAsync(text, token, AgentOrFlow), token);
                    _messages.Add(new ChatDisplayMessage { Author = "Assistent", Text = response, IsAsyncOnMessage = false });
                    ScrollToEnd();
                }
                catch (OperationCanceledException)
                {
                    _messages.Add(new ChatDisplayMessage { Author = "Systeem", Text = "Versturen gestopt.", IsAsyncOnMessage = false });
                    ScrollToEnd();
                }
                catch (Exception ex)
                {
                    _messages.Add(new ChatDisplayMessage { Author = "Systeem", Text = $"Fout: {ex.Message}", IsAsyncOnMessage = false });
                    ScrollToEnd();
                }
                finally
                {
                    SetSendingState(false);
                    MessageEditor.Focus();
                    SendButton.Visibility = Visibility.Visible;
                }
            }
        }

        private void ScrollToEnd()
        {
            if (_messages.Count == 0) return;
            ChatMessagesList.UpdateLayout();
            if (ChatMessagesList.ItemContainerGenerator.ContainerFromIndex(_messages.Count - 1) is FrameworkElement lastItem)
            {
                lastItem.BringIntoView();
            }
        }

        protected override void OnPreviewKeyDown(KeyEventArgs e)
        {
            if (e.Key == Key.Enter && (Keyboard.Modifiers & ModifierKeys.Shift) == 0)
            {
                if (MessageEditor.IsFocused && !IsSending)
                {
                    e.Handled = true;
                    SendMessage();
                }
            }
            base.OnPreviewKeyDown(e);
        }
    }
}
