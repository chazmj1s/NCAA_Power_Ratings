namespace NCAA_Power_Ratings.Utilities
{
    public class FileRecord(string value, string[] strings)
    {
        public string FileName { get; set; } = value;
        public string[] Fields { get; set; } = strings;
    }
}
