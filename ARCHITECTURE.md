# MAUI DB Manager architecture

This project uses MVVM. XAML files define presentation, the view model exposes UI state and commands, and services contain SQL Server behavior. Files are split by responsibility even when they form one logical class through C# `partial` classes.

## Screen composition

`MainPage.xaml` is intentionally small. It only arranges these views:

- `WorkspaceHeaderView` — title and theme selector
- `ConnectionSectionView` — server credentials and Connect/Reset
- `DatabaseSelectionSectionView` — working database selector
- `OverallModificationSectionView` — `dbo.overall_status` inputs and TB selection
- `GainModificationSectionView` — `dbo.gain`
- `AmplitudeModificationSectionView` — `dbo.amplitude`
- `ZakanModificationSectionView` — `dbo.zakan`
- `ApplyBarView` — Preview and Apply actions
- `PreviewDialogView` — read-only insert/update preview

Every view inherits the `ConnectionViewModel` binding context from `MainPage`. Its `.xaml.cs` file only calls `InitializeComponent`; business logic does not belong in view code-behind.

## View-model files

`ConnectionViewModel.cs` contains shared fields, dependency injection, collections, and command construction. The other partial files contain focused behavior:

- `.Connection.cs` — credentials, connecting, database selection, and reset
- `.Theme.cs` — system/light/dark selection
- `.Availability.cs` — required-table checks and missing-table messages
- `.Overall.cs` — Overall inputs and `type_b` option behavior
- `.TableSections.cs` — Gain, Amplitude, and Zakan input state
- `.OperationStatus.cs` — busy, connection, apply, and preview messages
- `.ModificationActions.cs` — builds one request used by Preview and Apply
- `.ChangeTracking.cs` — per-section pending-change snapshots and command availability

These files compile into one `ConnectionViewModel` class. A XAML binding therefore does not need to know which partial file owns a property.

## SQL services

- `SqlServerConnectionService.cs` — tests the server and lists accessible databases
- `SqlServerLookupService.cs` — inspects required tables and loads `dbo.type_b`
- `SqlServerModificationService.cs` — shared service identity and allowed-table configuration
- `SqlServerModificationService.Apply.cs` — transactional INSERT/UPDATE operations
- `SqlServerModificationService.Preview.cs` — read-only classification and before/after queries
- `SqlServerModificationService.Schema.cs` — safe identifier quoting and schema helpers

Preview and Apply accept the same modification models, which keeps parsing and user-selection behavior consistent. Apply writes all pending sections in one transaction. Preview never calls the apply methods.

## Styling

- `Colors.xaml` — semantic light/dark color palette
- `TypographyStyles.xaml` — labels and text hierarchy
- `FieldStyles.xaml` — entries, pickers, selectors, and field borders
- `ContainerStyles.xaml` — cards, apply bar, and preview table containers
- `ButtonStyles.xaml` — primary, secondary, and disabled buttons

The dictionaries are merged in `App.xaml`. Prefer changing a shared style instead of setting colors or font sizes directly in several views.

## Where to make common changes

- Change one screen section: edit its file under `Views/`.
- Change whether Apply is enabled: edit `ConnectionViewModel.ChangeTracking.cs`.
- Change request validation or input-to-model mapping: edit `ConnectionViewModel.ModificationActions.cs`.
- Change an SQL write: edit `SqlServerModificationService.Apply.cs`.
- Change preview output: edit `SqlServerModificationService.Preview.cs` and `PreviewDialogView.xaml`.
- Add a required table: update `ConnectionViewModel.Availability.cs` and the lookup service.
- Change global colors or typography: edit the matching resource dictionary.

## Adding another modification section

1. Add its request model under `Models/`.
2. Create a dedicated `ContentView` under `Views/` and add it to `MainPage.xaml`.
3. Add its UI state in a focused view-model partial file.
4. Add availability and change-signature rules.
5. Add request construction in `ConnectionViewModel.ModificationActions.cs`.
6. Add preview SQL and transactional apply SQL in their respective service partial files.
7. Build on macOS and Windows.
