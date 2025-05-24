namespace Assistant.Messaging;

public interface IMessagingService
{
    Task SendMessageAsync(string message, bool includeInLlmContext);

    Task SendMessageAsync(string message, MessagePriority priority, string userIdentifier, bool includeInLlmContext);
}
