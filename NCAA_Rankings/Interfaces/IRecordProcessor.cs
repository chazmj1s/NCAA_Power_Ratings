namespace NCAA_Rankings.Interfaces
{
    public interface IRecordProcessor
    {
        public Task ProcessSingleRecordAsync(string[] cells, string yearIn, CancellationToken token);
    }
}
