using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Hazina.AgenticOrchestration.Models;
using Hazina.AgenticOrchestration.Services;

namespace Hazina.AgenticOrchestration.Controllers;

/// <summary>
/// Controller for inter-agent mail system
/// Inspired by Overstory's mail commands
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class MailController : ControllerBase
{
    private readonly IMailService _mailService;
    private readonly ILogger<MailController> _logger;

    public MailController(IMailService mailService, ILogger<MailController> logger)
    {
        _mailService = mailService;
        _logger = logger;
    }

    /// <summary>
    /// Send a mail message
    /// </summary>
    [HttpPost("send")]
    public IActionResult SendMail([FromBody] SendMailRequest request)
    {
        try
        {
            var message = new MailMessage
            {
                Id = Guid.NewGuid().ToString(),
                From = request.From,
                To = request.To,
                Subject = request.Subject,
                Body = request.Body,
                Type = request.Type,
                Priority = request.Priority,
                Payload = request.Payload
            };

            var messageId = _mailService.Send(message);

            return Ok(new { messageId, message = "Mail sent successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send mail from {From} to {To}", request.From, request.To);
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Check inbox (unread messages)
    /// </summary>
    [HttpGet("check")]
    public IActionResult CheckMail([FromQuery] string agentName, [FromQuery] bool inject = false)
    {
        try
        {
            if (inject)
            {
                var output = _mailService.CheckInject(agentName);
                return Ok(new { output });
            }
            else
            {
                var messages = _mailService.Check(agentName);
                return Ok(messages);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check mail for {AgentName}", agentName);
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// List messages with filters
    /// </summary>
    [HttpGet("list")]
    public IActionResult ListMail([FromQuery] string? from = null, [FromQuery] string? to = null, [FromQuery] bool? unread = null)
    {
        try
        {
            var messages = _mailService.List(from, to, unread);
            return Ok(messages);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to list mail");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Mark message as read
    /// </summary>
    [HttpPost("{messageId}/read")]
    public IActionResult MarkAsRead(string messageId)
    {
        try
        {
            var (alreadyRead, message) = _mailService.MarkAsRead(messageId);

            if (message == null)
            {
                return NotFound(new { error = "Message not found" });
            }

            return Ok(new
            {
                messageId,
                alreadyRead,
                message = alreadyRead ? "Message was already read" : "Marked as read"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to mark message {MessageId} as read", messageId);
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Reply to a message
    /// </summary>
    [HttpPost("{messageId}/reply")]
    public IActionResult ReplyToMail(string messageId, [FromBody] ReplyRequest request)
    {
        try
        {
            var replyId = _mailService.Reply(messageId, request.Body, request.From);

            return Ok(new { replyId, message = "Reply sent successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to reply to message {MessageId}", messageId);
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Get pending nudge for agent
    /// </summary>
    [HttpGet("nudge/pending")]
    public async Task<IActionResult> GetPendingNudge([FromQuery] string agentName)
    {
        try
        {
            var nudge = await _mailService.GetPendingNudgeAsync(agentName);

            if (nudge == null)
            {
                return Ok(new { hasPendingNudge = false });
            }

            return Ok(new
            {
                hasPendingNudge = true,
                nudge
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get pending nudge for {AgentName}", agentName);
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Clear pending nudge for agent
    /// </summary>
    [HttpDelete("nudge/pending")]
    public async Task<IActionResult> ClearPendingNudge([FromQuery] string agentName)
    {
        try
        {
            await _mailService.ClearPendingNudgeAsync(agentName);
            return Ok(new { message = "Pending nudge cleared" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to clear pending nudge for {AgentName}", agentName);
            return StatusCode(500, new { error = ex.Message });
        }
    }
}

public class SendMailRequest
{
    public required string From { get; set; }
    public required string To { get; set; }
    public required string Subject { get; set; }
    public required string Body { get; set; }
    public MailMessageType Type { get; set; } = MailMessageType.Status;
    public MailPriority Priority { get; set; } = MailPriority.Normal;
    public string? Payload { get; set; }
}

public class ReplyRequest
{
    public required string From { get; set; }
    public required string Body { get; set; }
}
