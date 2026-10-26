using MauiDbManager.Models;
using MauiDbManager.Parsing;

namespace MauiDbManager.ViewModels;

public sealed partial class ConnectionViewModel
{
    private async Task ApplyAsync()
    {
        if (!CanApply() || SelectedDatabase is null)
            return;

        if (!TryBuildPendingRequest(out var request, out var validationMessage))
        {
            ShowApplyFailure(validationMessage);
            return;
        }

        IsBusy = true;
        IsPreviewVisible = false;
        ApplyMessage = string.Empty;

        try
        {
            var result = await _modificationService.ApplyModificationsAsync(
                ServerName.Trim(),
                SelectedDatabase,
                UserName.Trim(),
                Password,
                TrustServerCertificate,
                request.StatusModifications,
                request.OverallModification,
                request.AmplitudeModification);

            _applySucceeded = result.IsSuccess;
            ApplyMessage = result.Message;

            if (result.IsSuccess)
            {
                if (request.ApplyOverall)
                    _overallBaseline = CaptureOverallSignature();
                if (request.ApplyGain)
                    _gainBaseline = CaptureGainSignature();
                if (request.ApplyAmplitude)
                    _amplitudeBaseline = CaptureAmplitudeSignature();
                if (request.ApplyZakan)
                    _zakanBaseline = CaptureZakanSignature();

                RefreshApplyCommand();
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task PreviewAsync()
    {
        if (!CanApply() || SelectedDatabase is null)
            return;

        PreviewRows.Clear();
        PreviewSummaries.Clear();
        PreviewMessage = string.Empty;

        if (!TryBuildPendingRequest(out var request, out var validationMessage))
        {
            _previewSucceeded = false;
            PreviewMessage = validationMessage;
            IsPreviewVisible = true;
            return;
        }

        IsBusy = true;
        try
        {
            var result = await _modificationService.PreviewModificationsAsync(
                ServerName.Trim(),
                SelectedDatabase,
                UserName.Trim(),
                Password,
                TrustServerCertificate,
                request.StatusModifications,
                request.OverallModification,
                request.AmplitudeModification);

            foreach (var summary in result.Summaries)
                PreviewSummaries.Add(summary);
            foreach (var row in result.Rows)
                PreviewRows.Add(row);

            _previewSucceeded = result.IsSuccess;
            PreviewMessage = result.Message;
            OnPropertyChanged(nameof(PreviewMessageColor));
            IsPreviewVisible = true;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool TryBuildPendingRequest(
        out PendingModificationRequest request,
        out string validationMessage)
    {
        var applyOverall = HasOverallPendingChanges();
        var applyGain = HasGainPendingChanges();
        var applyAmplitude = HasAmplitudePendingChanges();
        var applyZakan = HasZakanPendingChanges();

        OverallModification? overallModification = null;
        if (applyOverall)
        {
            var midIds = LowIdSelectorParser.Parse(OverallMidIdSelector);
            if (!midIds.IsSuccess)
            {
                request = null!;
                validationMessage = $"Overall Modification mid IDs: {midIds.ErrorMessage}";
                return false;
            }

            var highIds = LowIdSelectorParser.Parse(OverallHighIdSelector);
            if (!highIds.IsSuccess)
            {
                request = null!;
                validationMessage = $"Overall Modification high IDs: {highIds.ErrorMessage}";
                return false;
            }

            var useAll = TypeBOptions.FirstOrDefault(option => option.IsAll)?.IsSelected == true;
            var selectedLowIds = TypeBOptions
                .Where(option => !option.IsAll && option.IsSelected && option.LowId.HasValue)
                .Select(option => option.LowId!.Value)
                .ToHashSet();

            overallModification = new(
                midIds.Values,
                highIds.Values,
                useAll,
                selectedLowIds,
                UpdateOverallStatus,
                SelectedOverallStatus.Value,
                UpdateOverallTime,
                ResolveEpochMilliseconds(OverallDate, OverallTime, _overallDateTimeEdited));
        }

        var statusModifications = new List<TableStatusModification>();
        if (applyGain)
            statusModifications.Add(new("dbo.gain", GainStatus));
        if (applyZakan)
            statusModifications.Add(new("dbo.zakan", ZakanStatus));

        AmplitudeModification? amplitudeModification = null;
        if (applyAmplitude)
        {
            var ids = LowIdSelectorParser.Parse(AmplitudeLowIdSelector);
            if (!ids.IsSuccess)
            {
                request = null!;
                validationMessage = $"Amplitude Modification: {ids.ErrorMessage}";
                return false;
            }

            amplitudeModification = new(
                ids.Values,
                UpdateAmplitudeStatus,
                AmplitudeStatus,
                UpdateAmplitudeTime,
                ResolveEpochMilliseconds(AmplitudeDate, AmplitudeTime, _amplitudeDateTimeEdited));
        }

        request = new(
            statusModifications,
            overallModification,
            amplitudeModification,
            applyOverall,
            applyGain,
            applyAmplitude,
            applyZakan);
        validationMessage = string.Empty;
        return true;
    }

    private static long ResolveEpochMilliseconds(DateTime date, TimeSpan time, bool wasEdited)
    {
        if (!wasEdited)
            return DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        var localDateTime = DateTime.SpecifyKind(date.Date.Add(time), DateTimeKind.Unspecified);
        var offset = TimeZoneInfo.Local.GetUtcOffset(localDateTime);
        return new DateTimeOffset(localDateTime, offset).ToUnixTimeMilliseconds();
    }

    private void ShowApplyFailure(string message)
    {
        _applySucceeded = false;
        ApplyMessage = message;
    }

    private sealed record PendingModificationRequest(
        IReadOnlyList<TableStatusModification> StatusModifications,
        OverallModification? OverallModification,
        AmplitudeModification? AmplitudeModification,
        bool ApplyOverall,
        bool ApplyGain,
        bool ApplyAmplitude,
        bool ApplyZakan);
}
