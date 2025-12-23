using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System;
using System.Windows.Data;
using System.Collections.Specialized;
using System.Windows.Media;
using System.Windows.Documents;

namespace DevGPT.App.Windows;
using WpfButton = System.Windows.Controls.Button;
using WpfListView = System.Windows.Controls.ListView;
using WpfListViewItem = System.Windows.Controls.ListViewItem;


public partial class FlowsCardsWindow : System.Windows.Window
{
    public FlowCardsBindingModel Model { get; set; }
    public FlowsCardsWindow(FlowCardsBindingModel model)
    {
        InitializeComponent();
        DataContext = Model = model;
        // expand state already set in model CollapseAllCards and/or AddNewFlowCard
        Model.HookCardPropertyChangedHandlers();
        this.Closing += FlowsCardsWindow_Closing;
    }
    public List<FlowConfig> ResultFlows { get; set; } = null;

    // ------------ DRAG & DROP Logica voor CallsAgents (herordening binnen 1 card) ------------

    // Houdt de drag-context vast per ListView (1 tegelijk per window)
    private System.Windows.Point? _dragStartPoint = null;
    private WpfListView _activeDragListView = null;
    private string _draggedAgent = null;
    private FlowCardModel _draggedCard = null;

    /// <summary>
    /// Detecteert de start van een drag: bewaar initi+½le muispositie en listview. 
    /// </summary>
    private void CallsAgentsListView_PreviewMouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        // Zoek het item waar op geklikt is.
        _activeDragListView = sender as WpfListView;
        _dragStartPoint = e.GetPosition(_activeDragListView);
        _draggedAgent = null;
        _draggedCard = null;

        if (_activeDragListView != null)
        {
            var item = GetListViewItemAt(e.GetPosition(_activeDragListView), _activeDragListView);
            if (item != null)
            {
                _draggedAgent = item.Content as string;
                _draggedCard = _activeDragListView.DataContext as FlowCardModel;
            }
        }
    }

    /// <summary>
    /// Start de drag-operation na voldoende movement.
    /// </summary>
    private void CallsAgentsListView_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (_dragStartPoint.HasValue && e.LeftButton == System.Windows.Input.MouseButtonState.Pressed && _draggedAgent != null && _draggedCard != null)
        {
            var currentPos = e.GetPosition(_activeDragListView);
            if (Math.Abs(currentPos.X - _dragStartPoint.Value.X) > SystemParameters.MinimumHorizontalDragDistance ||
                Math.Abs(currentPos.Y - _dragStartPoint.Value.Y) > SystemParameters.MinimumVerticalDragDistance)
            {
                // Slepen starten; gebruik DataObject (enkel local)
                System.Windows.DragDrop.DoDragDrop(_activeDragListView, new System.Windows.DataObject("DGPT_FLOW_DRAGCALLAGENT", _draggedAgent), System.Windows.DragDropEffects.Move);
            }
        }
    }

    /// <summary>
    /// Controleer of drop geldig is, geef visuele feedback op target.
    /// </summary>
    private void CallsAgentsListView_DragOver(object sender, System.Windows.DragEventArgs e)
    {
        e.Effects = System.Windows.DragDropEffects.None;

        var listView = sender as WpfListView;
        // Check of de drag binnen dezelfde ListView is en juiste type object
        if (listView != null && e.Data.GetDataPresent("DGPT_FLOW_DRAGCALLAGENT"))
        {
            // NIET tussen cards, alleen binnen de eigen ListView
            if (listView == _activeDragListView)
            {
                e.Effects = System.Windows.DragDropEffects.Move;
                e.Handled = true;
            }
        }

        // VISUELE FEEDBACK: highlight doel item (zie ook attached property!)
        HighlightListViewItemOnDragOver(listView, e.GetPosition(listView));
    }

    /// <summary>
    /// Drop event: voegt agent toe aan nieuw slot, verwijdert uit oud.
    /// </summary>
    private void CallsAgentsListView_Drop(object sender, System.Windows.DragEventArgs e)
    {
        var listView = sender as WpfListView;
        if (listView == null || !_dragStartPoint.HasValue) return;
        if (!e.Data.GetDataPresent("DGPT_FLOW_DRAGCALLAGENT")) return;

        string agent = e.Data.GetData("DGPT_FLOW_DRAGCALLAGENT") as string;
        var card = listView.DataContext as FlowCardModel;
        if (card == null || agent == null) return;

        int oldIndex = card.CallsAgents.IndexOf(agent);
        if (oldIndex == -1) return;

        // Bereken nieuwe index op basis van droppositie
        int newIndex = FindIndexForDrop(e.GetPosition(listView), listView);
        if (newIndex < 0) newIndex = card.CallsAgents.Count - 1;

        // Corrigeer wanneer over zichzelf wordt gezet
        if (newIndex == oldIndex || newIndex == oldIndex + 1)
        {
            ClearDragOverHighlights(listView);
            return;
        }

        // Herschik item in ObservableCollection
        if (newIndex > oldIndex) newIndex--;
        card.CallsAgents.Move(oldIndex, newIndex);
        Model.IsModified = true;

        // Reset state & feedback
        ClearDragOverHighlights(listView);
        _dragStartPoint = null;
        _draggedAgent = null;
        _draggedCard = null;
    }

    // ---------------- VISUELE DRAG-FEEDBACK (per ListViewItem) ----------------------
    // We gebruiken een attached property om IsDragOverItem in xaml uit te kunnen lezen voor stijl
    public static readonly DependencyProperty IsDragOverItemProperty = DependencyProperty.RegisterAttached(
        "IsDragOverItem", typeof(bool), typeof(FlowsCardsWindow), new PropertyMetadata(false));
    public static bool GetIsDragOverItem(DependencyObject obj) => (bool)obj.GetValue(IsDragOverItemProperty);
    public static void SetIsDragOverItem(DependencyObject obj, bool value) => obj.SetValue(IsDragOverItemProperty, value);

    /// <summary>
    /// Zet alle IsDragOverItem flags uit.
    /// </summary>
    private void ClearDragOverHighlights(WpfListView lv)
    {
        foreach (var item in lv.Items)
        {
            var lvi = lv.ItemContainerGenerator.ContainerFromItem(item) as WpfListViewItem;
            if (lvi != null) SetIsDragOverItem(lvi, false);
        }
    }

    /// <summary>
    /// Highlight alleen het item waarover wordt gedropt (op basis van muispositie)
    /// </summary>
    private void HighlightListViewItemOnDragOver(WpfListView lv, System.Windows.Point pt)
    {
        ClearDragOverHighlights(lv);
        var lvi = GetListViewItemAt(pt, lv);
        if (lvi != null) SetIsDragOverItem(lvi, true);
    }

    /// <summary>
    /// Vind ListViewItem bij mousepositie (helper method)
    /// </summary>
    private WpfListViewItem GetListViewItemAt(System.Windows.Point pt, WpfListView lv)
    {
        var hit = VisualTreeHelper.HitTest(lv, pt);
        if (hit == null) return null;
        DependencyObject obj = hit.VisualHit;
        while (obj != null && !(obj is WpfListViewItem))
            obj = VisualTreeHelper.GetParent(obj);
        return obj as WpfListViewItem;
    }

    /// <summary>
    /// Vind index in CallsAgents waar de drop plaatsvindt.
    /// </summary>
    private int FindIndexForDrop(System.Windows.Point pt, WpfListView lv)
    {
        for (int i = 0; i < lv.Items.Count; i++)
        {
            var item = lv.ItemContainerGenerator.ContainerFromIndex(i) as WpfListViewItem;
            if (item != null)
            {
                System.Windows.Rect bounds = VisualTreeHelper.GetDescendantBounds(item);
                System.Windows.Point topLeft = item.TranslatePoint(new System.Windows.Point(0, 0), lv);
                bounds.Offset(topLeft.X, topLeft.Y);
                if (pt.Y >= bounds.Top && pt.Y < bounds.Bottom)
                    return i;
            }
        }
        // Drop NA laatste item
        return lv.Items.Count;
    }

    // --------------- Testmethode: test herschikking van ObservableCollection ---------------
    /// <summary>
    /// Eenvoudige testfunctie voor herschikken van ObservableCollection<string>.
    /// </summary>
    private void TestReorderObservableColl()
    {
        var oc = new ObservableCollection<string>(new[] { "A", "B", "C", "D" });
        // Test verplaatsing: B van index 1 naar 3 (achter D)
        oc.Move(1, 3);
        if (oc[3] != "B" || oc[1] != "C")
            throw new Exception("Reordering failed");
        // Test verplaatsing: D naar positie 2
        oc.Move(3, 2);
        if (oc[2] != "D")
            throw new Exception("Reordering failed");
    }

    // ------- Rest van bestaande logica -------

    // SERIALIZER INTERFACE toegevoegde methodes/velden:
    // Zet deze delegate vanuit MainWindow of de aanroeper om serialisatie/updaten editor te implementeren.
    public Action<List<FlowConfig>> OnFlowsSaved;

    private void OpslaanButton_Click(object sender, RoutedEventArgs e)
    {
        var list = Model.Cards.Select(card => new FlowConfig
        {
            Name = card.Name,
            Description = card.Description,
            CallsAgents = card.CallsAgents.ToList()
        }).ToList();
        ResultFlows = list;
        Model.IsModified = false;
        DialogResult = true;
        // Toevoeging: na OK meteen eventuele OnFlowsSaved aanroepen, zodat parent de editor/data bijwerkt.
        OnFlowsSaved?.Invoke(ResultFlows);
        Close();
    }
    private void AnnuleerButton_Click(object sender, RoutedEventArgs e)
    {
        ResultFlows = null;
        DialogResult = false;
        Close();
    }
    private void AddAgentButton_Click(object sender, RoutedEventArgs e)
    {
        var b = (sender as WpfButton);
        var card = b?.CommandParameter as FlowCardModel;
        if (card != null && !string.IsNullOrWhiteSpace(card.NewAgentToAdd))
        {
            if (!card.CallsAgents.Contains(card.NewAgentToAdd))
            {
                card.CallsAgents.Add(card.NewAgentToAdd);
            }
            card.NewAgentToAdd = null;
        }
    }

    private void FlowsCardsWindow_Closing(object sender, CancelEventArgs e)
    {
        if (!(this.DialogResult ?? false) && Model.IsModified)
        {
            var msg = "Wilt u uw wijzigingen opslaan voordat u afsluit?";
            var res = System.Windows.MessageBox.Show(msg, "Wijzigingen opslaan?", MessageBoxButton.YesNoCancel, MessageBoxImage.Warning);
            if (res == MessageBoxResult.Yes)
            {
                OpslaanButton_Click(null, null);
            }
            else if (res == MessageBoxResult.No)
            {
                ResultFlows = null;
            }
            else if (res == MessageBoxResult.Cancel)
            {
                e.Cancel = true;
            }
        }
    }

    // Nieuw: handler voor nieuwe flow toevoegen-knop
    private void AddNewFlowButton_Click(object sender, RoutedEventArgs e)
    {
        Model.AddNewFlowCard();
    }

    public void RemoveFlowCardButton_Click(object sender, RoutedEventArgs e)
    {
        var btn = sender as WpfButton;
        var card = btn?.CommandParameter as FlowCardModel;
        if (card != null)
        {
            Model.Cards.Remove(card);
        }
    }

    // --- Handler voor per-kaart expand/collapse toggle ---
    public void ToggleExpandButton_Click(object sender, RoutedEventArgs e)
    {
        // Gebruik de CommandParameter of DataContext van de knop om het juiste FlowCardModel te krijgen
        var btn = sender as WpfButton;
        var card = btn?.CommandParameter as FlowCardModel;
        if (card == null)
            card = btn?.DataContext as FlowCardModel;

        if (card != null)
        {
            card.IsExpanded = !card.IsExpanded;
        }
    }

    // --- Oude broadened agent expand toggle (niet meer gebruikt, voor agentdetails per kaart, laat staan voor compatibiliteit) ---
    public void ToggleAgentExpandButton_Click(object sender, RoutedEventArgs e)
    {
        var button = sender as WpfButton;
        if (button == null) return;
        var listViewItem = FindParent<WpfListViewItem>(button);
        if (listViewItem == null) return;
        var listView = FindParent<WpfListView>(listViewItem);
        if (listView == null) return;
        var card = listView.DataContext as FlowCardModel;
        if (card != null)
        {
            card.IsExpanded = !card.IsExpanded;
        }
    }

    private static T FindParent<T>(DependencyObject child) where T : DependencyObject
    {
        DependencyObject parentObject = VisualTreeHelper.GetParent(child);
        if (parentObject == null) return null;
        if (parentObject is T parent) return parent;
        else return FindParent<T>(parentObject);
    }
}

