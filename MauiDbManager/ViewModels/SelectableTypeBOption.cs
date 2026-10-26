using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace MauiDbManager.ViewModels;

public sealed class SelectableTypeBOption : INotifyPropertyChanged
{
    private bool _isSelected;
    private bool _isEnabled = true;

    public SelectableTypeBOption(string text, long? lowId, bool isAll = false)
    {
        Text = text;
        LowId = lowId;
        IsAll = isAll;
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    public event EventHandler? SelectionChanged;

    public string Text { get; }
    public long? LowId { get; }
    public bool IsAll { get; }

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected == value)
                return;

            _isSelected = value;
            OnPropertyChanged();
            SelectionChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public bool IsEnabled
    {
        get => _isEnabled;
        set
        {
            if (_isEnabled == value)
                return;

            _isEnabled = value;
            OnPropertyChanged();
        }
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
