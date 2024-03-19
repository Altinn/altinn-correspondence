using Altinn.Correspondence.API.Models.Enums;

namespace Altinn.Correspondence.API.Models
{
    public class NotificationStatusEventExt
    {
        public string Status { get; set; }
        public string StatusText { get; set; } = string.Empty;
        public DateTimeOffset StatusChanged { get; set; }
    }
}
