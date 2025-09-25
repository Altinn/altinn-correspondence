using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Core.Repositories;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using System.Security.Claims;

namespace Altinn.Correspondence.Tests.Helpers;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection OverrideAuthentication(this IServiceCollection services)
    {
        services.AddAuthentication(options =>
        {
            var schemes = options.Schemes.ToList();
            if (schemes.Any())
            {
                options.DefaultAuthenticateScheme = "Test";
                options.DefaultChallengeScheme = "Test";
            }
        })
        .AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>("Test", _ => { });

        return services;
    }
    public static IServiceCollection OverrideAuthorization(this IServiceCollection services)
    {
        services.AddSingleton<IPolicyEvaluator>(provider =>
        {
            return new MockPolicyEvaluator();
        });
        return services;
    }

    public static IServiceCollection OverrideAltinnAuthorization(this IServiceCollection services)
    {
        var altinnAuthorizationService = new Mock<IAltinnAuthorizationService>();
        altinnAuthorizationService
            .Setup(x => x.CheckAccessAsRecipient(
                It.IsAny<ClaimsPrincipal>(),
                It.IsAny<CorrespondenceEntity>(),
                It.IsAny<CancellationToken>()))
            .Returns((ClaimsPrincipal? user, CorrespondenceEntity corr, CancellationToken token) => {
                return Task.FromResult(NotRecipient(user));
            });

        altinnAuthorizationService
            .Setup(x => x.CheckAccessAsSender(
                It.IsAny<ClaimsPrincipal>(),
                It.IsAny<CorrespondenceEntity>(),
                It.IsAny<CancellationToken>()))
            .Returns((ClaimsPrincipal? user, CorrespondenceEntity corr, CancellationToken token) => {
                return Task.FromResult(NotSender(user));
            });

        altinnAuthorizationService
            .Setup(x => x.CheckAccessAsSender(
                It.IsAny<ClaimsPrincipal>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Returns((ClaimsPrincipal? user, string resourceId, string sender, string? instance, CancellationToken token) => {
                return Task.FromResult(NotSender(user));
            });

        altinnAuthorizationService
            .Setup(x => x.CheckAccessAsAny(
                It.IsAny<ClaimsPrincipal>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Returns((ClaimsPrincipal? user, string resource, string party, CancellationToken token) => {
                return Task.FromResult(!NotRecipient(user) || !NotSender(user));
            });

        altinnAuthorizationService
            .Setup(x => x.CheckUserAccessAndGetMinimumAuthLevel(
                It.IsAny<ClaimsPrincipal>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<List<ResourceAccessLevel>>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Returns((ClaimsPrincipal? user, string ssn, string resourceId, List<ResourceAccessLevel> rights, string recipientOrgNo, CancellationToken token) =>
            {
                return Task.FromResult<int?>(3);
            });
        
        altinnAuthorizationService
            .Setup(x => x.CheckUserAccessAndGetMinimumAuthLevelWithMultirequest(
                It.IsAny<ClaimsPrincipal>(),
                It.IsAny<string>(),
                It.IsAny<List<CorrespondenceEntity>>(),
                It.IsAny<CancellationToken>()))
            .Returns((ClaimsPrincipal? user, string ssn, List<CorrespondenceEntity> correspondences, CancellationToken token) =>
            {
                List<(string Recipient, string ResourceId)> recipientWithResources = correspondences
                    .Select(correspondence => (correspondence.Recipient, correspondence.ResourceId))
                    .Distinct()
                    .ToList();
                var resultDict = recipientWithResources.ToDictionary(
                    keySelector: pair => pair,
                    elementSelector: _ => (int?)3
                );
                return Task.FromResult(resultDict);
            });


        return services.AddScoped(_ => altinnAuthorizationService.Object);
    }

    private static bool NotSender(ClaimsPrincipal? user)
    {
        return !user?.Claims.Any(c =>
            c.Type == "notSender") ?? true;
    }
    private static bool NotRecipient(ClaimsPrincipal? user)
    {
        return !user?.Claims.Any(c =>
            c.Type == "notRecipient") ?? true;
    }
}
