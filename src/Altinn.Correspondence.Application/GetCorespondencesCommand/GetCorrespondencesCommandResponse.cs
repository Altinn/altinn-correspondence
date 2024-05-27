using System.Text.Json.Serialization;

namespace Altinn.Correspondence.API.Models
{
    public class GetCorrespondencesCommandResponse
    {
        public List<Guid> Items { get; set; } = new List<Guid>();

        public PaginationMetaData Pagination { get; set; } = new PaginationMetaData();
    }

    public class PaginationMetaData
    {
        public int Offset { get; set; }

        public int Limit { get; set; }

        public int TotalItems { get; set; }
    }
}
