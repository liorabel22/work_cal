using MauiDbManager.Models;

namespace MauiDbManager.ViewModels;

public sealed partial class ConnectionViewModel
{
    public bool IsOverallModificationEnabled
    {
        get => _isOverallModificationEnabled;
        set
        {
            if (!SetProperty(ref _isOverallModificationEnabled, value))
                return;

            NotifyOverallEnabledProperties();
            RefreshApplyCommand();
        }
    }

    public string OverallMidIdSelector
    {
        get => _overallMidIdSelector;
        set
        {
            if (SetProperty(ref _overallMidIdSelector, value))
                RefreshApplyCommand();
        }
    }

    public string OverallHighIdSelector
    {
        get => _overallHighIdSelector;
        set
        {
            if (SetProperty(ref _overallHighIdSelector, value))
                RefreshApplyCommand();
        }
    }

    public bool UpdateOverallStatus
    {
        get => _updateOverallStatus;
        set
        {
            if (!SetProperty(ref _updateOverallStatus, value))
                return;

            OnPropertyChanged(nameof(IsOverallStatusEnabled));
            RefreshApplyCommand();
        }
    }

    public OverallStatusOption SelectedOverallStatus
    {
        get => _selectedOverallStatus;
        set
        {
            if (SetProperty(ref _selectedOverallStatus, value))
                RefreshApplyCommand();
        }
    }

    public bool UpdateOverallTime
    {
        get => _updateOverallTime;
        set
        {
            if (!SetProperty(ref _updateOverallTime, value))
                return;

            OnPropertyChanged(nameof(IsOverallTimeEnabled));
            RefreshApplyCommand();
        }
    }

    public DateTime OverallDate
    {
        get => _overallDate;
        set
        {
            if (!SetProperty(ref _overallDate, value))
                return;

            _overallDateTimeEdited = true;
            RefreshApplyCommand();
        }
    }

    public TimeSpan OverallTime
    {
        get => _overallTime;
        set
        {
            if (!SetProperty(ref _overallTime, value))
                return;

            _overallDateTimeEdited = true;
            RefreshApplyCommand();
        }
    }

    public bool IsOverallStatusEnabled => IsOverallModificationEnabled && UpdateOverallStatus;
    public bool IsOverallTimeEnabled => IsOverallModificationEnabled && UpdateOverallTime;

    public bool IsTypeBLoading
    {
        get => _isTypeBLoading;
        private set
        {
            if (!SetProperty(ref _isTypeBLoading, value))
                return;

            RefreshApplyCommand();
        }
    }

    public string TypeBLookupMessage
    {
        get => _typeBLookupMessage;
        private set
        {
            if (!SetProperty(ref _typeBLookupMessage, value))
                return;

            OnPropertyChanged(nameof(HasTypeBLookupMessage));
        }
    }

    public bool HasTypeBLookupMessage => !string.IsNullOrWhiteSpace(TypeBLookupMessage);

    private async Task LoadTypeBOptionsAsync(string databaseName)
    {
        IsTypeBLoading = true;
        TypeBLookupMessage = string.Empty;

        try
        {
            var result = await _lookupService.LoadTypeBOptionsAsync(
                ServerName.Trim(), databaseName, UserName.Trim(), Password, TrustServerCertificate);

            if (!string.Equals(SelectedDatabase, databaseName, StringComparison.Ordinal))
                return;

            ClearTypeBOptions();
            SetSectionAvailability(result.AvailableTables, result.IsSuccess, result.Message);
            if (!result.IsSuccess)
            {
                TypeBLookupMessage = result.Message;
                return;
            }

            TypeBLookupMessage = result.Message;

            _isUpdatingTypeBOptions = true;
            if (IsOverallSectionAvailable)
            {
                var all = new SelectableTypeBOption("All", null, isAll: true) { IsSelected = true };
                TypeBOptions.Add(all);
                foreach (var item in result.Items)
                {
                    TypeBOptions.Add(new(item.IdText, item.LowId)
                    {
                        IsEnabled = false
                    });
                }

                foreach (var option in TypeBOptions)
                    option.SelectionChanged += OnTypeBSelectionChanged;
            }
            _isUpdatingTypeBOptions = false;
        }
        finally
        {
            _isUpdatingTypeBOptions = false;
            IsTypeBLoading = false;
            RefreshApplyCommand();
        }
    }

    private void OnTypeBSelectionChanged(object? sender, EventArgs e)
    {
        if (_isUpdatingTypeBOptions || sender is not SelectableTypeBOption changed)
            return;

        _isUpdatingTypeBOptions = true;
        try
        {
            if (changed.IsAll)
            {
                foreach (var option in TypeBOptions.Where(option => !option.IsAll))
                {
                    option.IsSelected = false;
                    option.IsEnabled = !changed.IsSelected;
                }
            }
        }
        finally
        {
            _isUpdatingTypeBOptions = false;
            RefreshApplyCommand();
        }
    }

    private void NotifyOverallEnabledProperties()
    {
        OnPropertyChanged(nameof(IsOverallStatusEnabled));
        OnPropertyChanged(nameof(IsOverallTimeEnabled));
    }
}
