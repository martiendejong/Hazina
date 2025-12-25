using Microsoft.Win32;
using System.Windows;

namespace DevGPT.App.ExplorerIntegration
{
    public partial class EmbedDialog : Window
    {
        public string Folder { get; }
        public string FileFilters { get; private set; } = "txt,cs,js";
        public string OpenAIKey { get; private set; } = string.Empty;
        public bool SaveKeyToRegistry { get; private set; } = true;

        public EmbedDialog(string folder)
        {
            InitializeComponent();
            Folder = folder;
            FiltersBox.Text = "txt,cs,js";
            var existing = Registry.GetValue("HKEY_CURRENT_USER\\Software\\DevGPT\\OpenAI", "ApiKey", null) as string;
            if (!string.IsNullOrWhiteSpace(existing))
            {
                KeyBox.Password = existing;
                SaveKeyBox.IsChecked = true;
            }
        }

        private void Start_Click(object sender, RoutedEventArgs e)
        {
            FileFilters = FiltersBox.Text?.Trim() ?? "txt,cs,js";
            OpenAIKey = KeyBox.Password ?? string.Empty;
            SaveKeyToRegistry = SaveKeyBox.IsChecked == true;
            if (string.IsNullOrWhiteSpace(OpenAIKey))
            {
                MessageBox.Show(this, "Please provide an OpenAI API key.", "DevGPT", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            DialogResult = true;
        }
    }
}



