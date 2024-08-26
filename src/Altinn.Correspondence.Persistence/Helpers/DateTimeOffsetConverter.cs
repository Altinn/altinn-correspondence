using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Altinn.Correspondence.Persistence.Helpers
{
    internal class DateTimeOffsetConverter : ValueConverter<DateTimeOffset, DateTimeOffset>
    {
        public DateTimeOffsetConverter()
            : base(
                d => d.ToUniversalTime(),
                d => d.ToUniversalTime())
        {
        }
    }
}
