# MAUI DB Manager

A small cross-platform .NET MAUI desktop app for testing a SQL Server Authentication connection on macOS and Windows.

## What is implemented

- Server name, SQL user name, and hidden password inputs
- Read-only `SQL Server Authentication` connection method
- Input validation, busy indicator, and disabled buttons while connecting
- Success or failure feedback with a useful reason
- Reset button that clears credentials and status
- Encrypted SQL connection with certificate validation enabled
- Compact, unnumbered workflow cards with checkbox-only enable controls for modification sections
- System-aware light and dark themes, plus an in-app appearance selector
- Collapsed database selector populated with the online databases accessible to the login
- Overall Modification (section 3) derived from `type_a` and `type_b`
- Gain Modification (section 4) and Zakan Modification (section 6)
- Amplitude Modification (section 5) with reusable low-ID and time fields
- One bottom Apply action for all enabled modification sections
- Read-only modification preview with Insert/Update classification and before/after values
- Apply is enabled only for sections with unapplied user changes
- Required-table checks disable unavailable sections and explain missing tables in a tooltip

## Project structure

The app uses MVVM with files divided by responsibility:

- `MainPage.xaml` is a short composition file.
- `Views/` contains one XAML `ContentView` for each visible section, the Apply bar, and the preview dialog.
- `ConnectionViewModel.*.cs` partial files separate connection, availability, section state, actions, and change tracking.
- `SqlServerModificationService.*.cs` separates transactional Apply SQL, read-only Preview SQL, and shared schema helpers.
- `Resources/Styles/` separates colors, typography, fields, containers, and buttons.
- `MauiProgram.cs` connects services, the view model, and the page using dependency injection.

For a detailed, beginner-friendly explanation of the entire application—including MAUI, XAML, C#, MVVM, file locations, editable UI text, table-name locations, and every SQL preview/apply path—read [`MAUI_DB_MANAGER_COMPLETE_REPORT.md`](MAUI_DB_MANAGER_COMPLETE_REPORT.md).

See [`ARCHITECTURE.md`](ARCHITECTURE.md) for a shorter file map and quick guidance on where to make each kind of change.

Data typed into an `Entry` updates a view-model property through `{Binding ...}`. A button executes a view-model command. The view model asks the database service to connect, then changes its status properties; `INotifyPropertyChanged` tells the UI to redraw them.

After a successful connection, the service queries `sys.databases` and returns only online databases for which the login has access. Selecting one sets `SelectedDatabase` in the view model; future sections should use this value for their database operations.

The database selector normally shows only the active database. Clicking it expands a list approximately seven rows high. The SQL user name defaults to `sa`.

The screen is progressive: only connection section 1 is initially visible. Section 2 appears after a successful server connection. Sections 3–6 and the bottom Apply bar appear only after a working database is selected.

When a database is selected, the app inspects its `dbo` tables before allowing modification sections to be enabled. Overall Modification requires `dbo.overall_status`, `dbo.type_a`, and `dbo.type_b`; the other sections require their matching `dbo.gain`, `dbo.amplitude`, or `dbo.zakan` table. A section with missing requirements has a disabled enable checkbox, an inline explanation listing the missing table names, and the same explanation as a tooltip.

The app keeps a separate applied-state snapshot for each modification section. Apply is enabled only when an enabled section differs from its last successfully applied state. Only changed sections are sent to the modification service, and a successful Apply updates their snapshots so the button becomes disabled again until the user makes another relevant edit. Switching databases disables all modification sections and starts a fresh snapshot.

`Preview changes` uses the same pending-change request as Apply, but performs read-only queries against the selected database. Its dialog shows exact insert/update totals per table and displays the table, action, row key, current values, and proposed values for up to 100 rows per table. Overall and Amplitude ID lists are passed to SQL Server as JSON arrays so totals can be calculated as set-based queries. Preview never changes a target table. Values configured as "current time" are recalculated when Apply actually runs, and the dialog calls this out explicitly.

The interface follows the operating system theme by default. The Appearance selector can override it with Light or Dark for the current app session. Every field, picker, card, status message, and disabled state uses paired theme colors so text remains readable in either mode. Date and time controls use a dedicated high-contrast surface, and disabled fields remain visible while clearly muted. The visible cards are intentionally unnumbered; modification cards use the leading checkbox itself as their enable control.

Section 3 targets `dbo.overall_status`. Mid-ID and high-ID fields use the shared ID-selector syntax. Status is an enum: `0 - None`, `1 - In Progress`, `2 - Pass`, and `3 - Fail`; Pass is the default. Status and time can be updated independently, but at least one must be selected.

The TB text list is loaded from `dbo.type_b(id_text, low_id)` and expands to show every option without its own scroll area. `All` is selected by default; individual choices remain visible but their complete rows are grayed out and disabled until All is cleared. All first takes every `dbo.type_b.low_id`, then retains only `dbo.type_a(low_id, high_id)` rows whose `low_id` exists in that set. Individual text selections use only their corresponding `type_b.low_id` values and apply the same `type_a.low_id` filter. The final row set is the cross product of selected mid IDs, selected high IDs, and those filtered `(tb, ta)` pairs.

The first four `overall_status` columns—`mid_id`, `high_id`, `tb`, and `ta`—identify an existing row. Existing rows update only the selected output columns. New rows receive all six values; an unselected status defaults to Pass and an unselected time defaults to the current time.

Sections 4 and 6 are disabled by default. Enabling a section activates its shared `BooleanStatus` (`False`/`True`) selector. Apply performs all enabled changes in one SQL transaction:

- `dbo.gain` for section 4
- `dbo.zakan` for section 6

For each table, the service inspects the schema, treats its first two writable non-`status` columns as the key, and targets the row whose key is `(0, 0)`. It updates the `status` column when that row exists or inserts `(0, 0, status)` otherwise. A missing or incompatible table produces an error, and the transaction is rolled back.

Section 5 targets `dbo.amplitude`. Its low-ID selector accepts single values (`1`), lists (`1, 4`), ranges (`1-3`), parenthesized lists, and mixtures (`1, 4, 7-10`). Duplicate values are removed. Independent `Update status` and `Update time` choices determine which columns change on existing rows; both are selected by default. At least one must remain selected.

For a missing low ID, SQL must insert a complete row. It inserts `(low_id, 0, status, time)`, using `False` when status is not selected and the selected/current time when time is not selected. The table must begin with `low_id` and contain `status`, `time`, and one additional writable value column.

Sections 3 and 5 use date and time pickers. They initially display the current local date and time. If unchanged, Apply calculates the current instant at that moment; if changed, it interprets the chosen value in the computer's local timezone. SQL receives Unix epoch milliseconds.

## Prerequisites

- .NET 10 SDK
- .NET MAUI workload
- macOS: Xcode (for Mac Catalyst)
- Windows: Visual Studio 2026 or the corresponding .NET MAUI/Windows App SDK tooling

On this Mac, the SDK is installed system-wide, so workload installation requires an administrator:

```bash
sudo dotnet workload install maui
```

## Build and run

macOS:

```bash
dotnet restore MauiDbManager/MauiDbManager.csproj
dotnet build MauiDbManager/MauiDbManager.csproj -f net10.0-maccatalyst
dotnet run --project MauiDbManager/MauiDbManager.csproj -f net10.0-maccatalyst
```

Windows (run on Windows):

```powershell
dotnet restore MauiDbManager/MauiDbManager.csproj
dotnet build MauiDbManager/MauiDbManager.csproj -f net10.0-windows10.0.19041.0
dotnet run --project MauiDbManager/MauiDbManager.csproj -f net10.0-windows10.0.19041.0
```

The server field accepts SQL Server data-source formats such as `db.example.com`, `db.example.com,1433`, or `server\\instance`. All modifications target the explicitly selected working database.

## Security notes

- The password is hidden and kept only in memory; the app never saves it.
- The connection always uses encryption. The certificate-trust switch is enabled for the current container-based development environment.
- Disable that switch in production and install a certificate trusted by the Mac and Windows machines.
