using Altinn.Correspondence.API.Models;
using Altinn.Correspondence.Application.GetCorrespondenceHistory;
using System.Collections.Generic;

namespace Altinn.Correspondence.Mappers;

internal static class LegacyCorrespondenceHistoryMapper
{
    internal static List<LegacyCorrespondenceHistoryExt> MapToExternal(List<LegacyGetCorrespondenceHistoryResponse> historyResponses)
    {
        List<LegacyCorrespondenceHistoryExt> legacyHistoriesExt = [];
        foreach (var historyResponse in historyResponses)
        {
            legacyHistoriesExt.Add(MapToExternal(historyResponse));
        }
        return legacyHistoriesExt;
    }

    internal static LegacyCorrespondenceHistoryExt MapToExternal(LegacyGetCorrespondenceHistoryResponse historyResponse)
    {
        LegacyCorrespondenceHistoryExt legacyHistoryExt = new()
        {
            Status = historyResponse.Status,
            StatusChanged = historyResponse.StatusChanged,
            StatusText = historyResponse.StatusText,
            User = new LegacyUserExt
            {
                PartyId = historyResponse.User.PartyId,
                NationalIdentityNumber = historyResponse.User.NationalIdentityNumber,
                Name = historyResponse.User.Name
            },
            Notification = historyResponse.Notification != null ? new LegacyNotificationExt
            {
                EmailAddress = historyResponse.Notification.EmailAddress,
                MobileNumber = historyResponse.Notification.MobileNumber,
                OrganizationNumber = historyResponse.Notification.OrganizationNumber,
                NationalIdentityNumber = historyResponse.Notification.NationalIdentityNumber
            } : null,
            ForwardingAction = historyResponse.ForwardingAction != null ? new LegacyCorrespondenceForwardingEventExt
            {
                ForwardedByUserId = historyResponse.ForwardingAction.ForwardedByUserId,
                ForwardedToUserId = historyResponse.ForwardingAction.ForwardedToUserId,
                ForwardingText = historyResponse.ForwardingAction.ForwardingText,
                ForwardedToEmail = historyResponse.ForwardingAction.ForwardedToEmail,
                MailboxSupplier = historyResponse.ForwardingAction.MailboxSupplier
            } : null
        };
        return legacyHistoryExt;
    }
}
