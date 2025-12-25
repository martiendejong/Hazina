public class StoreRemoveEventArgs : EventArgs
{
    public string Key { get; }

    public StoreRemoveEventArgs(string key)
    {
        Key = key;
    }
}
