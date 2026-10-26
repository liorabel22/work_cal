using MauiDbManager.ViewModels;

namespace MauiDbManager;

public partial class MainPage : ContentPage
{
    public MainPage(ConnectionViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
