// Opt-in UI pacing diagnostic. Rendering callbacks are not GPU present telemetry.
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using PCL.Core.App;
using PCL.Core.UI.Controls;
using PCL.Core.UI.Theme;
namespace PCL;
internal static class FramePerformanceSmoke
{
    internal static async Task Run(FormMain form,string output,List<string> report)
    {
        report.Add("Unique RenderingTime UI callback pacing only; no claim of presented/displayed FPS. CPU is milliseconds per second across all process threads (1000 = one core).");
        var hwnd=(HwndSource?)PresentationSource.FromVisual(form);
        report.Add($"tier={RenderCapability.Tier>>16}; ps3={RenderCapability.IsPixelShaderVersionSupported(3,0)}; mode={RenderOptions.ProcessRenderMode}/{hwnd?.CompositionTarget?.RenderMode}; animationLimit={Config.System.AnimationFpsLimit}; dpi={VisualTreeHelper.GetDpi(form).DpiScaleX}");
        Config.Preference.Theme.ColorMode=ColorMode.Dark;ThemeService.RefreshColorMode();FluentTheme.Refresh();
        form.PageChange(FormMain.PageType.Setup,FormMain.PageSubType.SetupLaunch);
        await Task.Delay(4000);
        if(File.Exists(Path.Combine(AppContext.BaseDirectory,"frame-isolation.flag")))
        {
            form.Width=1280;form.Height=800;await Task.Delay(800);
            var all=Items(form).OfType<LiquidGlassSurface>().ToArray();
            foreach(var mode in new[]{"normal","no-blur","no-dock","no-input","no-content"})
            {
                foreach(var surface in all)
                {
                    surface.SetResourceReference(LiquidGlassSurface.BlurRadiusProperty,"LiquidGlass.BlurRadius");
                    surface.SetResourceReference(LiquidGlassSurface.GlassEnabledProperty,"LiquidGlass.Enabled");
                }
                form.PrimaryNavigation.Visibility=Visibility.Visible;
                if(mode=="no-blur")foreach(var surface in all)surface.BlurRadius=0;
                if(mode=="no-dock")form.PrimaryNavigation.Visibility=Visibility.Hidden;
                if(mode=="no-input")foreach(var surface in all.Where(x=>x.Material==LiquidGlassMaterial.Input))surface.GlassEnabled=false;
                if(mode=="no-content")foreach(var surface in all.Where(x=>x.Material==LiquidGlassMaterial.Content))surface.GlassEnabled=false;
                await Task.Delay(500);
                var scroll=ModMain.frmSetupLaunch!.PanBack;
                await Measure(mode,t=>scroll.ScrollToVerticalOffset((.5-.5*Math.Cos(t*3))*Math.Min(1000,scroll.ScrollableHeight)),true,report);
                report.Add("groups: "+string.Join(",",ModAnimation.aniGroups.Keys));
                File.WriteAllLines(Path.Combine(output,"frame-isolation-report.txt"),report);
            }
            return;
        }
        foreach(var width in new[]{810d,1280d})
        {
            form.Width=width;form.Height=width==810?470:800;await Task.Delay(700);
            foreach(var glass in new[]{true,false})
            {
                Application.Current.Resources["LiquidGlass.Enabled"]=glass;
                await Task.Delay(600);
                var scroll=ModMain.frmSetupLaunch!.PanBack;
                var surfaces=Items(form).OfType<LiquidGlassSurface>().ToArray();
                report.Add($"INFO width={width}; glass={glass}; surfaces={surfaces.Length}; visible={surfaces.Count(s=>s.IsVisible)}; source={form.ImgBack.Background?.GetType().Name}; scrollRange={scroll.ScrollableHeight:F0}");
                await Measure($"idle width={width} glass={glass}",null,false,report);
                await Measure($"scroll width={width} glass={glass}",t=>scroll.ScrollToVerticalOffset((.5-.5*Math.Cos(t*3))*Math.Min(1000,scroll.ScrollableHeight)),true,report);
                scroll.ScrollToTop();await Task.Delay(300);
                await Measure($"dock width={width} glass={glass}",t=>{
                    var dock=form.LiquidDock;
                    if(dock is null)return;
                    if(!dock.Motion.Dragging)dock.BeginDrag(new Point(System.Windows.Controls.Canvas.GetLeft(form.DockHighlight)+form.DockHighlight.Width/2,32),false);
                    dock.DragTo(form.PrimaryNavigation.ActualWidth*(.5+.32*Math.Sin(t*4)));
                },true,report);
                form.LiquidDock?.FinishDrag();form.PageChange(FormMain.PageType.Setup,FormMain.PageSubType.SetupLaunch);await Task.Delay(800);
            }
        }
        Application.Current.Resources["LiquidGlass.Enabled"]=true;
        form.Width=1280;form.Height=800;await Task.Delay(500);
        var lastPage=-1;
        await Measure("navigation glass=True",t=>{
            var page=(int)(t/.4)%2;
            if(page==lastPage)return;lastPage=page;
            form.PageChange(page==0?FormMain.PageType.Setup:FormMain.PageType.Tools);
        },true,report);
        form.PageChange(FormMain.PageType.Setup);await Task.Delay(800);
        await Measure("resize glass=True",t=>{form.Width=1050+200*Math.Sin(t*2);form.Height=650+120*Math.Sin(t*2);},true,report);
        File.WriteAllLines(Path.Combine(output,"frame-performance-report.txt"),report);
    }
    internal static async Task RunHome(FormMain form, string output, List<string> report)
    {
        report.Add("Homepage A/B is opt-in QA only. Rendering callback pacing is diagnostic, not presented/displayed FPS; the dock stays fully enabled in every sample.");
        var lightweightProperty = typeof(LiquidGlassSurface).GetField("UseLightweightMaterialProperty",
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)?.GetValue(null) as DependencyProperty;
        var right = ModMain.frmLaunchRight ?? throw new InvalidOperationException("Launch homepage right page is unavailable.");
        var left = ModMain.frmLaunchLeft ?? throw new InvalidOperationException("Launch homepage left page is unavailable.");
        var oldBackground = form.ImgBack.Background;
        var oldVisibility = form.ImgBack.Visibility;
        var oldOpacity = form.ImgBack.Opacity;
        var fixture = right.PanCustom;
        var oldFixture = fixture.Children.Cast<UIElement>().ToArray();
        var wallpaper = new DrawingBrush(new GeometryDrawing(
            new LinearGradientBrush(Color.FromRgb(24, 52, 92), Color.FromRgb(174, 95, 66), 35), null,
            Geometry.Parse("M0,0 L1280,0 1280,800 0,800Z M0,420 C260,110 620,690 1280,230 L1280,800 0,800Z")))
            { Stretch = Stretch.UniformToFill, ViewboxUnits = BrushMappingMode.Absolute };
        wallpaper.Freeze();
        try
        {
            Config.Preference.Theme.ColorMode = ColorMode.Dark;
            ThemeService.RefreshColorMode();
            FluentTheme.Refresh();
            Application.Current.Resources["LiquidGlass.Enabled"] = true;
            form.PageChange(FormMain.PageType.Launch);
            await Task.Delay(2600);
            form.ImgBack.Background = wallpaper;
            form.ImgBack.Visibility = Visibility.Visible;
            form.ImgBack.Opacity = 1;
            fixture.Children.Clear();
            fixture.Children.Add(new Border
            {
                Height = 1300, Background = new SolidColorBrush(Color.FromArgb(22, 255, 255, 255)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(40, 255, 255, 255)), BorderThickness = new Thickness(0, 1, 0, 0)
            });
            form.UpdateLayout();
            await Task.Delay(500);

            var dock = Items(form.PrimaryNavigation).OfType<LiquidGlassSurface>().ToArray();
            var tabs = dock.FirstOrDefault(surface => surface.Material == LiquidGlassMaterial.BottomTabs);
            var indicator = dock.FirstOrDefault(surface => surface.Material == LiquidGlassMaterial.BottomTabIndicator);
            if (tabs is null || indicator is null) throw new InvalidOperationException("Both dock glass surfaces are required.");
            foreach (var surface in new[] { tabs, indicator })
            {
                if (!surface.GlassEnabled) throw new InvalidOperationException($"Dock material {surface.Material} is not fully enabled.");
                if (!ReferenceEquals(LiquidGlassSurface.GetBackdropSource(surface), form.SceneBackdrop))
                    throw new InvalidOperationException($"Dock material {surface.Material} does not sample SceneBackdrop.");
                if (!HasFullGlassGraph(surface))
                    throw new InvalidOperationException($"Dock material {surface.Material} is missing its live glass graph.");
            }
            var homeSurfaces = Items(left).OfType<LiquidGlassSurface>()
                .Concat(Items(right).OfType<LiquidGlassSurface>()).Distinct().ToArray();
            report.Add($"INFO homepage materials={string.Join(",", homeSurfaces.GroupBy(s => s.Material).Select(g => $"{g.Key}:{g.Count()}"))}; dock={tabs.Material}/{indicator.Material}; dockBlur={tabs.BlurRadius}/{indicator.BlurRadius}; dockSource=SceneBackdrop");
            report.Add("PASS dock materials remain enabled and sample the live scene in both A/B groups");

            var modes = lightweightProperty is null ? new[] { false } : new[] { false, true };
            foreach (var width in new[] { 810d, 1280d })
            {
                form.Left = 0;
                form.Top = 0;
                form.Width = width;
                form.Height = width == 810 ? 470 : 800;
                form.UpdateLayout();
                await Task.Delay(700);
                for (var pass = 0; pass < 2; pass++)
                {
                    foreach (var lightweight in pass == 0 ? modes : modes.Reverse())
                    {
                        if (lightweightProperty is not null)
                        {
                            left.SetValue(lightweightProperty, lightweight);
                            right.SetValue(lightweightProperty, lightweight);
                            form.UpdateLayout();
                        }
                        await Task.Delay(600);
                        var mode = lightweightProperty is null ? "original (property absent)"
                            : lightweight ? "lightweight" : "original";
                        var scroll = right.PanBack;
                        scroll.ScrollToTop();
                        await Task.Delay(250);
                        var visible = homeSurfaces.Count(surface => surface.IsVisible);
                        var tracked = homeSurfaces.Count(IsCoordinateTracking);
                        var graphs = homeSurfaces.Count(HasFullGlassGraph);
                        if (lightweight && (tracked != 0 || graphs != 0))
                            throw new InvalidOperationException($"Lightweight homepage retained glass work: tracked={tracked}, graphs={graphs}");
                        if (!lightweight && lightweightProperty is not null && graphs == 0)
                            throw new InvalidOperationException("Original homepage mode did not restore its glass graph.");
                        if (!HasFullGlassGraph(tabs) || !HasFullGlassGraph(indicator))
                            throw new InvalidOperationException("Dock lost its glass graph during homepage A/B.");
                        if (pass == 0)
                            LiquidGlassSmoke.Capture(form, Path.Combine(output, $"home-perf-{width:F0}-{(lightweight ? "lightweight" : "original")}.png"));
                        report.Add($"INFO home width={width:F0} pass={pass + 1} mode={mode} surfaces={homeSurfaces.Length} visible={visible} scrollRange={scroll.ScrollableHeight:F0} lightProperty={(lightweightProperty is not null)} tracked={tracked} graphs={graphs}; dockGraphs={HasFullGlassGraph(tabs)}/{HasFullGlassGraph(indicator)}");
                        await Measure($"home idle width={width:F0} pass={pass + 1} mode={mode}", null, false, report);
                        await Measure($"home scroll width={width:F0} pass={pass + 1} mode={mode}",
                            t => scroll.ScrollToVerticalOffset((.5 - .5 * Math.Cos(t * 3)) * Math.Min(1000, scroll.ScrollableHeight)), true, report);
                        scroll.ScrollToTop();
                        await Task.Delay(250);
                        await Measure($"home dock width={width:F0} pass={pass + 1} mode={mode}", t =>
                        {
                            var dockController = form.LiquidDock;
                            if (dockController is null) return;
                            if (!dockController.Motion.Dragging)
                                dockController.BeginDrag(new Point(System.Windows.Controls.Canvas.GetLeft(form.DockHighlight) + form.DockHighlight.Width / 2, 32), false);
                            dockController.DragTo(form.PrimaryNavigation.ActualWidth * (.5 + .32 * Math.Sin(t * 4)));
                        }, true, report);
                        form.LiquidDock?.FinishDrag();
                        form.PageChange(FormMain.PageType.Launch);
                        form.UpdateLayout();
                        await Task.Delay(500);
                    }
                }
                LiquidGlassSmoke.Capture(form, Path.Combine(output, $"home-perf-{width:F0}.png"));
            }
            if (lightweightProperty is null)
                report.Add("INFO baseline only: UseLightweightMaterialProperty is absent in this build.");
            else
                report.Add("INFO A/B toggles only the inherited homepage material property; dock material, source and settings are unchanged.");
        }
        finally
        {
            if (lightweightProperty is not null)
            {
                left.ClearValue(lightweightProperty);
                right.ClearValue(lightweightProperty);
            }
            fixture.Children.Clear();
            foreach (var child in oldFixture) fixture.Children.Add(child);
            form.ImgBack.Background = oldBackground;
            form.ImgBack.Visibility = oldVisibility;
            form.ImgBack.Opacity = oldOpacity;
            form.LiquidDock?.FinishDrag();
            form.PageChange(FormMain.PageType.Launch);
        }
    }

    internal static async Task RunPages(FormMain form, string output, List<string> report)
    {
        report.Add("Cross-page material A/B is opt-in QA only. Rendering callback pacing is diagnostic, not presented/displayed FPS; the dock stays fully enabled in every sample.");
        var lightweightProperty = typeof(LiquidGlassSurface).GetField("UseLightweightMaterialProperty",
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)?.GetValue(null) as DependencyProperty;
        if (lightweightProperty is null) throw new InvalidOperationException("UseLightweightMaterialProperty is unavailable.");
        var oldLightweight = form.GetValue(lightweightProperty);
        var oldBackground = form.ImgBack.Background;
        var oldVisibility = form.ImgBack.Visibility;
        var oldOpacity = form.ImgBack.Opacity;
        var wallpaper = new DrawingBrush(new GeometryDrawing(
            new LinearGradientBrush(Color.FromRgb(24, 52, 92), Color.FromRgb(174, 95, 66), 35), null,
            Geometry.Parse("M0,0 L1280,0 1280,800 0,800Z M0,420 C260,110 620,690 1280,230 L1280,800 0,800Z")))
            { Stretch = Stretch.UniformToFill, ViewboxUnits = BrushMappingMode.Absolute };
        wallpaper.Freeze();
        try
        {
            Config.Preference.Theme.ColorMode = ColorMode.Dark;
            ThemeService.RefreshColorMode();
            FluentTheme.Refresh();
            Application.Current.Resources["LiquidGlass.Enabled"] = true;
            form.ImgBack.Background = wallpaper;
            form.ImgBack.Visibility = Visibility.Visible;
            form.ImgBack.Opacity = 1;
            var pages = new (string Name, FormMain.PageSubType Sub, Func<ScrollViewer?> Scroll)[]
            {
                ("setup-launch", FormMain.PageSubType.SetupLaunch, () => ModMain.frmSetupLaunch?.PanBack),
                ("setup-ui", FormMain.PageSubType.SetupUI, () => ModMain.frmSetupUI?.PanBack),
                ("setup-game", FormMain.PageSubType.SetupGameManage, () => ModMain.frmSetupGameManage?.PanBack),
                ("setup-about", FormMain.PageSubType.SetupAbout, () => ModMain.frmSetupAbout?.PanBack)
            };
            foreach (var width in new[] { 810d, 1280d })
            {
                form.Left = 0;
                form.Top = 0;
                form.Width = width;
                form.Height = width == 810 ? 470 : 800;
                form.PageChange(FormMain.PageType.Setup, FormMain.PageSubType.SetupLaunch);
                form.UpdateLayout();
                await Task.Delay(900);
                for (var pass = 0; pass < 2; pass++)
                {
                    foreach (var lightweight in pass == 0 ? new[] { false, true } : new[] { true, false })
                    {
                        form.SetValue(lightweightProperty, lightweight);
                        foreach (var page in pages)
                        {
                            form.PageChange(FormMain.PageType.Setup, page.Sub);
                            form.UpdateLayout();
                            await Task.Delay(700);
                            var scroll = page.Scroll();
                            if (scroll is null) throw new InvalidOperationException($"Missing scroll host for {page.Name}.");
                            var visible = Items(form).OfType<LiquidGlassSurface>()
                                .Where(surface => surface.IsVisible && surface.ActualWidth > 0 && surface.ActualHeight > 0).ToArray();
                            var pageSurfaces = visible.Where(surface => surface.Material is LiquidGlassMaterial.Content
                                or LiquidGlassMaterial.Chrome or LiquidGlassMaterial.Action or LiquidGlassMaterial.Input).ToArray();
                            var graphs = pageSurfaces.Count(HasFullGlassGraph);
                            var tracked = pageSurfaces.Count(IsCoordinateTracking);
                            var dock = visible.Where(surface => surface.Material is LiquidGlassMaterial.BottomTabs
                                or LiquidGlassMaterial.BottomTabIndicator).ToArray();
                            if (lightweight && (graphs != 0 || tracked != 0))
                                throw new InvalidOperationException($"{page.Name} retained glass work in lightweight mode: tracked={tracked}, graphs={graphs}");
                            if (!lightweight && graphs == 0)
                                throw new InvalidOperationException($"{page.Name} did not restore any glass graph in original mode.");
                            if (dock.Length == 0 || dock.Any(surface => !HasFullGlassGraph(surface)))
                                throw new InvalidOperationException($"Dock lost its glass graph on {page.Name}.");
                            var mode = lightweight ? "lightweight" : "original";
                            if (pass == 0)
                                LiquidGlassSmoke.Capture(form, Path.Combine(output, $"pages-{page.Name}-{width:F0}-{mode}.png"));
                            report.Add($"INFO page={page.Name} width={width:F0} pass={pass + 1} mode={mode} surfaces={pageSurfaces.Length} graphs={graphs} tracked={tracked} dockGraphs={dock.Count(HasFullGlassGraph)} scrollRange={scroll.ScrollableHeight:F0}");
                            await Measure($"pages {page.Name} width={width:F0} pass={pass + 1} mode={mode}",
                                t => scroll.ScrollToVerticalOffset((.5 - .5 * Math.Cos(t * 3)) * scroll.ScrollableHeight), true, report);
                            scroll.ScrollToTop();
                        }
                    }
                }
            }
            report.Add("INFO A/B toggles only the inherited material property on the main window; dock material, source and settings are unchanged.");
        }
        finally
        {
            form.SetValue(lightweightProperty, oldLightweight);
            form.ImgBack.Background = oldBackground;
            form.ImgBack.Visibility = oldVisibility;
            form.ImgBack.Opacity = oldOpacity;
            form.PageChange(FormMain.PageType.Launch);
        }
    }

    internal static async Task CheckAnimationScheduler(List<string> report)
    {
        Application.Current.Resources["LiquidGlass.Enabled"]=false;
        var oldLimit=Config.System.AnimationFpsLimit;
        try
        {
            foreach(var limit in new[]{59,143,239,240})
            {
                Config.System.AnimationFpsLimit=limit;
                var value=0d;var updates=0;
                ModAnimation.AniStart(ModAnimation.AaDouble(d=>{value+=(double)d;updates++;},1,180),"QA Frame Endpoint");
                await Task.Delay(450);
                if(Math.Abs(value-1)>.00001 || ModAnimation.AniIsRun("QA Frame Endpoint"))throw new Exception($"Animation endpoint failed limit={limit}: {value}");
                report.Add($"PASS animation endpoint limit={(limit>=240?"automatic":(limit+1).ToString())} updates={updates}");
            }
            var completion=new TaskCompletionSource();
            ModAnimation.AniStart(new[]{ModAnimation.AaCode(()=>ModAnimation.AniStart(ModAnimation.AaCode(()=>completion.TrySetResult()),"QA Replace"))},"QA Replace");
            await completion.Task.WaitAsync(TimeSpan.FromSeconds(2));
            report.Add("PASS same-name replacement inside animation callback survives removal of previous entry");
            await Task.Run(()=>ModAnimation.AniStart(ModAnimation.AaCode(()=>{}),"QA Worker Wake"));
            await Task.Delay(150);
            if(ModAnimation.AniIsRun("QA Worker Wake"))throw new Exception("Worker animation did not drain");
            report.Add("PASS worker-thread animation wakes UI rendering and drains");
            var host=new System.Windows.Controls.Grid();
            var loader=new MyLoading { Visibility=Visibility.Collapsed };
            host.Children.Add(loader);
            var window=new Window { Width=180,Height=140,Content=host,ShowInTaskbar=false };
            try
            {
                window.Show();await Task.Delay(100);
                if(ModAnimation.aniGroups.Keys.Any(k=>k.StartsWith("MyLoader Loop ")))throw new Exception("Invisible loader kept animation alive");
                loader.Visibility=Visibility.Visible;await Task.Delay(150);
                if(!ModAnimation.aniGroups.Keys.Any(k=>k.StartsWith("MyLoader Loop ")))throw new Exception("Visible loader did not animate");
                loader.Visibility=Visibility.Collapsed;await Task.Delay(100);
                if(ModAnimation.aniGroups.Keys.Any(k=>k.StartsWith("MyLoader Loop ")))throw new Exception("Hidden loader did not stop");
                loader.Visibility=Visibility.Visible;await Task.Delay(100);window.Close();await Task.Delay(100);
                if(ModAnimation.aniGroups.Keys.Any(k=>k.StartsWith("MyLoader Loop ")))throw new Exception("Unloaded loader did not stop");
                report.Add("PASS loading animation starts on visibility, stops when hidden/unloaded");
            }
            finally {window.Close();}
            await Task.Delay(500);
            if(ModAnimation.aniGroups.IsEmpty && ModAnimation.IsFrameSubscribed)throw new Exception("Idle frame handler did not detach");
            report.Add("PASS idle renderer detaches when animation queue is empty; remaining groups="+string.Join(",",ModAnimation.aniGroups.Keys));
        }
        finally {Config.System.AnimationFpsLimit=oldLimit;FluentTheme.Refresh();}
    }
    private static async Task Measure(string name,Action<double>? action,bool frames,List<string> report)
    {
        using var process=Process.GetCurrentProcess();
        var cpu=process.TotalProcessorTime;var allocation=GC.GetTotalAllocatedBytes();var timer=Stopwatch.StartNew();
        var intervals=new List<double>();double previous=0;var layouts=0;var rawCallbacks=0;
        var lastRenderingTime=TimeSpan.MinValue;
        EventHandler layout=(_,_)=>layouts++;
        EventHandler render=(_,e)=>{
            rawCallbacks++;
            var stamp=((RenderingEventArgs)e).RenderingTime;
            if(stamp==lastRenderingTime)return;
            lastRenderingTime=stamp;
            var now=timer.Elapsed.TotalMilliseconds;
            if(previous>0)intervals.Add(now-previous);
            previous=now;action?.Invoke(now/1000);
        };
        ModMain.frmMain.LayoutUpdated+=layout;
        if(frames)CompositionTarget.Rendering+=render;
        try { await Task.Delay(3000); }
        finally {if(frames)CompositionTarget.Rendering-=render;ModMain.frmMain.LayoutUpdated-=layout;}
        timer.Stop();intervals.Sort();
        double P(double q)=>intervals.Count==0?0:intervals[Math.Min(intervals.Count-1,(int)((intervals.Count-1)*q))];
        report.Add($"{name}: callbacks={intervals.Count}; rawCallbacks={rawCallbacks}; median={P(.5):F2}ms; p95={P(.95):F2}ms; p99={P(.99):F2}ms; max={P(1):F2}ms; >16.67ms={intervals.Count(x=>x>16.67)}; cpu={(process.TotalProcessorTime-cpu).TotalMilliseconds/timer.Elapsed.TotalSeconds:F1}ms/s; allocated={(GC.GetTotalAllocatedBytes()-allocation)/timer.Elapsed.TotalSeconds:F0}B/s; layoutEvents={layouts}; animationGroups={ModAnimation.aniGroups.Count}");
    }
    private static object? PrivateField(LiquidGlassSurface surface, string name) =>
        typeof(LiquidGlassSurface).GetField(name, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.GetValue(surface);

    private static bool HasFullGlassGraph(LiquidGlassSurface surface) =>
        PrivateField(surface, "_sample") is not null || PrivateField(surface, "_effect") is not null;

    private static bool IsCoordinateTracking(LiquidGlassSurface surface) =>
        PrivateField(surface, "_coordinateTracking") is true;

    private static IEnumerable<DependencyObject> Items(DependencyObject root)
    {
        for(var i=0;i<VisualTreeHelper.GetChildrenCount(root);i++)
        {
            var child=VisualTreeHelper.GetChild(root,i);yield return child;
            foreach(var nested in Items(child))yield return nested;
        }
    }
}

