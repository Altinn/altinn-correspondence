namespace Altinn.Correspondence.Core.Services;
public interface IAltinnRegisterService
{
    Task<string?> LookUpPartyId(string identificationId, CancellationToken cancellationToken);
}
