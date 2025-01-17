using Altinn.Correspondence.Core.Repositories;
using Altinn.Correspondence.Core.Services;
using Microsoft.Extensions.Logging;
using OneOf;
using System.Security.Claims;

namespace Altinn.Correspondence.Application.ProcessLegacyParty;

public class ProcessLegacyPartyHandler(
    ILogger<ProcessLegacyPartyHandler> logger,
    IAltinnRegisterService altinnRegisterService,
    IAltinnSblBridgeService sblBridgeService,
    ILegacyPartyRepository legacyPartyRepository)
{
    public async Task Process(string recipient, ClaimsPrincipal? user, CancellationToken cancellationToken)
    {
        logger.LogInformation("Process legacy party {recipient}", recipient);
        var partyId = await altinnRegisterService.LookUpPartyId(recipient, cancellationToken);
        if (partyId is null)
        {
            throw new Exception("Failed to look up party in Altinn Register");
        }
        var exists = await legacyPartyRepository.PartyAlreadyExists((int)partyId, cancellationToken);
        if (!exists)
        {
            var success = await sblBridgeService.AddPartyToSblBridge((int)partyId, cancellationToken);
            if (!success)
            {
                throw new Exception("Failed to send party to SBL");
            }
            logger.Log(LogLevel.Information, "Party {partyId} added to SBL", partyId);
            await legacyPartyRepository.AddLegacyPartyId((int)partyId, cancellationToken);
        }
    }
}
