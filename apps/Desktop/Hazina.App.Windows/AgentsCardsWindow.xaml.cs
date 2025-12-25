using System;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Data;
using System.Collections.Generic;

namespace Hazina.App.Windows;

public partial class AgentsCardsWindow : Window
{
    private AgentsCardsBindingModel viewModel;
    public List<AgentConfig> ResultAgents = null;

    public Action<AgentsCardsBindingModel> OnAgentsSaved;

    public AgentsCardsWindow(AgentsCardsBindingModel model)
    {
        InitializeComponent();
        viewModel = model;
        DataContext = viewModel;
        viewModel.PropertyChanged += ViewModel_PropertyChanged;
    }

    // Nieuw: knop event, voegt lege agent toe via bindingmodel
    private void AddNewAgentButton_Click(object sender, RoutedEventArgs e)
    {
        viewModel.AddNewAgentCard();
    }

    // Nieuw: Handler voor toggelen van expand/collapse van een AgentCardModel
    private void ToggleAgentExpandButton_Click(object sender, RoutedEventArgs e)
    {
        var btn = sender as System.Windows.Controls.Button;
        // De DataContext van de knop is het AgentCardModel
        if (btn?.DataContext is AgentCardModel card)
        {
            card.IsExpanded = !card.IsExpanded;
        }
    }

    // (Optioneel: verwijder uit xaml dubbele '+ Nieuwe agent' indien nodig)
    // Event om een agentcard te verwijderen
    private void RemoveAgentCardButton_Click(object sender, RoutedEventArgs e)
    {
        var btn = sender as System.Windows.Controls.Button;
        var card = btn?.CommandParameter as AgentCardModel;
        if (card != null)
        {
            viewModel.Cards.Remove(card);
        }
    }

    private void AddStoreButton_Click(object sender, RoutedEventArgs e)
    {
        var btn = sender as System.Windows.Controls.Button;
        var card = btn?.CommandParameter as AgentCardModel;
        if (card != null && !string.IsNullOrWhiteSpace(card.NewStoreToAdd))
        {
            if (!card.Stores.Any(s => s.Name == card.NewStoreToAdd))
            {
                card.Stores.Add(new StoreRef
                {
                    Name = card.NewStoreToAdd,
                    Write = card.NewStoreWritable
                });
            }
            card.NewStoreToAdd = null;
            card.NewStoreWritable = false;
        }
    }

    private void RemoveStoreButton_Click(object sender, RoutedEventArgs e)
    {
        var btn = sender as System.Windows.Controls.Button;
        var store = btn?.CommandParameter as StoreRef;
        foreach (var card in viewModel.Cards)
        {
            if (card.Stores.Contains(store))
            {
                card.Stores.Remove(store);
                break;
            }
        }
    }

    private void AddFunctionButton_Click(object sender, RoutedEventArgs e)
    {
        var btn = sender as System.Windows.Controls.Button;
        var card = btn?.CommandParameter as AgentCardModel;
        if (card != null && !string.IsNullOrWhiteSpace(card.NewFunctionToAdd))
        {
            if (!card.Functions.Contains(card.NewFunctionToAdd))
            {
                card.Functions.Add(card.NewFunctionToAdd);
            }
            card.NewFunctionToAdd = null;
        }
    }

    private void RemoveFunctionButton_Click(object sender, RoutedEventArgs e)
    {
        var btn = sender as System.Windows.Controls.Button;
        var func = btn?.CommandParameter as string;
        foreach (var card in viewModel.Cards)
        {
            if (card.Functions.Contains(func))
            {
                card.Functions.Remove(func);
                break;
            }
        }
    }

    private void AddAgentButton_Click(object sender, RoutedEventArgs e)
    {
        var btn = sender as System.Windows.Controls.Button;
        var card = btn?.CommandParameter as AgentCardModel;
        if (card != null && !string.IsNullOrWhiteSpace(card.NewAgentToAdd))
        {
            if (!card.CallsAgents.Contains(card.NewAgentToAdd))
            {
                card.CallsAgents.Add(card.NewAgentToAdd);
            }
            card.NewAgentToAdd = null;
        }
    }

    private void RemoveAgentButton_Click(object sender, RoutedEventArgs e)
    {
        var btn = sender as System.Windows.Controls.Button;
        var agent = btn?.CommandParameter as string;
        foreach (var card in viewModel.Cards)
        {
            if (card.CallsAgents.Contains(agent))
            {
                card.CallsAgents.Remove(agent);
                break;
            }
        }
    }

    private void AddFlowButton_Click(object sender, RoutedEventArgs e)
    {
        var btn = sender as System.Windows.Controls.Button;
        var card = btn?.CommandParameter as AgentCardModel;
        if (card != null && !string.IsNullOrWhiteSpace(card.NewFlowToAdd))
        {
            if (!card.CallsFlows.Contains(card.NewFlowToAdd))
            {
                card.CallsFlows.Add(card.NewFlowToAdd);
            }
            card.NewFlowToAdd = null;
        }
    }

    private void RemoveFlowButton_Click(object sender, RoutedEventArgs e)
    {
        var btn = sender as System.Windows.Controls.Button;
        var flow = btn?.CommandParameter as string;
        foreach (var card in viewModel.Cards)
        {
            if (card.CallsFlows.Contains(flow))
            {
                card.CallsFlows.Remove(flow);
                break;
            }
        }
    }

    private void OpslaanButton_Click(object sender, RoutedEventArgs e)
    {
        var list = viewModel.Cards.Select(card => new AgentConfig
        {
            Name = card.Name,
            Description = card.Description,
            CallsAgents = card.CallsAgents.ToList(),
            CallsFlows = card.CallsFlows.ToList(),
            Functions = card.Functions.ToList(),
            Prompt = card.Prompt,
            Stores = card.Stores.ToList(),
            ExplicitModify = card.ExplicitModify                
        }).ToList();
        ResultAgents = list;
        DialogResult = true;
        OnAgentsSaved?.Invoke(viewModel);
        Close();
    }

    private void AnnuleerButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void ViewModel_PropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        // Niets op dit moment
    }

    private void ShowError(string message)
    {
        System.Windows.MessageBox.Show(this, message, "Fout", MessageBoxButton.OK, MessageBoxImage.Error);
    }
}

