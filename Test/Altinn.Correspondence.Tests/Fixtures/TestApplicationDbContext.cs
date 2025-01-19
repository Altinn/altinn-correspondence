using Altinn.Correspondence.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Altinn.Correspondence.Tests.Fixtures
{
    public class TestApplicationDbContext : ApplicationDbContext
    {
        public TestApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        protected new bool IsAccessTokenValid() => false;
    }

}
