using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OCRTest.Models;
using OCRTest.Models.Enums;
using OCRTest.Services;

namespace OCRTest.ViewModels;

public partial class MainPageViewModel : ObservableObject
{
    private readonly IImageToTextService _imageToTextService;

    public MainPageViewModel(IImageToTextService imageToTextService)
    {
        _imageToTextService = imageToTextService;

        InitPickerItems();
    }

    [ObservableProperty]
    private ObservableCollection<PickerDisplayModel> pickerItems;

    [ObservableProperty]
    private PickerDisplayModel pickerSelectedItem;

    [ObservableProperty]
    private int pickerSelectedIndex = 0;

    [ObservableProperty]
    private string resultsText = "Waiting for results ..";

    [ObservableProperty]
    private bool isTryHard = true;

    public async void OnAppearing()
    {
        await Permissions.RequestAsync<Permissions.Camera>();
    }

    [RelayCommand]
    private void OnClearButtonTapped()
    {
        ResultsText = "Waiting for results ..";
    }

    [RelayCommand]
    private async Task OpenFromCameraButtonTapped()
    {
        if (MediaPicker.Default.IsCaptureSupported)
        {
            var result = await _imageToTextService.OpenFromCameraAsync(PickerSelectedItem.Pattern, IsTryHard);

            if (result != null)
            {
                ResultsText = result;
            }
        }
        else
        {
            await Shell.Current.DisplayAlert(title: "Sorry", message: "Image capture is not supported on this device.", cancel: "OK");
        }
    }

    [RelayCommand]
    private async Task OpenFromFileButtonTapped()
    {
        var result = await _imageToTextService.OpenFromFileAsync(PickerSelectedItem.Pattern, IsTryHard);

        if (result != null)
        {
            ResultsText = result;
        }
    }

    private void InitPickerItems()
    {
        PickerItems = [];

        foreach (TextPattern enumValue in Enum.GetValues(typeof(TextPattern)))
        {
            var alias = enumValue switch
            {
                TextPattern.None => "No pattern",
                TextPattern.ContainsDigits => "With digits",
                TextPattern.TextOnly => "Text only",
                TextPattern.ElementsPositions => "Elements with position",
                TextPattern.ElementsGroupY => "Elements grouped by Y",
                TextPattern.ElementsGroupYClustering => "Elements grouped by Y Clustering",
                _ => string.Empty
            };

            PickerItems.Add(new PickerDisplayModel(enumValue, alias));
        }
    }
}
