namespace Assistant.Messaging;

public interface IMessagingService
{
    Task SendMessageAsync(string message, bool includeInLlmContext);
}
