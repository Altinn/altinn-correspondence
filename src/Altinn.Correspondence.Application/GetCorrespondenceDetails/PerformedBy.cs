namespace Altinn.Correspondence.Application.GetCorrespondenceDetails;

public enum PerformedByActorType
{
    PartyRepresentative,
    ServiceOwner
}

/// <summary>
/// The actor that performed a correspondence status event.
/// </summary>
public class PerformedBy
{
    public required PerformedByActorType ActorType { get; init; }

    public string? ActorName { get; init; }
}
