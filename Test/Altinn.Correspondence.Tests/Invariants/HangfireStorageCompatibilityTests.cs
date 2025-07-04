using Altinn.Correspondence.Tests.Fixtures;
using Altinn.Correspondence.Tests.Helpers;
using Hangfire;
using Hangfire.PostgreSql;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using System.Transactions;

namespace Altinn.Correspondence.Tests.Invariants;

[Collection(nameof(CustomWebApplicationTestsCollection))]
public class HangfireStorageCompatibilityTests(CustomWebApplicationFactory factory)
{
}
