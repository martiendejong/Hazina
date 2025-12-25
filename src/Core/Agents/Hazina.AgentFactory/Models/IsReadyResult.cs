using System.Text.Json.Serialization;

public class IsReadyResult : ChatResponse<IsReadyResult>
{
    public bool IsTheUserRequestProperlyHandledAndFinished { get; set; }
    public string Message { get; set; }
    [JsonIgnore]
    public override IsReadyResult _example => new IsReadyResult { IsTheUserRequestProperlyHandledAndFinished = true };
    [JsonIgnore]
    public override string _signature => "{ IsReady: bool, Message: string }";
}