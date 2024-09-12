using System.Text;
using System.Text.RegularExpressions;
using Plugin.Maui.OCR;

namespace OCRTest.Views;

public partial class MainPage : ContentPage
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

    public MainPage(IOcrService ocrService)
    {
        InitializeComponent();

        _ocrService = ocrService;

        foreach (var key in _patterns.Keys)
        {
            PatternPicker.Items.Add(key);
        }

        PatternPicker.SelectedIndex = 0;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        await _ocrService.InitAsync();

        await Permissions.RequestAsync<Permissions.Camera>();
        await Permissions.RequestAsync<Permissions.Media>();
    }

    private void ClearBtn_Clicked(object sender, EventArgs e)
    {
        ResultLbl.Text = string.Empty;
        ClearBtn.IsEnabled = false;
    }

    private async void OpenFromCameraBtn_Clicked(object sender, EventArgs e)
    {
        if (MediaPicker.Default.IsCaptureSupported)
        {
            var photo = await MediaPicker.Default.CapturePhotoAsync();

            if (photo != null)
            {
                var result = await ProcessPhotoAsync(photo);

                ResultLbl.Text = result.AllText;

                ClearBtn.IsEnabled = true;
            }
        }
        else
        {
            await DisplayAlert(title: "Sorry", message: "Image capture is not supported on this device.", cancel: "OK");
        }
    }

    private async void OpenFromFileBtn_Clicked(object sender, EventArgs e)
    {
        var photo = await MediaPicker.Default.PickPhotoAsync();

        if (photo == null)
        {
            return;
        }

        var result = await ProcessPhotoAsync(photo);

        var selectedPattern = _patterns[PatternPicker.Items[PatternPicker.SelectedIndex]];

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

            ResultLbl.Text = builder.ToString();
        }
        else if (PatternPicker.SelectedIndex == 3)
        {
            var builder = new StringBuilder();
            foreach (var el in result.Elements)
            {
                builder.AppendLine($"{el.Text} ({el.X};{el.Y})");
            }
            ResultLbl.Text = builder.ToString();
        }
        else if (PatternPicker.SelectedIndex == 4)
        {
            var builder = new StringBuilder();
            var groups = result.Elements.GroupBy(x => x.Y);

            foreach (var group in groups)
            {
                var texts = group.Select(x => x.Text);
                builder.AppendLine(string.Join(" ", texts));
            }
            ResultLbl.Text = builder.ToString();
        }
        else if (PatternPicker.SelectedIndex == 5)
        {
            var clusters = ClusterPointsByY(result.Elements, 5);
            var builder = new StringBuilder();

            foreach (var texts in clusters.Select(cluster => cluster.Select(x => x.Text)))
            {
                builder.AppendLine(string.Join(" ", texts));
            }

            ResultLbl.Text = builder.ToString();
        }
        else
        {
            ResultLbl.Text = result.AllText;
        }

        ClearBtn.IsEnabled = true;
    }

    private List<List<OcrResult.OcrElement>> ClusterPointsByY(IList<OcrResult.OcrElement> elements, int threshold)
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
        await using var sourceStream = await photo.OpenReadAsync();

        var imageData = new byte[sourceStream.Length];
        var a = _ocrService.SupportedLanguages;
        using var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        await sourceStream.ReadAsync(imageData, cancellationTokenSource.Token);

        var options = new OcrOptions.Builder().SetTryHard(TryHardSwitch.IsToggled).SetLanguage("en-US").Build();

        return await _ocrService.RecognizeTextAsync(imageData, options, cancellationTokenSource.Token);
    }
}

