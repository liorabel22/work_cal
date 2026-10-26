using MauiDbManager.Models;

namespace MauiDbManager.ViewModels;

public sealed partial class ConnectionViewModel
{
    public bool IsGainModificationEnabled
    {
        get => _isGainModificationEnabled;
        set
        {
            if (SetProperty(ref _isGainModificationEnabled, value))
                RefreshApplyCommand();
        }
    }

    public BooleanStatus GainStatus
    {
        get => _gainStatus;
        set
        {
            if (SetProperty(ref _gainStatus, value))
                RefreshApplyCommand();
        }
    }

    public bool IsAmplitudeModificationEnabled
    {
        get => _isAmplitudeModificationEnabled;
        set
        {
            if (!SetProperty(ref _isAmplitudeModificationEnabled, value))
                return;

            OnPropertyChanged(nameof(IsAmplitudeStatusEnabled));
            OnPropertyChanged(nameof(IsAmplitudeTimeEnabled));
            RefreshApplyCommand();
        }
    }

    public string AmplitudeLowIdSelector
    {
        get => _amplitudeLowIdSelector;
        set
        {
            if (SetProperty(ref _amplitudeLowIdSelector, value))
                RefreshApplyCommand();
        }
    }

    public bool UpdateAmplitudeStatus
    {
        get => _updateAmplitudeStatus;
        set
        {
            if (!SetProperty(ref _updateAmplitudeStatus, value))
                return;

            OnPropertyChanged(nameof(IsAmplitudeStatusEnabled));
            RefreshApplyCommand();
        }
    }

    public BooleanStatus AmplitudeStatus
    {
        get => _amplitudeStatus;
        set
        {
            if (SetProperty(ref _amplitudeStatus, value))
                RefreshApplyCommand();
        }
    }

    public bool UpdateAmplitudeTime
    {
        get => _updateAmplitudeTime;
        set
        {
            if (!SetProperty(ref _updateAmplitudeTime, value))
                return;

            OnPropertyChanged(nameof(IsAmplitudeTimeEnabled));
            RefreshApplyCommand();
        }
    }

    public DateTime AmplitudeDate
    {
        get => _amplitudeDate;
        set
        {
            if (!SetProperty(ref _amplitudeDate, value))
                return;

            _amplitudeDateTimeEdited = true;
            RefreshApplyCommand();
        }
    }

    public TimeSpan AmplitudeTime
    {
        get => _amplitudeTime;
        set
        {
            if (!SetProperty(ref _amplitudeTime, value))
                return;

            _amplitudeDateTimeEdited = true;
            RefreshApplyCommand();
        }
    }

    public bool IsAmplitudeStatusEnabled => IsAmplitudeModificationEnabled && UpdateAmplitudeStatus;
    public bool IsAmplitudeTimeEnabled => IsAmplitudeModificationEnabled && UpdateAmplitudeTime;

    public bool IsZakanModificationEnabled
    {
        get => _isZakanModificationEnabled;
        set
        {
            if (SetProperty(ref _isZakanModificationEnabled, value))
                RefreshApplyCommand();
        }
    }

    public BooleanStatus ZakanStatus
    {
        get => _zakanStatus;
        set
        {
            if (SetProperty(ref _zakanStatus, value))
                RefreshApplyCommand();
        }
    }
}
