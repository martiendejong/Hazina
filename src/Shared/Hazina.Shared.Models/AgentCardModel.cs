using System;
using System.Collections.ObjectModel;
using System.ComponentModel;

namespace DevGPT
{
    public class AgentCardModel : INotifyPropertyChanged
    {
        private string name;
        private string description;
        private string prompt;
        private ObservableCollection<StoreRef> stores = new ObservableCollection<StoreRef>();
        private ObservableCollection<string> functions = new ObservableCollection<string>();
        private ObservableCollection<string> callsAgents = new ObservableCollection<string>();
        private ObservableCollection<string> callsFlows = new ObservableCollection<string>();
        private bool explicitModify;
        private string newStoreToAdd;
        private bool newStoreWritable = false;
        private string newFunctionToAdd;
        private string newAgentToAdd;
        private string newFlowToAdd;
        private bool isExpanded = false; // Nieuw toegevoegde property

        public string Name { get => name; set { name = value; NotifyPropertyChanged(nameof(Name)); } }
        public string Description { get => description; set { description = value; NotifyPropertyChanged(nameof(Description)); } }
        public string Prompt { get => prompt; set { prompt = value; NotifyPropertyChanged(nameof(Prompt)); } }
        public ObservableCollection<StoreRef> Stores { get => stores; set { stores = value; NotifyPropertyChanged(nameof(Stores)); } }
        public ObservableCollection<string> Functions { get => functions; set { functions = value; NotifyPropertyChanged(nameof(Functions)); } }
        public ObservableCollection<string> CallsAgents { get => callsAgents; set { callsAgents = value; NotifyPropertyChanged(nameof(CallsAgents)); } }
        public ObservableCollection<string> CallsFlows { get => callsFlows; set { callsFlows = value; NotifyPropertyChanged(nameof(CallsFlows)); } }
        public bool ExplicitModify { get => explicitModify; set { explicitModify = value; NotifyPropertyChanged(nameof(ExplicitModify)); } }
        public string NewStoreToAdd { get => newStoreToAdd; set { newStoreToAdd = value; NotifyPropertyChanged(nameof(NewStoreToAdd)); } }
        public bool NewStoreWritable { get => newStoreWritable; set { newStoreWritable = value; NotifyPropertyChanged(nameof(NewStoreWritable)); } }
        public string NewFunctionToAdd { get => newFunctionToAdd; set { newFunctionToAdd = value; NotifyPropertyChanged(nameof(NewFunctionToAdd)); } }
        public string NewAgentToAdd { get => newAgentToAdd; set { newAgentToAdd = value; NotifyPropertyChanged(nameof(NewAgentToAdd)); } }
        public string NewFlowToAdd { get => newFlowToAdd; set { newFlowToAdd = value; NotifyPropertyChanged(nameof(NewFlowToAdd)); } }
        public bool IsExpanded { get => isExpanded; set { isExpanded = value; NotifyPropertyChanged(nameof(IsExpanded)); } } // Nieuw, default false

        public event PropertyChangedEventHandler PropertyChanged;
        private void NotifyPropertyChanged(string propertyName) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
