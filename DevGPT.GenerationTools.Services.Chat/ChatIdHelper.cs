using System;

namespace DevGPT.GenerationTools.Services.Chat
{
    /// <summary>
    /// Helper class for parsing and handling chat IDs
    /// </summary>
    public static class ChatIdHelper
    {
        /// <summary>
        /// Extracts the parent chat ID from a sub-chat ID.
        /// Returns null if this is not a sub-chat.
        /// </summary>
        /// <param name="chatId">The chat ID to parse (format: "parentId.subId")</param>
        /// <returns>The parent chat ID or null</returns>
        public static string GetParentChatId(string chatId)
        {
            if (string.IsNullOrEmpty(chatId))
                return null;

            return chatId.Contains(".")
                ? chatId.Substring(0, chatId.IndexOf("."))
                : null;
        }

        /// <summary>
        /// Checks if the given chat ID represents a sub-chat
        /// </summary>
        public static bool IsSubChat(string chatId)
        {
            return !string.IsNullOrEmpty(chatId) && chatId.Contains(".");
        }

        /// <summary>
        /// Gets the sub-chat portion of a chat ID
        /// </summary>
        public static string GetSubChatId(string chatId)
        {
            if (string.IsNullOrEmpty(chatId) || !chatId.Contains("."))
                return null;

            return chatId.Substring(chatId.IndexOf(".") + 1);
        }
    }
}
