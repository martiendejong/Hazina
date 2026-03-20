/// <summary>
/// Exception thrown when a file operation violates the configured <see cref="StoreSafetyPolicy"/>.
/// </summary>
public class StoreSafetyPolicyViolationException : InvalidOperationException
{
    public StoreSafetyPolicyViolationException(string message) : base(message) { }

    public StoreSafetyPolicyViolationException(string message, Exception innerException)
        : base(message, innerException) { }
}
