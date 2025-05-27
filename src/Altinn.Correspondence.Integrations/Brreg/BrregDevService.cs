using Altinn.Correspondence.Core.Services;
using Altinn.Correspondence.Integrations.Brreg.Models;
using Microsoft.Extensions.Logging;

namespace Altinn.Correspondence.Integrations.Brreg
{
    /// <summary>
    /// Development implementation of IBrregService for local testing
    /// </summary>
    /// <remarks>
    /// Initializes a new instance of the <see cref="BrregDevService"/> class.
    /// </remarks>
    /// <param name="logger">Logger</param>
    public class BrregDevService(ILogger<BrregDevService> logger) : IBrregService
    {
        private readonly ILogger<BrregDevService> _logger = logger;

        public Task<bool> CheckOrganizationRolesAsync(string organizationNumber, IEnumerable<string> roles, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(true);
        }

        public Task<bool> IsOrganizationBankruptOrDeletedAsync(string organizationNumber, CancellationToken cancellationToken = default)
        { 
            return Task.FromResult(false);
        }
    }
} 