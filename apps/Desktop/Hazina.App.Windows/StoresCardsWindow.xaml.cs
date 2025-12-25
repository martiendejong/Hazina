using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
namespace DevGPT.App.Windows {
    public partial class StoresCardsWindow : Window
    {
        private StoresCardsBindingModel viewModel;
        public List<StoreConfig> ResultStores = null;

        public StoresCardsWindow(StoresCardsBindingModel model)
        {
            InitializeComponent();
            viewModel = model;
            DataContext = viewModel;
        }

        private void AddNewStoreButton_Click(object sender, RoutedEventArgs e)
        {
            viewModel.AddNewStoreCard();
        }

        private void RemoveStoreCardButton_Click(object sender, RoutedEventArgs e)
        {
            var btn = sender as System.Windows.Controls.Button;
            var card = btn?.CommandParameter as StoreCardModel;
            if (card != null) viewModel.Cards.Remove(card);
        }

        private void OpslaanButton_Click(object sender, RoutedEventArgs e)
        {
            ResultStores = viewModel.Cards.Select(card => new StoreConfig {
                Name = card.Name,
                Description = card.Description,
                Path = card.Path,
                SubDirectory = card.SubDirectory,
                ExcludePattern = StringSplitter.Split(card.ExcludePattern),
                FileFilters = StringSplitter.Split(card.FileFilters)
            }).ToList();
            DialogResult = true;
            Close();
        }

        private void AnnuleerButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}

