namespace Altinn.Correspondence.Application.GetCorrespondenceHistory
{
    public class LegacyGetCorrespondenceHistoryResponse
    {
        public string Status { get; set; }
        public DateTimeOffset? StatusChanged { get; set; }
        public string StatusText { get; set; }
        public LegacyUser User { get; set; }
        public LegacyNotification? Notification { get; set; }
        public LegacyForwardingAction? ForwardingAction { get; set; }
    }
    public class LegacyUser
    {
        public int? PartyId { get; set; }
        public string? NationalIdentityNumber { get; set; }
        public string? Name { get; set; }
    }
    public class LegacyNotification
    {
        public string? EmailAddress { get; set; }
        public string? MobileNumber { get; set; }
        public string? OrganizationNumber { get; set; }
        public string? NationalIdentityNumber { get; set; }
    }

    public class LegacyForwardingAction
    {
        public required DateTimeOffset ForwardedOnDate { get; set; }

        public int ForwardedByUserPartyId { get; set; }

        public int? ForwardedToUserPartyId { get; set; }

        public string? ForwardingText { get; set; }

        public string? ForwardedToEmailAddress { get; set; }

        public string? MailboxSupplier { get; set; }
    }
}