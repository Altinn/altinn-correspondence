namespace Altinn.Correspondence.LoadTests.DatabasePopulater
{
    public class BatchingOptions
    {
        public int BatchSize { get; set; } = 50000;
        public int MaxDegreeOfParallelism { get; set; } = 4;
        public Action<string> Logger { get; set; } = Console.WriteLine;
    }
}
