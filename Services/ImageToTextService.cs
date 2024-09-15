using OCRTest.Models.Enums;
using Plugin.Maui.OCR;
using System.Text;
using System.Text.RegularExpressions;

namespace OCRTest.Services;

public interface IImageToTextService
{
    Dictionary<TextPattern, string> Patterns { get; }
    Task<string?> OpenFromCameraAsync(TextPattern patternKey, bool tryHard = true);
    Task<string?> OpenFromFileAsync(TextPattern patternKey, bool tryHard = true);
}

public class ImageToTextService : IImageToTextService
{
    private readonly IOcrService _ocrService;
    private readonly IMediaPicker _mediaPicker;

    public ImageToTextService(IOcrService ocrService, IMediaPicker mediaPicker)
    {
        _ocrService = ocrService;
        _mediaPicker = mediaPicker;
    }

    #region -- IImageToTextService implementation--

    public Dictionary<TextPattern, string> Patterns { get; } = new()
    {
        { TextPattern.None, string.Empty },
        { TextPattern.ContainsDigits, @"\d+" },
        { TextPattern.TextOnly, @"^(?!.*\d).*" },
        { TextPattern.ElementsPositions, string.Empty },
        { TextPattern.ElementsGroupY, string.Empty },
        { TextPattern.ElementsGroupYClustering, string.Empty }
    };

    public async Task<string?> OpenFromCameraAsync(TextPattern patternKey, bool tryHard = true)
    {
        string? result = null;

        try
        {
            var photo = await _mediaPicker.CapturePhotoAsync();

            if (photo != null)
            {
                var ocrResult = await ProcessPhotoAsync(photo, tryHard);

                result = ApplyRegex(ocrResult, patternKey);
            }
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }

        return result;
    }

    public async Task<string?> OpenFromFileAsync(TextPattern patternKey, bool tryHard = true)
    {
        string? result = null;

        try
        {
            var photo = await _mediaPicker.PickPhotoAsync();

            if (photo != null)
            {
                var ocrResult = await ProcessPhotoAsync(photo);

                result = ApplyRegex(ocrResult, patternKey);
            }
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }

        return result;
    }

    #endregion

    #region -- Private helpers --

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

    private async Task<OcrResult> ProcessPhotoAsync(FileResult photo, bool tryHard = true)
    {
        await _ocrService.InitAsync();

        await using var sourceStream = await photo.OpenReadAsync();

        var imageData = new byte[sourceStream.Length];

        await sourceStream.ReadAsync(imageData);

        var options = new OcrOptions.Builder().SetTryHard(tryHard).SetLanguage("en-US").Build();

        return await _ocrService.RecognizeTextAsync(imageData, options);
    }

    private string? ApplyRegex(OcrResult ocrResult, TextPattern patternKey)
    {
        var selectedPattern = Patterns[patternKey];

        string? result;

        if (patternKey is TextPattern.None)
        {
            result = ocrResult.AllText;
        }
        else
        {
            var regex = new Regex(selectedPattern);
            var builder = new StringBuilder();

            var lines = patternKey switch
            {
                TextPattern.ContainsDigits or TextPattern.TextOnly => ocrResult.Lines.Where(line => regex.IsMatch(line)),
                TextPattern.ElementsPositions => ocrResult.Elements.Select(el => $"{el.Text} ({el.X};{el.Y})"),
                TextPattern.ElementsGroupY => ocrResult.Elements.GroupBy(x => x.Y).Select(group => string.Join(" ", group.Select(x => x.Text))),
                TextPattern.ElementsGroupYClustering => ClusterElementsByY(ocrResult.Elements, 5).Select(cluster => string.Join(" ", cluster.Select(x => x.Text))),
                _ => [],
            };

            foreach (var line in lines)
            {
                builder.AppendLine(line);
            }

            result = builder.ToString();
        }

        return result;
    }

    #endregion
}
