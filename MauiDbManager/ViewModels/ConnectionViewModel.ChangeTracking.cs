using MauiDbManager.Commands;

namespace MauiDbManager.ViewModels;

public sealed partial class ConnectionViewModel
{
    private bool CanApply()
    {
        if (IsBusy || !IsConnected || !HasSelectedDatabase)
            return false;

        var applyOverall = HasOverallPendingChanges();
        var applyGain = HasGainPendingChanges();
        var applyAmplitude = HasAmplitudePendingChanges();
        var applyZakan = HasZakanPendingChanges();
        if (!applyOverall && !applyGain && !applyAmplitude && !applyZakan)
            return false;

        if (applyOverall &&
            ((!UpdateOverallStatus && !UpdateOverallTime) || IsTypeBLoading || !HasTypeBSelection()))
            return false;

        if (applyAmplitude && !UpdateAmplitudeStatus && !UpdateAmplitudeTime)
            return false;

        if ((applyOverall && !IsOverallSectionAvailable) ||
            (applyGain && !IsGainSectionAvailable) ||
            (applyAmplitude && !IsAmplitudeSectionAvailable) ||
            (applyZakan && !IsZakanSectionAvailable))
            return false;

        return true;
    }

    private bool HasTypeBSelection() =>
        TypeBOptions.Any(option => option.IsSelected);

    private bool HasOverallPendingChanges() =>
        IsOverallModificationEnabled && CaptureOverallSignature() != _overallBaseline;

    private bool HasGainPendingChanges() =>
        IsGainModificationEnabled && CaptureGainSignature() != _gainBaseline;

    private bool HasAmplitudePendingChanges() =>
        IsAmplitudeModificationEnabled && CaptureAmplitudeSignature() != _amplitudeBaseline;

    private bool HasZakanPendingChanges() =>
        IsZakanModificationEnabled && CaptureZakanSignature() != _zakanBaseline;

    private string CaptureOverallSignature()
    {
        var selectedTypeB = string.Join(",", TypeBOptions
            .Where(option => option.IsSelected)
            .Select(option => option.IsAll ? "All" : option.LowId?.ToString() ?? option.Text)
            .OrderBy(value => value, StringComparer.Ordinal));

        return string.Join("|",
            IsOverallModificationEnabled,
            OverallMidIdSelector,
            OverallHighIdSelector,
            UpdateOverallStatus,
            SelectedOverallStatus.Value,
            UpdateOverallTime,
            OverallDate.Ticks,
            OverallTime.Ticks,
            _overallDateTimeEdited,
            selectedTypeB);
    }

    private string CaptureGainSignature() =>
        string.Join("|", IsGainModificationEnabled, GainStatus);

    private string CaptureAmplitudeSignature() =>
        string.Join("|",
            IsAmplitudeModificationEnabled,
            AmplitudeLowIdSelector,
            UpdateAmplitudeStatus,
            AmplitudeStatus,
            UpdateAmplitudeTime,
            AmplitudeDate.Ticks,
            AmplitudeTime.Ticks,
            _amplitudeDateTimeEdited);

    private string CaptureZakanSignature() =>
        string.Join("|", IsZakanModificationEnabled, ZakanStatus);

    private void MarkAllSectionsClean()
    {
        _overallBaseline = CaptureOverallSignature();
        _gainBaseline = CaptureGainSignature();
        _amplitudeBaseline = CaptureAmplitudeSignature();
        _zakanBaseline = CaptureZakanSignature();
        RefreshApplyCommand();
    }

    private void DisableModificationSections()
    {
        IsOverallModificationEnabled = false;
        IsGainModificationEnabled = false;
        IsAmplitudeModificationEnabled = false;
        IsZakanModificationEnabled = false;
    }

    private void RefreshApplyCommand()
    {
        ((AsyncRelayCommand)ApplyCommand).RaiseCanExecuteChanged();
        ((AsyncRelayCommand)PreviewCommand).RaiseCanExecuteChanged();
    }
}
