namespace HazinaStore.Models
{
    /// <summary>
    /// Processing status for uploaded documents
    /// </summary>
    public enum ProcessingStatus
    {
        /// <summary>
        /// Document has not been processed yet
        /// </summary>
        NotProcessed = 0,

        /// <summary>
        /// Document is currently being processed
        /// </summary>
        Processing = 1,

        /// <summary>
        /// Document processing completed successfully
        /// </summary>
        Completed = 2,

        /// <summary>
        /// Document processing failed
        /// </summary>
        Failed = 3
    }
}
