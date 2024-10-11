﻿using Altinn.Correspondence.Core.Dialogporten.Mappers;
using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Services.Enums;
using Altinn.Correspondence.Integrations.Dialogporten.Mappers;
using Altinn.Correspondence.Integrations.Dialogporten.Models;
using UUIDNext;

namespace Altinn.Correspondence.Integrations.Dialogporten
{
    internal class CreateDialogActivityRequestMapper
    {
        internal static CreateDialogActivityRequest CreateDialogActivityRequest(CorrespondenceEntity correspondence, DialogportenActorType actorType, DialogportenTextType textType, params string[] tokens)
        {
            var dialogActivityId = Uuid.NewDatabaseFriendly(Database.PostgreSql).ToString(); // Dialogporten requires time-stamped GUIDs, not supported natively until .NET 9.0
            var urnActorId = actorType switch
            {
                DialogportenActorType.ServiceOwner => null,
                DialogportenActorType.Sender => correspondence.GetSenderUrn(),
                DialogportenActorType.Recipient => correspondence.GetRecipientUrn(),
                _ => throw new NotImplementedException()
            };
            return new CreateDialogActivityRequest()
            {
                Id = dialogActivityId,
                CreatedAt = DateTime.UtcNow,
                Description = new List<ActivityDescription>()
                {
                    new ActivityDescription()
                    {
                        LanguageCode = "nb",
                        Value = DialogportenText.GetDialogportenText(textType, DialogportenLanguageCode.NB, tokens)
                    },
                    new ActivityDescription()
                    {
                        LanguageCode = "nn",
                        Value = DialogportenText.GetDialogportenText(textType, DialogportenLanguageCode.NN, tokens)
                    },
                    new ActivityDescription()
                    {
                        LanguageCode = "en",
                        Value = DialogportenText.GetDialogportenText(textType, DialogportenLanguageCode.EN, tokens)
                    }
                },
                PerformedBy = new ActivityPerformedBy()
                {
                    ActorType = actorType == DialogportenActorType.ServiceOwner ? "ServiceOwner" : "PartyRepresentative",
                    ActorId = urnActorId
                },
                Type = ActivityType.Information
            };
        }
    }
}
