using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Collections.Specialized;

namespace DevGPT.App.Windows
{
    public class StoreCardModel : INotifyPropertyChanged
    {
        private string _name;
        private string _description;
        private string _path;
        private string _subDirectory;
        private string _excludePattern;
        private string _fileFilters;

        public string Name { get => _name; set { if (_name != value) { _name = value; OnPropertyChanged(nameof(Name)); } } }
        public string Description { get => _description; set { if (_description != value) { _description = value; OnPropertyChanged(nameof(Description)); } } }
        public string Path { get => _path; set { if (_path != value) { _path = value; OnPropertyChanged(nameof(Path)); } } }
        public string SubDirectory { get => _subDirectory; set { if (_subDirectory != value) { _subDirectory = value; OnPropertyChanged(nameof(SubDirectory)); } } }
        public string ExcludePattern { get => _excludePattern; set { if (_excludePattern != value) { _excludePattern = value; OnPropertyChanged(nameof(ExcludePattern)); } } }
        public string FileFilters { get => _fileFilters; set { if (_fileFilters != value) { _fileFilters = value; OnPropertyChanged(nameof(FileFilters)); } } }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string name) { PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name)); }
    }

    public class StoresCardsBindingModel : INotifyPropertyChanged
    {
        public ObservableCollection<StoreCardModel> Cards { get; set; } = new ObservableCollection<StoreCardModel>();

        private bool _isModified;
        public bool IsModified
        {
            get => _isModified;
            set { if (_isModified != value) { _isModified = value; OnPropertyChanged(nameof(IsModified)); } }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string property) { PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(property)); }

        public StoresCardsBindingModel()
        {
            Cards.CollectionChanged += Cards_CollectionChanged;
        }

        private void Cards_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            IsModified = true;
        }

        public void AddNewStoreCard()
        {
            Cards.Add(new StoreCardModel());
        }
    }
}

