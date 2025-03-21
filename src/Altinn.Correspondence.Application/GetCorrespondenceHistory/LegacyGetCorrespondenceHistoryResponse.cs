namespace Altinn.Correspondence.Application.GetCorrespondenceHistory
{
    public class LegacyGetCorrespondenceHistoryResponse
    {
        public required string Status { get; set; }
        public DateTimeOffset? StatusChanged { get; set; }
        public required string StatusText { get; set; }
        public required LegacyUser User { get; set; }
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
        public required string ForwardedBy { get; set; }
        public string? ForwardedTo { get; set; }
        public string? ForwardingText { get; set; }
        public string? ForwardedToEmail { get; set; }
        public string? MailboxSupplier { get; set; }
    }
}