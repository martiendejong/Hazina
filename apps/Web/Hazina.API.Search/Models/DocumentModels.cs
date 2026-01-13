using System.ComponentModel.DataAnnotations;

namespace Hazina.API.Search.Models;

public class DocumentResponse
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string MimeType { get; set; } = string.Empty;
    public List<string> Tags { get; set; } = new();
    public DateTime CreatedAt { get; set; }
    public DateTime ModifiedAt { get; set; }
    public long SizeBytes { get; set; }
}

public class PagedResponse<T>
{
    public List<T> Items { get; set; } = new();
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalItems { get; set; }
    public int TotalPages { get; set; }
}

public class UploadDocumentRequest
{
    public IFormFile? File { get; set; }
    public string? Tags { get; set; }
    public bool GenerateEmbeddings { get; set; } = true;
}

public class UpdateDocumentRequest
{
    public string? Title { get; set; }
    public string? Tags { get; set; }
}
