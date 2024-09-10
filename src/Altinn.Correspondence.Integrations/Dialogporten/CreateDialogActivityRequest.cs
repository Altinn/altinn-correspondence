namespace Altinn.Correspondence.Integrations.Dialogporten;

public class CreateDialogActivityRequest
{
    public string Id;
    public DateTime CreatedAt;
    public string ExtendedType;
    public string Type;
    public string RelatedActivityId;
    public string TransmissionId;
    public PerformedBy PerformedBy;
    public List<Description> Description;
}

