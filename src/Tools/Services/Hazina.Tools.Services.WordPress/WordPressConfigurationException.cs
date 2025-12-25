using System;

namespace HazinaStore.Services
{
    public class WordPressConfigurationException : Exception
    {
        public WordPressConfigurationException(string message) : base(message) {}
    }
}

