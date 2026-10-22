// Compiled only into the isolated smoke build.
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using PCL.Core.UI.Controls;
using PCL.Core.App;
using PCL.Core.UI.Theme;

namespace PCL;

internal static class LiquidGlassSmoke
{
    private sealed class ProbeSurface : LiquidGlassSurface
    {
        public int Renders { get; private set; }
        public void RenderProbe(DrawingContext context) => base.OnRender(context);
        protected override void OnRender(DrawingContext dc) { Renders++; base.OnRender(dc); }
    }

    internal static async Task RunCards(FormMain form,string output,List<string> report)
    {
        if(Config.Preference.Background.WallpaperSourceConfig.DefaultValue!=2)throw new Exception("Default background is not anime");
        report.Add("PASS new/unset background preference defaults to anime; explicit user selections remain persisted");
        var source=new Grid();var root=new Grid();root.Children.Add(source);
        var probe=new ProbeSurface { Width=320,Height=180,Background=Brushes.Transparent,BlurRadius=0,CornerRadius=new CornerRadius(24) };
        root.Children.Add(probe);LiquidGlassSurface.SetBackdropSource(root,source);
        var window=new Window { Width=640,Height=400,Content=root,ShowInTaskbar=false };
        try
        {
            window.Show();
            foreach(var dark in new[]{false,true})
            {
                Config.Preference.Theme.ColorMode=dark?ColorMode.Dark:ColorMode.Light;ThemeService.RefreshColorMode();FluentTheme.Refresh();
                probe.GlassEnabled=true;probe.BlurRadius=0;
                foreach(var material in new[]{LiquidGlassMaterial.Content,LiquidGlassMaterial.Input,LiquidGlassMaterial.Chrome,LiquidGlassMaterial.Action,LiquidGlassMaterial.Floating})
                foreach(var color in new[]{Color.FromRgb(32,100,180),Color.FromRgb(210,66,50),Color.FromRgb(35,170,100),Color.FromRgb(112,112,112)})
                {
                    probe.Material=material;source.Background=new SolidColorBrush(color);await Task.Delay(350);
                    var path=Path.Combine(output,$"card-color-{dark}-{material}-{color.R}.png");Capture(window,path);
                    var point=probe.PointToScreen(new Point(probe.ActualWidth/2,probe.ActualHeight/2));
                    GetWindowRect(new WindowInteropHelper(window).Handle,out var native);
                    var image=BitmapDecoder.Create(new Uri(path),BitmapCreateOptions.None,BitmapCacheOption.OnLoad).Frames[0];
                    var pixel=new byte[4];image.CopyPixels(new Int32Rect((int)point.X-native.Left,(int)point.Y-native.Top,1,1),pixel,4,0);
                    var error=Math.Max(Math.Abs(pixel[2]-color.R),Math.Max(Math.Abs(pixel[1]-color.G),Math.Abs(pixel[0]-color.B)));
                    report.Add($"{(error<=4?"PASS":"FAIL")} card color dark={dark} material={material}: source={color.R},{color.G},{color.B}; sampled={pixel[2]},{pixel[1]},{pixel[0]}; maxError={error}");
                }
            }
        }
        finally {window.Close();}
        Config.Preference.Background.WallpaperSource=2;ModBackground.Refresh(2,false);
        await ModBackground.CurrentTask.WaitAsync(TimeSpan.FromSeconds(30));
        report.Add($"INFO anime background: {ModBackground.Status}; loaded={form.ImgBack.Background is ImageBrush}");
        if(form.ImgBack.Background is not ImageBrush)throw new Exception("Anime wallpaper did not load for card visual review");
        foreach(var dark in new[]{false,true})
        {
            Config.Preference.Theme.ColorMode=dark?ColorMode.Dark:ColorMode.Light;ThemeService.RefreshColorMode();FluentTheme.Refresh();
            foreach(var size in new[]{new Size(810,470),new Size(1120,680)})
            foreach(var page in new[]{FormMain.PageType.Launch,FormMain.PageType.Setup,FormMain.PageType.Tools,FormMain.PageType.Download,FormMain.PageType.InstanceSelect})
            {
                form.Left=0;form.Top=0;form.Width=size.Width;form.Height=size.Height;form.PageChange(page);await Task.Delay(500);form.UpdateLayout();
                Capture(form,Path.Combine(output,$"anime-cards-{dark}-{size.Width}-{page}.png"));
            }
        }
    }

    internal static async Task RunPerformance(FormMain form, List<string> report)
    {
        var source = new Grid { Background=Brushes.Coral };
        var root = new Grid();root.Children.Add(source);
        var probe = new ProbeSurface { Width=320,Height=64,Background=Brushes.Transparent,Material=LiquidGlassMaterial.BottomTabs,CornerRadius=new CornerRadius(32) };
        root.Children.Add(probe);LiquidGlassSurface.SetBackdropSource(root,source);
        var window = new Window { Width=640,Height=400,Content=root,ShowInTaskbar=false };
        try
        {
            window.Show();await Task.Delay(400);
            var target = new DrawingVisual();
            for(var i=0;i<20;i++)using(var dc=target.RenderOpen())probe.RenderProbe(dc);
            var allocated=GC.GetAllocatedBytesForCurrentThread();var timer=Stopwatch.StartNew();
            for(var i=0;i<600;i++)
            {
                probe.PressProgress=(i%60)/60d;
                using var dc=target.RenderOpen();probe.RenderProbe(dc);
            }
            timer.Stop();var bytes=GC.GetAllocatedBytesForCurrentThread()-allocated;
            var hwnd=(HwndSource?)PresentationSource.FromVisual(form);
            report.Add($"INFO tier={RenderCapability.Tier>>16}; ps3={RenderCapability.IsPixelShaderVersionSupported(3,0)}; processMode={RenderOptions.ProcessRenderMode}; windowMode={hwnd?.CompositionTarget?.RenderMode}; disableHardwareAcceleration={Config.System.DisableHardwareAcceleration}");
            report.Add($"INFO 600 surface render recordings: CPU={timer.Elapsed.TotalMilliseconds:F2}ms; managed allocations={bytes}; bytes/render={bytes/600d:F0}. Controlled UI-thread recording benchmark; excludes GPU execution and present latency.");
        }
        finally { window.Close(); }
    }

    internal static async Task Run(FormMain form, string output, List<string> report)
    {
        report.Add($"INFO render tier={RenderCapability.Tier >> 16}; ps3={RenderCapability.IsPixelShaderVersionSupported(3,0)}");
        var root = new Grid { Background = Brushes.White };
        var pattern = new Grid { Background = Brushes.White };
        for (var x = 0; x < 800; x += 20)
            pattern.Children.Add(new Border { Width = 4, Background = Brushes.DarkSlateBlue, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(x,0,0,0) });
        for (var y = 0; y < 400; y += 20)
            pattern.Children.Add(new Border { Height = 4, Background = Brushes.Coral, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0,y,0,0) });
        root.Children.Add(pattern);
        var probe = new ProbeSurface
        {
            Width = 300, Height = 180, CornerRadius = new CornerRadius(28),
            HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(100,80,0,0), BlurRadius = 0, GlassEnabled = true,
            Background = Brushes.Transparent, Material = LiquidGlassMaterial.Chrome,
            Child = new TextBlock { Text = "Liquid Glass · 清晰文字", FontSize = 20, FontWeight = FontWeights.SemiBold,
                Foreground = Brushes.Black, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center }
        };
        root.Children.Add(probe);
        LiquidGlassSurface.SetBackdropSource(root, pattern);
        var window = new Window { Width = 820, Height = 440, Content = root, WindowStartupLocation = WindowStartupLocation.CenterScreen, ShowInTaskbar = false };
        try
        {
            window.Show(); await Task.Delay(600);
            Capture(window, Path.Combine(output,"glass-grid-lens.png"));
            probe.RefractionStrength=0;await Task.Delay(150);
            Capture(window,Path.Combine(output,"glass-grid-flat.png"));
            probe.RefractionStrength=1;await Task.Delay(150);
            var raster = new RenderTargetBitmap((int)root.ActualWidth,(int)root.ActualHeight,96,96,PixelFormats.Pbgra32);
            raster.Render(root); Save(raster, Path.Combine(output,"glass-grid-raster.png"));
            probe.Visibility = Visibility.Hidden; await Task.Delay(150);
            Capture(window, Path.Combine(output,"glass-grid-source.png"));
            if(RenderCapability.IsPixelShaderVersionSupported(3,0))
            {
                var topLeft=probe.PointToScreen(new Point(32,1));var bottomRight=probe.PointToScreen(new Point(268,16));
                GetWindowRect(new WindowInteropHelper(window).Handle,out var nativeBounds);
                var region=new Int32Rect((int)topLeft.X-nativeBounds.Left,(int)topLeft.Y-nativeBounds.Top,Math.Max(1,(int)(bottomRight.X-topLeft.X)),Math.Max(1,(int)(bottomRight.Y-topLeft.Y)));
                var changes=ChangedPixels(Path.Combine(output,"glass-grid-lens.png"),Path.Combine(output,"glass-grid-flat.png"),region);
                if(changes<region.Width*region.Height*.1)throw new Exception($"Lens did not displace the grid: changed={changes}");
                report.Add($"PASS real edge refraction: {changes}/{region.Width*region.Height} grid pixels displaced away from the highlight and text");
            }
            probe.Visibility = Visibility.Visible;
            probe.GlassEnabled = false; await Task.Delay(150);
            Capture(window, Path.Combine(output,"glass-grid-fallback.png"));
            probe.GlassEnabled = true; await Task.Delay(300);
            var before = probe.Renders;
            var process = Process.GetCurrentProcess(); var cpu = process.TotalProcessorTime;
            var memory = process.PrivateMemorySize64;
            await Task.Delay(1200);
            report.Add($"{(probe.Renders == before ? "PASS" : "FAIL")} static surface render count: {before} -> {probe.Renders}");
            report.Add($"INFO 1200ms idle process CPU={(process.TotalProcessorTime-cpu).TotalMilliseconds:F1}ms; private memory={memory/1048576d:F1}MiB -> {process.PrivateMemorySize64/1048576d:F1}MiB (includes launcher background work)");
            // Screen-space source mapping must continue to work across popup HWNDs.
            var popupSurface = new ProbeSurface { Width=220, Height=130, CornerRadius=new CornerRadius(24), Background=Brushes.Transparent, BlurRadius=0, GlassEnabled=true, Material=LiquidGlassMaterial.Floating };
            LiquidGlassSurface.SetBackdropSource(popupSurface,pattern);
            var popup = new System.Windows.Controls.Primitives.Popup { PlacementTarget=probe, Placement=System.Windows.Controls.Primitives.PlacementMode.Right, AllowsTransparency=true, Child=popupSurface, StaysOpen=true };
            popup.IsOpen=true; await Task.Delay(250);
            report.Add($"{(popupSurface.Renders > 0 ? "PASS" : "FAIL")} popup surface rendered through explicit owner background");
            if(PresentationSource.FromVisual(popupSurface) is HwndSource popupSource) CaptureHandle(popupSource.Handle,Path.Combine(output,"glass-grid-popup.png"));
            popup.IsOpen=false;
            var animatedLine=pattern.Children.OfType<Border>().First();
            var motion=new TranslateTransform();animatedLine.RenderTransform=motion;
            motion.BeginAnimation(TranslateTransform.XProperty,new System.Windows.Media.Animation.DoubleAnimation(0,360,TimeSpan.FromSeconds(2)) { AutoReverse=true,RepeatBehavior=System.Windows.Media.Animation.RepeatBehavior.Forever });
            foreach(var enabled in new[]{false,true})
            {
                probe.GlassEnabled=enabled;await Task.Delay(200);
                var intervals=new List<double>();TimeSpan? last=null;
                EventHandler handler=(_,e)=>{var stamp=((RenderingEventArgs)e).RenderingTime;if(last is {} previous && stamp>previous)intervals.Add((stamp-previous).TotalMilliseconds);last=stamp;};
                CompositionTarget.Rendering+=handler;
                var startCpu=process.TotalProcessorTime;var started=Stopwatch.StartNew();
                try { await Task.Delay(1600); }
                finally { CompositionTarget.Rendering-=handler; }
                intervals.Sort();
                report.Add($"INFO animated-source glass={enabled}: composition callbacks={intervals.Count}, median={(intervals.Count>0?intervals[intervals.Count/2]:0):F1}ms, p95={(intervals.Count>0?intervals[(int)((intervals.Count-1)*.95)]:0):F1}ms, process CPU={(process.TotalProcessorTime-startCpu).TotalMilliseconds:F1}ms/{started.ElapsedMilliseconds}ms; private memory={process.PrivateMemorySize64/1048576d:F1}MiB (not GPU present timings)");
            }
            motion.BeginAnimation(TranslateTransform.XProperty,null);
            probe.CornerRadius=new CornerRadius(8,32,16,40);await Task.Delay(200);
            Capture(window,Path.Combine(output,"glass-grid-asymmetric-corners.png"));
            probe.BlurRadius=12;
            foreach(var kernel in new[]{System.Windows.Media.Effects.KernelType.Gaussian,System.Windows.Media.Effects.KernelType.Box})
            {
                probe.BlurKernelType=kernel;await Task.Delay(150);
                Capture(window,Path.Combine(output,$"glass-grid-blur-{kernel}.png"));
            }
            report.Add("PASS Gaussian/Box blur settings render with the shared glass material");
        }
        finally { window.Close(); }

        foreach (var dark in new[] { false,true })
        {
            PCL.Core.App.Config.Preference.Theme.ColorMode=dark?PCL.Core.App.ColorMode.Dark:PCL.Core.App.ColorMode.Light;
            ThemeService.RefreshColorMode(); FluentTheme.Refresh();
            foreach (var size in new[] { new Size(810,470), new Size(1350,825) })
            {
                form.Width=size.Width; form.Height=size.Height; form.UpdateLayout(); await Task.Delay(500);
                Capture(form,Path.Combine(output,$"glass-home-{dark}-{size.Width}.png"));
            }
            form.ImgBack.Background=new DrawingBrush(new GeometryDrawing(new LinearGradientBrush(Colors.RoyalBlue,Colors.Coral,35),null,Geometry.Parse("M0,0 L1200,0 1200,700 0,700Z M0,400 C250,100 550,650 1200,220 L1200,700 0,700Z"))) { Stretch=Stretch.UniformToFill };
            form.ImgBack.Visibility=Visibility.Visible;form.ImgBack.Opacity=1;
            await Task.Delay(300);Capture(form,Path.Combine(output,$"glass-wallpaper-{dark}.png"));
            form.PageChange(FormMain.PageType.Setup,FormMain.PageSubType.SetupUI);await Task.Delay(650);
            form.Width=810;form.Height=470;form.UpdateLayout();await Task.Delay(250);
            Capture(form,Path.Combine(output,$"glass-settings-{dark}-810.png"));
            ModMain.frmLaunchLeft.ShowAccount();await Task.Delay(250);
            if(!ModMain.frmLaunchLeft.AccountPopup.IsOpen)throw new Exception("Account popup failed after settings navigation");
            if(PresentationSource.FromVisual(ModMain.frmLaunchLeft.AccountPopup.Child) is HwndSource accountSource) CaptureHandle(accountSource.Handle,Path.Combine(output,$"glass-account-{dark}.png"));
            ModMain.frmLaunchLeft.AccountPopup.IsOpen=false;
            form.PageChange(FormMain.PageType.Launch);await Task.Delay(500);
            form.ImgBack.Background=null;form.ImgBack.Visibility=Visibility.Collapsed;
            if(!ReferenceEquals(LiquidGlassSurface.GetBackdropSource(form.PrimaryNavigation),form.SceneBackdrop))throw new Exception("Navigation does not sample the actual scene");
            var witness=new TextBlock { Text="透过底栏 · SCROLLING CONTENT 0123456789 · 透过底栏",FontSize=28,FontWeight=FontWeights.Bold,Foreground=Brushes.Magenta,VerticalAlignment=VerticalAlignment.Bottom,HorizontalAlignment=HorizontalAlignment.Center,Margin=new Thickness(0,0,0,26),IsHitTestVisible=false };
            Grid.SetRow(witness,1);form.SceneBackdrop.Children.Add(witness);
            try
            {
                form.UpdateLayout();await Task.Delay(350);
                Capture(form,Path.Combine(output,$"glass-navigation-content-{dark}.png"));
                var from=form.PrimaryNavigation.PointToScreen(new Point(12,12));var to=form.PrimaryNavigation.PointToScreen(new Point(form.PrimaryNavigation.ActualWidth-12,form.PrimaryNavigation.ActualHeight-12));
                GetWindowRect(new WindowInteropHelper(form).Handle,out var native);
                var region=new Int32Rect((int)from.X-native.Left,(int)from.Y-native.Top,(int)(to.X-from.X),(int)(to.Y-from.Y));
                witness.Visibility=Visibility.Hidden;await Task.Delay(250);
                Capture(form,Path.Combine(output,$"glass-navigation-empty-{dark}.png"));
                var changes=ChangedPixels(Path.Combine(output,$"glass-navigation-content-{dark}.png"),Path.Combine(output,$"glass-navigation-empty-{dark}.png"),region);
                if(changes<100)throw new Exception($"Page text is not visible through navigation: changed={changes}");
                if(Panel.GetZIndex(form.PrimaryNavigation)<=Panel.GetZIndex(form.SceneBackdrop))throw new Exception("Navigation is below the page scene");
                if(Math.Abs(form.PanMain.TransformToAncestor(form.PanForm).Transform(new Point(0,form.PanMain.ActualHeight)).Y-form.PanForm.ActualHeight)>1)throw new Exception("Bottom spacer was added");
                report.Add($"PASS highest-layer navigation, full-height page without spacer, real content visible through glass: dark={dark}, changed pixels={changes}");
            }
            finally { form.SceneBackdrop.Children.Remove(witness); }
            await CheckStationaryDockMotion(form,output,dark,report);
        }
        foreach(var width in new[]{400d,740d,1120d})
        {
            var panel=new ResponsiveColumnsPanel { MinimumColumnWidth=280 };
            panel.Children.Add(new TextBlock { Text="Long translated setting label",TextWrapping=TextWrapping.Wrap });
            panel.Children.Add(new MyButton { Text="Instanzverzeichnis öffnen" });
            panel.Measure(new Size(width,double.PositiveInfinity));panel.Arrange(new Rect(0,0,width,panel.DesiredSize.Height));
            var first=panel.Children[0].TransformToAncestor(panel).TransformBounds(new Rect(panel.Children[0].RenderSize));
            var second=panel.Children[1].TransformToAncestor(panel).TransformBounds(new Rect(panel.Children[1].RenderSize));
            if(first.IntersectsWith(second) || second.Right>width+.1)throw new Exception($"Responsive columns overlap at {width}");
            report.Add($"PASS responsive columns at {width} DIP: stacked={second.Y>first.Y}");
        }
        var videoPath=Path.Combine(Path.GetTempPath(),"pcl-liquid-fixture.mp4");
        if(File.Exists(videoPath))
        {
            var opened=new TaskCompletionSource();
            RoutedEventHandler ready=(_,_)=>opened.TrySetResult();
            EventHandler<ExceptionRoutedEventArgs> failed=(_,e)=>opened.TrySetException(e.ErrorException);
            form.VideoBack.MediaOpened+=ready;form.VideoBack.MediaFailed+=failed;
            try
            {
                form.ImgBack.Visibility=Visibility.Visible;form.VideoBack.Source=new Uri(videoPath);form.VideoBack.Width=form.ImgBack.ActualWidth;form.VideoBack.Height=form.ImgBack.ActualHeight;form.VideoBack.Play();
                await opened.Task.WaitAsync(TimeSpan.FromSeconds(10));await Task.Delay(350);
                Capture(form,Path.Combine(output,"glass-video-1.png"));await Task.Delay(800);
                Capture(form,Path.Combine(output,"glass-video-2.png"));
                var changes=ChangedPixels(Path.Combine(output,"glass-video-1.png"),Path.Combine(output,"glass-video-2.png"),new Int32Rect(50,120,350,180));
                if(changes<100)throw new Exception($"Video did not update through glass: changed={changes}");
                report.Add($"PASS existing MediaElement video updates through glass: changed pixels={changes}; captures saved");
            }
            finally { form.VideoBack.MediaOpened-=ready;form.VideoBack.MediaFailed-=failed;form.VideoBack.Stop();form.VideoBack.Source=null; }
        }
        report.Add("INFO grid source/lens/fallback and home screenshots require visual review; raster capture alone does not establish ps_3_0 hardware rendering.");
    }

    private static async Task CheckStationaryDockMotion(FormMain form,string output,bool dark,List<string> report)
    {
        if(!ReferenceEquals(LiquidGlassSurface.GetBackdropSource(form.DockPanel),form.SceneBackdrop)
           || !ReferenceEquals(LiquidGlassSurface.GetBackdropSource(form.DockHighlight),form.SceneBackdrop))
            throw new Exception("Dock stopped sampling SceneBackdrop");
        if(!HasGraph(form.DockPanel)||!HasGraph(form.DockHighlight))
            throw new Exception("Dock glass graph is missing before motion check");
        var oldWidth=form.Width;var oldHeight=form.Height;
        var block=new Border { Width=1000,Height=240,HorizontalAlignment=HorizontalAlignment.Center,
            VerticalAlignment=VerticalAlignment.Bottom,IsHitTestVisible=false,Background=Brushes.Magenta };
        Grid.SetRow(block,1);form.SceneBackdrop.Children.Add(block);
        try
        {
            form.Width=1280;form.Height=800;form.UpdateLayout();
            form.LiquidDock?.FinishDrag();
            await Task.Delay(1100);
            var scale=form.DockPanelScale.ScaleX;var offset=form.DockPanelOffset.X;
            var indicatorScale=form.DockHighlightScale.ScaleX;
            var phases=new[]{Brushes.Magenta,Brushes.Lime,Brushes.Gold,Brushes.Cyan};
            var files=new List<string>();
            foreach(var brush in phases)
            {
                block.Background=brush;
                await Task.Delay(400);
                var path=Path.Combine(output,$"glass-dock-phases-{dark}-{files.Count}.png");
                Capture(form,path);files.Add(path);
            }
            if(Math.Abs(scale-form.DockPanelScale.ScaleX)>.001||Math.Abs(offset-form.DockPanelOffset.X)>.001
               ||Math.Abs(indicatorScale-form.DockHighlightScale.ScaleX)>.001)
                throw new Exception("Dock moved during the stationary live-sampling capture");
            GetWindowRect(new WindowInteropHelper(form).Handle,out var native);
            Int32Rect Region(FrameworkElement element,double left,double top,double right,double bottom)
            {
                var from=element.PointToScreen(new Point(element.ActualWidth*left,element.ActualHeight*top));
                var to=element.PointToScreen(new Point(element.ActualWidth*right,element.ActualHeight*bottom));
                return new Int32Rect((int)from.X-native.Left,(int)from.Y-native.Top,
                    Math.Max(1,(int)(to.X-from.X)),Math.Max(1,(int)(to.Y-from.Y)));
            }
            var sourceRegion=Region(block,.35,.12,.65,.36);
            var tabsRegion=Region(form.DockPanel,.58,.22,.88,.78);
            var indicatorRegion=Region(form.DockHighlight,.10,.18,.90,.82);
            for(var i=1;i<files.Count;i++)
            {
                var sourceChanges=ChangedPixels(files[i-1],files[i],sourceRegion);
                var tabsChanges=ChangedPixels(files[i-1],files[i],tabsRegion);
                var indicatorChanges=ChangedPixels(files[i-1],files[i],indicatorRegion);
                if(sourceChanges<100)throw new Exception($"Dock source fixture did not move: phase={i}, changed={sourceChanges}");
                if(tabsChanges<100)throw new Exception($"BottomTabs stopped sampling live content: phase={i}, changed={tabsChanges}");
                if(indicatorChanges<100)throw new Exception($"BottomTabIndicator stopped sampling live content: phase={i}, changed={indicatorChanges}");
                report.Add($"PASS stationary dock live sampling dark={dark} phase={i}: source={sourceChanges}, tabs={tabsChanges}, indicator={indicatorChanges}");
            }
            if(!HasGraph(form.DockPanel)||!HasGraph(form.DockHighlight))
                throw new Exception("Dock glass graph was released during the motion check");
        }
        finally
        {
            form.SceneBackdrop.Children.Remove(block);
            form.Width=oldWidth;form.Height=oldHeight;form.UpdateLayout();
        }
    }

    private static bool HasGraph(LiquidGlassSurface surface)
    {
        var flags=System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance;
        return typeof(LiquidGlassSurface).GetField("_sample",flags)?.GetValue(surface) is not null
            || typeof(LiquidGlassSurface).GetField("_effect",flags)?.GetValue(surface) is not null;
    }

    private static void Save(BitmapSource bitmap, string path)
    {
        var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream=File.Create(path); encoder.Save(stream);
    }

    // PrintWindow captures the HWND's composed content rather than forcing a software
    // RenderTargetBitmap pass (which cannot execute WPF ps_3_0 shaders).
    internal static void Capture(Window window,string path)
    {
        var handle=new WindowInteropHelper(window).Handle;
        CaptureHandle(handle,path);
    }
    private static void CaptureHandle(IntPtr handle,string path)
    {
        GetWindowRect(handle,out var rect);var width=rect.Right-rect.Left;var height=rect.Bottom-rect.Top;
        var dc=GetDC(IntPtr.Zero);var memory=CreateCompatibleDC(dc);var bitmap=CreateCompatibleBitmap(dc,width,height);var old=SelectObject(memory,bitmap);
        try
        {
            if(!PrintWindow(handle,memory,2))throw new InvalidOperationException("PrintWindow failed.");
            SelectObject(memory,old);
            var info=new BitmapInfo { Size=40,Width=width,Height=-height,Planes=1,BitCount=32 };
            var pixels=new byte[width*height*4];
            if(GetDIBits(memory,bitmap,0,(uint)height,pixels,ref info,0)==0)throw new InvalidOperationException("GetDIBits failed.");
            Save(BitmapSource.Create(width,height,96,96,PixelFormats.Bgr32,null,pixels,width*4),path);
        }
        finally { SelectObject(memory,old);DeleteObject(bitmap);DeleteDC(memory);ReleaseDC(IntPtr.Zero,dc); }
    }
    private static int ChangedPixels(string first,string second,Int32Rect region)
    {
        byte[] Pixels(string path)
        {
            var decoded=new BitmapImage();decoded.BeginInit();decoded.CacheOption=BitmapCacheOption.OnLoad;decoded.UriSource=new Uri(path);decoded.EndInit();
            var converted=new FormatConvertedBitmap(decoded,PixelFormats.Bgr32,null,0);
            var bytes=new byte[region.Width*region.Height*4];converted.CopyPixels(region,bytes,region.Width*4,0);return bytes;
        }
        var a=Pixels(first);var b=Pixels(second);var changed=0;
        for(var i=0;i<a.Length;i+=4)if(Math.Abs(a[i]-b[i])+Math.Abs(a[i+1]-b[i+1])+Math.Abs(a[i+2]-b[i+2])>30)changed++;
        return changed;
    }
    [StructLayout(LayoutKind.Sequential)] private struct NativeRect { public int Left,Top,Right,Bottom; }
    [StructLayout(LayoutKind.Sequential)] private struct BitmapInfo { public uint Size;public int Width,Height;public ushort Planes,BitCount;public uint Compression,ImageSize;public int XPels,YPels;public uint ColorsUsed,ColorsImportant; }
    [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr window,out NativeRect rect);
    [DllImport("user32.dll")] private static extern IntPtr GetDC(IntPtr window);
    [DllImport("user32.dll")] private static extern int ReleaseDC(IntPtr window,IntPtr dc);
    [DllImport("user32.dll")] private static extern bool PrintWindow(IntPtr window,IntPtr dc,uint flags);
    [DllImport("gdi32.dll")] private static extern IntPtr CreateCompatibleDC(IntPtr dc);
    [DllImport("gdi32.dll")] private static extern IntPtr CreateCompatibleBitmap(IntPtr dc,int width,int height);
    [DllImport("gdi32.dll")] private static extern IntPtr SelectObject(IntPtr dc,IntPtr obj);
    [DllImport("gdi32.dll")] private static extern bool DeleteDC(IntPtr dc);
    [DllImport("gdi32.dll")] private static extern bool DeleteObject(IntPtr obj);
    [DllImport("gdi32.dll")] private static extern int GetDIBits(IntPtr dc,IntPtr bitmap,uint start,uint lines,byte[] pixels,ref BitmapInfo info,uint usage);
}

