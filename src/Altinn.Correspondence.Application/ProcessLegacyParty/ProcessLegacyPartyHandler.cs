using Altinn.Correspondence.Core.Extensions;
using Altinn.Correspondence.Core.Repositories;
using Altinn.Correspondence.Core.Services;
using Microsoft.Extensions.Logging;
using System.Security.Claims;

namespace Altinn.Correspondence.Application.ProcessLegacyParty;

public class ProcessLegacyPartyHandler(
    ILogger<ProcessLegacyPartyHandler> logger,
    IAltinnRegisterService altinnRegisterService,
    IAltinnStorageService altinnStorageService,
    ILegacyPartyRepository legacyPartyRepository)
{
    public async Task Process(string recipient, ClaimsPrincipal? user, CancellationToken cancellationToken)
    {
        logger.LogInformation("Process legacy party {recipient}", recipient);
        var party = await altinnRegisterService.LookUpPartyById(recipient, cancellationToken);
        var partyId = party?.GetPartyId();
        if (partyId is null)
        {
            throw new Exception("Failed to look up party in Altinn Register");
        }
        var exists = await legacyPartyRepository.PartyAlreadyExists(partyId.Value, cancellationToken);
        if (!exists)
        {
            var success = await altinnStorageService.AddPartyToSblBridge(partyId.Value, cancellationToken);
            if (!success)
            {
                throw new Exception("Failed to send party to SBL");
            }
            logger.Log(LogLevel.Information, "Party {partyId} added to SBL", partyId);
            await legacyPartyRepository.AddLegacyPartyId(partyId.Value, cancellationToken);
        }
    }
}
