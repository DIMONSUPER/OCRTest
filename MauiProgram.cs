using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using OCRTest.Services;
using OCRTest.ViewModels;
using OCRTest.Views;
using Plugin.Maui.OCR;

namespace OCRTest;
public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            })
            .UseOcr();

#if DEBUG
		builder.Logging.AddDebug();
#endif

        builder.Services.AddSingleton(OcrPlugin.Default);
        builder.Services.AddSingleton(MediaPicker.Default);

        builder.Services.AddTransient<IImageToTextService, ImageToTextService>();

        builder.Services.AddTransient<MainPage>();
        builder.Services.AddTransient<MainPageViewModel>();

        return builder.Build();
    }
}
