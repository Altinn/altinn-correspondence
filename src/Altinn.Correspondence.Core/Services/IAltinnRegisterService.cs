namespace Altinn.Correspondence.Core.Services;
public interface IAltinnRegisterService
{
    Task<string?> LookUpOrganizationId(string organizationId, CancellationToken cancellationToken);
}
