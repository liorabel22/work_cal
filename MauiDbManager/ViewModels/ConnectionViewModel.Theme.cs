using MauiDbManager.Models;

namespace MauiDbManager.ViewModels;

public sealed partial class ConnectionViewModel
{
    public ThemeOption SelectedTheme
    {
        get => _selectedTheme;
        set
        {
            if (!SetProperty(ref _selectedTheme, value))
                return;

            if (Application.Current is null)
                return;

            Application.Current.UserAppTheme = value.Value switch
            {
                ThemePreference.Light => AppTheme.Light,
                ThemePreference.Dark => AppTheme.Dark,
                _ => AppTheme.Unspecified
            };
        }
    }

    private bool IsDarkTheme => Application.Current?.RequestedTheme == AppTheme.Dark;

    private void OnRequestedThemeChanged(object? sender, AppThemeChangedEventArgs e)
    {
        OnPropertyChanged(nameof(StatusColor));
        OnPropertyChanged(nameof(StatusBackgroundColor));
        OnPropertyChanged(nameof(ApplyStatusColor));
        OnPropertyChanged(nameof(PreviewMessageColor));
    }
}
