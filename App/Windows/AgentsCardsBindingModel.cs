using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.ComponentModel;
using System.Collections.Specialized;

namespace DevGPT.App.Windows
{
    public class AgentsCardsBindingModel : INotifyPropertyChanged
    {
        public ObservableCollection<AgentCardModel> Cards { get; set; } = new ObservableCollection<AgentCardModel>();
        public List<string> AllStores { get; set; } = new List<string>();
        public List<string> AllFunctions { get; set; } = new List<string>();
        public List<string> AllAgents { get; set; } = new List<string>();
        public List<string> AllFlows { get; set; } = new List<string>();

        private bool _isModified;
        public bool IsModified
        {
            get => _isModified;
            set { if (_isModified != value) { _isModified = value; OnPropertyChanged(nameof(IsModified)); } }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string property) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(property));

        public AgentsCardsBindingModel()
        {
            Cards.CollectionChanged += Cards_CollectionChanged;
        }

        private void Cards_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.NewItems != null)
            {
                foreach (var item in e.NewItems)
                {
                    if (item is AgentCardModel model)
                        model.PropertyChanged += OnCardPropertyChanged;
                }
            }
            if (e.OldItems != null)
            {
                foreach (var item in e.OldItems)
                {
                    if (item is AgentCardModel model)
                        model.PropertyChanged -= OnCardPropertyChanged;
                }
            }
            IsModified = true;
        }

        private void OnCardPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            IsModified = true;
        }

        public void HookCardPropertyChangedHandlers()
        {
            foreach (var card in Cards)
                card.PropertyChanged += OnCardPropertyChanged;
        }

        public void AddNewAgentCard()
        {
            Cards.Add(new AgentCardModel { Name = "", Description = "", Prompt = "" });
        }
    }
}

