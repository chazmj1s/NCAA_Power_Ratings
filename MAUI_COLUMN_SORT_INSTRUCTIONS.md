# 📚 MAUI Column Sorting Tutorial - Step by Step

## What You've Learned So Far

### ✅ Changes Made to ViewModel (`PowerRankingsViewModel.cs`):

1. **Added sort direction tracking**:
   ```csharp
   private bool _isSortAscending = true;
   ```

2. **Added new command**:
   ```csharp
   public ICommand SortColumnCommand { get; }
   ```

3. **Added SortByColumn method** that:
   - Takes a column name as a string parameter
   - Maps it to a `RankingSort` enum value
   - Toggles ASC/DESC if you click the same column again
   - Defaults to DESC for Rating and SOS (higher is better)
   - Defaults to ASC for everything else

4. **Updated ApplyFiltersAndSort** to respect the `_isSortAscending` flag

### ✅ Changes Made to Model (`TeamRanking.cs`):

Added a display property for SOS:
```csharp
public string DisplaySOS => CombinedSOS?.ToString("F2") ?? "N/A";
```

---

## 🔧 What YOU Need to Do in XAML

Open `PowerRankingsPage.xaml` and make these 3 changes:

### Change 1: Update Column Headers (around line 70-81)

**FIND THIS:**
```xml
<CollectionView.Header>
    <Grid Padding="10,5"
          BackgroundColor="{AppThemeBinding Light=#f0f0f0, Dark=#2b2b2b}"
          ColumnDefinitions="60,*,80,100,100">
        <Label Grid.Column="0" Text="Rank" FontAttributes="Bold" />
        <Label Grid.Column="1" Text="Team" FontAttributes="Bold" />
        <Label Grid.Column="2" Text="Record" FontAttributes="Bold" />
        <Label Grid.Column="3" Text="Rating" FontAttributes="Bold" />
        <Label Grid.Column="4" Text="Conference" FontAttributes="Bold" />
    </Grid>
</CollectionView.Header>
```

**REPLACE WITH:**
```xml
<CollectionView.Header>
    <Grid Padding="10,5"
          BackgroundColor="{AppThemeBinding Light=#f0f0f0, Dark=#2b2b2b}"
          ColumnDefinitions="60,*,80,100,100,80">

        <!-- Rank Header -->
        <Label Grid.Column="0" Text="Rank ▼" FontAttributes="Bold">
            <Label.GestureRecognizers>
                <TapGestureRecognizer Command="{Binding SortColumnCommand}"
                                    CommandParameter="Rank"/>
            </Label.GestureRecognizers>
        </Label>

        <!-- Team Header -->
        <Label Grid.Column="1" Text="Team ▼" FontAttributes="Bold">
            <Label.GestureRecognizers>
                <TapGestureRecognizer Command="{Binding SortColumnCommand}"
                                    CommandParameter="Team"/>
            </Label.GestureRecognizers>
        </Label>

        <!-- Record Header -->
        <Label Grid.Column="2" Text="Record ▼" FontAttributes="Bold">
            <Label.GestureRecognizers>
                <TapGestureRecognizer Command="{Binding SortColumnCommand}"
                                    CommandParameter="Record"/>
            </Label.GestureRecognizers>
        </Label>

        <!-- Rating Header -->
        <Label Grid.Column="3" Text="Rating ▼" FontAttributes="Bold">
            <Label.GestureRecognizers>
                <TapGestureRecognizer Command="{Binding SortColumnCommand}"
                                    CommandParameter="Rating"/>
            </Label.GestureRecognizers>
        </Label>

        <!-- Conference Header -->
        <Label Grid.Column="4" Text="Conference ▼" FontAttributes="Bold">
            <Label.GestureRecognizers>
                <TapGestureRecognizer Command="{Binding SortColumnCommand}"
                                    CommandParameter="Conference"/>
            </Label.GestureRecognizers>
        </Label>

        <!-- SOS Header (NEW!) -->
        <Label Grid.Column="5" Text="SOS ▼" FontAttributes="Bold">
            <Label.GestureRecognizers>
                <TapGestureRecognizer Command="{Binding SortColumnCommand}"
                                    CommandParameter="SOS"/>
            </Label.GestureRecognizers>
        </Label>
    </Grid>
</CollectionView.Header>
```

---

### Change 2: Update ItemTemplate Grid (around line 83-86)

**FIND THIS:**
```xml
<CollectionView.ItemTemplate>
    <DataTemplate x:DataType="models:TeamRanking">
        <Grid Padding="10,8"
              ColumnDefinitions="60,*,80,100,100">
```

**REPLACE WITH:**
```xml
<CollectionView.ItemTemplate>
    <DataTemplate x:DataType="models:TeamRanking">
        <Grid Padding="10,8"
              ColumnDefinitions="60,*,80,100,100,80">
```

---

### Change 3: Add SOS Display Column (around line 127-132)

**FIND THIS (at the end of the Grid, just before `</Grid>`):**
```xml
                        <!-- Conference Abbreviation -->
                        <Label Grid.Column="4"
                               Text="{Binding ConferenceAbbr}"
                               FontSize="12"
                               VerticalOptions="Center"/>
                    </Grid>
                </DataTemplate>
            </CollectionView.ItemTemplate>
```

**REPLACE WITH:**
```xml
                        <!-- Conference Abbreviation -->
                        <Label Grid.Column="4"
                               Text="{Binding ConferenceAbbr}"
                               FontSize="12"
                               VerticalOptions="Center"/>

                        <!-- SOS (NEW!) -->
                        <Label Grid.Column="5"
                               Text="{Binding DisplaySOS}"
                               FontSize="12"
                               VerticalOptions="Center"/>
                    </Grid>
                </DataTemplate>
            </CollectionView.ItemTemplate>
```

---

## 🎓 What Each Part Does (LEARNING TIME!)

### 1. **ColumnDefinitions**
```xml
ColumnDefinitions="60,*,80,100,100,80"
```
- Defines 6 columns in the grid
- `60` = 60 pixels wide (fixed)
- `*` = takes up remaining space (flexible)
- `80`, `100`, `100`, `80` = fixed widths

### 2. **Grid.Column**
```xml
<Label Grid.Column="5" Text="SOS ▼" />
```
- Tells the label which column to appear in (0-indexed)
- Column 5 is the 6th column (our new SOS column)

### 3. **TapGestureRecognizer**
```xml
<Label.GestureRecognizers>
    <TapGestureRecognizer Command="{Binding SortColumnCommand}"
                        CommandParameter="Rank"/>
</Label.GestureRecognizers>
```
- **`GestureRecognizers`** - Handles touch/click events
- **`Command`** - Calls the `SortColumnCommand` in your ViewModel
- **`CommandParameter`** - Passes "Rank" as a string to the command
- **`{Binding ...}`** - Connects to the ViewModel property/command

### 4. **Data Binding**
```xml
Text="{Binding DisplaySOS}"
```
- The `{Binding}` syntax connects XAML to your C# properties
- `DisplaySOS` must exist in your `TeamRanking` model
- Updates automatically when the property changes (if using `INotifyPropertyChanged`)

### 5. **DataTemplate & x:DataType**
```xml
<DataTemplate x:DataType="models:TeamRanking">
```
- **`DataTemplate`** - Defines how each item in the list looks
- **`x:DataType`** - Enables compiled bindings (faster, compile-time checking)
- Each row in the CollectionView uses this template

---

## 🧪 How to Test

1. **Build the solution** (`Ctrl+Shift+B`)
2. **Run the MAUI app**
3. **Click on any column header** - it should sort by that column
4. **Click the same header again** - should toggle between ASC/DESC
5. **Look for the SOS column** on the right side

---

## 🐛 Troubleshooting

### If columns don't click:
- Check that `SortColumnCommand` is public in the ViewModel
- Make sure the command is initialized in the constructor
- Check the Output window for binding errors

### If SOS column doesn't show:
- Verify `DisplaySOS` property exists in `TeamRanking.cs`
- Check that you updated BOTH the header grid AND the item template grid
- Make sure `ColumnDefinitions` has 6 values, not 5

### If data doesn't sort:
- Check the Output window for errors
- Put a breakpoint in `SortByColumn` method
- Verify `ApplyFiltersAndSort` is being called

---

## 🚀 Next Steps You Can Try

1. **Add up/down arrows** that change based on sort direction
2. **Highlight the currently sorted column**
3. **Add more columns** (like Wins, Losses, BaseSOS)
4. **Make rows clickable** to see team details
5. **Add a search bar** to filter by team name

---

## 💡 Key MAUI Concepts Cheat Sheet

| Concept | Purpose | Example |
|---------|---------|---------|
| `CollectionView` | Displays lists | `<CollectionView ItemsSource="{Binding Teams}">` |
| `DataTemplate` | Defines item layout | `<CollectionView.ItemTemplate><DataTemplate>` |
| `Grid` | Layout container with rows/columns | `<Grid ColumnDefinitions="60,*,80">` |
| `{Binding}` | Connects UI to data | `Text="{Binding TeamName}"` |
| `Command` | Handles interactions | `Command="{Binding SortCommand}"` |
| `GestureRecognizers` | Handles touch/click | `<TapGestureRecognizer Command="..."/>` |
| `x:DataType` | Enables compiled bindings | `x:DataType="models:TeamRanking"` |

---

Good luck! You've got this! 🎉
