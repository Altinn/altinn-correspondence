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
    ILegacyPartyRepository legacyPartyRepository) : IHandler<string, Task>
{
    public async Task<OneOf<Task, Error>> Process(string recipient, ClaimsPrincipal? user, CancellationToken cancellationToken)
    {
        logger.LogInformation("Process legacy party {recipient}", recipient);
        var partyId = await altinnRegisterService.LookUpPartyId(recipient, cancellationToken);
        if (partyId is null)
        {
            throw new Exception("Failed to look up party in Altinn Register");
        }
        var exists = await legacyPartyRepository.PartyAlreadyExists((int)partyId, cancellationToken);
        if (exists)
        {
            return Task.CompletedTask;
        }
        var success = true; await sblBridgeService.AddPartyToSblBridge((int)partyId, cancellationToken);
        if (!success)
        {
            throw new Exception("Failed to send party to SBL");
        }
        await legacyPartyRepository.AddLegacyPartyId((int)partyId, cancellationToken);
        return Task.CompletedTask;
    }
}
