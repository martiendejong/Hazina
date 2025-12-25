public class DeleteDocumentRequest
{
    public string Path { get; set; } = "";
}

public class MoveDocumentRequest
{
    public string Path { get; set; } = "";
    public string NewPath { get; set; } = "";
}
