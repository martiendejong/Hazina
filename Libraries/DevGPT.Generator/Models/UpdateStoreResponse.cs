using System.Text.Json.Serialization;

public class UpdateStoreResponse : ChatResponse<UpdateStoreResponse>
{
    public List<MoveDocumentRequest>? Moves { get; set; }
    public List<DeleteDocumentRequest>? Deletions { get; set; }
    public List<ModifyDocumentRequest>? Modifications { get; set; }
    public string ResponseMessage { get; set; } = "";

    [JsonIgnore]
    public override UpdateStoreResponse _example
    {
        get
        {
            return new()
            {
                Modifications = new List<ModifyDocumentRequest>() { new ModifyDocumentRequest { Name = "Name of the document, ie. My Personal File", Path = "The relative path, ie. info\\personalfile.txt", Contents = "The (updated) contents of the file." } },
                Deletions = new List<DeleteDocumentRequest>() { new DeleteDocumentRequest { Path = "The relative path, ie. info\\olddocument.txt" } },
                Moves = new List<MoveDocumentRequest>() { new MoveDocumentRequest { Path = "The relative path, ie. info\\olddocument.txt", NewPath = "The relative path, ie. info\\subfolder\\newdocument.txt" } },
                ResponseMessage = "The response to the user"
            };
        }
    }

    [JsonIgnore]
    public override string _signature
    {
        get
        {
            return @"{Moves: [{Path: string, NewPath: string} or null, Deletions: [{Path: string}] or null, Modifications: [{Name: string, Path: string, Contents: string}] or null] or null, ResponseMessage: string}";
        }
    }
}
