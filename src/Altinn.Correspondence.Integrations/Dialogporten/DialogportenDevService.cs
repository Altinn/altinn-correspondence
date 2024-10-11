﻿using Altinn.Correspondence.Core.Services;
using Altinn.Correspondence.Core.Services.Enums;

namespace Altinn.Correspondence.Integrations.Dialogporten
{
    internal class DialogportenDevService : IDialogportenService
    {
        public Task<string> CreateCorrespondenceDialog(Guid correspondenceId)
        {
            return Task.FromResult(Guid.NewGuid().ToString());
        }

        public Task CreateInformationActivity(Guid correspondenceId, DialogportenActorType actorType, string description, string? extendedType = null)
        {
            return Task.CompletedTask;
        }
    }
}
