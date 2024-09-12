using System.Collections.ObjectModel;
using System.Text.RegularExpressions;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Plugin.Maui.OCR;

namespace OCRTest.ViewModels;

public partial class MainPageViewModel : ObservableObject
{
    private readonly IOcrService _ocrService;

    private readonly Dictionary<string, string> _patterns = new()
    {
        { "No pattern", string.Empty },
        { "With digits", @"\d+" },
        { "Text only", @"^(?!.*\d).*" },
        { "Elements with position", string.Empty },
        { "Elements grouped by Y", string.Empty },
        { "Elements grouped by Y Clustering", string.Empty }
    };

    public MainPageViewModel(IOcrService ocrService)
    {
        _ocrService = ocrService;

        PickerItems = new(_patterns.Keys);
    }

    [ObservableProperty]
    private ObservableCollection<string> pickerItems;

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
            var photo = await MediaPicker.Default.CapturePhotoAsync();

            if (photo != null)
            {
                var result = await ProcessPhotoAsync(photo);

                ProcessResult(result);
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
        var photo = await MediaPicker.Default.PickPhotoAsync();

        if (photo == null)
        {
            return;
        }

        var result = await ProcessPhotoAsync(photo);

        ProcessResult(result);
    }

    private List<List<OcrResult.OcrElement>> ClusterElementsByY(IList<OcrResult.OcrElement> elements, int threshold)
    {
        var sortedPoints = elements.OrderBy(p => p.Y).ToList();
        var clusters = new List<List<OcrResult.OcrElement>>();
        List<OcrResult.OcrElement> currentCluster =
        [
            sortedPoints[0]
        ];

        for (int i = 1; i < sortedPoints.Count; i++)
        {
            if (Math.Abs(sortedPoints[i].Y - sortedPoints[i - 1].Y) > threshold)
            {
                clusters.Add(currentCluster);
                currentCluster = new();
            }

            currentCluster.Add(sortedPoints[i]);
        }

        clusters.Add(currentCluster);

        return clusters;
    }

    private async Task<OcrResult> ProcessPhotoAsync(FileResult photo)
    {
        await _ocrService.InitAsync();

        await using var sourceStream = await photo.OpenReadAsync();

        var imageData = new byte[sourceStream.Length];

        await sourceStream.ReadAsync(imageData);

        var options = new OcrOptions.Builder().SetTryHard(IsTryHard).SetLanguage("en-US").Build();

        return await _ocrService.RecognizeTextAsync(imageData, options);
    }

    private void ProcessResult(OcrResult result)
    {
        var selectedPattern = _patterns[PickerItems[PickerSelectedIndex]];

        if (!string.IsNullOrWhiteSpace(selectedPattern))
        {
            var regex = new Regex(selectedPattern);
            var builder = new StringBuilder();

            foreach (var line in result.Lines)
            {
                if (regex.IsMatch(line))
                {
                    builder.AppendLine(line);
                }
            }

            ResultsText = builder.ToString();
        }
        else if (PickerSelectedIndex == 3)
        {
            var builder = new StringBuilder();
            foreach (var el in result.Elements)
            {
                builder.AppendLine($"{el.Text} ({el.X};{el.Y})");
            }
            ResultsText = builder.ToString();
        }
        else if (PickerSelectedIndex == 4)
        {
            var builder = new StringBuilder();
            var groups = result.Elements.GroupBy(x => x.Y);

            foreach (var group in groups)
            {
                var texts = group.Select(x => x.Text);
                builder.AppendLine(string.Join(" ", texts));
            }
            ResultsText = builder.ToString();
        }
        else if (PickerSelectedIndex == 5)
        {
            var clusters = ClusterElementsByY(result.Elements, 5);
            var builder = new StringBuilder();

            foreach (var texts in clusters.Select(cluster => cluster.Select(x => x.Text)))
            {
                builder.AppendLine(string.Join(" ", texts));
            }

            ResultsText = builder.ToString();
        }
        else
        {
            ResultsText = result.AllText;
        }
    }
}
