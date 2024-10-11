﻿using Altinn.Correspondence.Core.Services.Enums;

namespace Altinn.Correspondence.Core.Services;

public interface IDialogportenService
{
    Task<string> CreateCorrespondenceDialog(Guid correspondenceId);

    Task CreateInformationActivity(Guid correspondenceId, DialogportenActorType actorType, string description, string? extendedType);
}
