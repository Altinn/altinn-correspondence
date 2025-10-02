namespace Altinn.Correspondence.Core.Models.Register
{
    public class MainUnitsRequest
    {
        public string Data { get; set; } = string.Empty;
    }

    public class MainUnitsResponse
    {
        public List<MainUnitItem> Data { get; set; } = new List<MainUnitItem>();
    }

    public class MainUnitItem
    {
        public string PartyType { get; set; } = string.Empty;
        public string OrganizationIdentifier { get; set; } = string.Empty;
        public Guid PartyUuid { get; set; }
        public long VersionId { get; set; }
        public string Urn { get; set; } = string.Empty;
        public int? PartyId { get; set; }
        public string DisplayName { get; set; } = string.Empty;
    }
}