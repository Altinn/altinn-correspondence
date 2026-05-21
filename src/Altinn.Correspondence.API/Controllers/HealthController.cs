using Altinn.Correspondence.API.Swagger;
using Altinn.Correspondence.Persistence;
using Microsoft.AspNetCore.Mvc;

namespace Altinn.Correspondence.Controllers
{
    [ApiController]
    [Route("health")]
    [HideFromPublicApi]
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

        [HttpGet("/healthz")]
        public ActionResult LivenessCheck()
        {
            return Ok(new
            {
                Status = "Healthy",
                Message = "Process is responsive"
            });
        }
    }
}
