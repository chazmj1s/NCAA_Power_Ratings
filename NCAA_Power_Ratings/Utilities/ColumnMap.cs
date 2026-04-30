namespace NCAA_Power_Ratings.Utilities
{
    public static class ColumnMap
    {
        public static ColumnIndexes ForYear(int year) => year switch
        {
            >= 2013 => new ColumnIndexes(
                RowId: 0,
                Week: 1,
                WinnerName: 5,
                WPoints: 6,
                Location: 7,
                LoserName: 8,
                LPoints: 9
            ),

            _ => new ColumnIndexes(    // old format
                RowId: 0,
                Week: 1,
                WinnerName: 4,
                WPoints: 5,
                Location: 6,
                LoserName: 7,
                LPoints: 8
            )
        };

        public record ColumnIndexes(
            int RowId,
            int Week,
            int WinnerName,
            int WPoints,
            int Location,
            int LoserName,            
            int LPoints
        );
    }
}
