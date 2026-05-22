using Altinn.Correspondence.Common.Constants;
using Altinn.Correspondence.Core.Models.Entities;

namespace Altinn.Correspondence.Application.SmsNotificationLengthStatistics;

public enum RecipientCategory
{
    Organization,
    Person
}

public static class RecipientCategoryClassifier
{
    public static RecipientCategory Classify(string? recipientType, string? recipient)
    {
        var effectiveType = string.IsNullOrWhiteSpace(recipientType)
            ? CorrespondenceEntity.ComputeRecipientType(recipient)
            : recipientType;

        return string.Equals(effectiveType, UrnConstants.OrganizationNumberAttribute, StringComparison.Ordinal)
            ? RecipientCategory.Organization
            : RecipientCategory.Person;
    }
}
