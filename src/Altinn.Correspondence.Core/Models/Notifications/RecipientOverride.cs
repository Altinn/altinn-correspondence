
namespace Altinn.Correspondence.Core.Models.Notifications;

public class RecipientOverride
{
    public string RecipientToOverride { get; set; }
    public List<Recipient> recipients { get; set; }
}