namespace NCAA_Rankings.Utilities
{
    public class FileRecord
    {
        public FileRecord(string value, string[] strings)
        {
            FileName = value;
            Fields = strings;
        }

        public  string FileName { get; set; }
        public  string[] Fields { get; set; }
    }
}
