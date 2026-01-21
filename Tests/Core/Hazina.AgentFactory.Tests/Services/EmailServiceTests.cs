using FluentAssertions;

using Hazina.AgentFactory.Services.Email;
using Hazina.AgentFactory.Tests.Fixtures;

namespace Hazina.AgentFactory.Tests.Services;

/// <summary>
/// Unit tests for EmailService.
/// Note: These tests focus on configuration and interface behavior.
/// Actual email sending requires integration tests with a real mail server.
/// </summary>
public class EmailServiceTests : TestBase
{
    private readonly EmailSettings _settings;
    private readonly EmailService _sut;

    public EmailServiceTests()
    {
        _settings = CreateTestEmailSettings();
        _sut = new EmailService(_settings);
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_WithNullSettings_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new EmailService(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("settings");
    }

    [Fact]
    public void Constructor_WithValidSettings_CreatesInstance()
    {
        // Arrange
        var settings = new EmailSettings();

        // Act
        var service = new EmailService(settings);

        // Assert
        service.Should().NotBeNull();
    }

    #endregion

    #region EmailSettings Tests

    [Fact]
    public void EmailSettings_DefaultValues_AreCorrect()
    {
        // Arrange & Act
        var settings = new EmailSettings();

        // Assert
        settings.SmtpHost.Should().Be("mail.zxcs.nl");
        settings.SmtpPort.Should().Be(587);
        settings.ImapHost.Should().Be("mail.zxcs.nl");
        settings.ImapPort.Should().Be(993);
        settings.UseSsl.Should().BeTrue();
    }

    [Fact]
    public void EmailSettings_CanBeModified()
    {
        // Arrange
        var settings = new EmailSettings();

        // Act
        settings.SmtpHost = "custom.smtp.com";
        settings.SmtpPort = 465;
        settings.UseSsl = false;

        // Assert
        settings.SmtpHost.Should().Be("custom.smtp.com");
        settings.SmtpPort.Should().Be(465);
        settings.UseSsl.Should().BeFalse();
    }

    #endregion

    #region EmailSummary Tests

    [Fact]
    public void EmailSummary_CanBeCreated()
    {
        // Arrange & Act
        var summary = FakeDataGenerator.GenerateEmailSummary();

        // Assert
        summary.Should().NotBeNull();
        summary.Id.Should().NotBeNullOrEmpty();
        summary.Sender.Should().NotBeNullOrEmpty();
        summary.Subject.Should().NotBeNullOrEmpty();
        summary.Date.Should().BeBefore(DateTime.UtcNow.AddMinutes(1));
    }

    [Fact]
    public void EmailSummary_DefaultValues_AreEmpty()
    {
        // Arrange & Act
        var summary = new EmailSummary();

        // Assert
        summary.Id.Should().BeEmpty();
        summary.Sender.Should().BeEmpty();
        summary.Subject.Should().BeEmpty();
        summary.Date.Should().Be(default);
    }

    #endregion

    #region EmailDetail Tests

    [Fact]
    public void EmailDetail_CanBeCreated()
    {
        // Arrange & Act
        var detail = FakeDataGenerator.GenerateEmailDetail();

        // Assert
        detail.Should().NotBeNull();
        detail.Sender.Should().NotBeNullOrEmpty();
        detail.Subject.Should().NotBeNullOrEmpty();
        detail.Body.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void EmailDetail_DefaultValues_AreEmpty()
    {
        // Arrange & Act
        var detail = new EmailDetail();

        // Assert
        detail.Sender.Should().BeEmpty();
        detail.Subject.Should().BeEmpty();
        detail.Body.Should().BeEmpty();
    }

    #endregion

    #region Interface Implementation Tests

    [Fact]
    public void EmailService_ImplementsIEmailService()
    {
        // Assert
        _sut.Should().BeAssignableTo<IEmailService>();
    }

    [Fact]
    public void IEmailService_HasAllRequiredMethods()
    {
        // Arrange
        var interfaceType = typeof(IEmailService);

        // Assert - verify all methods exist
        interfaceType.GetMethod("SendEmailAsync").Should().NotBeNull();
        interfaceType.GetMethod("ListInboxEmailsAsync").Should().NotBeNull();
        interfaceType.GetMethod("ReadEmailAsync").Should().NotBeNull();
        interfaceType.GetMethod("CreateMailboxFolderAsync").Should().NotBeNull();
        interfaceType.GetMethod("MoveEmailToFolderAsync").Should().NotBeNull();
        interfaceType.GetMethod("ListMailboxFoldersAsync").Should().NotBeNull();
    }

    #endregion

    #region Cancellation Token Tests

    [Fact]
    public async Task SendEmailAsync_WithCancelledToken_ThrowsOperationCanceledException()
    {
        // Arrange
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act
        Func<Task> act = async () => await _sut.SendEmailAsync("test@test.com", "Subject", "Body", cts.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task ListInboxEmailsAsync_WithCancelledToken_ThrowsOperationCanceledException()
    {
        // Arrange
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act
        Func<Task> act = async () => await _sut.ListInboxEmailsAsync(10, false, cts.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    #endregion
}
