# MAUI DB Manager — Complete Learning and Maintenance Guide

## 1. Purpose of this report

This report explains the MAUI DB Manager application from the beginning: why it is structured this way, how the screen communicates with C#, how C# communicates with SQL Server, where each visible text and database table name is defined, and how to safely modify or extend the program.

The report is written for someone learning both C# and .NET MAUI. It describes the code as it exists now, after the UI, database selection, modification sections, theme support, table validation, change tracking, preview, and code organization were completed.

Related project documents:

- [README.md](README.md) — setup, build, and feature summary
- [ARCHITECTURE.md](ARCHITECTURE.md) — short architectural map
- [MauiDbManager.sln](MauiDbManager.sln) — Visual Studio/Rider solution
- [MauiDbManager/MauiDbManager.csproj](MauiDbManager/MauiDbManager.csproj) — project and package configuration

---

## 2. What was built, in chronological terms

The application began as a compact SQL Server connection form and grew into a controlled database-modification workspace.

The implemented behavior is:

1. The user enters a SQL Server address, SQL login, and password.
2. The app connects using SQL Server Authentication.
3. After a successful connection, it loads accessible online databases.
4. After the user chooses a database, the app inspects which required `dbo` tables exist.
5. Only sections whose required tables exist can be enabled.
6. The user can configure Overall, Gain, Amplitude, and Zakan changes.
7. Apply and Preview stay disabled until an enabled section contains an unapplied change.
8. Preview reads current rows and shows Insert/Update actions without modifying target tables.
9. Apply executes all pending sections in one SQL transaction.
10. After success, the applied sections are marked clean and Apply becomes disabled again.
11. The UI follows the system theme by default and can be manually switched to Light or Dark.
12. The application runs on Mac Catalyst and Windows.

The four modification sections are:

| UI section | Main target | Supporting tables |
|---|---|---|
| Overall Modification | `dbo.overall_status` | `dbo.type_a`, `dbo.type_b` |
| Gain Modification | `dbo.gain` | None |
| Amplitude Modification | `dbo.amplitude` | None |
| Zakan Modification | `dbo.zakan` | None |

---

## 3. Technology overview

### 3.1 C#

C# is the programming language used for:

- Application state
- Commands
- Validation
- Parsing ID selectors
- SQL Server connections
- Preview queries
- Transactional database writes
- Error handling

The project enables nullable-reference checking and implicit standard-library imports in the project file:

~~~xml
<ImplicitUsings>enable</ImplicitUsings>
<Nullable>enable</Nullable>
~~~

Nullable checking helps catch mistakes such as assuming that a selected database always exists.

### 3.2 .NET MAUI

.NET MAUI is the cross-platform UI framework. One shared project produces:

- A Mac Catalyst desktop application on macOS
- A Windows desktop application on Windows

The project uses the MAUI “single project” model. Shared pages, views, models, and services live once in the project, while platform-specific files live under `Platforms/MacCatalyst` and `Platforms/Windows`.

### 3.3 XAML

XAML describes the visual tree: labels, inputs, buttons, grids, borders, pickers, and bindings.

For example:

~~~xml
<Entry
    Placeholder="192.168.0.56,1433"
    Text="{Binding ServerName}" />
~~~

This means:

- The visible object is an `Entry`.
- Its hint is `192.168.0.56,1433`.
- Its content is connected to the C# `ServerName` property.

### 3.4 SQL client

The project uses Microsoft’s SQL Server driver:

~~~xml
<PackageReference Include="Microsoft.Data.SqlClient" Version="7.0.2" />
~~~

The main SQL classes used are:

- `SqlConnectionStringBuilder` — safely constructs connection strings.
- `SqlConnection` — opens a SQL Server connection.
- `SqlCommand` — executes parameterized SQL.
- `SqlDataReader` — reads query results.
- `SqlTransaction` — makes all Apply operations commit or roll back together.

---

## 4. Project startup: what happens when the app launches

The startup path is:

~~~text
Operating system
    ↓
MauiProgram.CreateMauiApp()
    ↓
App constructor
    ↓
App.CreateWindow()
    ↓
Dependency injection creates MainPage
    ↓
Dependency injection creates ConnectionViewModel
    ↓
MainPage assigns the view model as BindingContext
    ↓
Every child ContentView inherits that BindingContext
~~~

### 4.1 Project configuration

File: [MauiDbManager/MauiDbManager.csproj](MauiDbManager/MauiDbManager.csproj)

Important settings:

- `UseMaui=true` enables MAUI build behavior.
- `SingleProject=true` enables the shared-project structure.
- `net10.0-maccatalyst` is always targeted.
- The Windows target is added when the build runs on Windows.
- `ApplicationId` is `com.example.mauidbmanager`.
- MAUI Controls and SQL Client package versions are declared here.

If you want to change the application title, ID, version, target framework, package version, icon, or splash screen, begin in this file.

### 4.2 Dependency injection

File: [MauiDbManager/MauiProgram.cs](MauiDbManager/MauiProgram.cs)

Dependency injection means that classes declare what they need in their constructor instead of creating dependencies themselves.

Registered services:

~~~csharp
builder.Services.AddSingleton<IDatabaseConnectionService, SqlServerConnectionService>();
builder.Services.AddSingleton<IDatabaseLookupService, SqlServerLookupService>();
builder.Services.AddSingleton<IDatabaseModificationService, SqlServerModificationService>();
builder.Services.AddTransient<ConnectionViewModel>();
builder.Services.AddTransient<MainPage>();
~~~

`Singleton` means one service instance is reused for the application lifetime.

`Transient` means a new instance is created whenever requested.

The view model constructor asks for three interfaces:

~~~csharp
public ConnectionViewModel(
    IDatabaseConnectionService connectionService,
    IDatabaseLookupService lookupService,
    IDatabaseModificationService modificationService)
~~~

MAUI’s service container supplies the registered implementations automatically.

### 4.3 Application resources and window

Files:

- [MauiDbManager/App.xaml](MauiDbManager/App.xaml)
- [MauiDbManager/App.xaml.cs](MauiDbManager/App.xaml.cs)

`App.xaml` merges global color and style dictionaries. They become available to every page and view.

`App.xaml.cs` creates the main window and sets:

- Window title
- Minimum width of 900
- Minimum height of 700

The page is resolved only after `InitializeComponent` loads the resources. This matters because the page uses global `StaticResource` values.

---

## 5. The UI structure

### 5.1 MainPage is only composition

Files:

- [MauiDbManager/MainPage.xaml](MauiDbManager/MainPage.xaml)
- [MauiDbManager/MainPage.xaml.cs](MauiDbManager/MainPage.xaml.cs)

`MainPage.xaml` intentionally contains almost no section details. It arranges named views:

~~~xml
<views:WorkspaceHeaderView />
<views:ConnectionSectionView />
<views:DatabaseSelectionSectionView />
<views:OverallModificationSectionView />
<views:GainModificationSectionView />
<views:AmplitudeModificationSectionView />
<views:ZakanModificationSectionView />
~~~

The Apply bar is placed in the bottom grid row, and the Preview dialog spans both rows as an overlay.

`MainPage.xaml.cs` receives the view model and assigns it:

~~~csharp
BindingContext = viewModel;
~~~

That one line is central to MVVM. All descendant views inherit this object as their binding source.

### 5.2 Why each section is a ContentView

A `ContentPage` represents a whole screen or window page.

A `ContentView` represents a reusable piece of a screen.

Each section has:

- A `.xaml` file containing its UI
- A `.xaml.cs` file that calls `InitializeComponent()`

The code-behind files intentionally contain no business logic. This keeps SQL and validation outside the UI.

### 5.3 View-to-purpose map

| What you see | XAML file |
|---|---|
| Page title and Appearance picker | [WorkspaceHeaderView.xaml](MauiDbManager/Views/WorkspaceHeaderView.xaml) |
| Server, username, password, certificate, Connect, Reset | [ConnectionSectionView.xaml](MauiDbManager/Views/ConnectionSectionView.xaml) |
| Working database selector | [DatabaseSelectionSectionView.xaml](MauiDbManager/Views/DatabaseSelectionSectionView.xaml) |
| Overall Modification | [OverallModificationSectionView.xaml](MauiDbManager/Views/OverallModificationSectionView.xaml) |
| Gain Modification | [GainModificationSectionView.xaml](MauiDbManager/Views/GainModificationSectionView.xaml) |
| Amplitude Modification | [AmplitudeModificationSectionView.xaml](MauiDbManager/Views/AmplitudeModificationSectionView.xaml) |
| Zakan Modification | [ZakanModificationSectionView.xaml](MauiDbManager/Views/ZakanModificationSectionView.xaml) |
| Preview and Apply buttons | [ApplyBarView.xaml](MauiDbManager/Views/ApplyBarView.xaml) |
| Preview overlay and result grid | [PreviewDialogView.xaml](MauiDbManager/Views/PreviewDialogView.xaml) |

---

## 6. Where visible text is stored

Most fixed text is directly in the relevant XAML file as `Text="..."` or `Placeholder="..."`.

### 6.1 Header text

File: `WorkspaceHeaderView.xaml`

Examples:

- “Database workspace”
- “Configure the workspace one section at a time.”
- “Appearance”

Theme option labels are not in XAML. They are constructed in [ConnectionViewModel.cs](MauiDbManager/ViewModels/ConnectionViewModel.cs):

- “System theme”
- “Light”
- “Dark”

### 6.2 Connection text

File: `ConnectionSectionView.xaml`

Examples:

- “SQL Server connection”
- “SQL Server Authentication”
- “Server”
- “User name”
- “Password”
- “Certificate”
- “Trust server certificate”
- “Development networks only”
- “Reset”
- “Connect”

Dynamic connection success/failure text is produced by:

- [SqlServerConnectionService.cs](MauiDbManager/Services/SqlServerConnectionService.cs)
- [ConnectionViewModel.Connection.cs](MauiDbManager/ViewModels/ConnectionViewModel.Connection.cs)
- [ConnectionViewModel.OperationStatus.cs](MauiDbManager/ViewModels/ConnectionViewModel.OperationStatus.cs)

### 6.3 Database selector text

Fixed labels live in `DatabaseSelectionSectionView.xaml`.

Dynamic help and selector text live in `ConnectionViewModel.Connection.cs`:

~~~csharp
public string DatabaseSelectorText => SelectedDatabase ?? "Select a database";
~~~

### 6.4 Modification section text

Each modification XAML file contains its title, description, field labels, and examples.

Missing-table messages are built in:

[ConnectionViewModel.Availability.cs](MauiDbManager/ViewModels/ConnectionViewModel.Availability.cs)

### 6.5 Apply and preview text

Files:

- `ApplyBarView.xaml` — “Preview changes”, “Apply changes”
- `PreviewDialogView.xaml` — dialog title, headers, buttons, safety note
- [ModificationPreviewResult.cs](MauiDbManager/Models/ModificationPreviewResult.cs) — preview success message
- [SqlServerModificationService.Apply.cs](MauiDbManager/Services/SqlServerModificationService.Apply.cs) — apply success/failure messages

### 6.6 Fastest way to find visible text

From the project root:

~~~bash
rg -n 'Text="|Placeholder="' MauiDbManager/Views
~~~

To search for a specific phrase:

~~~bash
rg -n "Apply changes" MauiDbManager
~~~

In Rider or Visual Studio, use “Find in Files” and search the exact words visible on screen.

---

## 7. XAML layout concepts used here

### 7.1 Grid

`Grid` arranges controls into rows and columns.

Example:

~~~xml
<Grid ColumnDefinitions="1.25*,*" RowDefinitions="Auto,Auto">
~~~

- `Auto` uses only the required size.
- `*` divides remaining space proportionally.
- `1.25*` receives more space than `1*`.

### 7.2 Stack layouts

- `VerticalStackLayout` places controls from top to bottom.
- `HorizontalStackLayout` places controls left to right.

`Spacing` controls distance between child controls.

### 7.3 Border

`Border` creates the card and field surfaces. It supports:

- Background
- Stroke color
- Rounded shape
- Padding
- Enabled/disabled opacity

### 7.4 ScrollView and CollectionView

`ScrollView` allows the whole workspace to scroll.

`CollectionView` displays dynamic lists, such as databases and preview rows. It is preferable to manually creating controls for every row.

### 7.5 BindableLayout

The TB options use `BindableLayout.ItemsSource` because the desired behavior is a compact list that expands to show all choices without its own scrolling region.

### 7.6 DataTrigger

A `DataTrigger` changes appearance when a bound value changes.

Example concept:

~~~xml
<DataTrigger Binding="{Binding IsAmplitudeModificationEnabled}" Value="False">
    <Setter Property="Opacity" Value="0.62" />
</DataTrigger>
~~~

This is how disabled sections remain visible but clearly muted.

### 7.7 AutomationId

IDs such as `ServerNameEntry` and `ApplyButton` are stable names for accessibility and UI automation. They do not affect normal appearance.

---

## 8. Styles, colors, light mode, and dark mode

Files:

- [Colors.xaml](MauiDbManager/Resources/Styles/Colors.xaml)
- [TypographyStyles.xaml](MauiDbManager/Resources/Styles/TypographyStyles.xaml)
- [FieldStyles.xaml](MauiDbManager/Resources/Styles/FieldStyles.xaml)
- [ContainerStyles.xaml](MauiDbManager/Resources/Styles/ContainerStyles.xaml)
- [ButtonStyles.xaml](MauiDbManager/Resources/Styles/ButtonStyles.xaml)

### 8.1 Semantic colors

Colors are named by purpose, not by screen location:

- `PageBackground`
- `CardBackground`
- `TextColor`
- `MutedText`
- `BorderColor`
- `FieldBackground`
- `ErrorText`
- `DisabledButtonBackground`

This is better than placing color hex values in every section.

### 8.2 AppThemeBinding

Example:

~~~xml
TextColor="{AppThemeBinding
    Light={StaticResource TextColor},
    Dark={StaticResource DarkTextColor}}"
~~~

MAUI selects the correct value for the current application theme.

### 8.3 Theme selection logic

File: [ConnectionViewModel.Theme.cs](MauiDbManager/ViewModels/ConnectionViewModel.Theme.cs)

The picker changes `Application.Current.UserAppTheme`:

- System → `AppTheme.Unspecified`
- Light → `AppTheme.Light`
- Dark → `AppTheme.Dark`

`Unspecified` means “follow the operating system.”

When the requested theme changes, the view model raises notifications for dynamic success/error colors that are calculated in C#.

### 8.4 Where to change typography

Use `TypographyStyles.xaml` for labels and hierarchy.

Use `FieldStyles.xaml` for input and picker font sizes.

Use `ButtonStyles.xaml` for buttons.

Use `ContainerStyles.xaml` for preview table cells and cards.

---

## 9. MVVM: how XAML and C# communicate

MVVM means Model–View–ViewModel.

### 9.1 View

The View is XAML. It displays data and forwards user actions through bindings.

### 9.2 ViewModel

The ViewModel exposes properties and commands without knowing about specific native Mac or Windows controls.

The logical view model is one C# class split across partial files:

| File | Responsibility |
|---|---|
| [ConnectionViewModel.cs](MauiDbManager/ViewModels/ConnectionViewModel.cs) | Fields, constructor, collections, commands, common notification helper |
| [ConnectionViewModel.Connection.cs](MauiDbManager/ViewModels/ConnectionViewModel.Connection.cs) | Credentials, connection, database selection, reset |
| [ConnectionViewModel.Theme.cs](MauiDbManager/ViewModels/ConnectionViewModel.Theme.cs) | Theme |
| [ConnectionViewModel.Availability.cs](MauiDbManager/ViewModels/ConnectionViewModel.Availability.cs) | Required tables and missing-table messages |
| [ConnectionViewModel.Overall.cs](MauiDbManager/ViewModels/ConnectionViewModel.Overall.cs) | Overall fields and TB options |
| [ConnectionViewModel.TableSections.cs](MauiDbManager/ViewModels/ConnectionViewModel.TableSections.cs) | Gain, Amplitude, and Zakan fields |
| [ConnectionViewModel.OperationStatus.cs](MauiDbManager/ViewModels/ConnectionViewModel.OperationStatus.cs) | Busy/status/apply/preview messages |
| [ConnectionViewModel.ModificationActions.cs](MauiDbManager/ViewModels/ConnectionViewModel.ModificationActions.cs) | Builds requests, Preview, Apply |
| [ConnectionViewModel.ChangeTracking.cs](MauiDbManager/ViewModels/ConnectionViewModel.ChangeTracking.cs) | Dirty state and command enablement |

### 9.3 Partial classes

Each file says:

~~~csharp
public sealed partial class ConnectionViewModel
~~~

At compile time, C# combines all partial declarations into one class. This gives smaller files without changing bindings or creating many objects.

A property defined in `ConnectionViewModel.Overall.cs` is available to the constructor in `ConnectionViewModel.cs` because both are parts of the same class.

### 9.4 Property notifications

The class implements `INotifyPropertyChanged`.

`SetProperty`:

1. Compares old and new values.
2. Updates the backing field.
3. raises `PropertyChanged`.

MAUI listens to this event and redraws bound properties.

For dependent properties, explicit notifications are required. For example, when `IsConnected` changes, the view model also notifies:

- `StatusSymbol`
- `StatusColor`
- `StatusBackgroundColor`
- `DatabaseHelpText`

### 9.5 ObservableCollection

`Databases`, `TypeBOptions`, `PreviewSummaries`, and `PreviewRows` are `ObservableCollection<T>`.

When items are added or removed, MAUI updates the visible list automatically.

### 9.6 Commands

Buttons bind to commands:

~~~xml
<Button Command="{Binding ConnectCommand}" Text="Connect" />
~~~

`AsyncRelayCommand` wraps an asynchronous method and prevents duplicate execution while it is already running.

`CanExecute` controls whether the button is enabled. MAUI automatically applies the disabled style when the command cannot execute.

---

## 10. C# language features used in the project

### 10.1 Interfaces

An interface defines a contract without implementation.

Example:

~~~csharp
public interface IDatabaseConnectionService
{
    Task<ConnectionResult> TestConnectionAsync(...);
}
~~~

The view model depends on this interface, not directly on `SqlServerConnectionService`. This improves separation and makes testing easier.

### 10.2 Records

Records are concise immutable data carriers.

Example:

~~~csharp
public sealed record TableStatusModification(
    string TableName,
    BooleanStatus Status);
~~~

Records are used for request and result objects because they primarily represent values.

### 10.3 Enums

Enums give names to a restricted set of numeric values.

`BooleanStatus`:

- False = 0
- True = 1

`OverallStatus`:

- None = 0
- InProgress = 1
- Pass = 2
- Fail = 3

### 10.4 async and await

Database operations are asynchronous:

~~~csharp
await connection.OpenAsync(cancellationToken);
~~~

This prevents the UI thread from freezing while waiting for network or SQL Server operations.

### 10.5 await using

Database objects implement asynchronous disposal:

~~~csharp
await using var connection = new SqlConnection(connectionString);
~~~

The connection is reliably closed, even if an exception occurs.

### 10.6 LINQ

LINQ is used to filter, project, sort, and deduplicate collections.

Example behavior:

~~~csharp
typeAPairs
    .Where(pair => selectedLowIds.Contains(pair.Tb))
    .ToArray();
~~~

### 10.7 Switch expressions

Switch expressions select values compactly:

~~~csharp
Application.Current.UserAppTheme = value.Value switch
{
    ThemePreference.Light => AppTheme.Light,
    ThemePreference.Dark => AppTheme.Dark,
    _ => AppTheme.Unspecified
};
~~~

### 10.8 Raw string literals

SQL is written with triple-quote raw strings:

~~~csharp
const string query = """
    SELECT ...
    FROM ...
    """;
~~~

This avoids excessive escaping and keeps SQL readable.

---

## 11. Connection workflow

### 11.1 User input

XAML file: `ConnectionSectionView.xaml`

Bindings:

| UI field | View-model property |
|---|---|
| Server | `ServerName` |
| User name | `UserName` |
| Password | `Password` |
| Trust certificate | `TrustServerCertificate` |

The username defaults to `sa`.

The server syntax for port 1433 is:

~~~text
192.168.0.56,1433
~~~

SQL Server connection strings use a comma between server and port.

### 11.2 Input validation

File: `ConnectionViewModel.Connection.cs`

Before connecting:

- Server must not be blank.
- Username must not be blank.
- Password must not be empty.

### 11.3 Connection string

File: [SqlServerConnectionService.cs](MauiDbManager/Services/SqlServerConnectionService.cs)

Important values:

- `DataSource` comes from the server field.
- `UserID` and `Password` come from the user.
- `IntegratedSecurity=false` means SQL Server Authentication.
- `Encrypt=true` always requests encryption.
- `TrustServerCertificate` comes from the switch.
- `ConnectTimeout=10` stops long connection attempts.

`SqlConnectionStringBuilder` safely handles escaping. Do not build a connection string with string concatenation.

### 11.4 Database discovery

After connection, this query runs:

~~~sql
SELECT [name]
FROM sys.databases
WHERE [state] = 0
  AND HAS_DBACCESS([name]) = 1
ORDER BY [name];
~~~

Meaning:

- `state = 0` → database is online.
- `HAS_DBACCESS = 1` → login can access it.

The service returns the database names in `ConnectionResult`.

### 11.5 Error handling

Common SQL error numbers receive clearer explanations:

- 18456 → username/password rejected
- -2 → timeout
- 53 or 11001 → server not found
- 4060 → default database unavailable

Other exceptions return the SQL driver’s message.

The password is never saved to disk by this application; it remains in memory.

---

## 12. Database selection and required-table checking

### 12.1 Selecting a database

The database list binds `SelectedItem` to `SelectedDatabase`.

When `SelectedDatabase` changes, the view model:

1. Closes the dropdown.
2. Clears old TB choices.
3. Disables all modification sections.
4. Resets availability.
5. creates a fresh clean-state snapshot.
6. Starts loading table information.

### 12.2 Table inspection query

File: [SqlServerLookupService.cs](MauiDbManager/Services/SqlServerLookupService.cs)

The app queries `sys.tables` and `sys.schemas` for `dbo` tables.

The results become a case-insensitive `HashSet<string>` such as:

~~~text
dbo.overall_status
dbo.type_a
dbo.type_b
dbo.gain
dbo.amplitude
dbo.zakan
~~~

### 12.3 Required-table rules

File: `ConnectionViewModel.Availability.cs`

- Overall requires all three: `dbo.overall_status`, `dbo.type_a`, `dbo.type_b`.
- Gain requires `dbo.gain`.
- Amplitude requires `dbo.amplitude`.
- Zakan requires `dbo.zakan`.

If a requirement is absent:

- The section checkbox is disabled.
- The section remains visually grayed.
- An inline red message says exactly which table is missing.
- The same message is available as a tooltip.

A login that lacks metadata visibility may make a table appear unavailable. SQL Server permissions therefore matter in addition to physical existence.

### 12.4 Loading TB choices

If `dbo.type_b` exists, the app runs:

~~~sql
SELECT [low_id], [id_text]
FROM [dbo].[type_b]
ORDER BY [id_text], [low_id];
~~~

Each row becomes a `TypeBLookupItem` and then a selectable UI option.

---

## 13. ID selector parsing

File: [LowIdSelectorParser.cs](MauiDbManager/Parsing/LowIdSelectorParser.cs)

Accepted forms:

- Single value: `1`
- List: `1, 4`
- Range: `1-3`
- Parentheses: `(1, 4)`
- Mixture: `1, 4, 7-10`

Parser behavior:

1. Removes parentheses.
2. Splits on commas.
3. Detects individual numbers or ranges.
4. Requires non-negative integers.
5. Requires ascending ranges.
6. Stores values in a `SortedSet<long>`.
7. Removes duplicates.
8. Sorts the result.
9. Limits expansion to 10,000 IDs.

The same parser is reused for Overall mid IDs, Overall high IDs, and Amplitude low IDs, even though its historical class name says “LowId.”

---

## 14. Change tracking and why Apply is disabled

File: [ConnectionViewModel.ChangeTracking.cs](MauiDbManager/ViewModels/ConnectionViewModel.ChangeTracking.cs)

The app does not enable Apply merely because a database is selected.

Each section has a signature: a string representing all values relevant to that section.

For Overall, the signature includes:

- Section enabled state
- Mid selector text
- High selector text
- Update-status choice
- Selected status
- Update-time choice
- Date and time
- Whether date/time was manually edited
- Selected TB choices

Similar smaller signatures exist for Gain, Amplitude, and Zakan.

### 14.1 Baselines

A baseline signature means “last known applied configuration.”

Baselines are created:

- At startup
- When the database changes
- After a successful Apply, for each section that was actually applied

### 14.2 Pending changes

A section is pending only when:

1. The section is enabled.
2. Its current signature differs from its baseline.

This prevents an unchanged enabled section from being written again when another section changes.

### 14.3 CanApply rules

Preview and Apply share `CanApply`.

They are disabled if:

- The app is busy.
- The server is not connected.
- No database is selected.
- No enabled section has pending changes.
- Overall wants neither status nor time.
- Overall’s TB lookup is still running.
- Overall has no TB selection.
- Amplitude wants neither status nor time.
- A pending section’s required table is unavailable.

`RefreshApplyCommand` raises `CanExecuteChanged` for Preview and Apply whenever relevant state changes.

---

## 15. One shared request for Preview and Apply

File: [ConnectionViewModel.ModificationActions.cs](MauiDbManager/ViewModels/ConnectionViewModel.ModificationActions.cs)

`TryBuildPendingRequest` is important because it prevents Preview and Apply from interpreting user input differently.

It:

1. Determines which sections are pending.
2. Parses Overall mid and high selectors.
3. Resolves All or selected TB low IDs.
4. Creates an `OverallModification` record.
5. Creates `TableStatusModification` records for pending Gain/Zakan.
6. Parses Amplitude IDs.
7. Creates an `AmplitudeModification` record.
8. Returns one `PendingModificationRequest`.

Preview and Apply send the same records to different service methods.

---

## 16. Time conversion

Overall and Amplitude use a date picker plus a time picker.

`ResolveEpochMilliseconds` returns Unix epoch milliseconds.

If the user has not edited the default date/time, the function uses:

~~~csharp
DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
~~~

This means “current time at the moment Preview or Apply builds the request.”

If the user edits the controls:

1. The selected date and time are combined.
2. They are interpreted in the computer’s local timezone.
3. They are converted to Unix epoch milliseconds.

Because Preview is read-only and Apply may happen later, a “current time” value can differ slightly between Preview and Apply. The preview dialog explains this.

---

## 17. SQL safety principles used

### 17.1 Parameters

User values are passed as SQL parameters:

~~~csharp
command.Parameters.Add("@status", SqlDbType.Bit).Value = ...;
~~~

This protects against SQL injection and handles data types correctly.

### 17.2 Identifiers

Table and column identifiers cannot be normal value parameters.

For Gain and Zakan, table names must first exist in the hardcoded `AllowedTables` dictionary. Discovered column names are escaped by `QuoteIdentifier`.

This combination prevents arbitrary user-provided table names from entering SQL.

### 17.3 Schema validation

`GetWritableColumnsAsync` queries `sys.columns` and excludes:

- Computed columns
- Identity columns

It preserves column order using `column_id`.

Before modifying a table, each operation verifies the columns it needs.

### 17.4 Transaction

All pending Apply operations use one `SqlTransaction`.

~~~text
Begin transaction
    → Apply Gain/Zakan changes
    → Apply Overall changes
    → Apply Amplitude changes
    → Commit
~~~

If any operation throws:

~~~text
Catch error
    → Roll back everything
    → Return failure
~~~

This avoids a partial state where one table changed and another failed.

---

## 18. Gain and Zakan SQL logic

Files:

- [SqlServerModificationService.cs](MauiDbManager/Services/SqlServerModificationService.cs)
- [SqlServerModificationService.Apply.cs](MauiDbManager/Services/SqlServerModificationService.Apply.cs)
- [SqlServerModificationService.Preview.cs](MauiDbManager/Services/SqlServerModificationService.Preview.cs)

### 18.1 Allowed tables

The central allowlist contains:

~~~csharp
["dbo.gain"] = "dbo.gain",
["dbo.zakan"] = "dbo.zakan"
~~~

Only these simple status tables can pass through the generic Gain/Zakan operation.

### 18.2 Column discovery

For the selected table:

1. Find a writable column named `status`.
2. Take the first two other writable columns as keys.
3. Require both key columns and status.

### 18.3 Target row

The target key is always:

~~~text
(first key column = 0, second key column = 0)
~~~

### 18.4 Apply action

Conceptually:

~~~sql
IF row (0, 0) exists
    UPDATE status
ELSE
    INSERT (0, 0, status)
~~~

Status is sent as SQL `bit`.

### 18.5 Preview action

Preview performs a `SELECT TOP (1)` for the same key.

It displays:

- Update if a row exists
- Insert if it does not
- Current status
- Proposed status

Preview does not execute the write SQL.

---

## 19. Amplitude SQL logic

Target: `dbo.amplitude`

### 19.1 Expected schema

The writable columns must satisfy:

- First writable column is `low_id`.
- A writable `status` column exists.
- A writable `time` column exists.
- One additional writable column exists.

The extra column is the inserted zero-value column.

### 19.2 Existing rows

For each selected low ID:

~~~sql
UPDATE dbo.amplitude
SET selected columns
WHERE low_id = @lowId
~~~

The update clause depends on the UI:

- Status and time
- Status only
- Time only

### 19.3 Missing rows

If the update affected zero rows:

~~~text
Insert:
(low_id, extra value column, status, time)
(user ID, 0, resolved status, resolved time)
~~~

Even when the user only selected one output for existing rows, a new SQL row needs all required insert values.

Defaults are:

- Status: False when the user did not select status
- Time: selected/current time when the user did not select time

### 19.4 Preview

Amplitude IDs are serialized to JSON and passed in one parameter.

SQL Server uses `OPENJSON` to turn the JSON array into a row set.

`OUTER APPLY` checks whether each requested low ID exists.

Window functions calculate exact:

- Total row count
- Insert count
- Update count

Only the first 100 details are returned to the UI.

---

## 20. Overall SQL logic

Overall is the most complex operation.

Target: `dbo.overall_status`

Supporting tables:

- `dbo.type_a`
- `dbo.type_b`

### 20.1 Expected target columns

`dbo.overall_status` must contain:

- `mid_id`
- `high_id`
- `tb`
- `ta`
- `status`
- `time`

The first four identify a logical row.

### 20.2 TB text mapping

The UI displays `dbo.type_b.id_text`.

When the user selects a text:

1. Find its `dbo.type_b.low_id`.
2. Compare that value to `dbo.type_a.low_id`.
3. Keep matching `type_a` rows.
4. Map:
   - `type_a.low_id` → target `tb`
   - `type_a.high_id` → target `ta`

### 20.3 All behavior

All does not blindly use every row in `type_a`.

The SQL relationship is:

~~~sql
FROM dbo.type_a AS typeA
INNER JOIN dbo.type_b AS typeB
    ON typeB.low_id = typeA.low_id
~~~

Therefore, a `type_a.low_id` is included only if it exists in `type_b.low_id`.

With All selected, every matching pair is kept.

With specific texts selected, matching pairs are further filtered to the selected `type_b.low_id` values.

### 20.4 Final row set

Let:

- M = selected mid IDs
- H = selected high IDs
- P = filtered distinct `(tb, ta)` pairs

The final target keys are the Cartesian product:

~~~text
M × H × P
~~~

Example:

- 2 mid IDs
- 3 high IDs
- 4 type pairs

Result:

~~~text
2 × 3 × 4 = 24 target rows
~~~

### 20.5 Safety limit

Overall refuses more than 100,000 rows per Apply.

This check exists in both Preview and Apply.

### 20.6 Existing row

An existing row matches all four keys:

~~~sql
WHERE mid_id = @midId
  AND high_id = @highId
  AND tb = @tb
  AND ta = @ta
~~~

Only selected output columns are updated.

### 20.7 Missing row

If no existing row was updated, the service inserts:

~~~text
mid_id, high_id, tb, ta, status, time
~~~

A new row always receives complete values.

Defaults:

- Overall status defaults to 2 / Pass.
- Time defaults to current epoch milliseconds.

### 20.8 Preview query

Preview sends mid IDs, high IDs, and selected TB IDs as JSON arrays.

SQL CTEs create row sets from those arrays.

The query:

1. Creates distinct mid IDs.
2. Creates distinct high IDs.
3. Creates selected TB IDs.
4. Builds valid distinct type pairs using the `type_a/type_b` join.
5. Cross joins the three sets.
6. Uses `OUTER APPLY` to find an existing target row.
7. Labels each requested key Insert or Update.
8. Calculates exact counts with window functions.
9. Returns at most 100 displayed rows.

No target-table write occurs.

---

## 21. Preview design

The preview models are:

- [ModificationPreviewResult.cs](MauiDbManager/Models/ModificationPreviewResult.cs)
- [ModificationPreviewSummary.cs](MauiDbManager/Models/ModificationPreviewSummary.cs)
- [ModificationPreviewRow.cs](MauiDbManager/Models/ModificationPreviewRow.cs)

A preview row contains:

- Table
- Action
- Key
- Current values
- Proposed values

A summary contains:

- Insert count
- Update count
- Total count
- Displayed count

The dialog is an overlay in `PreviewDialogView.xaml`.

The outer `ContentView` binds its own `IsVisible` property to `IsPreviewVisible`. This is important: when hidden, the whole overlay must leave the hit-testing tree. If only the inner grid were hidden, a transparent outer view could block interaction with the app underneath.

---

## 22. Exact table-name edit map

Table names currently appear in multiple layers because they affect UI text, availability, request mapping, SQL allowlists, and SQL statements.

Use “Find in Files” before renaming:

~~~bash
rg -n "dbo\.gain|dbo\.zakan|dbo\.amplitude|dbo\.overall_status|dbo\.type_a|dbo\.type_b" MauiDbManager
~~~

### 22.1 Rename `dbo.gain`

Check and update:

1. `GainModificationSectionView.xaml` — visible description
2. `ConnectionViewModel.Availability.cs` — required table and message
3. `ConnectionViewModel.ModificationActions.cs` — request table name
4. `SqlServerModificationService.cs` — allowlist
5. Documentation

Because Gain uses the generic status operation, its SQL table is derived from the allowlist.

### 22.2 Rename `dbo.zakan`

Use the same locations as Gain, replacing the Zakan equivalents.

### 22.3 Rename `dbo.amplitude`

Check and update:

1. `AmplitudeModificationSectionView.xaml`
2. `ConnectionViewModel.Availability.cs`
3. `SqlServerModificationService.Apply.cs`
4. `SqlServerModificationService.Preview.cs`
5. Apply success table list
6. Documentation

### 22.4 Rename `dbo.overall_status`

Check and update:

1. `OverallModificationSectionView.xaml`
2. `ConnectionViewModel.Availability.cs`
3. `SqlServerModificationService.Apply.cs`
4. `SqlServerModificationService.Preview.cs`
5. Apply success table list
6. Documentation

### 22.5 Rename `dbo.type_a` or `dbo.type_b`

Check and update:

1. `OverallModificationSectionView.xaml`
2. `ConnectionViewModel.Availability.cs`
3. `SqlServerLookupService.cs`
4. `SqlServerModificationService.Schema.cs`
5. `SqlServerModificationService.Preview.cs`
6. Documentation

If column names change, also update the schema checks and SQL projections.

---

## 23. Model map

| Model | Purpose |
|---|---|
| `ConnectionResult` | Connection success/message/database list |
| `TypeBLookupItem` | One `type_b.low_id/id_text` pair |
| `TypeBLookupResult` | Lookup outcome, options, and available tables |
| `IdSelectionResult` | Parsed IDs or validation error |
| `OverallModification` | Complete Overall request |
| `AmplitudeModification` | Complete Amplitude request |
| `TableStatusModification` | Gain or Zakan request |
| `ModificationResult` | Apply outcome |
| `ModificationPreviewResult` | Preview outcome |
| `ModificationPreviewSummary` | Per-table counts |
| `ModificationPreviewRow` | One displayed preview row |
| `ThemeOption` | Picker-friendly theme value and label |
| `OverallStatusOption` | Picker-friendly status value and label |

A “picker-friendly” option overrides `ToString()` so MAUI displays the label rather than the record type name.

---

## 24. Common modification recipes

### 24.1 Change a label

1. Identify the visible section.
2. Open its `Views/*.xaml` file.
3. Find `Text="..."`.
4. Change the string.
5. Build.

### 24.2 Change a placeholder example

Find `Placeholder="..."` in the relevant XAML.

Changing a placeholder does not change validation or SQL behavior.

### 24.3 Change a color

Open `Colors.xaml` and change the semantic color.

If only one kind of control should change, modify its style instead.

### 24.4 Change font size

- General labels → `TypographyStyles.xaml`
- Entries and pickers → `FieldStyles.xaml`
- Buttons → `ButtonStyles.xaml`
- Preview cells/cards → `ContainerStyles.xaml`

### 24.5 Change a default status

- Overall default: constructor in `ConnectionViewModel.cs` selects `OverallStatusOptions[2]`.
- Gain: field `_gainStatus` in `ConnectionViewModel.cs`.
- Amplitude: field `_amplitudeStatus`.
- Zakan: field `_zakanStatus`.

### 24.6 Change the preview row limit

Change:

~~~csharp
private const int PreviewRowsPerTable = 100;
~~~

in `SqlServerModificationService.cs`.

This changes displayed detail count, not the exact total calculation.

### 24.7 Change the Overall maximum write count

Change `maximumRowsPerApply` in `SqlServerModificationService.Apply.cs`.

Also change the matching 100,000 validation in `SqlServerModificationService.Preview.cs` so Preview and Apply agree.

### 24.8 Add a new UI field to an existing section

1. Add the control and binding in the section XAML.
2. Add backing field and public property in the appropriate view-model partial.
3. Call `RefreshApplyCommand` when it changes.
4. Add it to the section’s signature in `ChangeTracking.cs`.
5. Add it to the request model.
6. Populate it in `TryBuildPendingRequest`.
7. Use it in Preview SQL.
8. Use it in Apply SQL.
9. Update this guide and tests.

### 24.9 Add a new modification section

Follow the checklist in [ARCHITECTURE.md](ARCHITECTURE.md). The key rule is to update UI, availability, change tracking, request construction, preview, and apply together.

---

## 25. Build and run

### 25.1 macOS

~~~bash
dotnet restore MauiDbManager/MauiDbManager.csproj
dotnet build MauiDbManager/MauiDbManager.csproj -f net10.0-maccatalyst
dotnet run --project MauiDbManager/MauiDbManager.csproj -f net10.0-maccatalyst
~~~

### 25.2 Windows

Run on Windows:

~~~powershell
dotnet restore MauiDbManager/MauiDbManager.csproj
dotnet build MauiDbManager/MauiDbManager.csproj -f net10.0-windows10.0.19041.0
dotnet run --project MauiDbManager/MauiDbManager.csproj -f net10.0-windows10.0.19041.0
~~~

### 25.3 Useful searches

Find bindings:

~~~bash
rg -n "\{Binding " MauiDbManager/Views
~~~

Find a property:

~~~bash
rg -n "AmplitudeLowIdSelector" MauiDbManager
~~~

Find SQL:

~~~bash
rg -n "SELECT|INSERT|UPDATE|OPENJSON|BEGIN" MauiDbManager/Services
~~~

Find table names:

~~~bash
rg -n "dbo\." MauiDbManager
~~~

---

## 26. Debugging guide

### 26.1 UI text changes but C# property does not

Check:

- Binding name spelling
- Property setter
- `INotifyPropertyChanged`
- Whether the control is disabled
- Whether an overlay is intercepting input

### 26.2 Button stays disabled

Start in `ConnectionViewModel.ChangeTracking.cs` and inspect `CanApply`.

Then inspect the relevant section signature and availability property.

### 26.3 Section checkbox is unavailable

Check:

- Inline missing-table message
- `ConnectionViewModel.Availability.cs`
- `SqlServerLookupService.cs`
- SQL permissions to see `sys.tables`

### 26.4 Preview and Apply differ

Both should use `TryBuildPendingRequest`. If you add a field, ensure both service paths receive the same request model.

For current-time values, a small time difference is expected.

### 26.5 SQL error after Preview succeeds

The database can change between Preview and Apply. Apply deliberately recalculates and runs in its own transaction.

Also compare schema validation in Preview and Apply if you recently changed a table.

### 26.6 App becomes non-interactive

Inspect full-screen overlay views first. A transparent view with a high `ZIndex` can intercept input. `PreviewDialogView` must hide its outer `ContentView`, not only an inner child.

---

## 27. Security and production notes

1. The password is hidden in the UI and is not intentionally persisted.
2. Connection strings are built with `SqlConnectionStringBuilder`.
3. SQL values use parameters.
4. Dynamic table names are allowlisted.
5. Dynamic identifiers are quoted.
6. Apply uses a transaction.
7. Preview is read-only against target tables.
8. Encryption is always requested.
9. `TrustServerCertificate=true` is useful for the current private/container development environment but is not ideal for production.
10. In production, install a certificate trusted by the Mac and Windows clients, then disable Trust Server Certificate.
11. The `sa` login is powerful. A least-privilege application login is safer for production.
12. Grant only the database permissions the application actually needs.

---

## 28. Suggested learning path through this codebase

Read the project in this order:

1. `MainPage.xaml` — understand composition.
2. `ConnectionSectionView.xaml` — see basic controls and bindings.
3. `MainPage.xaml.cs` — see BindingContext assignment.
4. `ConnectionViewModel.cs` — see fields, constructor, commands, collections.
5. `ConnectionViewModel.Connection.cs` — follow Connect end to end.
6. `SqlServerConnectionService.cs` — see the first SQL query.
7. `DatabaseSelectionSectionView.xaml` and `SqlServerLookupService.cs`.
8. `LowIdSelectorParser.cs` — practice C# validation and collections.
9. `ConnectionViewModel.ChangeTracking.cs` — understand command enablement.
10. `ConnectionViewModel.ModificationActions.cs` — understand request construction.
11. The request records under `Models/`.
12. `SqlServerModificationService.Preview.cs`.
13. `SqlServerModificationService.Apply.cs`.
14. Theme and style resource files.

A useful exercise is to choose one field, such as Amplitude status, and trace it:

~~~text
AmplitudeStatusPicker
    ↓ SelectedItem binding
ConnectionViewModel.AmplitudeStatus
    ↓ request construction
AmplitudeModification.Status
    ↓ preview service
Proposed value display
    ↓ apply service
@status SQL parameter
    ↓
dbo.amplitude.status
~~~

That trace is the essence of the application architecture.

---

## 29. Final mental model

Think of the program as five layers:

~~~text
1. XAML Views
   What the user sees and interacts with

2. ConnectionViewModel partial files
   Screen state, validation, commands, and orchestration

3. Models
   Plain typed data passed between layers

4. Services
   SQL connection, lookup, preview, and apply behavior

5. SQL Server
   Actual database metadata and rows
~~~

Data normally flows downward:

~~~text
User → XAML → ViewModel → Model → Service → SQL Server
~~~

Results flow upward:

~~~text
SQL Server → Service result → ViewModel property/collection → XAML
~~~

If you know which layer owns a concern, finding the correct file becomes much easier:

- Appearance problem → View or Styles
- Button enablement problem → ChangeTracking
- Input validation problem → ViewModel or Parser
- Table availability problem → Lookup + Availability
- Preview mismatch → Preview service
- Incorrect write → Apply service
- Wrong data shape → Model

This separation is the main design idea behind the finished application.
