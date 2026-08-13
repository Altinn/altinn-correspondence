namespace Altinn.Correspondence.Application.PurgeCorrespondence;

public sealed record PurgeCorrespondenceResult(Guid CorrespondenceId, IReadOnlyList<Action> PendingSideEffects);
