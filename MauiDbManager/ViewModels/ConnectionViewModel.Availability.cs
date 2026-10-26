namespace MauiDbManager.ViewModels;

public sealed partial class ConnectionViewModel
{
    public bool IsOverallSectionAvailable
    {
        get => _isOverallSectionAvailable;
        private set
        {
            if (SetProperty(ref _isOverallSectionAvailable, value))
                OnPropertyChanged(nameof(IsOverallSectionUnavailable));
        }
    }

    public bool IsGainSectionAvailable
    {
        get => _isGainSectionAvailable;
        private set
        {
            if (SetProperty(ref _isGainSectionAvailable, value))
                OnPropertyChanged(nameof(IsGainSectionUnavailable));
        }
    }

    public bool IsAmplitudeSectionAvailable
    {
        get => _isAmplitudeSectionAvailable;
        private set
        {
            if (SetProperty(ref _isAmplitudeSectionAvailable, value))
                OnPropertyChanged(nameof(IsAmplitudeSectionUnavailable));
        }
    }

    public bool IsZakanSectionAvailable
    {
        get => _isZakanSectionAvailable;
        private set
        {
            if (SetProperty(ref _isZakanSectionAvailable, value))
                OnPropertyChanged(nameof(IsZakanSectionUnavailable));
        }
    }

    public bool IsOverallSectionUnavailable => HasSelectedDatabase && !IsOverallSectionAvailable;
    public bool IsGainSectionUnavailable => HasSelectedDatabase && !IsGainSectionAvailable;
    public bool IsAmplitudeSectionUnavailable => HasSelectedDatabase && !IsAmplitudeSectionAvailable;
    public bool IsZakanSectionUnavailable => HasSelectedDatabase && !IsZakanSectionAvailable;

    public string OverallAvailabilityMessage
    {
        get => _overallAvailabilityMessage;
        private set => SetProperty(ref _overallAvailabilityMessage, value);
    }

    public string GainAvailabilityMessage
    {
        get => _gainAvailabilityMessage;
        private set => SetProperty(ref _gainAvailabilityMessage, value);
    }

    public string AmplitudeAvailabilityMessage
    {
        get => _amplitudeAvailabilityMessage;
        private set => SetProperty(ref _amplitudeAvailabilityMessage, value);
    }

    public string ZakanAvailabilityMessage
    {
        get => _zakanAvailabilityMessage;
        private set => SetProperty(ref _zakanAvailabilityMessage, value);
    }

    private void ResetSectionAvailability()
    {
        IsOverallSectionAvailable = false;
        IsGainSectionAvailable = false;
        IsAmplitudeSectionAvailable = false;
        IsZakanSectionAvailable = false;

        var message = HasSelectedDatabase
            ? "Checking required tables..."
            : "Select a database to check required tables.";
        OverallAvailabilityMessage = message;
        GainAvailabilityMessage = message;
        AmplitudeAvailabilityMessage = message;
        ZakanAvailabilityMessage = message;
        NotifySectionAvailabilityProperties();
    }

    private void SetSectionAvailability(
        IReadOnlySet<string> availableTables,
        bool lookupSucceeded,
        string lookupMessage)
    {
        if (!lookupSucceeded)
        {
            var reason = string.IsNullOrWhiteSpace(lookupMessage)
                ? "The selected database could not be inspected."
                : lookupMessage;
            OverallAvailabilityMessage = $"Cannot enable Overall Modification. {reason}";
            GainAvailabilityMessage = $"Cannot enable Gain Modification. {reason}";
            AmplitudeAvailabilityMessage = $"Cannot enable Amplitude Modification. {reason}";
            ZakanAvailabilityMessage = $"Cannot enable Zakan Modification. {reason}";
            NotifySectionAvailabilityProperties();
            RefreshApplyCommand();
            return;
        }

        var overallRequiredTables = new[] { "dbo.overall_status", "dbo.type_a", "dbo.type_b" };
        var missingOverallTables = overallRequiredTables
            .Where(table => !availableTables.Contains(table))
            .ToArray();

        IsOverallSectionAvailable = missingOverallTables.Length == 0;
        IsGainSectionAvailable = availableTables.Contains("dbo.gain");
        IsAmplitudeSectionAvailable = availableTables.Contains("dbo.amplitude");
        IsZakanSectionAvailable = availableTables.Contains("dbo.zakan");

        OverallAvailabilityMessage = IsOverallSectionAvailable
            ? string.Empty
            : $"Cannot enable Overall Modification. Missing table(s): {string.Join(", ", missingOverallTables)}.";
        GainAvailabilityMessage = BuildMissingTableMessage(IsGainSectionAvailable, "Gain", "dbo.gain");
        AmplitudeAvailabilityMessage = BuildMissingTableMessage(IsAmplitudeSectionAvailable, "Amplitude", "dbo.amplitude");
        ZakanAvailabilityMessage = BuildMissingTableMessage(IsZakanSectionAvailable, "Zakan", "dbo.zakan");

        if (!IsOverallSectionAvailable)
            IsOverallModificationEnabled = false;
        if (!IsGainSectionAvailable)
            IsGainModificationEnabled = false;
        if (!IsAmplitudeSectionAvailable)
            IsAmplitudeModificationEnabled = false;
        if (!IsZakanSectionAvailable)
            IsZakanModificationEnabled = false;

        NotifySectionAvailabilityProperties();
        RefreshApplyCommand();
    }

    private static string BuildMissingTableMessage(bool isAvailable, string sectionName, string tableName) =>
        isAvailable ? string.Empty : $"Cannot enable {sectionName} Modification. Missing table: {tableName}.";

    private void NotifySectionAvailabilityProperties()
    {
        OnPropertyChanged(nameof(IsOverallSectionUnavailable));
        OnPropertyChanged(nameof(IsGainSectionUnavailable));
        OnPropertyChanged(nameof(IsAmplitudeSectionUnavailable));
        OnPropertyChanged(nameof(IsZakanSectionUnavailable));
    }
}
