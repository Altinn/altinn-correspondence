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
                Name = historyResponse.User.Name
            },
            Notification = historyResponse.Notification != null ? new LegacyNotificationExt
            {
                EmailAddress = historyResponse.Notification.EmailAddress,
                MobileNumber = historyResponse.Notification.MobileNumber,
                OrganizationNumber = historyResponse.Notification.OrganizationNumber,
                NationalIdentityNumber = historyResponse.Notification.NationalIdentityNumber
            } : null,
            ForwardingEvent = historyResponse.ForwardingEvent != null ? new LegacyCorrespondenceForwardingEventExt
            {
                ForwardedByUserId = historyResponse.ForwardingEvent.ForwardedByUserId,
                ForwardedToUserId = historyResponse.ForwardingEvent.ForwardedToUserId,
                ForwardingText = historyResponse.ForwardingEvent.ForwardingText,
                ForwardedToEmail = historyResponse.ForwardingEvent.ForwardedToEmail,
                MailboxSupplier = historyResponse.ForwardingEvent.MailboxSupplier
            } : null
        };
        return legacyHistoryExt;
    }
}
