using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

public static class TreeMaker
{
    public static List<TreeNode<string>> GetTree(this List<string> files)
    {
        var nodes = new List<TreeNode<string>>();
        var result = new List<TreeNode<string>>();
        foreach (var file in files)
        {
            var names = file.Split(['/', '\\']);
            TreeNode<string> parentNode = new TreeNode<string>(names[0], file);
            result.Add(parentNode);

            for (var i = 1; i < names.Length - 1; ++i)
            {
                var childNode = nodes.SingleOrDefault(n => n.Name == names[i] && n.Parent == parentNode);
                if (childNode == null)
                {
                    childNode = new TreeNode<string>(names[i]);
                    childNode.Parent = parentNode;
                    parentNode.Children.Add(childNode);
                    nodes.Add(childNode);
                    parentNode = childNode;
                }
            }
        }
        return result;
    }

    public static List<TreeNode<T>> GetTree<T>(this IDictionary<string, T> files)
    {
        var nodes = new List<TreeNode<T>>();
        var result = new List<TreeNode<T>>();
        foreach (var file in files)
        {
            var names = file.Key.Split(['/', '\\']);
            TreeNode<T> parentNode = new TreeNode<T>(names[0], file.Value);
            result.Add(parentNode);

            for (var i = 1; i < names.Length - 1; ++i)
            {
                var childNode = nodes.SingleOrDefault(n => n.Name == names[i] && n.Parent == parentNode);
                if (childNode == null)
                {
                    childNode = new TreeNode<T>(names[i]);
                    childNode.Parent = parentNode;
                    parentNode.Children.Add(childNode);
                    nodes.Add(childNode);
                    parentNode = childNode;
                }
            }
        }
        return result;
    }
}
