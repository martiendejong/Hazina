namespace HazinaStore.Models
{
    /// <summary>
    /// Content type classification for uploaded files
    /// </summary>
    public enum ContentType
    {
        /// <summary>
        /// Unknown or uncategorized content
        /// </summary>
        Unknown = 0,

        /// <summary>
        /// Image content only
        /// </summary>
        Image = 1,

        /// <summary>
        /// Text content only
        /// </summary>
        Text = 2,

        /// <summary>
        /// Mixed content (both text and images)
        /// </summary>
        Mixed = 3,

        /// <summary>
        /// PDF document
        /// </summary>
        Pdf = 4,

        /// <summary>
        /// Office document (Word, PowerPoint, Excel)
        /// </summary>
        Office = 5
    }
}
