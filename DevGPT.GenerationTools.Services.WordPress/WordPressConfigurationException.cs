using System;

namespace DevGPTStore.Services
{
    public class WordPressConfigurationException : Exception
    {
        public WordPressConfigurationException(string message) : base(message) {}
    }
}

