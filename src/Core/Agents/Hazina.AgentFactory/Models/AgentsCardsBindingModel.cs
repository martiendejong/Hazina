//using System;
//using System.Collections.ObjectModel;
//using System.Collections.Generic;
//using System.ComponentModel;
//using System.Linq;
//using System.Collections.Specialized;

//    /// <summary>
//    /// Collection binding model for multiple agent card models.
//    /// </summary>
//    public class AgentsCardsBindingModel : INotifyPropertyChanged
//    {
//        private ObservableCollection<AgentCardModel> _cards = new ObservableCollection<AgentCardModel>();
//        private List<string> _allStores = new List<string>();
//        private List<string> _allFunctions = new List<string>();
//        private List<string> _allAgents = new List<string>();
//        private List<string> _allFlows = new List<string>();
//        private bool _isModified;

//        public ObservableCollection<AgentCardModel> Cards
//        {
//            get => _cards;
//            set { if (_cards != value) { _cards = value; OnPropertyChanged(nameof(Cards)); } }
//        }
//        public List<string> AllStores
//        {
//            get => _allStores;
//            set { if (_allStores != value) { _allStores = value; OnPropertyChanged(nameof(AllStores)); } }
//        }
//        public List<string> AllFunctions
//        {
//            get => _allFunctions;
//            set { if (_allFunctions != value) { _allFunctions = value; OnPropertyChanged(nameof(AllFunctions)); } }
//        }
//        public List<string> AllAgents
//        {
//            get => _allAgents;
//            set { if (_allAgents != value) { _allAgents = value; OnPropertyChanged(nameof(AllAgents)); } }
//        }
//        public List<string> AllFlows
//        {
//            get => _allFlows;
//            set { if (_allFlows != value) { _allFlows = value; OnPropertyChanged(nameof(AllFlows)); } }
//        }
//        public bool IsModified
//        {
//            get => _isModified;
//            set { if (_isModified != value) { _isModified = value; OnPropertyChanged(nameof(IsModified)); } }
//        }
//        public event PropertyChangedEventHandler PropertyChanged;
//        protected void OnPropertyChanged(string property) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(property));

//        public AgentsCardsBindingModel()
//        {
//            _cards.CollectionChanged += Cards_CollectionChanged;
//        }

//        private void Cards_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
//        {
//            // Attach event listeners for new agent cards, as in FlowCardsBindingModel
//            if (e.NewItems != null)
//            {
//                foreach (var item in e.NewItems)
//                {
//                    if (item is AgentCardModel model)
//                    {
//                        model.PropertyChanged += OnCardPropertyChanged;
//                    }
//                }
//            }
//            if (e.OldItems != null)
//            {
//                foreach (var item in e.OldItems)
//                {
//                    if (item is AgentCardModel model)
//                        model.PropertyChanged -= OnCardPropertyChanged;
//                }
//            }
//            IsModified = true;
//        }
//        private void OnCardPropertyChanged(object sender, PropertyChangedEventArgs e)
//        {
//            IsModified = true;
//        }
//        public void HookCardPropertyChangedHandlers()
//        {
//            foreach (var card in Cards)
//                card.PropertyChanged += OnCardPropertyChanged;
//        }
//    }
