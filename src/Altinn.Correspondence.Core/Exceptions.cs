namespace Altinn.Correspondence.Core.Exceptions;
public class HashMismatchException : InvalidOperationException
{
    public HashMismatchException(string message) : base(message) { }
}

public class DataLocationUrlException : InvalidOperationException
{
    public DataLocationUrlException(string message) : base(message) { }
}

public class DialogNotFoundException : Exception
{
    public string DialogId { get; }
    
    public DialogNotFoundException(string dialogId) 
        : base($"Dialog with id '{dialogId}' was not found in Dialogporten")
    {
        DialogId = dialogId;
    }
}