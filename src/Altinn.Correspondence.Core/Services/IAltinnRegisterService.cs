using Altinn.Correspondence.Core.Models.Entities;
using System.IO;

namespace Altinn.Correspondence.Core.Services;
public interface IAltinnRegisterService
{
    Task<string?> LookUpPartyId(string identificationId, CancellationToken cancellationToken);
    Task<string?> LookUpName(string identificationId, CancellationToken cancellationToken);

    Task<SimpleParty?> LookUpParty(int partyId, CancellationToken cancellationToken);
}
