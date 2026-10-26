namespace MauiDbManager.ViewModels;

public sealed partial class ConnectionViewModel
{
    public string ServerName
    {
        get => _serverName;
        set => SetProperty(ref _serverName, value);
    }

    public string UserName
    {
        get => _userName;
        set => SetProperty(ref _userName, value);
    }

    public string Password
    {
        get => _password;
        set => SetProperty(ref _password, value);
    }

    public bool TrustServerCertificate
    {
        get => _trustServerCertificate;
        set => SetProperty(ref _trustServerCertificate, value);
    }

    public string ConnectionMethod => "SQL Server Authentication";

    public string? SelectedDatabase
    {
        get => _selectedDatabase;
        set
        {
            if (!SetProperty(ref _selectedDatabase, value))
                return;

            OnPropertyChanged(nameof(HasSelectedDatabase));
            OnPropertyChanged(nameof(DatabaseSelectorText));
            NotifySectionAvailabilityProperties();
            IsDatabaseListOpen = false;
            ClearTypeBOptions();
            DisableModificationSections();
            ResetSectionAvailability();
            MarkAllSectionsClean();

            if (IsConnected && !string.IsNullOrWhiteSpace(value))
                _ = LoadTypeBOptionsAsync(value);

            RefreshApplyCommand();
        }
    }

    public bool IsConnected
    {
        get => _connectionSucceeded;
        private set
        {
            if (!SetProperty(ref _connectionSucceeded, value))
                return;

            OnPropertyChanged(nameof(StatusSymbol));
            OnPropertyChanged(nameof(StatusColor));
            OnPropertyChanged(nameof(StatusBackgroundColor));
            OnPropertyChanged(nameof(DatabaseHelpText));
            ((Command)ToggleDatabaseListCommand).ChangeCanExecute();
            if (!value)
                IsDatabaseListOpen = false;
            RefreshApplyCommand();
        }
    }

    public bool IsDatabaseListOpen
    {
        get => _isDatabaseListOpen;
        private set => SetProperty(ref _isDatabaseListOpen, value);
    }

    public string DatabaseSelectorText => SelectedDatabase ?? "Select a database";
    public bool HasSelectedDatabase => !string.IsNullOrWhiteSpace(SelectedDatabase);
    public string DatabaseHelpText => IsConnected
        ? "Choose the database used by the remaining sections."
        : "Connect to SQL Server first to load accessible databases.";

    private async Task ConnectAsync()
    {
        var validationMessage = ValidateInputs();
        if (validationMessage is not null)
        {
            ClearDatabaseSelection();
            IsConnected = false;
            StatusMessage = validationMessage;
            return;
        }

        IsBusy = true;
        ClearDatabaseSelection();
        IsConnected = false;
        StatusMessage = string.Empty;
        ApplyMessage = string.Empty;

        try
        {
            var result = await _connectionService.TestConnectionAsync(
                ServerName.Trim(), UserName.Trim(), Password, TrustServerCertificate);

            foreach (var database in result.Databases)
                Databases.Add(database);

            IsConnected = result.IsSuccess;
            StatusMessage = result.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }


    private string? ValidateInputs()
    {
        if (string.IsNullOrWhiteSpace(ServerName))
            return "Enter a server name.";
        if (string.IsNullOrWhiteSpace(UserName))
            return "Enter a database user name.";
        if (string.IsNullOrEmpty(Password))
            return "Enter a database password.";
        return null;
    }

    private void Reset()
    {
        ServerName = string.Empty;
        UserName = "sa";
        Password = string.Empty;
        ClearDatabaseSelection();
        IsConnected = false;
        StatusMessage = string.Empty;
        ApplyMessage = string.Empty;
        IsPreviewVisible = false;
        PreviewRows.Clear();
        PreviewSummaries.Clear();
        PreviewMessage = string.Empty;
    }

    private void ClearDatabaseSelection()
    {
        IsPreviewVisible = false;
        PreviewRows.Clear();
        PreviewSummaries.Clear();
        PreviewMessage = string.Empty;
        SelectedDatabase = null;
        Databases.Clear();
        ClearTypeBOptions();
    }

    private void ClearTypeBOptions()
    {
        foreach (var option in TypeBOptions)
            option.SelectionChanged -= OnTypeBSelectionChanged;
        TypeBOptions.Clear();
        TypeBLookupMessage = string.Empty;
        RefreshApplyCommand();
    }

}
