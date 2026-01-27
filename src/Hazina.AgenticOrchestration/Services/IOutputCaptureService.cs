using Hazina.AgenticOrchestration.Models;

namespace Hazina.AgenticOrchestration.Services;

public interface IOutputCaptureService
{
    Task StreamOutputAsync(string sessionId, CancellationToken cancellationToken);
    Task<List<OutputLine>> GetOutputHistoryAsync(string sessionId, int lastN = 100);
}
