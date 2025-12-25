using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using Microsoft.Win32;

namespace Hazina.App.EmbeddingsViewer
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private void SelectFileButton_Click(object sender, RoutedEventArgs e)
        {
            ErrorMessageTextBlock.Visibility = Visibility.Collapsed;
            ErrorMessageTextBlock.Text = string.Empty;
            EmbeddingKeysListBox.ItemsSource = null;
            try
            {
                var dlg = new OpenFileDialog
                {
                    Filter = "All files (*.*)|*.*"
                };
                if (dlg.ShowDialog() == true)
                {
                    var filePath = dlg.FileName;
                    var store = new EmbeddingFileStore(filePath);
                    var embeddings = store.Embeddings;

                    if (embeddings == null || embeddings.Length == 0)
                    {
                        EmbeddingKeysListBox.ItemsSource = new List<string>();
                        ErrorMessageTextBlock.Text = "No embeddings found in file.";
                        ErrorMessageTextBlock.Visibility = Visibility.Visible;
                        return;
                    }

                    var names = embeddings.Select(e => e.Key).ToList();
                    EmbeddingKeysListBox.ItemsSource = names;
                    ErrorMessageTextBlock.Visibility = Visibility.Collapsed;
                    ErrorMessageTextBlock.Text = string.Empty;
                }
            }
            catch (Exception ex)
            {
                EmbeddingKeysListBox.ItemsSource = null;
                ErrorMessageTextBlock.Text = $"Error: {ex.Message}";
                ErrorMessageTextBlock.Visibility = Visibility.Visible;
            }
        }
    }
}

