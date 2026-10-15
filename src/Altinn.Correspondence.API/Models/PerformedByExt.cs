using Altinn.Correspondence.API.Models.Enums;
using System.Text.Json.Serialization;

namespace Altinn.Correspondence.API.Models
{
    /// <summary>
    /// The actor that performed the activity.
    /// </summary>
    public class PerformedByExt
    {
        /// <summary>
        /// The type of actor; either the service owner, or someone representing the party.
        /// </summary>
        [JsonPropertyName("actorType")]
        public ActorTypeExt ActorType { get; set; }

        /// <summary>
        /// The name of the actor.
        /// </summary>
        /// <example>Ola Nordmann</example>
        [JsonPropertyName("actorName")]
        public string? ActorName { get; set; }
    }
}
