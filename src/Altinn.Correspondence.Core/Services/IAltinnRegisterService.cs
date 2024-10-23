using Altinn.Correspondence.Core.Models.Entities;
using System.IO;

namespace Altinn.Correspondence.Core.Services;
public interface IAltinnRegisterService
{
    Task<string?> LookUpPartyId(string identificationId, CancellationToken cancellationToken);
    Task<string?> LookUpName(string identificationId, CancellationToken cancellationToken);

    Task<Party?> LookUpPartyByPartyId(int partyId, CancellationToken cancellationToken);
    Task<Party> LookUpPartyById(string identificationId, CancellationToken cancellationToken);
}
