using Altinn.Correspondence.Persistence;
using Microsoft.AspNetCore.Mvc;

namespace Altinn.Correspondence.Controllers
{
    [ApiController]
    [Route("health")]
    [ApiExplorerSettings(IgnoreApi = true)]
    public class HealthController(ApplicationDbContext dbContext) : ControllerBase
    {
        private readonly ApplicationDbContext _dbContext = dbContext;

        [HttpGet]
        public async Task<ActionResult> HealthCheckAsync()
        {
            try
            {
                await _dbContext.Database.CanConnectAsync();
                return Ok(new
                {
                    Status = "Healthy",
                    Message = "Environment properly configured and database is accessible"
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    Status = "Unhealthy",
                    Message = $"Health check failed: {ex.Message}"
                });
            }
        }
    }
}
