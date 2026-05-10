using System.ComponentModel.DataAnnotations;

namespace Hazina.API.DocumentStore.Models;

/// <summary>
/// Document response model
/// </summary>
public class DocumentResponse
{
    public string DocumentId { get; set; } = string.Empty;
    public Guid RAGStoreId { get; set; }
    public string Filename { get; set; } = string.Empty;
    public string MimeType { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public int PageCount { get; set; }
    public int ChunkCount { get; set; }
    public DateTime UploadedAt { get; set; }
    public string Status { get; set; } = string.Empty;
    public Dictionary<string, object>? Metadata { get; set; }
}

/// <summary>
/// Paged response model
/// </summary>
public class PagedResponse<T>
{
    public List<T> Items { get; set; } = new();
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
    public int TotalPages { get; set; }
}

/// <summary>
/// Upload document request
/// </summary>
public class UploadDocumentRequest
{
    public Microsoft.AspNetCore.Http.IFormFile? File { get; set; }
}

/// <summary>
/// Add text document request
/// </summary>
public class AddTextDocumentRequest
{
    [Required]
    public string Text { get; set; } = string.Empty;
    public Dictionary<string, object>? Metadata { get; set; }
}

/// <summary>
/// Update document request
/// </summary>
public class UpdateDocumentRequest
{
    public Dictionary<string, object>? Metadata { get; set; }
}

/// <summary>
/// Document chunk model
/// </summary>
public class DocumentChunk
{
    public Guid Id { get; set; }
    public Guid DocumentId { get; set; }
    public int ChunkIndex { get; set; }
    public string Text { get; set; } = string.Empty;
}

/// <summary>
/// Document info returned from store
/// </summary>
public class DocumentInfo
{
    public string DocumentId { get; set; } = string.Empty;
    public Guid StoreId { get; set; }
    public string? Content { get; set; }
    public string Filename { get; set; } = string.Empty;
    public string MimeType { get; set; } = string.Empty;
    public Dictionary<string, string> Metadata { get; set; } = new();
}
