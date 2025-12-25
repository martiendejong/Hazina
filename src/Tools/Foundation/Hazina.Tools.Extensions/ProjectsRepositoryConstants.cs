using Hazina.Tools.Data;

namespace backend.Extensions
{
    /// <summary>
    /// Constants for backward compatibility with legacy code
    /// </summary>
    public static class ProjectsRepositoryConstants
    {
        // Re-export ProjectFileLocator constants for backward compatibility
        public static string ContentFile => ProjectFileLocator.ContentFile;
        public static string ContentChatFile => ProjectFileLocator.ContentChatFile;
        public static string AcceptedPlannedContentFile => ProjectFileLocator.AcceptedPlannedContentFile;
        public static string RejectedContentFile => ProjectFileLocator.RejectedContentFile;
        public static string DeletedContentFile => ProjectFileLocator.DeletedContentFile;
        public static string AcceptedContentFile => ProjectFileLocator.AcceptedContentFile;
        public static string PublishedContentFile => ProjectFileLocator.PublishedContentFile;
        public static string RolePromptsFile => ProjectFileLocator.RolePromptsFile;
    }
}
