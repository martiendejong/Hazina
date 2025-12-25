public class StoreUpdateEventArgs<T> : EventArgs
{
    public string Key { get; }
    public T Value { get; }

    public StoreUpdateEventArgs(string key, T value)
    {
        Key = key;
        Value = value;
    }
}
