
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Altinn.Correspondence.API.Models;
using Altinn.Correspondence.Application.InitializeCorrespondences;

namespace Altinn.Correspondence.Tests.Helpers;
internal static class CorrespondenceHelper
{
    public static async Task<CorrespondenceDetails> GetInitializedCorrespondence(HttpClient client, JsonSerializerOptions serializerOptions, InitializeCorrespondencesExt payload)
    {
        var initResponse = await client.PostAsJsonAsync("correspondence/api/v1/correspondence", payload);
        Assert.Equal(HttpStatusCode.OK, initResponse.StatusCode);
        var correspondence = await initResponse.Content.ReadFromJsonAsync<InitializeCorrespondencesResponseExt>(serializerOptions);
        Assert.NotNull(correspondence?.Correspondences.First());
        return correspondence.Correspondences.First();
    }
}