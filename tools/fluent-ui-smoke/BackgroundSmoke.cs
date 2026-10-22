using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using PCL.Core.App;
using PCL.Core.UI.Theme;

namespace PCL;

internal static class BackgroundSmoke
{
    internal static async Task Run(FormMain form, string root, List<string> report)
    {
        form.PageChange(FormMain.PageType.Setup, FormMain.PageSubType.SetupUI);
        await Task.Delay(700);
        var page = ModMain.frmSetupUI;
        page.ComboBackgroundSource.SelectedIndex = 0;
        PageSetupUI.BackgroundRefresh(false, true);
        CheckOriginal();
        report.Add("PASS explicitly selecting original colors clears the image and video");

        foreach (var source in new[] { 1, 2 })
        {
            page.ComboBackgroundSource.SelectedIndex = source;
            if (Config.Preference.Background.WallpaperSource != source) throw new Exception("Source selector was not persisted");
            if (!ModBackground.IsLoading || page.BtnBackgroundRefresh.IsEnabled) throw new Exception("Image loading does not disable refresh");
            await ModBackground.CurrentTask.WaitAsync(TimeSpan.FromSeconds(30));
            if (ModBackground.Status != PCL.Core.App.Localization.Lang.Text("Setup.Ui.Background.Online.Ready", source == 1 ? "栗次元" : "ZHUQIY"))
                throw new Exception("Live API did not refresh successfully: " + ModBackground.Status);
            if (form.ImgBack.Background is not ImageBrush image || form.ImgBack.Visibility != Visibility.Visible || !File.Exists(ModBackground.CachePath(source)))
                throw new Exception("Live background failed: " + source + "; " + ModBackground.Status);
            if (image.ImageSource.Width < 500 || image.ImageSource.Height < 200) throw new Exception("Background image has invalid dimensions");
            report.Add($"PASS live {(source == 1 ? "landscape" : "anime")} API, image decode/display and PNG cache: {image.ImageSource.Width}x{image.ImageSource.Height}");
            var completed = ModBackground.CurrentTask;
            page.Reload();
            if (!ReferenceEquals(completed, ModBackground.CurrentTask)) throw new Exception("Opening settings refetched the image");
            if (page.ComboBackgroundSource.SelectedIndex != source) throw new Exception("Settings reload lost selected source");

            foreach (var dark in new[] { false, true })
            foreach (var size in new[] { (810d, 470d), (1280d, 800d) })
            {
                Config.Preference.Theme.ColorMode = dark ? ColorMode.Dark : ColorMode.Light;
                ThemeService.RefreshColorMode(); FluentTheme.Refresh();
                form.Width = size.Item1; form.Height = size.Item2;
                page.PanBack.ScrollToVerticalOffset(page.CardBackground.TranslatePoint(new Point(), page.PanBack).Y + page.PanBack.VerticalOffset);
                await Task.Delay(500); form.UpdateLayout();
                if (page.ComboBackgroundSource.ActualWidth < 120) throw new Exception("Background source selector is clipped");
                var actionBottom = page.BtnBackgroundRefresh.TranslatePoint(new Point(), page.CardBackground).Y + page.BtnBackgroundRefresh.ActualHeight;
                if (actionBottom > page.CardBackground.ActualHeight + 1) throw new Exception("Background action extends beyond its card");
                var bitmap = new RenderTargetBitmap((int)form.ActualWidth, (int)form.ActualHeight, 96, 96, PixelFormats.Pbgra32);
                bitmap.Render(form);
                SavePng(bitmap, Path.Combine(root, "screenshots", $"background-{source}-{dark}-{size.Item1}.png"));
                report.Add($"PASS background settings layout: source={source}, dark={dark}, width={size.Item1}");
            }
        }

        var previous = form.ImgBack.Background;
        var cache = File.ReadAllBytes(ModBackground.CachePath(2));
        PageSetupUI.BackgroundRefresh(false, true);
        var pending = ModBackground.CurrentTask;
        ((CancellationTokenSource?)typeof(ModBackground).GetField("_pending", BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null))?.Cancel();
        await pending;
        if (form.ImgBack.Background is null || !File.ReadAllBytes(ModBackground.CachePath(2)).SequenceEqual(cache))
            throw new Exception("A timed out refresh discarded the image or damaged its cache");
        report.Add("PASS timed out refresh preserves previous background and valid cache");

        page.ComboBackgroundSource.SelectedIndex = 1;
        var stale = ModBackground.CurrentTask;
        page.ComboBackgroundSource.SelectedIndex = 0;
        await stale;
        CheckOriginal();
        report.Add("PASS switching to original colors cancels pending work without late image replacement");

        var pictures = Path.Combine(ModBase.exePath, "PCL", "Pictures");
        Directory.CreateDirectory(pictures);
        var fixture = new RenderTargetBitmap(64, 64, 96, 96, PixelFormats.Pbgra32);
        var visual = new DrawingVisual();
        using (var drawing = visual.RenderOpen()) drawing.DrawRectangle(Brushes.CadetBlue, null, new Rect(0, 0, 64, 64));
        fixture.Render(visual);
        SavePng(fixture, Path.Combine(pictures, "background-smoke-fixture.png"));
        page.ComboBackgroundSource.SelectedIndex = 3;
        if (form.ImgBack.Background is not ImageBrush || page.BtnBackgroundOpen.Visibility != Visibility.Visible)
            throw new Exception("Existing local wallpaper mode broke");
        report.Add("PASS local image background and folder controls remain available");
        page.ComboBackgroundSource.SelectedIndex = 0;
        CheckOriginal();

        void CheckOriginal()
        {
            if (form.ImgBack.Background is not null || form.ImgBack.Visibility != Visibility.Collapsed || form.VideoBack.Source is not null || page.BtnBackgroundRefresh.Visibility != Visibility.Collapsed)
                throw new Exception("Original theme did not clear the wallpaper/video or hide refresh");
        }
    }

    private static void SavePng(BitmapSource image, string path)
    {
        var png = new PngBitmapEncoder(); png.Frames.Add(BitmapFrame.Create(image));
        using var file = File.Create(path); png.Save(file);
    }
}
