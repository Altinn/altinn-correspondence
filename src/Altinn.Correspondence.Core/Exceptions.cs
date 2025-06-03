namespace Altinn.Correspondence.Core.Exceptions;
public class HashMismatchException : InvalidOperationException
{
    public HashMismatchException(string message) : base(message) { }
}

public class DataLocationUrlException : InvalidOperationException
{
    public DataLocationUrlException(string message) : base(message) { }
}

public class BrregNotFoundException : Exception
{
    public BrregNotFoundException(string organizationNumber) 
        : base($"Organization {organizationNumber} not found in Brønnøysund Registry") 
    {
        OrganizationNumber = organizationNumber;
    }

    public string OrganizationNumber { get; }
}