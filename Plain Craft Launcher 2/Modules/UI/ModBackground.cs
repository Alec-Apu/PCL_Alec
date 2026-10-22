using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using PCL.Core.App;
using PCL.Core.App.Localization;
using PCL.Core.IO.Net;
using PCL.Core.UI;

namespace PCL;

internal static class ModBackground
{
    internal const string LandscapeApi = "https://t.alcy.cc/fj";
    internal const string AnimeApi = "https://rimg.zhuqiy.top/api/random?type=pc";
    private static CancellationTokenSource? _pending;
    internal static Task CurrentTask { get; private set; } = Task.CompletedTask;
    internal static bool IsLoading { get; private set; }
    internal static string Status { get; private set; } = "";
    internal static string CachePath(int source) => Path.Combine(ModBase.pathTemp, "Cache", "Background", source == 1 ? "landscape.png" : "anime.png");

    internal static void CancelPending()
    {
        _pending?.Cancel();
        _pending = null;
        IsLoading = false;
        Status = "";
    }

    internal static void Refresh(int source, bool hint)
    {
        CancelPending();
        if (source is not (1 or 2))
        {
            ModVideoBack.VideoStop();
            ModMain.frmMain.VideoBack.Source = null;
            ModMain.frmMain.ImgBack.Background = null;
            ModMain.frmMain.ImgBack.Visibility = Visibility.Collapsed;
            UpdateUi();
            CurrentTask = Task.CompletedTask;
            return;
        }
        var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        _pending = cancellation;
        CurrentTask = RefreshOnlineAsync(source, hint, cancellation);
    }

    private static async Task RefreshOnlineAsync(int source, bool hint, CancellationTokenSource cancellation)
    {
        var token = cancellation.Token;
        var cache = CachePath(source);
        IsLoading = true;
        Status = Lang.Text("Setup.Ui.Background.Online.Loading");
        UpdateUi();
        try
        {
            if (File.Exists(cache))
            {
                try
                {
                    var cached = await Task.Run(() => Decode(File.ReadAllBytes(cache)), token);
                    if (IsCurrent()) Apply(cached);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                { ModBase.Log(ex, "读取在线背景缓存失败", ModBase.LogLevel.Debug); }
            }
            var bytes = await FetchImageAsync(source, token);
            var image = await Task.Run(() => Decode(bytes), token);
            token.ThrowIfCancellationRequested();
            if (!IsCurrent()) return;
            await Task.Run(() => SaveCache(image, cache, token), token);
            if (!IsCurrent()) return;
            Apply(image);
            Status = Lang.Text("Setup.Ui.Background.Online.Ready", source == 1 ? "栗次元" : "ZHUQIY");
            if (hint) HintService.Hint(Lang.Text("Setup.Ui.Background.Online.Success"), HintType.Success);
        }
        catch (OperationCanceledException) when (!ReferenceEquals(_pending, cancellation)) { }
        catch (Exception ex)
        {
            if (!IsCurrent()) return;
            ModBase.Log(ex, "在线背景加载失败", ModBase.LogLevel.Debug);
            Status = Lang.Text(ModMain.frmMain.ImgBack.Background is null
                ? "Setup.Ui.Background.Online.Failed" : "Setup.Ui.Background.Online.KeptPrevious");
            if (hint) HintService.Hint(Status, HintType.Error);
        }
        finally
        {
            if (ReferenceEquals(_pending, cancellation))
            {
                _pending = null;
                IsLoading = false;
                UpdateUi();
            }
            cancellation.Dispose();
        }
        bool IsCurrent() => ReferenceEquals(_pending, cancellation) && Config.Preference.Background.WallpaperSource == source;
    }

    private static async Task<byte[]> FetchImageAsync(int source, CancellationToken token)
    {
        var url = source == 1 ? LandscapeApi : AnimeApi;
        using var request = new HttpRequestMessage(HttpMethod.Get, url + (url.Contains('?') ? "&" : "?") + "_=" + Guid.NewGuid().ToString("N"));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("image/*"));
        request.Headers.CacheControl = new CacheControlHeaderValue { NoCache = true };
        using var response = await NetworkService.GetClient().SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token);
        response.EnsureSuccessStatusCode();
        if (response.Content.Headers.ContentType?.MediaType is not { } type || !type.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Background API did not return an image.");
        const int limit = 20 * 1024 * 1024;
        if (response.Content.Headers.ContentLength > limit) throw new InvalidDataException("Background image exceeds 20 MiB.");
        await using var input = await response.Content.ReadAsStreamAsync(token);
        using var output = new MemoryStream();
        var buffer = new byte[81920];
        int count;
        while ((count = await input.ReadAsync(buffer, token)) > 0)
        {
            if (output.Length + count > limit) throw new InvalidDataException("Background image exceeds 20 MiB.");
            output.Write(buffer, 0, count);
        }
        return output.ToArray();
    }

    private static BitmapSource Decode(byte[] bytes)
    {
        using var stream = new MemoryStream(bytes);
        var image = new BitmapImage();
        image.BeginInit();
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.DecodePixelWidth = 2560;
        image.StreamSource = stream;
        image.EndInit();
        image.Freeze();
        return image;
    }

    private static void SaveCache(BitmapSource image, string path, CancellationToken token)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            var png = new PngBitmapEncoder();
            png.Frames.Add(BitmapFrame.Create(image));
            using (var file = File.Create(temporary)) png.Save(file);
            token.ThrowIfCancellationRequested();
            File.Move(temporary, path, true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }

    private static void Apply(BitmapSource image)
    {
        ModVideoBack.VideoStop();
        ModMain.frmMain.VideoBack.Source = null;
        ModMain.frmMain.ImgBack.Background = new ImageBrush(image) { Stretch = Stretch.UniformToFill };
        ModMain.frmMain.ImgBack.Visibility = Visibility.Visible;
        ModSetup.UiBackgroundSuit(Config.Preference.Background.WallpaperSuitMode);
        ModSetup.UiBackgroundOpacity(Config.Preference.Background.WallpaperOpacity);
        ModSetup.UiBackgroundBlur(Config.Preference.Background.WallpaperBlurRadius);
    }

    private static void UpdateUi() => ModMain.frmSetupUI?.BackgroundRefreshUI(ModMain.frmMain.ImgBack.Background is not null, 1);
}
