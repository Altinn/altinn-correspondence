﻿using Altinn.Correspondence.Core.Models.Entities;
using System.Text.RegularExpressions;

namespace Altinn.Correspondence.Integrations.Dialogporten.Mappers
{
    public static class DialogportenCorrespondenceMapper
    {
        private const string OrgNoPrefix = "urn:altinn:organization:identifier-no";
        private const string SsnPrefix = "urn:altinn:person:identifier-no";

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
            var personFormat = new Regex(@"^\d{11}$");
            if (organizationWithoutPrefixFormat.IsMatch(input))
            {
                return $"{OrgNoPrefix}:{input}";
            }
            else if (organizationWithPrefixFormat.IsMatch(input))
            {
                return $"{OrgNoPrefix}:{input.Substring(5)}";
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