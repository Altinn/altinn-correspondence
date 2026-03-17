namespace Altinn.Correspondence.API.Models.Enums
{
    /// <summary>
    /// Defines allowed Dialogporten dialog status values for external reference validation.
    /// </summary>
    public enum DialogportenDialogStatusExt : int
    {
        New = 1,
        InProgress = 2,
        Draft = 3,
        Sent = 4,
        RequiresAttention = 5,
        Completed = 6,
        NotApplicable = 7,
        Awaiting = 8
    }
}
