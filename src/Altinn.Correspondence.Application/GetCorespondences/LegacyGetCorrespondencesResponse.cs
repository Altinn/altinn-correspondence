using Altinn.Correspondence.Core.Models.Enums;
using System.Text.Json.Serialization;

namespace Altinn.Correspondence.Application.GetCorrespondences
{
    public class LegacyGetCorrespondencesResponse
    {
        public List<LegacyCorrespondenceItem> Items { get; set; } = new List<LegacyCorrespondenceItem>();
    }

    public class LegacyCorrespondenceItem
    {
        public required Guid CorrespondenceId { get; set; }
        public required int? Altinn2CorrespondenceId { get; set; }
        public required string MessageTitle { get; set; }
        public required string ServiceOwnerName { get; set; }
        public required CorrespondenceStatus Status { get; set; }
        public required int MinimumAuthenticationLevel { get; set; }
        public DateTimeOffset? Published { get; set; }
        public CorrespondenceStatus? PurgedStatus { get; set; }
        public DateTimeOffset? Purged { get; set; }
        public int InstanceOwnerPartyId { get; set; }
        public DateTimeOffset? DueDateTime { get; set; }
        public DateTimeOffset? Archived { get; set; }
        public string? MessageSender { get; set; }
        public DateTimeOffset? ConfirmationDate { get; set; }
        public bool IsConfirmationNeeded { get; set; }
    }
}
