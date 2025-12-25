using System.ComponentModel;

namespace Hazina.ChatShared
{
    public class ChatDisplayMessage : INotifyPropertyChanged
    {
        public string Id { get; set; }
        public string Author { get; set; }
        public string Text { get; set; }
        public string Response { get; set; }
        public bool IsAsyncOnMessage { get; set; }
        public event PropertyChangedEventHandler PropertyChanged;
    }
}
