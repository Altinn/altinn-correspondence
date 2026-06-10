namespace Altinn.Correspondence.Application.GenerateDelegatedBlobSasUrl;

public class GenerateDelegatedBlobSasUrlResponse
{
    public required string SasUrl { get; set; }

    public DateTimeOffset ExpiresOn { get; set; }
}
