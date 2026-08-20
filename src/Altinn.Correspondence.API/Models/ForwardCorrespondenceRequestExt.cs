namespace Altinn.Correspondence.API.Models
{
    public class ForwardCorrespondenceRequestExt
    {
        public required string ForwardTo { get; set; }
        public string? ForwardingText { get; set; }
    }
}