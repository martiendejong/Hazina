using System.Collections.ObjectModel;

public class TreeNode<T>
{
    public string Name { get; set; }
    public T? Value { get; set; }
    public TreeNode<T>? Parent { get; set; }
    public ObservableCollection<TreeNode<T>> Children { get; set; }

    public TreeNode(string name, T? value = default!, ObservableCollection<TreeNode<T>>? children = null)
    {
        Name = name;
        Value = value;
        Children = children ?? new ObservableCollection<TreeNode<T>>();
    }
}
