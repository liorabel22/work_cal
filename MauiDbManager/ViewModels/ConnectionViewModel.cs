using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using MauiDbManager.Commands;
using MauiDbManager.Models;
using MauiDbManager.Parsing;
using MauiDbManager.Services;

namespace MauiDbManager.ViewModels;

public sealed partial class ConnectionViewModel : INotifyPropertyChanged
{
    private readonly IDatabaseConnectionService _connectionService;
    private readonly IDatabaseLookupService _lookupService;
    private readonly IDatabaseModificationService _modificationService;

    private string _serverName = string.Empty;
    private string _userName = "sa";
    private string _password = string.Empty;
    private bool _trustServerCertificate = true;
    private string? _selectedDatabase;
    private bool _isDatabaseListOpen;
    private bool _isBusy;
    private string _statusMessage = string.Empty;
    private bool _connectionSucceeded;

    private bool _isOverallModificationEnabled;
    private string _overallMidIdSelector = string.Empty;
    private string _overallHighIdSelector = string.Empty;
    private bool _updateOverallStatus = true;
    private OverallStatusOption _selectedOverallStatus;
    private bool _updateOverallTime = true;
    private DateTime _overallDate;
    private TimeSpan _overallTime;
    private bool _overallDateTimeEdited;
    private bool _isTypeBLoading;
    private string _typeBLookupMessage = string.Empty;
    private bool _isUpdatingTypeBOptions;
    private bool _isOverallSectionAvailable;
    private bool _isGainSectionAvailable;
    private bool _isAmplitudeSectionAvailable;
    private bool _isZakanSectionAvailable;
    private string _overallAvailabilityMessage = "Select a database to check required tables.";
    private string _gainAvailabilityMessage = "Select a database to check dbo.gain.";
    private string _amplitudeAvailabilityMessage = "Select a database to check dbo.amplitude.";
    private string _zakanAvailabilityMessage = "Select a database to check dbo.zakan.";

    private bool _isGainModificationEnabled;
    private BooleanStatus _gainStatus = BooleanStatus.False;

    private bool _isAmplitudeModificationEnabled;
    private string _amplitudeLowIdSelector = string.Empty;
    private bool _updateAmplitudeStatus = true;
    private BooleanStatus _amplitudeStatus = BooleanStatus.False;
    private bool _updateAmplitudeTime = true;
    private DateTime _amplitudeDate;
    private TimeSpan _amplitudeTime;
    private bool _amplitudeDateTimeEdited;

    private bool _isZakanModificationEnabled;
    private BooleanStatus _zakanStatus = BooleanStatus.False;
    private string _applyMessage = string.Empty;
    private bool _applySucceeded;
    private ThemeOption _selectedTheme;
    private string _overallBaseline = string.Empty;
    private string _gainBaseline = string.Empty;
    private string _amplitudeBaseline = string.Empty;
    private string _zakanBaseline = string.Empty;
    private bool _isPreviewVisible;
    private string _previewMessage = string.Empty;
    private bool _previewSucceeded;

    public ConnectionViewModel(
        IDatabaseConnectionService connectionService,
        IDatabaseLookupService lookupService,
        IDatabaseModificationService modificationService)
    {
        _connectionService = connectionService;
        _lookupService = lookupService;
        _modificationService = modificationService;

        OverallStatusOptions =
        [
            new(OverallStatus.None, "0 - None"),
            new(OverallStatus.InProgress, "1 - In Progress"),
            new(OverallStatus.Pass, "2 - Pass"),
            new(OverallStatus.Fail, "3 - Fail")
        ];
        _selectedOverallStatus = OverallStatusOptions[2];
        ThemeOptions =
        [
            new(ThemePreference.System, "System theme"),
            new(ThemePreference.Light, "Light"),
            new(ThemePreference.Dark, "Dark")
        ];
        _selectedTheme = ThemeOptions[0];

        var now = DateTime.Now;
        _overallDate = now.Date;
        _overallTime = new(now.Hour, now.Minute, now.Second);
        _amplitudeDate = now.Date;
        _amplitudeTime = new(now.Hour, now.Minute, now.Second);

        ConnectCommand = new AsyncRelayCommand(ConnectAsync, () => !IsBusy);
        PreviewCommand = new AsyncRelayCommand(PreviewAsync, CanApply);
        ApplyCommand = new AsyncRelayCommand(ApplyAsync, CanApply);
        ClosePreviewCommand = new Command(() => IsPreviewVisible = false);
        ResetCommand = new Command(Reset, () => !IsBusy);
        ToggleDatabaseListCommand = new Command(
            () => IsDatabaseListOpen = !IsDatabaseListOpen,
            () => IsConnected && Databases.Count > 0);
        MarkAllSectionsClean();

        if (Application.Current is not null)
            Application.Current.RequestedThemeChanged += OnRequestedThemeChanged;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<string> Databases { get; } = [];
    public ObservableCollection<SelectableTypeBOption> TypeBOptions { get; } = [];
    public ObservableCollection<ModificationPreviewSummary> PreviewSummaries { get; } = [];
    public ObservableCollection<ModificationPreviewRow> PreviewRows { get; } = [];
    public IReadOnlyList<BooleanStatus> StatusOptions { get; } = Enum.GetValues<BooleanStatus>();
    public IReadOnlyList<OverallStatusOption> OverallStatusOptions { get; }
    public IReadOnlyList<ThemeOption> ThemeOptions { get; }
    public ICommand ConnectCommand { get; }
    public ICommand PreviewCommand { get; }
    public ICommand ApplyCommand { get; }
    public ICommand ClosePreviewCommand { get; }
    public ICommand ResetCommand { get; }
    public ICommand ToggleDatabaseListCommand { get; }


    private bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return false;

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
