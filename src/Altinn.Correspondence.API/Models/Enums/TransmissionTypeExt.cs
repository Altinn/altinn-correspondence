namespace Altinn.Correspondence.API.Models.Enums
{
    /// <summary>
    /// Defines the type of a transmission.
    /// </summary>
    public enum TransmissionTypeExt : int
    {
        Information = 1,
        Acceptance = 2,
        Rejection = 3,
        Request = 4,
        Alert = 5,
        Decision = 6,
        Submission = 7,
        Correction = 8
    }
}
