using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Altinn.Correspondence.Controllers
{
    [ApiController]
    [Route("health")]
    public class HealthController(DbContext dbContext, ILogger<HealthController> logger) : ControllerBase
    {
        private readonly DbContext _dbContext = dbContext;
        private readonly ILogger<HealthController> _logger;

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
                _logger.LogError("Health check failed!)", ex);
                return StatusCode(500, new
                {
                    Status = "Unhealthy",
                    Message = $"Health check failed: {ex.Message}"
                });
            }
        }
    }
}
