namespace Altinn.Correspondence.Core.Exceptions;
public class HashMismatchException : InvalidOperationException
{
    public HashMismatchException(string message) : base(message) { }
}

public class DataLocationUrlException : InvalidOperationException
{
    public DataLocationUrlException(string message) : base(message) { }
}
public class RecipientLookupException : InvalidOperationException
{
    public RecipientLookupException(string message) : base(message) { }
}
public class NotificationCreationException : InvalidOperationException
{
    public NotificationCreationException(string message) : base(message) { }
}