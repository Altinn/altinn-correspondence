using Altinn.Correspondence.API.Models.Enums;

namespace Altinn.Correspondence.API.Models
{
    public class CorrespondenceStatusEventExt
    {
        public CorrespondenceStatusExt Status { get; set; }
        public string StatusText { get; set; } = string.Empty;
        public DateTimeOffset StatusChanged { get; set; }
    }
}
