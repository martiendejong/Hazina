using System.Collections;
using System.Diagnostics.CodeAnalysis;

public abstract class AbstractStore<T> : IDictionary<string, T>
{
    public abstract Task Store(string key, T value);
    public abstract bool Remove(string key);

    public event EventHandler<StoreUpdateEventArgs<T>>? BeforeUpdate;
    public event EventHandler<StoreUpdateEventArgs<T>>? AfterUpdate;
    public event EventHandler<StoreRemoveEventArgs>? BeforeRemove;
    public event EventHandler<StoreRemoveEventArgs>? AfterRemove;

    protected void InvokeBeforeUpdate(string key, T value)
    {
        BeforeUpdate?.Invoke(this, new StoreUpdateEventArgs<T>(key, value));
    }

    protected void InvokeAfterUpdate(string key, T value)
    {
        AfterUpdate?.Invoke(this, new StoreUpdateEventArgs<T>(key, value));
    }

    protected void InvokeBeforeRemove(string key)
    {
        BeforeRemove?.Invoke(this, new StoreRemoveEventArgs(key));
    }

    protected void InvokeAfterRemove(string key)
    {
        AfterRemove?.Invoke(this, new StoreRemoveEventArgs(key));
    }

    public Dictionary<string, T> Data = [];

    #region implementation of IDictionary

    public T this[string key] { get => Data[key]; set => Store(key, value).RunSynchronously(); }

    public ICollection<string> Keys
    {
        get
        {
            return Data.Keys;
        }
    }

    public ICollection<T> Values
    {
        get
        {
            return Data.Values;
        }
    }

    public int Count
    {
        get
        {
            return Data.Count;
        }
    }

    public bool IsReadOnly
    {
        get
        {
            return false;
        }
    }

    public void Add(string key, T value)
    {
        Store(key, value).RunSynchronously();
    }

    public void Add(KeyValuePair<string, T> item)
    {
        Store(item.Key, item.Value).RunSynchronously();
    }

    public void Clear()
    {
        Data.Clear();
    }

    public bool Contains(KeyValuePair<string, T> item)
    {
        return Data.Contains(item);
    }

    public bool ContainsKey(string key)
    {
        return Data.ContainsKey(key);
    }

    public void CopyTo(KeyValuePair<string, T>[] array, int arrayIndex)
    {
        Data.ToList().CopyTo(array, arrayIndex);
    }

    public IEnumerator<KeyValuePair<string, T>> GetEnumerator()
    {
        return Data.GetEnumerator();
    }

    public bool Remove(KeyValuePair<string, T> item)
    {
        return Remove(item.Key);
    }

    public bool TryGetValue(string key, [MaybeNullWhen(false)] out T value)
    {
        return Data.TryGetValue(key, out value);
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return Data.GetEnumerator();
    }

    #endregion
}
