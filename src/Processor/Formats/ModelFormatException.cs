namespace Processor.Formats;

public abstract class ModelFormatException(string message, string clientMessage)
    : IOException(message)
{
    public string ClientMessage { get; } = clientMessage;
}
