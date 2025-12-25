using Microsoft.Win32;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace Hazina.App.ExplorerIntegration
{
    public partial class MainWindow : Window
    {
        private string _folder;

        public MainWindow()
        {
            InitializeComponent();
            _folder = GetFolderFromArgs() ?? Directory.GetCurrentDirectory();
            FolderBox.Text = _folder;
            UpdateButtons();

            var args = System.Environment.GetCommandLineArgs();
            if (args.Any(a => a.Equals("--embed", System.StringComparison.OrdinalIgnoreCase)))
            {
                // Defer to UI thread after load
                Loaded += async (_, __) => { EmbedButton.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent)); };
            }
            else if (args.Any(a => a.Equals("--chat", System.StringComparison.OrdinalIgnoreCase)))
            {
                Loaded += async (_, __) => { ChatButton.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent)); };
            }
        }

        private void UpdateButtons()
        {
            var hasEmbeddings = File.Exists(System.IO.Path.Combine(_folder, "embeddings"))
                                 || File.Exists(System.IO.Path.Combine(_folder, "embeddings.spec"));
            EmbedButton.IsEnabled = !hasEmbeddings;
            ChatButton.IsEnabled = hasEmbeddings;
            StatusText.Text = hasEmbeddings ? "Embeddings detected. You can Start Chat." : "No embeddings found. Click Embed Files.";
        }

        private static string? GetFolderFromArgs()
        {
            var args = System.Environment.GetCommandLineArgs();
            if (args.Length >= 2)
            {
                var candidate = args[1].Trim('"');
                if (Directory.Exists(candidate)) return candidate;
            }
            return null;
        }

        private async void EmbedButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new EmbedDialog(_folder);
            if (dialog.ShowDialog() == true)
            {
                StatusText.Text = "Embedding files...";
                EmbedButton.IsEnabled = false;
                try
                {
                    await EmbeddingService.GenerateEmbeddingsAsync(
                        _folder,
                        dialog.FileFilters,
                        dialog.OpenAIKey,
                        dialog.SaveKeyToRegistry);
                    StatusText.Text = "Embeddings generated successfully.";
                    UpdateButtons();
                }
                catch (System.Exception ex)
                {
                    StatusText.Text = $"Error: {ex.Message}";
                    EmbedButton.IsEnabled = true;
                }
            }
        }

        private async void ChatButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var chat = await ChatService.CreateChatWindowAsync(_folder);
                chat.Owner = this;
                chat.ShowDialog();
            }
            catch (System.Exception ex)
            {
                StatusText.Text = $"Error: {ex.Message}";
            }
        }
    }
}


