using Altinn.Correspondence.Common.Constants;
using Altinn.Correspondence.Common.Helpers;
using Altinn.Correspondence.Core.Models.Entities;
using System.Text.RegularExpressions;

namespace Altinn.Correspondence.Integrations.Dialogporten.Mappers
{
    public static class DialogportenCorrespondenceMapper
    {
        private const string OrgNoPrefix = UrnConstants.OrganizationNumberAttribute;
        private const string SsnPrefix = UrnConstants.PersonIdAttribute;

        public static string GetSenderUrn(this CorrespondenceEntity correspondence)
        {
            var urn = GetUrn(correspondence.Sender);
            if (urn is null)
            {
                throw new ArgumentException("Correspondence had invalid recipient");
            }
            return urn;
        }

        public static string GetRecipientUrn(this CorrespondenceEntity correspondence)
        {
            var urn = GetUrn(correspondence.Recipient);
            if (urn is null)
            {
                throw new ArgumentException("Correspondence had invalid recipient");
            }
            return urn;
        }

        private static string? GetUrn(string input)
        {
            var organizationWithoutPrefixFormat = new Regex(@"^\d{9}$");
            var organizationWithPrefixFormat = new Regex(@"^\d{4}:\d{9}$");
            var correctOrgFormat = new Regex($@"^{OrgNoPrefix}:\d{{9}}$");
            var correctSSNFormat = new Regex($@"^{SsnPrefix}:\d{{11}}$");
            var personFormat = new Regex(@"^\d{11}$");
            if (correctOrgFormat.IsMatch(input) || correctSSNFormat.IsMatch(input))
            {
                return input;
            }
            else if (organizationWithoutPrefixFormat.IsMatch(input))
            {
                return $"{OrgNoPrefix}:{input}";
            }
            else if (organizationWithPrefixFormat.IsMatch(input))
            {
                return $"{OrgNoPrefix}:{input.WithoutPrefix()}";
            }
            else if (personFormat.IsMatch(input))
            {
                return $"{SsnPrefix}:{input}";
            }
            else
            {
                return null;
            }
        }
    }
}
