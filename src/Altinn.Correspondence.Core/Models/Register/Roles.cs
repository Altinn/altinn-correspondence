namespace Altinn.Correspondence.Core.Models.Register
{
    public class Roles
    {
        public List<RoleItem> Data { get; set; } = new List<RoleItem>();
    }

    public class RoleItem
    {
        public RoleDescriptor Role { get; set; } = default!;
        public RoleParty To { get; set; } = default!;
        public RoleParty From { get; set; } = default!;
    }

    public class RoleDescriptor
    {
        public string Source { get; set; } = string.Empty;
        public string Identifier { get; set; } = string.Empty;
        public string Urn { get; set; } = string.Empty;
    }

    public class RoleParty
    {
        public Guid PartyUuid { get; set; }
        public string Urn { get; set; } = string.Empty;
    }
}