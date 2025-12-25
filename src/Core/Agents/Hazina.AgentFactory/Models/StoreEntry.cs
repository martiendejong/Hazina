using System.ComponentModel;
/// <summary>
/// Represents a store entry for agents; supports WPF data binding.
/// </summary>
public class StoreEntry : INotifyPropertyChanged
    {
        private string _storeName;
        private bool _writable;
        public string StoreName
        {
            get => _storeName;
            set { if (_storeName != value) { _storeName = value; OnPropertyChanged(nameof(StoreName)); } }
        }
        public bool Writable
        {
            get => _writable;
            set { if (_writable != value) { _writable = value; OnPropertyChanged(nameof(Writable)); } }
        }
        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string property) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(property));
    }
