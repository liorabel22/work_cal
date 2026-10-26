using MauiDbManager.Commands;

namespace MauiDbManager.ViewModels;

public sealed partial class ConnectionViewModel
{
    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (!SetProperty(ref _isBusy, value))
                return;

            ((AsyncRelayCommand)ConnectCommand).RaiseCanExecuteChanged();
            RefreshApplyCommand();
            ((Command)ResetCommand).ChangeCanExecute();
        }
    }

    public bool HasStatus => !string.IsNullOrWhiteSpace(StatusMessage);
    public string StatusSymbol => IsConnected ? "✓" : "!";
    public Color StatusColor => IsDarkTheme
        ? IsConnected ? Color.FromArgb("#86EFAC") : Color.FromArgb("#FCA5A5")
        : IsConnected ? Color.FromArgb("#166534") : Color.FromArgb("#B91C1C");
    public Color StatusBackgroundColor => IsDarkTheme
        ? IsConnected ? Color.FromArgb("#1622C55E") : Color.FromArgb("#16EF4444")
        : IsConnected ? Color.FromArgb("#F0FDF4") : Color.FromArgb("#FEF2F2");

    public string StatusMessage
    {
        get => _statusMessage;
        private set
        {
            if (!SetProperty(ref _statusMessage, value))
                return;

            OnPropertyChanged(nameof(HasStatus));
            OnPropertyChanged(nameof(StatusSymbol));
            OnPropertyChanged(nameof(StatusColor));
            OnPropertyChanged(nameof(StatusBackgroundColor));
        }
    }

    public bool HasApplyStatus => !string.IsNullOrWhiteSpace(ApplyMessage);
    public Color ApplyStatusColor => IsDarkTheme
        ? _applySucceeded ? Color.FromArgb("#86EFAC") : Color.FromArgb("#FCA5A5")
        : _applySucceeded ? Color.FromArgb("#166534") : Color.FromArgb("#B91C1C");

    public string ApplyMessage
    {
        get => _applyMessage;
        private set
        {
            if (!SetProperty(ref _applyMessage, value))
                return;

            OnPropertyChanged(nameof(HasApplyStatus));
            OnPropertyChanged(nameof(ApplyStatusColor));
        }
    }

    public bool IsPreviewVisible
    {
        get => _isPreviewVisible;
        private set => SetProperty(ref _isPreviewVisible, value);
    }

    public string PreviewMessage
    {
        get => _previewMessage;
        private set
        {
            if (!SetProperty(ref _previewMessage, value))
                return;

            OnPropertyChanged(nameof(PreviewMessageColor));
        }
    }

    public Color PreviewMessageColor => IsDarkTheme
        ? _previewSucceeded ? Color.FromArgb("#86EFAC") : Color.FromArgb("#FCA5A5")
        : _previewSucceeded ? Color.FromArgb("#166534") : Color.FromArgb("#B91C1C");
}
