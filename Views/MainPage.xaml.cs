using OCRTest.ViewModels;

namespace OCRTest.Views;

public partial class MainPage : ContentPage
{
    public MainPage(MainPageViewModel viewModel)
    {
        BindingContext = viewModel;

        InitializeComponent();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        if (BindingContext is MainPageViewModel viewModel)
        {
            viewModel.OnAppearing();
        }
    }
}

