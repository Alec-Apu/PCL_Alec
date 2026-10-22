using System.IO;
// Included only by Inject.targets; never compiled into normal launcher builds.
using System.Runtime.CompilerServices;
using System.Windows.Threading;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using PCL.Core.App;
using PCL.Core.App.IoC;
using PCL.Core.UI.Theme;
namespace PCL;
internal static class FluentQa
{
    private static readonly string Root = Path.Combine(Path.GetTempPath(), "pcl-ui-efficiency-integration");
    [ModuleInitializer] public static void Initialize()
    {
        foreach(var pair in new[] { ("PCL_PATH", "instance"), ("PCL_PATH_SHARED", "shared"), ("PCL_PATH_LOCAL", "local"), ("PCL_PATH_TEMP", "temp") })
            Environment.SetEnvironmentVariable(pair.Item1, Path.Combine(Root, pair.Item2));
        // Paths only reads these environment overrides in DEBUG. Set the public
        // paths before application startup as well, so Release QA remains isolated.
        Paths.Data=Path.Combine(Root,"instance");
        Paths.SharedData=Path.Combine(Root,"shared");
        Paths.SharedLocalData=Path.Combine(Root,"local");
        Paths.Temp=Path.Combine(Root,"temp");
        foreach(var path in new[]{Paths.Data,Paths.SharedData,Paths.SharedLocalData,Paths.Temp})Directory.CreateDirectory(path);
        Lifecycle.When(LifecycleState.WindowCreated, () => System.Windows.Application.Current.Dispatcher.BeginInvoke(async () => await Run()));
    }
    private static IEnumerable<MyListItem> NavigationItems(System.Windows.DependencyObject root)
    {
        for(var i=0;i<VisualTreeHelper.GetChildrenCount(root);i++)
        {
            var child=VisualTreeHelper.GetChild(root,i);
            if(child is MyListItem item)yield return item;
            foreach(var nested in NavigationItems(child))yield return nested;
        }
    }
    private static async Task Run()
    {
        var report = new List<string>();
        DispatcherTimer? networkDialogTimer=null;
        try
        {
            await Task.Delay(1800);
            var form=ModMain.frmMain;
            foreach(var path in new[]{Paths.Data,Paths.SharedData,Paths.SharedLocalData,Paths.Temp,
                PCL.Core.App.Configuration.ConfigService.SharedConfigPath,PCL.Core.App.Configuration.ConfigService.LocalConfigPath})
                if(!Path.GetFullPath(path).StartsWith(Path.GetFullPath(Root)+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))
                    throw new Exception("QA configuration escaped its isolated root");
            report.Add("PASS QA data/config paths remain inside the isolated temporary root");
            var dismissedNetworkDialogs=new HashSet<MyMsgText>();
            networkDialogTimer=new DispatcherTimer { Interval=TimeSpan.FromMilliseconds(200) };
            networkDialogTimer.Tick+=(_,_)=>
            {
                foreach(var dialog in form.PanMsg.Children.OfType<MyMsgText>().ToArray())
                {
                    var text=dialog.LabCaption.Text;
                    if(dialog.LabTitle.Text==PCL.Core.App.Localization.Lang.Text("Application.EnvironmentWarning.Title"))
                    {
                        if(dismissedNetworkDialogs.Add(dialog)) { report.Add("INFO isolated temporary-build environment warning acknowledged"); dialog.Btn1_Click(); }
                        continue;
                    }
                    if((text.Contains("HttpRequestException") || text.Contains("SocketException") || text.Contains("All connection attempts failed")) && dismissedNetworkDialogs.Add(dialog))
                    {
                        report.Add("INFO background metadata network failure cancelled in isolated layout test: "+text.Split('\n')[0]);
                        if(dialog.Btn2.Visibility==System.Windows.Visibility.Visible)dialog.Btn2_Click(null!,null!);else dialog.Btn1_Click();
                    }
                }
            };
            networkDialogTimer.Start();
            var output=Path.Combine(Root,"screenshots"); Directory.CreateDirectory(output);
            if(File.Exists(Path.Combine(AppContext.BaseDirectory,"animation-scheduler-only.flag")))
            {
                await FramePerformanceSmoke.CheckAnimationScheduler(report);
                File.WriteAllLines(Path.Combine(Root,"animation-scheduler-report.txt"),report);return;
            }
            if(File.Exists(Path.Combine(AppContext.BaseDirectory,"frame-home-only.flag")))
            {
                await FramePerformanceSmoke.RunHome(form,Root,report);
                File.WriteAllLines(Path.Combine(Root,"frame-home-report.txt"),report);return;
            }
            if(File.Exists(Path.Combine(AppContext.BaseDirectory,"frame-pages-only.flag")))
            {
                await FramePerformanceSmoke.RunPages(form,Root,report);
                File.WriteAllLines(Path.Combine(Root,"frame-pages-report.txt"),report);return;
            }
            if(File.Exists(Path.Combine(AppContext.BaseDirectory,"frame-performance-only.flag")))
            {
                await FramePerformanceSmoke.Run(form,Root,report);return;
            }
            if(File.Exists(Path.Combine(AppContext.BaseDirectory,"navigation-style-only.flag")))
            {
                foreach(var dark in new[]{false,true})
                {
                    Config.Preference.Theme.ColorMode=dark?ColorMode.Dark:ColorMode.Light;
                    ThemeService.RefreshColorMode();FluentTheme.Refresh();
                    foreach(var size in new[]{new System.Windows.Size(810,470),new System.Windows.Size(1280,800)})
                    foreach(var page in new[]{FormMain.PageType.Setup,FormMain.PageType.Download,FormMain.PageType.Tools})
                    {
                        form.Width=size.Width;form.Height=size.Height;form.PageChange(page);await Task.Delay(700);form.UpdateLayout();
                        var expected=dark?Colors.Black:Colors.White;
                        if(form.PanTitle.BackgroundBrush.Color!=expected)throw new Exception("Title bar is tinted");
                        foreach(var item in NavigationItems(form).Where(x=>x.IsNavigationChip && x.IsVisible))
                        {
                            var chrome=item.Children.OfType<PCL.Core.UI.Controls.LiquidGlassSurface>().Single();
                            if(chrome.BorderThickness!=new System.Windows.Thickness(0))throw new Exception("Legacy navigation border");
                            if(item.Checked && chrome.Visibility!=System.Windows.Visibility.Visible)throw new Exception("Missing glass selection");
                        }
                        LiquidGlassSmoke.Capture(form,Path.Combine(output,$"navigation-style-{dark}-{size.Width}-{page}.png"));
                        report.Add($"PASS neutral title and glass categories: dark={dark}, width={size.Width}, page={page}");
                    }
                }
                File.WriteAllLines(Path.Combine(Root,"navigation-style-report.txt"),report);return;
            }
            if(File.Exists(Path.Combine(AppContext.BaseDirectory,"cards-only.flag")))
            {
                await LiquidGlassSmoke.RunCards(form,output,report);
                File.WriteAllLines(Path.Combine(Root,"cards-report.txt"),report);return;
            }
            if(File.Exists(Path.Combine(AppContext.BaseDirectory,"glass-perf.flag")))
            {
                await LiquidGlassSmoke.RunPerformance(form,report);
                File.WriteAllLines(Path.Combine(Root,"glass-perf-report.txt"),report);return;
            }
            if(File.Exists(Path.Combine(AppContext.BaseDirectory,"settings-only.flag")))
            {
                await AuditSettingsLayouts(form,output,report);
                File.WriteAllLines(Path.Combine(Root,"settings-report.txt"),report);return;
            }
            if(File.Exists(Path.Combine(AppContext.BaseDirectory,"glass-only.flag")))
            {
                await LiquidGlassSmoke.Run(form, output, report);
                File.WriteAllLines(Path.Combine(Root,"glass-report.txt"),report);return;
            }
            if(File.Exists(Path.Combine(AppContext.BaseDirectory,"background-only.flag")))
            {
                await BackgroundSmoke.Run(form,Root,report);
                File.WriteAllLines(Path.Combine(Root,"background-report.txt"),report);return;
            }
            if(File.Exists(Path.Combine(AppContext.BaseDirectory,"install-header-only.flag")))
            {
                var page=new PageDownloadInstall();
                var header=page.PanInstallHeader;
                ((System.Windows.Controls.Panel)header.Parent).Children.Remove(header);
                typeof(PageDownloadInstall).GetField("isSelectNameChanging",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance)!.SetValue(page,true);
                page.TextSelectName.Text="1.21.1-Fabric";
                foreach(var dark in new[]{false,true})
                {
                    Config.Preference.Theme.ColorMode=dark?ColorMode.Dark:ColorMode.Light;
                    ThemeService.RefreshColorMode();FluentTheme.Refresh();
                    foreach(var width in new[]{810d,1280d})
                    foreach(var caption in new[]{"开始下载","Start download","Download starten"})
                    {
                        page.BtnStart.Text=caption;
                        var host=new System.Windows.Controls.Border {Width=width-270,Height=62,Background=(Brush)System.Windows.Application.Current.FindResource("ColorBrushBg1"),Child=header};
                        host.Measure(new System.Windows.Size(host.Width,host.Height));host.Arrange(new System.Windows.Rect(0,0,host.Width,host.Height));host.UpdateLayout();
                        var button=page.BtnStart;var text=page.TextSelectName;
                        var buttonPoint=button.TranslatePoint(new System.Windows.Point(),host);
                        var textPoint=text.TranslatePoint(new System.Windows.Point(),host);
                        if(text.ActualWidth<160 || buttonPoint.X<textPoint.X+text.ActualWidth+11 || buttonPoint.X+button.ActualWidth>host.Width || buttonPoint.Y<0 || buttonPoint.Y+button.ActualHeight>host.Height)
                            throw new Exception("Installation header overlaps or clips: "+width+", "+caption);
                        if(Math.Abs(buttonPoint.Y+button.ActualHeight/2-(textPoint.Y+text.ActualHeight/2))>1)throw new Exception("Download button not aligned with name field");
                        var bitmap=new RenderTargetBitmap((int)host.Width,62,96,96,PixelFormats.Pbgra32);bitmap.Render(host);
                        var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(bitmap));
                        using(var file=File.Create(Path.Combine(output,$"install-header-{dark}-{width}-{caption.Length}.png")))png.Save(file);
                        report.Add($"PASS inline download action: dark={dark}, window={width}, caption={caption}, nameWidth={text.ActualWidth:0}");
                        host.Child=null;
                    }
                }
                File.WriteAllLines(Path.Combine(Root,"install-header-report.txt"),report);return;
            }
            if(File.Exists(Path.Combine(AppContext.BaseDirectory,"modrinth-only.flag")))
            {
                var providerLogs=new System.Collections.Concurrent.ConcurrentQueue<string>();
                void CaptureSources(PCL.Core.Logging.LogLevel level,string message,string? module,Exception? error)
                {
                    if(message.Contains("api.curseforge.com") || message.Contains("mod.mcimirror.top") || message.Contains("doh.pysio.online"))providerLogs.Enqueue(message);
                }
                PCL.Core.Logging.LogWrapper.OnLog+=CaptureSources;
                try
                {
                    foreach(var url in new[]{"https://api.curseforge.com/v1/mods", "https://mod.mcimirror.top/curseforge/v1/mods"})
                    {
                        try {ModDownload.DlModRequest(url);throw new Exception("Removed API was not blocked");}
                        catch(NotSupportedException) {report.Add("PASS removed resource API blocked before network: "+new Uri(url).Host);}
                    }
                    if(ModComp.ResolveLinkToProjectId("https://www.curseforge.com/minecraft/mc-mods/jei") is not null)throw new Exception("Removed resource link resolved");
                    if(ModComp.CompRequest.GetCompProjectsByIds(new(){"123456"}).Count!=0)throw new Exception("Legacy provider favorite fetched");
                    report.Add("PASS legacy resource links and numeric favorite IDs do not fetch removed provider");
                    var pages=new PageComp[]{new PageDownloadMod().Content,new PageDownloadPack().Content,new PageDownloadResourcePack().Content,new PageDownloadShader().Content,new PageDownloadDataPack().Content};
                    foreach(var page in pages)
                    {
                        if(page.FindName("ComboSearchSource") is not null)throw new Exception("Provider selector remains");
                        foreach(var option in page.ComboSearchTag.Items.OfType<MyComboBoxItem>())
                            if(option.Tag?.ToString() is {Length:>0} tag && tag.EndsWith('/'))throw new Exception("Removed provider-only category remains: "+tag);
                    }
                    var searches=new List<ModLoader.LoaderTask<ModComp.CompProjectRequest,int>>();
                    foreach(var type in new[]{ModComp.CompType.Mod,ModComp.CompType.ModPack,ModComp.CompType.ResourcePack,ModComp.CompType.Shader,ModComp.CompType.DataPack})
                    {
                        var request=new ModComp.CompProjectRequest(type,new(),40){source=(ModComp.CompSourceType)1};
                        var search=new ModLoader.LoaderTask<ModComp.CompProjectRequest,int>("Modrinth regression "+type,ModComp.CompProjectsGet);
                        searches.Add(search);search.Start(request);
                    }
                    var deadline=DateTime.UtcNow.AddSeconds(45);
                    while(searches.Any(s=>s.State==ModBase.LoadState.Loading) && DateTime.UtcNow<deadline)await Task.Delay(100);
                    foreach(var search in searches)
                    {
                        if(search.State!=ModBase.LoadState.Finished)throw new Exception("Live search failed: "+search.name,search.Error);
                        if(search.input.storage.results.Count==0 || search.input.storage.results.Any(p=>p.FromCurseForge))throw new Exception("Invalid Modrinth search results");
                        if(search.input.source!=ModComp.CompSourceType.Modrinth)throw new Exception("Stale source selection not normalized");
                        report.Add($"PASS live {search.input.type} search: {search.input.storage.results.Count} Modrinth results");
                    }
                    var filterRequest=new ModComp.CompProjectRequest(ModComp.CompType.Mod,new(),40){tag="406/adventure",modLoader=ModComp.CompLoaderType.Fabric,gameVersion="1.21.1"};
                    var filtered=await Task.Run(()=>ModDownload.DlModRequest<System.Text.Json.Nodes.JsonObject>(filterRequest.GetModrinthAddress()));
                    if(filtered["hits"]!.AsArray().Count==0)throw new Exception("Filtered Modrinth search returned no matches");
                    report.Add("PASS live combined category/loader/game-version filtering");
                    var shaderRequest=new ModComp.CompProjectRequest(ModComp.CompType.Shader,new(),40){tag="/fantasy/iris"};
                    var shaderUrl=Uri.UnescapeDataString(shaderRequest.GetModrinthAddress());
                    if(!shaderUrl.Contains("categories:fantasy") || !shaderUrl.Contains("categories:iris"))throw new Exception("Shader filters overwritten");
                    var project=searches[0].input.storage.results.First();
                    var files=await Task.Run(()=>ModComp.CompFilesGet(project.Id,false));
                    if(files.Count==0 || files.Any(f=>f.FromCurseForge || f.DownloadUrls.Any(u=>u.Contains("mcimirror"))))throw new Exception("File list contains removed source");
                    report.Add($"PASS live project/version/dependency lookup: {files.Count} Modrinth files");
                    form.PageChange(FormMain.PageType.Download,FormMain.PageSubType.DownloadMod);await Task.Delay(1200);
                    if(ModMain.frmDownloadLeft.FindName("ItemWorld") is not null)throw new Exception("Unsupported world download entry remains");
                    foreach(var page in pages)
                    {
                        form.PageChange(FormMain.PageType.Download,page.PageType switch
                        {
                            ModComp.CompType.Mod=>FormMain.PageSubType.DownloadMod,ModComp.CompType.ModPack=>FormMain.PageSubType.DownloadPack,
                            ModComp.CompType.ResourcePack=>FormMain.PageSubType.DownloadResourcePack,ModComp.CompType.Shader=>FormMain.PageSubType.DownloadShader,
                            _=>FormMain.PageSubType.DownloadDataPack
                        });
                        await Task.Delay(650);
                    }
                    if(providerLogs.Any())throw new Exception("Removed provider used: "+providerLogs.First());
                    report.Add("PASS download UI navigation and no requests to removed APIs, mirror or expired DoH");
                    File.WriteAllLines(Path.Combine(Root,"modrinth-report.txt"),report);
                }
                finally {PCL.Core.Logging.LogWrapper.OnLog-=CaptureSources;}
                return;
            }
            if(File.Exists(Path.Combine(AppContext.BaseDirectory,"network-only.flag")))
            {
                var capture=new System.Collections.Concurrent.ConcurrentQueue<string>();
                void Capture(PCL.Core.Logging.LogLevel level,string message,string? module,Exception? error)
                {
                    if(module is "HTTP diagnostic" or "DoH query" or "HostConnectionHandler")capture.Enqueue($"{module}: {message}\n{error}");
                }
                PCL.Core.Logging.LogWrapper.OnLog+=Capture;
                try
                {
                    capture.Enqueue($"Resource source=Modrinth; EnableDoH={Config.Network.EnableDoH}");
                    var jobs=new List<Task>();
                    foreach(var url in new[]{"https://api.modrinth.com/v2/search?limit=1"})
                        jobs.Add(Task.Run(async ()=>
                        {
                            try {await PCL.Network.Requester.FetchStringAsync(url,new PCL.Network.RequestParam {Timeout=12000});capture.Enqueue("API success: "+new Uri(url).Host);}
                            catch(Exception ex){capture.Enqueue("API failure: "+new Uri(url).Host+"\n"+ex);}
                        }));
                    foreach(var url in new[]{"https://dns.alidns.com/dns-query","https://maven.neoforged.net/releases/net/neoforged/neoforge/maven-metadata.xml"})
                    foreach(var customDns in new[]{false,true})
                    {
                        if(customDns && !url.Contains("neoforged"))continue;
                        jobs.Add(Task.Run(async ()=>
                        {
                            using var handler=new System.Net.Http.SocketsHttpHandler {Proxy=PCL.Core.IO.Net.Http.HttpProxyManager.Instance};
                            if(customDns)handler.ConnectCallback=PCL.Core.IO.Net.Http.HostConnectionHandler.Instance.GetConnectionAsync;
                            handler.SslOptions.RemoteCertificateValidationCallback=(_,certificate,chain,errors)=>
                            {
                                capture.Enqueue($"TLS {new Uri(url).Host}, DoH={customDns}; errors={errors}; subject={certificate?.Subject}; issuer={certificate?.Issuer}; validFrom={certificate?.GetEffectiveDateString()}; validTo={certificate?.GetExpirationDateString()}; chain={string.Join(",",chain?.ChainStatus.Select(s=>s.Status.ToString())??[])}");
                                return errors==System.Net.Security.SslPolicyErrors.None;
                            };
                            using var client=new System.Net.Http.HttpClient(handler){Timeout=TimeSpan.FromSeconds(15)};
                            try {using var response=await client.GetAsync(url);capture.Enqueue($"HTTPS {new Uri(url).Host}, DoH={customDns}: {(int)response.StatusCode}");}
                            catch(Exception ex){capture.Enqueue($"HTTPS {new Uri(url).Host}, DoH={customDns}: {ex}");}
                        }));
                    }
                    foreach(var provider in new[]{"https://dns.alidns.com/"})
                    foreach(var queryType in new[]{Ae.Dns.Protocol.Enums.DnsQueryType.A,Ae.Dns.Protocol.Enums.DnsQueryType.AAAA})
                        jobs.Add(Task.Run(async ()=>
                        {
                            using var handler=new System.Net.Http.HttpClientHandler {Proxy=PCL.Core.IO.Net.Http.HttpProxyManager.Instance};
                            using var client=new System.Net.Http.HttpClient(handler){BaseAddress=new Uri(provider),Timeout=TimeSpan.FromSeconds(12)};
                            using var resolver=new Ae.Dns.Client.DnsHttpClient(client);
                            try
                            {
                                var answer=await resolver.Query(Ae.Dns.Protocol.DnsQueryFactory.CreateQuery("maven.neoforged.net",queryType),default);
                                capture.Enqueue($"DNS provider={provider}; type={queryType}; addresses={string.Join(",",answer.Answers.Select(a=>a.Resource).OfType<Ae.Dns.Protocol.Records.DnsIpAddressResource>().Select(a=>a.IPAddress.ToString()))}");
                            }
                            catch(Exception ex){capture.Enqueue($"DNS provider={provider}; type={queryType}; {ex}");}
                        }));
                    jobs.Add(Task.Run(async ()=>
                    {
                        try {var ips=await System.Net.Dns.GetHostAddressesAsync("maven.neoforged.net");capture.Enqueue("System DNS maven.neoforged.net: "+string.Join(",",ips.Select(ip=>ip.ToString())));}
                        catch(Exception ex){capture.Enqueue("System DNS failure: "+ex);}
                    }));
                    await Task.WhenAll(jobs);
                    File.WriteAllLines(Path.Combine(Root,"network-report.txt"),capture);
                }
                finally {PCL.Core.Logging.LogWrapper.OnLog-=Capture;}
                return;
            }
            if(File.Exists(Path.Combine(AppContext.BaseDirectory,"downloads-only.flag")))
            {
                foreach(var dark in new[]{false,true})
                {
                    Config.Preference.Theme.ColorMode=dark?ColorMode.Dark:ColorMode.Light;
                    ThemeService.RefreshColorMode();FluentTheme.Refresh();
                    foreach(var type in Enum.GetValues<ModComp.CompType>().Where(t=>t!=ModComp.CompType.Any))
                    foreach(var source in new[]{"Modrinth","CurseForge"})
                    foreach(var version in new[]{false,true})
                    foreach(var loader in new[]{false,true})
                    {
                        var project=new ModComp.CompProject(new System.Text.Json.Nodes.JsonObject
                        {
                            ["Tags"]=new System.Text.Json.Nodes.JsonArray(), ["DataSource"]=source,
                            ["Type"]=(int)type, ["Slug"]="download-regression", ["Id"]="download-regression",
                            ["RawName"]="Download regression", ["Description"]="Local fixture",
                            ["Website"]="https://example.invalid", ["DownloadCount"]=123
                        });
                        var placeholder=project.ToCompItem(version,loader,true);
                        var panel=new System.Windows.Controls.StackPanel();panel.Children.Add(placeholder);
                        var item=placeholder.Init();
                        if(panel.Children[0]!=item || item.Title!="Download regression")throw new Exception("Resource item initialization failed");
                        if(item.LabTitleRaw.Visibility!=System.Windows.Visibility.Collapsed)throw new Exception("Empty subtitle visible");
                        item.SubTitle="Original title";
                        if(item.LabTitleRaw.Visibility!=System.Windows.Visibility.Visible)throw new Exception("Subtitle cannot be restored");
                        item.SubTitle="";
                        if(item.LabTitleRaw.Visibility!=System.Windows.Visibility.Collapsed)throw new Exception("Subtitle cannot be hidden");
                        if(!version && !loader && item.LabVersion.Visibility!=System.Windows.Visibility.Collapsed)throw new Exception("Version metadata visible");
                        if(placeholder.Height!=item.Height)throw new Exception("Virtualized resource row height mismatch");
                        report.Add($"PASS resource item {type}, {source}, version={version}, loader={loader}, dark={dark}");
                    }
                    foreach(var page in new[]{new PageDownloadMod().Content,new PageDownloadPack().Content,new PageDownloadResourcePack().Content,new PageDownloadShader().Content,new PageDownloadWorld().Content,new PageDownloadDataPack().Content})
                    {
                        typeof(PageComp).GetMethod("Load_OnFinish",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance)!.Invoke(page,null);
                        if(page.PanProjects.Children.Count!=0 || page.CardPages.Visibility!=System.Windows.Visibility.Collapsed)throw new Exception("Empty search result invalid");
                    }
                    report.Add($"PASS six resource page constructors and empty search results, dark={dark}");
                }
                foreach(var destinationName in Enum.GetNames<FormMain.PageSubType>().Where(p=>p.StartsWith("Download") && p!="DownloadWorld"))
                {
                    var destination=Enum.Parse<FormMain.PageSubType>(destinationName);
                    form.PageChange(FormMain.PageType.Download,destination);
                    await Task.Delay(650);form.UpdateLayout();
                    if(form.PanMainRight.Child is not MyPageRight)throw new Exception("Missing download page: "+destination);
                    report.Add("PASS download navigation "+destinationName);
                }
                await Task.Delay(12000);
                if(form.PanMsg.Children.OfType<MyMsgText>().Any())throw new Exception("Unexpected resource error dialog");
                File.WriteAllLines(Path.Combine(Root,"downloads-report.txt"),report);return;
            }
            if(File.Exists(Path.Combine(AppContext.BaseDirectory,"tools-only.flag")))
            {
                foreach(var dark in new[]{false,true})
                {
                    Config.Preference.Theme.ColorMode=dark?ColorMode.Dark:ColorMode.Light;
                    ThemeService.RefreshColorMode();FluentTheme.Refresh();
                    foreach(var size in new[]{(810d,470d),(1350d,825d)})
                    {
                        form.Width=size.Item1;form.Height=size.Item2;
                        form.PageChange(FormMain.PageType.Tools);await Task.Delay(900);form.UpdateLayout();
                        if(form.CategoryColumn.ActualWidth>1 || form.CategoryHost.Visibility!=System.Windows.Visibility.Collapsed)
                            throw new Exception("Tools retains empty sidebar space");
                        if(Math.Abs(form.ContentHost.ActualWidth-form.PanMain.ActualWidth)>1)
                            throw new Exception("Tools does not fill content width");
                        var tools=(PageToolsTest)form.PanMainRight.Child;
                        foreach(var button in tools.UtilitiesActions.Children.OfType<MyButton>().Where(b=>b.Visibility==System.Windows.Visibility.Visible))
                        {
                            var point=button.TranslatePoint(new System.Windows.Point(),tools.UtilitiesActions);
                            if(button.ActualHeight<40 || point.Y+button.ActualHeight>tools.UtilitiesActions.ActualHeight+0.5)
                                throw new Exception("Utilities button clipped: "+button.Text);
                        }
                        var gap=form.BtnTitleMin.TranslatePoint(new System.Windows.Point(),form.WindowControls).X-form.BtnTitleClose.TranslatePoint(new System.Windows.Point(),form.WindowControls).X-13;
                        if(Math.Abs(gap-10.4)>0.1)throw new Exception("Traffic light gap is not reduced by 20%: "+gap);
                        if(form.BtnTitleClose.ToolTip is not null || form.BtnTitleMin.ToolTip is not null || form.BtnTitleZoom.ToolTip is not null)
                            throw new Exception("Traffic light tooltips remain");
                        var bitmap=new RenderTargetBitmap((int)form.ActualWidth,(int)form.ActualHeight,96,96,PixelFormats.Pbgra32);bitmap.Render(form);
                        var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(bitmap));
                        using var file=File.Create(Path.Combine(output,$"tools-fixed-{dark}-{size.Item1}.png"));png.Save(file);
                        report.Add($"PASS Tools fills width, Utilities actions not clipped, traffic light gap and tooltip removal: dark={dark}, size={size}");
                        form.PageChange(FormMain.PageType.Setup);await Task.Delay(650);
                        if(form.CategoryColumn.ActualWidth<190 || form.CategoryHost.Visibility!=System.Windows.Visibility.Visible)
                            throw new Exception("Settings sidebar not restored after leaving Tools");
                    }
                }
                File.WriteAllLines(Path.Combine(Root,"tools-report.txt"),report);return;
            }
            if(File.Exists(Path.Combine(AppContext.BaseDirectory,"no-update.flag")) || File.Exists(Path.Combine(AppContext.BaseDirectory,"no-announcements.flag")))
            {
                var assembly=typeof(FormMain).Assembly;
                foreach(var name in new[]{"PCL.UpdateManager","PCL.PageSetupUpdate","PCL.UpdatesMinioModel","PCL.UpdatesMirrorChyanModel","PCL.IUpdateSource","PCL.AnnouncementService","PCL.VersionAnnouncementDataModel","PCL.VersionAnnouncementContentModel","PCL.AnnouncementBtnInfoModel"})
                    if(assembly.GetType(name) is not null)throw new Exception("Updater still compiled: "+name);
                foreach(var name in new[]{"PCL.Core.App.Essentials.UpdateService","PCL.Core.App.Essentials.UpdateHelper"})
                    if(typeof(Config).Assembly.GetType(name) is not null)throw new Exception("Installer still compiled: "+name);
                report.Add("PASS updater UI, providers, installer and announcement service/models absent from built assemblies");
                if(States.System.GetType().GetProperty("AnnounceSolution") is not null || States.Hint.GetType().GetProperty("ShowedAnnouncements") is not null)
                    throw new Exception("Announcement state configuration remains");
                foreach(var dark in new[]{false,true})
                {
                    Config.Preference.Theme.ColorMode=dark?ColorMode.Dark:ColorMode.Light;
                    ThemeService.RefreshColorMode();FluentTheme.Refresh();
                    form.Width=1000;form.Height=700;
                    foreach(var page in new[]{FormMain.PageSubType.SetupLaunch,FormMain.PageSubType.SetupJava,FormMain.PageSubType.SetupGameManage,FormMain.PageSubType.SetupUI,FormMain.PageSubType.SetupLauncherLanguage,FormMain.PageSubType.SetupLauncherMisc,FormMain.PageSubType.SetupAbout,FormMain.PageSubType.SetupFeedback,FormMain.PageSubType.SetupLog})
                    {
                        form.PageChange(FormMain.PageType.Setup,page);await Task.Delay(650);form.UpdateLayout();
                        if(form.PanMainRight.Child is null)throw new Exception("Missing settings content: "+page);
                        report.Add($"PASS settings {page}, dark={dark}");
                    }
                    if(ModMain.frmSetupLauncherMisc.FindName("ComboSystemActivity") is not null)
                        throw new Exception("Announcement settings remain");
                    if(ModMain.frmSetupUI.ComboCustomPreset.Items.Count!=15)
                        throw new Exception("Homepage announcement preset remains or other presets were removed");
                    Config.Preference.Homepage.SelectedPreset=14;
                    ModMain.frmSetupUI.Reload();
                    if(ModMain.frmSetupUI.ComboCustomPreset.SelectedIndex!=14)throw new Exception("Removed preset does not map to feed");
                    ModMain.frmSetupUI.ComboCustomPreset.SelectedIndex=0;
                    ModMain.frmSetupUI.ComboCustomPreset.SelectedIndex=14;
                    if(Config.Preference.Homepage.SelectedPreset!=15)throw new Exception("Minecraft feed preset ID changed");
                    report.Add("PASS announcement setting removed; old homepage selection maps to feed and selection persists stable feed ID");
                    if(ModMain.frmSetupLeft.PanItem.Children.OfType<MyListItem>().Any(item=>ModBase.Val(item.Tag)==8))
                        throw new Exception("Update navigation item remains");
                    form.PageChange(FormMain.PageType.Setup,FormMain.PageSubType.SetupUI);await Task.Delay(650);
                    var bitmap=new RenderTargetBitmap((int)form.ActualWidth,(int)form.ActualHeight,96,96,PixelFormats.Pbgra32);bitmap.Render(form);
                    var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(bitmap));
                    using var file=File.Create(Path.Combine(output,$"no-update-{dark}.png"));png.Save(file);
                }
                File.WriteAllLines(Path.Combine(Root,"no-announcements-report.txt"),report);return;
            }
            if(File.Exists(Path.Combine(AppContext.BaseDirectory,"dock-only.flag")))
            {
                form.Width=1000;form.Height=700;
                foreach(var dark in new[]{false,true})
                {
                    Config.Preference.Theme.ColorMode=dark?ColorMode.Dark:ColorMode.Light;
                    ThemeService.RefreshColorMode();FluentTheme.Refresh();
                    form.PageChange(FormMain.PageType.Setup);await Task.Delay(800);
                    form.PageChange(FormMain.PageType.Launch);await Task.Delay(600);
                    var contentHeight=form.PanMain.ActualHeight;
                    form.PageChange(FormMain.PageType.Setup);
                    await Task.Delay(155);
                    form.UpdateLayout();
                    if(System.Windows.SystemParameters.ClientAreaAnimation && form.DockHighlightScale.ScaleX<=1.05)
                        throw new Exception($"Upstream press spring did not expand the thumb: scale={form.DockHighlightScale.ScaleX}");
                    void Capture(string phase)
                    {
                        var bitmap=new RenderTargetBitmap((int)form.ActualWidth,(int)form.ActualHeight,96,96,PixelFormats.Pbgra32);bitmap.Render(form);
                        var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(bitmap));
                        using var file=File.Create(Path.Combine(output,$"dock-{dark}-{phase}.png"));png.Save(file);
                        LiquidGlassSmoke.Capture(form,Path.Combine(output,$"dock-native-{dark}-{phase}.png"));
                    }
                    Capture("stretch");
                    form.PageChange(FormMain.PageType.Download);await Task.Delay(70);
                    form.PageChange(FormMain.PageType.InstanceSelect);await Task.Delay(650);
                    var selected=form.DockInstances.TranslatePoint(new System.Windows.Point(),form.DockSurface);
                    if(Math.Abs(System.Windows.Controls.Canvas.GetLeft(form.DockHighlight)-selected.X)>1 || Math.Abs(form.DockHighlight.Width-form.DockInstances.ActualWidth)>1)
                        throw new Exception("Interrupted highlight does not settle on current selection");
                    if(Math.Abs(contentHeight-form.PanMain.ActualHeight)>1) throw new Exception("Highlight animation resizes content viewport");
                    Capture("settled");
                    var controller=form.LiquidDock ?? throw new Exception("Dock controller not initialized");
                    var cellWidth=form.DockInstances.ActualWidth;
                    var downX=System.Windows.Controls.Canvas.GetLeft(form.DockHighlight)+cellWidth/2;
                    if(!controller.BeginDrag(new System.Windows.Point(downX,32),capture:false))throw new Exception("Selected thumb did not accept a drag");
                    await Task.Delay(130);controller.DragTo(downX+1.6*cellWidth);await Task.Delay(180);Capture("drag");
                    if(Math.Abs(controller.Motion.Position.Target-2.6)>.02)throw new Exception($"Dragging did not follow equal-width tab coordinates: target={controller.Motion.Position.Target}, count={controller.Motion.Count}, width={cellWidth}");
                    controller.FinishDrag();await Task.Delay(1500);form.UpdateLayout();Capture("released");
                    if(form.DockTools.IsChecked!=true || controller.Motion.Active || controller.IsRendering)throw new Exception("Release did not settle on nearest tab and stop rendering");
                    downX=System.Windows.Controls.Canvas.GetLeft(form.DockHighlight)+cellWidth/2;
                    controller.BeginDrag(new System.Windows.Point(downX,32),capture:false);controller.DragTo(downX-.6*cellWidth);await Task.Delay(90);
                    form.PrimaryNavigation.RaiseEvent(new System.Windows.Input.MouseEventArgs(System.Windows.Input.Mouse.PrimaryDevice,Environment.TickCount) { RoutedEvent=System.Windows.Input.Mouse.LostMouseCaptureEvent });await Task.Delay(1500);
                    if(form.DockDownload.IsChecked!=true || controller.Motion.Active || controller.IsRendering)throw new Exception("Capture cancellation did not snap and release the thumb");
                    form.DockPanel.GlassEnabled=false;form.DockHighlight.GlassEnabled=false;await Task.Delay(150);Capture("fallback");
                    form.DockPanel.SetResourceReference(PCL.Core.UI.Controls.LiquidGlassSurface.GlassEnabledProperty,"LiquidGlass.Enabled");
                    form.DockHighlight.SetResourceReference(PCL.Core.UI.Controls.LiquidGlassSurface.GlassEnabledProperty,"LiquidGlass.Enabled");
                    report.Add($"PASS upstream press springs, interrupted click, drag follow, nearest-tab snap, capture cancellation, settled idle and full content height: dark={dark}");
                }
                File.WriteAllLines(Path.Combine(Root,"dock-report.txt"),report);return;
            }
            if(File.Exists(Path.Combine(AppContext.BaseDirectory,"title-only.flag")))
            {
                foreach(var dark in new[]{false,true})
                {
                    Config.Preference.Theme.ColorMode=dark?ColorMode.Dark:ColorMode.Light;
                    ThemeService.RefreshColorMode(); FluentTheme.Refresh();
                    foreach(var size in new[]{(810d,470d),(1350d,825d)})
                    {
                        form.Width=size.Item1;form.Height=size.Item2;
                        foreach(var page in new[]{FormMain.PageType.Launch,FormMain.PageType.Setup,FormMain.PageType.InstanceSelect})
                        {
                            form.PageChange(page); await Task.Delay(650); form.UpdateLayout();
                            if(form.BtnTitleClose.TranslatePoint(new System.Windows.Point(),form.PanTitle).X>=form.BtnTitleMin.TranslatePoint(new System.Windows.Point(),form.PanTitle).X)
                                throw new Exception("Traffic lights not in macOS order");
                            if(form.WindowControls.ActualWidth>100 || form.WindowControls.ActualHeight>form.PanTitle.ActualHeight)
                                throw new Exception("Window controls overflow title bar");
                            var bitmap=new RenderTargetBitmap((int)form.ActualWidth,(int)form.ActualHeight,96,96,PixelFormats.Pbgra32);bitmap.Render(form);
                            var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(bitmap));
                            using var file=File.Create(Path.Combine(output,$"title-{dark}-{size.Item1}-{page}.png"));png.Save(file);
                            report.Add($"PASS title {dark} {size} {page}");
                        }
                    }
                }
                var before=new System.Windows.Rect(form.Left,form.Top,form.Width,form.Height);
                form.BtnTitleZoom.RaiseEvent(new System.Windows.RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
                form.BtnTitleZoom.RaiseEvent(new System.Windows.RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
                if(Math.Abs(form.Width-before.Width)>1 || Math.Abs(form.Height-before.Height)>1 || Math.Abs(form.Left-before.Left)>1 || Math.Abs(form.Top-before.Top)>1)
                    throw new Exception("Zoom does not restore bounds");
                report.Add("PASS zoom/restore returns original bounds");
                form.BtnTitleMin.RaiseEvent(new System.Windows.RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
                if(form.WindowState!=System.Windows.WindowState.Minimized)throw new Exception("Minimize failed");
                form.WindowState=System.Windows.WindowState.Normal;
                report.Add("PASS minimize");
                File.WriteAllLines(Path.Combine(Root,"title-report.txt"),report);
                return;
            }
            if(File.Exists(Path.Combine(AppContext.BaseDirectory,"lists-only.flag")))
            {
                await AuditRemainingListToolbars(output,report);
                AuditServerCard(output,report);
                await AuditLogLayout(output, report);
                File.WriteAllLines(Path.Combine(Root,"report.txt"),report);
                return;
            }
            // Let WPF measure real glyphs; no string-length approximation or resize polling.
            var sizingHost=new System.Windows.Controls.Grid { Width=900, Height=100, HorizontalAlignment=System.Windows.HorizontalAlignment.Left, VerticalAlignment=System.Windows.VerticalAlignment.Top, IsHitTestVisible=false };
            System.Windows.Controls.Grid.SetRowSpan(sizingHost,3);
            form.PanForm.Children.Add(sizingHost);
            var sizingButton=new MyButton(); sizingHost.Children.Add(sizingButton);
            var lastWidth=0d;
            foreach(var label in new[]{"确定","Open instance folder","Instanzverzeichnis öffnen","100%","0%"})
            {
                sizingButton.Text=label;
                sizingHost.UpdateLayout();
                // Current buttons are borderless; width is real text + 12px padding per side.
                var expected=sizingButton.LabText.DesiredSize.Width+24;
                if(!double.IsNaN(sizingButton.Width) || Math.Abs(sizingButton.ActualWidth-expected)>1)
                    throw new Exception($"Button width is not text + 12px padding per side: {label}: {sizingButton.ActualWidth} vs {expected}");
                if(label=="0%" && sizingButton.ActualWidth>=lastWidth) throw new Exception("Dynamic button text does not shrink");
                lastWidth=sizingButton.ActualWidth;
            }
            form.PanForm.Children.Remove(sizingHost);
            report.Add("PASS localized and dynamically changing button labels resize to real glyphs plus 12px on each side");
            foreach(var dark in new[] {false,true})
            {
                Config.Preference.Theme.ColorMode = dark ? ColorMode.Dark : ColorMode.Light;
                ThemeService.RefreshColorMode();
                await Task.Delay(300);
                foreach(var size in new[] {(810d,470d),(1280d,800d)})
                {
                    form.Width=size.Item1;form.Height=size.Item2;
                    foreach(var entry in new[] {FormMain.PageType.Launch,FormMain.PageType.InstanceSelect,FormMain.PageType.Download,FormMain.PageType.Tools,FormMain.PageType.Setup,FormMain.PageType.TaskManager})
                    {
                        form.PageChange(entry);
                        await Task.Delay(650);
                        form.UpdateLayout();
                        var contentBottom=form.PanMain.TransformToAncestor(form.PanForm).Transform(new System.Windows.Point(0,form.PanMain.ActualHeight)).Y;
                        var navigationTop=form.PrimaryNavigation.TransformToAncestor(form.PanForm).Transform(new System.Windows.Point(0,0)).Y;
                        if(Math.Abs(contentBottom-form.PanForm.ActualHeight)>1) throw new Exception($"Content does not extend to window bottom: {entry}, {size}");
                        if(navigationTop>=contentBottom || System.Windows.Controls.Panel.GetZIndex(form.PrimaryNavigation)<=System.Windows.Controls.Panel.GetZIndex(form.PanMain))
                            throw new Exception($"Navigation is not floating above content: {entry}, {size}");
                        if(form.PanMainRight.ActualHeight < 100) throw new Exception($"Content viewport too short: {entry}, {size}");
                        foreach(var scale in new[]{1d,1.5d,2d})
                        {
                            var bitmap=new RenderTargetBitmap((int)(form.ActualWidth*scale),(int)(form.ActualHeight*scale),96*scale,96*scale,PixelFormats.Pbgra32);
                            bitmap.Render(form);
                            var encoder=new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
                            using var file=File.Create(Path.Combine(output,$"{(dark?"dark":"light")}-{size.Item1}-{entry}-{scale}.png"));encoder.Save(file);
                        }
                        report.Add($"PASS {entry} {(dark?"dark":"light")} {size.Item1}x{size.Item2}: floating navigation overlap {contentBottom-navigationTop:0}px; content height {form.PanMainRight.ActualHeight:0}px; raster 100/150/200%");
                        LiquidGlassSmoke.Capture(form,Path.Combine(output,$"material-{(dark?"dark":"light")}-{size.Item1}-{entry}.png"));
                    }
                }
            }
            form.PageChange(FormMain.PageType.Launch);
            await Task.Delay(700);
            ModMain.frmLaunchLeft.ShowAccount();
            await Task.Delay(200);
            if(!ModMain.frmLaunchLeft.AccountPopup.IsOpen) throw new Exception("Account popup did not open");
            ModMain.frmLaunchLeft.AccountPopup.IsOpen=false;
            report.Add("PASS account popup opens after navigating back from another section");
            var oldBlur=Config.Preference.Blur.IsEnabled;
            Config.Preference.Blur.IsEnabled=false; FluentTheme.Refresh();
            if((double)System.Windows.Application.Current.Resources["Fluent.BlurRadius"]!=0) throw new Exception("Explicit blur-off preference ignored");
            Config.Preference.Blur.IsEnabled=oldBlur;FluentTheme.Refresh();
            report.Add("PASS existing material preference disables Fluent blur without changing other appearance options");
            var actionField=typeof(PageLaunchLeft).GetField("_launchButtonAction", System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!;
            var oldAction=actionField.GetValue(ModMain.frmLaunchLeft);
            actionField.SetValue(ModMain.frmLaunchLeft,Enum.Parse(actionField.FieldType,"Launch"));
            ModMain.frmLaunchLeft.BtnLaunch.IsEnabled=true;
            ModMain.frmLaunchLeft.LaunchButtonClick();
            if(!ModMain.frmLaunchLeft.AccountPopup.IsOpen) throw new Exception("No-account launch action does not open account entry");
            ModMain.frmLaunchLeft.AccountPopup.IsOpen=false;
            actionField.SetValue(ModMain.frmLaunchLeft,oldAction);
            report.Add("PASS launch action opens account entry when no profile exists");
            form.Width=810; form.Height=470;
            form.PageChange(FormMain.PageType.Setup);
            await Task.Delay(700);
            ModMain.frmSetupLeft.OpenCategory(FormMain.PageSubType.SetupLaunch);
            await Task.Delay(700);
            if(form.pageRight is not PageSetupLaunch) throw new Exception("First settings category cannot be opened");
            if(form.PanMainLeft.Visibility!=System.Windows.Visibility.Visible) throw new Exception("Settings category strip is hidden");
            report.Add("PASS Settings opens its category strip and Launch content directly");
            var categories=(MyScrollViewer)ModMain.frmSetupLeft.Children[0];
            categories.ScrollToBottom(); await Task.Delay(100);
            if(categories.VerticalOffset<=0 || categories.HorizontalOffset>0) throw new Exception("Settings sidebar does not scroll vertically");
            report.Add("PASS narrow settings sidebar exposes its final category with vertical scrolling");
            if(form.CategoryActions.Children.Count==0) throw new Exception("Selected category lost its reset action");
            var beforeAccount=form.pageCurrent;
            ModMain.frmLaunchLeft.ShowAccount(); await Task.Delay(100);
            if(!ModMain.frmLaunchLeft.AccountPopup.IsOpen || !ReferenceEquals(beforeAccount, form.pageCurrent)) throw new Exception("Account popup changes the active page");
            ModMain.frmLaunchLeft.AccountPopup.IsOpen=false;
            report.Add("PASS account popup opens from Settings without changing page or history");
            form.DockSettings.RaiseEvent(new System.Windows.RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
            await Task.Delay(700);
            if(form.pageRight is not PageSetupLaunch || form.PanMainLeft.Visibility!=System.Windows.Visibility.Visible)
                throw new Exception("Active Settings Dock item lost its category/content layout");
            ModMain.frmSetupLeft.OpenCategory(FormMain.PageSubType.SetupUI); await Task.Delay(700);
            if(form.pageRight is not PageSetupUI) throw new Exception("Settings category does not change content");
            ModMain.frmSetupLeft.OpenCategory(FormMain.PageSubType.SetupLaunch); await Task.Delay(700);
            if(form.pageRight is not PageSetupLaunch) throw new Exception("Cannot return to the first settings category");
            report.Add("PASS settings categories switch content directly and can return to Launch");
            ModMain.frmSetupLeft.ItemLaunch.Focus();
            ModMain.frmSetupLeft.ItemLaunch.RaiseEvent(new System.Windows.Input.KeyEventArgs(System.Windows.Input.Keyboard.PrimaryDevice,System.Windows.PresentationSource.FromVisual(form),Environment.TickCount,System.Windows.Input.Key.Down) { RoutedEvent=System.Windows.Input.Keyboard.KeyDownEvent });
            await Task.Delay(700);
            if(form.pageRight is not PageSetupJava) throw new Exception("Sidebar Down key does not select the next destination");
            ModMain.frmSetupLeft.ItemLaunch.SetChecked(true,true,true);await Task.Delay(700);
            report.Add("PASS sidebar arrow keys select the next destination and keep it visible");
            if(!ReferenceEquals(ModMain.frmSetupLaunch.PanScroll,ModMain.frmSetupLaunch.PanBack))throw new Exception("Launch settings scroll owner is incorrect");
            ModMain.frmSetupLaunch.PanBack.ScrollToVerticalOffset(220);await Task.Delay(100);
            var launchSettingsOffset=ModMain.frmSetupLaunch.PanBack.VerticalOffset;
            if(launchSettingsOffset<=0)throw new Exception("Launch settings cannot scroll through its options");
            ModMain.frmSetupLeft.OpenCategory(FormMain.PageSubType.SetupUI);await Task.Delay(700);
            ModMain.frmSetupLeft.OpenCategory(FormMain.PageSubType.SetupLaunch);await Task.Delay(700);
            if(Math.Abs(ModMain.frmSetupLaunch.PanBack.VerticalOffset-launchSettingsOffset)>1)throw new Exception("Settings navigation loses the previous scroll position");
            report.Add("PASS launch settings register their real viewport and restore scrolling after category navigation");
            form.PageChange(FormMain.PageType.Launch);await Task.Delay(700);
            ModMain.frmLaunchRight.PanBack.ScrollToVerticalOffset(90);await Task.Delay(100);
            var offset=ModMain.frmLaunchRight.PanBack.VerticalOffset;
            form.PageChange(FormMain.PageType.TaskManager);await Task.Delay(700);
            form.PageBack();await Task.Delay(700);
            if(Math.Abs(ModMain.frmLaunchRight.PanBack.VerticalOffset-offset)>1) throw new Exception("Return lost scroll position");
            report.Add("PASS task return restores homepage scroll position");
            var tiles=new FluentTilePanel();
            for(var i=0;i<5;i++) tiles.Children.Add(new MyListItem {Title="Preview instance "+(i+1), Info="Minecraft 1.21", Height=96});
            tiles.Measure(new System.Windows.Size(720,500));tiles.Arrange(new System.Windows.Rect(0,0,720,tiles.DesiredSize.Height));
            if(System.Windows.Media.VisualTreeHelper.GetOffset(tiles.Children[1]).X<=0 || System.Windows.Media.VisualTreeHelper.GetOffset(tiles.Children[2]).Y<=0)
                throw new Exception("Instance tile layout does not wrap into two columns");
            var tileBitmap=new RenderTargetBitmap(720,360,96,96,PixelFormats.Pbgra32);tileBitmap.Render(tiles);
            var tileEncoder=new PngBitmapEncoder();tileEncoder.Frames.Add(BitmapFrame.Create(tileBitmap));
            using(var tileFile=File.Create(Path.Combine(output,"instance-tiles.png"))) tileEncoder.Save(tileFile);
            report.Add("PASS instance tiles measure, wrap and render using the existing StackPanel population contract");
            var scrollHost=new System.Windows.Controls.Grid { Width=500, Height=200, HorizontalAlignment=System.Windows.HorizontalAlignment.Left, VerticalAlignment=System.Windows.VerticalAlignment.Top };
            System.Windows.Controls.Grid.SetRow(scrollHost,2);
            form.PanForm.Children.Add(scrollHost);
            var fixedHeader=new System.Windows.Controls.Border { Height=40, Background=System.Windows.Media.Brushes.LightBlue };
            var inner=new MyScrollViewer { Height=100, Content=new System.Windows.Controls.Border { Height=600 } };
            var scrollContent=new System.Windows.Controls.StackPanel();
            scrollContent.Children.Add(inner);
            scrollContent.Children.Add(new System.Windows.Controls.Border { Height=1200 });
            var scroll=new MyScrollViewer { FixedHeader=fixedHeader, Content=scrollContent, VerticalScrollBarVisibility=System.Windows.Controls.ScrollBarVisibility.Auto };
            scrollHost.Children.Add(scroll); scrollHost.UpdateLayout(); await Task.Delay(100);
            var bar=scroll.scrollBar;
            var track=(System.Windows.Controls.Primitives.Track)bar.Template.FindName("PART_Track",bar);
            if(track.Thumb.ActualWidth<16 || track.Thumb.InputHitTest(new System.Windows.Point(1,track.Thumb.ActualHeight/2)) is null)
                throw new Exception("Scrollbar thumb does not expose its full 16px hit target");
            report.Add("PASS scrollbar thumb exposes a full 16px hit target while retaining a narrower visible grip");
            var headerY=fixedHeader.TranslatePoint(new System.Windows.Point(),scrollHost).Y;
            scroll.ScrollToVerticalOffset(80); await Task.Delay(100); scrollHost.UpdateLayout();
            if(Math.Abs(fixedHeader.TranslatePoint(new System.Windows.Point(),scrollHost).Y-headerY)>0.1 || scroll.ViewportHeight>165)
                throw new Exception("Pinned header moves or is included in viewport height");
            report.Add("PASS pinned toolbar remains fixed while results scroll and viewport excludes toolbar height");
            scroll.ScrollToTop(); inner.ScrollToBottom(); await Task.Delay(100);
            inner.RaiseEvent(new System.Windows.Input.MouseWheelEventArgs(System.Windows.Input.Mouse.PrimaryDevice,Environment.TickCount,-120) { RoutedEvent=System.Windows.Input.Mouse.MouseWheelEvent });
            await Task.Delay(250);
            if(System.Windows.SystemParameters.WheelScrollLines!=0 && scroll.VerticalOffset<=0) throw new Exception("Nested wheel does not chain at bottom edge");
            report.Add("PASS nested scrolling hands wheel input to the outer viewport at its boundary");
            var tinyIcon=new MyIconButton { Width=14, Height=14 };
            tinyIcon.Measure(new System.Windows.Size(100,100)); tinyIcon.Arrange(new System.Windows.Rect(0,0,100,100));
            if(tinyIcon.ActualWidth<32 || tinyIcon.ActualHeight<32) throw new Exception("Small icon action has an undersized hit target");
            report.Add("PASS legacy 14px icon declarations retain a minimum 32px hit target");
            form.PanForm.Children.Remove(scrollHost);
            var resourceSearch=new PageComp();
            resourceSearch.PanLoad.Visibility=System.Windows.Visibility.Collapsed;
            resourceSearch.FilterOptions.IsExpanded=true;
            for(var i=0;i<12;i++)
            {
                var result=new MyCompItem();
                result.LabTitle.Text="A very long resource title that should stay inside the row and preserve its actions "+i;
                result.LabInfo.Text="A readable description with supplementary information";
                resourceSearch.PanProjects.Children.Add(result);
            }
            resourceSearch.Measure(new System.Windows.Size(566,352)); resourceSearch.Arrange(new System.Windows.Rect(0,0,566,352));resourceSearch.UpdateLayout();
            if(resourceSearch.ComboSearchSort.ActualWidth<150)
                throw new Exception("Narrow resource search squeezes filter controls");
            var searchY=resourceSearch.PanSearchBox.TranslatePoint(new System.Windows.Point(),resourceSearch).Y;
            resourceSearch.ScrollToVerticalOffset(150);resourceSearch.UpdateLayout();await Task.Delay(100);
            if(Math.Abs(resourceSearch.PanSearchBox.TranslatePoint(new System.Windows.Point(),resourceSearch).Y-searchY)>0.1)
                throw new Exception("Real search page toolbar scrolls away");
            var searchBitmap=new RenderTargetBitmap(566,352,96,96,PixelFormats.Pbgra32);searchBitmap.Render(resourceSearch);
            var searchEncoder=new PngBitmapEncoder();searchEncoder.Frames.Add(BitmapFrame.Create(searchBitmap));
            using(var file=File.Create(Path.Combine(output,"resource-search-narrow.png")))searchEncoder.Save(file);
            report.Add("PASS real resource search preserves usable filter widths, bounded result titles and a pinned search field");
            var resources=new PageInstanceCompResource();
            resources.PanLoad.Visibility=System.Windows.Visibility.Collapsed;
            foreach(var card in resources.PanAllBack.Children.OfType<MyCard>())card.Visibility=System.Windows.Visibility.Collapsed;
            resources.PanListBack.Opacity=1;
            for(var i=0;i<30;i++)resources.PanList.Children.Add(new System.Windows.Controls.TextBlock { Text="Example resource "+i, Height=44 });
            resources.Measure(new System.Windows.Size(566,352));resources.Arrange(new System.Windows.Rect(0,0,566,352));resources.UpdateLayout();
            var manageY=resources.SearchBox.TranslatePoint(new System.Windows.Point(),resources).Y;
            resources.PanBack.ScrollToVerticalOffset(150);resources.UpdateLayout();await Task.Delay(100);
            if(Math.Abs(resources.SearchBox.TranslatePoint(new System.Windows.Point(),resources).Y-manageY)>0.1)
                throw new Exception("Real instance resource toolbar scrolls away");
            var resourcesBitmap=new RenderTargetBitmap(566,352,96,96,PixelFormats.Pbgra32);resourcesBitmap.Render(resources);
            var resourcesEncoder=new PngBitmapEncoder();resourcesEncoder.Frames.Add(BitmapFrame.Create(resourcesBitmap));
            using(var file=File.Create(Path.Combine(output,"instance-resources-narrow.png")))resourcesEncoder.Save(file);
            report.Add("PASS real instance resource search and primary actions remain pinned above long lists");
            await CheckBatchClearance(resources,resources.CardSelect,resources.PanListBack,"instance resources",report);
            foreach(var code in new[]{"en-US","fr-FR","ja-JP","zh-CN"})
            {
                PCL.Core.App.Localization.LocalizationService.Apply(code,save:false);
                form.PageChange(FormMain.PageType.Setup);
                ModMain.frmSetupLeft.OpenCategory(FormMain.PageSubType.SetupLaunch);
                ModMain.frmSetupLeft.ItemLaunch.SetChecked(true,true,true);
                await Task.Delay(600); form.UpdateLayout();
                ModMain.frmSetupLeft.ItemLaunch.BringIntoView();await Task.Delay(100);
                if(form.CategoryActions.Children.OfType<MyButton>().FirstOrDefault()?.Text!=PCL.Core.App.Localization.Lang.Text("Common.Action.Reset"))
                    throw new Exception($"Category action does not track language: {code}");
                var navBounds=form.FluentDock.TransformToAncestor(form.PanForm).TransformBounds(new System.Windows.Rect(form.FluentDock.RenderSize));
                if(navBounds.Left<0 || navBounds.Right>form.PanForm.ActualWidth+1) throw new Exception($"Localized primary navigation overflows: {code}");
                var bitmap=new RenderTargetBitmap((int)form.ActualWidth,(int)form.ActualHeight,96,96,PixelFormats.Pbgra32);bitmap.Render(form);
                var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));
                using var file=File.Create(Path.Combine(output,$"localized-{code}-settings.png"));encoder.Save(file);
                report.Add($"PASS {code} primary navigation fits at 810x470");
            }
            await AuditRemainingListToolbars(output, report);
            AuditServerCard(output, report);
            await AuditLogLayout(output, report);
            await AuditSettingsLayouts(form, output, report);
            File.WriteAllLines(Path.Combine(Root,"report.txt"),report);
        }
        catch(Exception ex) { report.Add("FAIL "+ex);File.WriteAllLines(Path.Combine(Root,"report.txt"),report); }
        finally { networkDialogTimer?.Stop(); System.Windows.Application.Current.Shutdown(); }
    }
    private static async Task AuditRemainingListToolbars(string output,List<string> report)
    {
        foreach(var code in new[]{"en-US","zh-CN"})
        {
            PCL.Core.App.Localization.LocalizationService.Apply(code,save:false);
            var saves=new PageInstanceSaves();
            saves.PanNoWorld.Visibility=System.Windows.Visibility.Collapsed;
            saves.PanContent.Visibility=System.Windows.Visibility.Visible;
            await CheckPinned(saves,saves.PanBack,saves.SearchBox,saves.PanList,"worlds",code,output,report);
            saves.PanContent.Visibility=System.Windows.Visibility.Collapsed;saves.UpdateLayout();await Task.Delay(50);
            if(((System.Windows.FrameworkElement)saves.PanBack.FixedHeader).Visibility!=System.Windows.Visibility.Collapsed)
                throw new Exception("World toolbar does not follow the empty-world state");
            report.Add($"PASS {code} world toolbar preserves empty-state visibility");
            var packs=new PageInstanceSavesDatapack();
            packs.PanAllBack.Visibility=System.Windows.Visibility.Visible;
            packs.PanLoad.Visibility=System.Windows.Visibility.Collapsed;
            foreach(var card in packs.PanAllBack.Children.OfType<MyCard>())card.Visibility=System.Windows.Visibility.Collapsed;
            packs.PanListBack.Opacity=1;
            await CheckPinned(packs,packs.PanBack,packs.SearchBox,packs.PanList,"datapacks",code,output,report);
            await CheckBatchClearance(packs,packs.CardSelect,packs.PanListBack,"datapacks",report);
            var jars=new PageInstanceCompJarInJar();
            jars.PanEmpty.Visibility=System.Windows.Visibility.Collapsed;jars.PanLoad.Visibility=System.Windows.Visibility.Collapsed;
            await CheckPinned(jars,jars.PanBack,jars.SearchBox,jars.PanRelationList,"embedded-mods",code,output,report);
            var favorites=new PageDownloadCompFavorites();await Task.Delay(200);
            favorites.PanContent.Visibility=System.Windows.Visibility.Visible;favorites.PanContent.Opacity=1;
            favorites.PanLoad.Visibility=System.Windows.Visibility.Collapsed;favorites.CardNoContent.Visibility=System.Windows.Visibility.Collapsed;
            favorites.PanContentList.Visibility=System.Windows.Visibility.Visible;
            await CheckPinned(favorites,favorites.PanBack,favorites.PanSearchBox,favorites.PanContentList,"favorites",code,output,report);
            await CheckBatchClearance(favorites,favorites.CardSelect,favorites.PanContentList,"favorites",report);
            favorites.PanContent.Visibility=System.Windows.Visibility.Collapsed;favorites.UpdateLayout();await Task.Delay(50);
            if(((System.Windows.FrameworkElement)favorites.PanBack.FixedHeader).Visibility!=System.Windows.Visibility.Collapsed)
                throw new Exception($"Favorites toolbar visibility: body={favorites.PanContent.Visibility}, header={((System.Windows.FrameworkElement)favorites.PanBack.FixedHeader).Visibility}, loader={favorites.loader.State}");
            report.Add($"PASS {code} favorites toolbar preserves loader visibility");
            var servers=new PageInstanceServer();servers.PanNoServer.Visibility=System.Windows.Visibility.Collapsed;
            servers.PanContent.Visibility=System.Windows.Visibility.Visible;servers.PanServers.Visibility=System.Windows.Visibility.Visible;
            await CheckPinned(servers,servers.PanBack,servers.PanContent,servers.PanServers,"servers",code,output,report);
        }
    }

    private static async Task CheckPinned(MyPageRight page,MyScrollViewer scroll,System.Windows.FrameworkElement header,
        System.Windows.Controls.Panel rows,string label,string code,string output,List<string> report)
    {
        for(var i=0;i<30;i++)rows.Children.Add(new System.Windows.Controls.TextBlock { Text=$"Example {label} {i}",Height=44 });
        page.Measure(new System.Windows.Size(566,352));page.Arrange(new System.Windows.Rect(0,0,566,352));page.UpdateLayout();await Task.Delay(70);
        if(!ReferenceEquals(page.PanScroll,scroll))throw new Exception($"{label} does not expose the results viewport for return-to-top");
        if(System.Windows.LogicalTreeHelper.GetParent((System.Windows.DependencyObject)scroll.FixedHeader)!=scroll)
            throw new Exception($"{label} fixed header does not participate in resource and namescope inheritance");
        var y=header.TranslatePoint(new System.Windows.Point(),page).Y;
        scroll.ScrollToVerticalOffset(150);page.UpdateLayout();await Task.Delay(70);
        if(scroll.VerticalOffset<100 || Math.Abs(header.TranslatePoint(new System.Windows.Point(),page).Y-y)>.1 || scroll.ViewportHeight<100)
            throw new Exception($"{label} toolbar: {code}; offset={scroll.VerticalOffset}, header delta={header.TranslatePoint(new System.Windows.Point(),page).Y-y}, viewport={scroll.ViewportHeight}, rows={rows.Visibility}");
        var issues=FindInteractiveOverlaps(page).ToArray();
        if(issues.Length>0)throw new Exception($"{label} layout: "+string.Join("; ",issues));
        var bitmap=new RenderTargetBitmap(566,352,96,96,PixelFormats.Pbgra32);bitmap.Render(page);
        var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using(var file=File.Create(Path.Combine(output,$"pinned-{label}-{code}.png")))encoder.Save(file);
        report.Add($"PASS {code} {label}: pinned controls, no overlap, usable result viewport");
    }

    private static async Task CheckBatchClearance(MyPageRight page,MyCard footer,System.Windows.FrameworkElement list,string label,List<string> report)
    {
        footer.Visibility=System.Windows.Visibility.Visible;footer.Opacity=1;
        page.Measure(new System.Windows.Size(566,352));page.Arrange(new System.Windows.Rect(0,0,566,352));page.UpdateLayout();await Task.Delay(100);
        if(list.Margin.Bottom<footer.ActualHeight+23)throw new Exception($"{label} selection actions cover the end of the list");
        report.Add($"PASS {label} measured selection-footer clearance: {list.Margin.Bottom:0}px for {footer.ActualHeight:0}px actions");
        footer.Visibility=System.Windows.Visibility.Collapsed;
    }

    private static void AuditServerCard(string output,List<string> report)
    {
        var card=new ServerCard();card.ServerName.Text="A long server name that must retain its actions in a narrow panel";
        card.ServerPlayer.Text="100000/100000";
        foreach(var width in new[]{300d,566d})
        {
            card.Measure(new System.Windows.Size(width,80));card.Arrange(new System.Windows.Rect(0,0,width,64));card.UpdateLayout();
            card.MotdRenderer.RenderMotd("A readable server description",ThemeService.IsDarkMode,maxLines:2);
            var bounds=card.BtnSetting.TransformToAncestor(card).TransformBounds(new System.Windows.Rect(card.BtnSetting.RenderSize));
            if(bounds.Right>width+1 || card.ServerName.ActualWidth<60 || card.MotdRenderer.ActualWidth<60)
                throw new Exception($"Server card squeezes its name or action area at {width}px");
            if(card.MotdRenderer.ActualWidth>width-100)throw new Exception("Server description retains a fixed oversized width");
        }
        card.Measure(new System.Windows.Size(400,80));card.Arrange(new System.Windows.Rect(0,0,400,64));card.UpdateLayout();
        var bitmap=new RenderTargetBitmap(400,64,96,96,PixelFormats.Pbgra32);bitmap.Render(card);
        var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using(var file=File.Create(Path.Combine(output,"server-card-responsive.png")))encoder.Save(file);
        card.MotdRenderer.ClearCanvas();
        card.Measure(new System.Windows.Size(450,80));card.Arrange(new System.Windows.Rect(0,0,450,64));card.UpdateLayout();
        if(VisualDescendants(card.MotdRenderer).OfType<System.Windows.Controls.Canvas>().Any(canvas=>canvas.Children.Count>0))
            throw new Exception("A cleared server description reappears after resizing");
        report.Add("PASS server cards resize titles, descriptions and action hit targets at 300px and 566px");
    }

    private static async Task AuditLogLayout(string output, List<string> report)
    {
        foreach(var code in new[]{"en-US","zh-CN"})
        foreach(var dark in new[]{false,true})
        {
            PCL.Core.App.Localization.LocalizationService.Apply(code,save:false);
            Config.Preference.Theme.ColorMode=dark?ColorMode.Dark:ColorMode.Light;
            ThemeService.RefreshColorMode();
            FluentTheme.Refresh();
            await Task.Delay(250);
            var log=new PageLogRight();
            log.CardOperation.Opacity=1;
            if(log.PanLog.Foreground is not SolidColorBrush logBrush ||
                System.Windows.Application.Current.Resources["ColorBrush1"] is not SolidColorBrush themeBrush || logBrush.Color!=themeBrush.Color)
                throw new Exception($"Log text does not follow the {(dark?"dark":"light")} theme");
            log.PanLog.Document.Blocks.Add(new System.Windows.Documents.Paragraph(new System.Windows.Documents.Run(string.Join("\n",Enumerable.Range(1,100).Select(i=>$"Log entry {i}")))));
            log.Measure(new System.Windows.Size(566,352));log.Arrange(new System.Windows.Rect(0,0,566,352));log.UpdateLayout();
            await Task.Delay(250);log.UpdateLayout();
            var cardBrush=VisualDescendants(log.PanLogCard).OfType<PCL.Core.UI.Controls.LiquidGlassSurface>().First().Background as SolidColorBrush;
            static double Linear(byte channel) { var value=channel/255d;return value<=.04045?value/12.92:Math.Pow((value+.055)/1.055,2.4); }
            static double Luminance(Color color)=>.2126*Linear(color.R)+.7152*Linear(color.G)+.0722*Linear(color.B);
            if(cardBrush is null)throw new Exception("Log background does not expose its rendered material color");
            var foreground=Luminance(logBrush.Color);var background=Luminance(cardBrush.Color);
            var contrast=(Math.Max(foreground,background)+.05)/(Math.Min(foreground,background)+.05);
            if(contrast<4.5)throw new Exception($"Log text contrast is too low in {(dark?"dark":"light")}: {contrast:0.0}:1");
            var logBottom=log.PanLogCard.TranslatePoint(new System.Windows.Point(0,log.PanLogCard.ActualHeight),log).Y;
            var actionsTop=log.CardOperation.TranslatePoint(new System.Windows.Point(),log).Y;
            if(logBottom>actionsTop+1 || log.PanBack.ActualHeight<100)throw new Exception($"Log actions overlap the readable viewport: {code}");
            var overlaps=FindInteractiveOverlaps(log).ToArray();
            if(overlaps.Length>0)throw new Exception("Log action overlap: "+string.Join("; ",overlaps));
            var bitmap=new RenderTargetBitmap(566,352,96,96,PixelFormats.Pbgra32);bitmap.Render(log);
            var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using(var file=File.Create(Path.Combine(output,$"game-log-actions-{code}-{(dark?"dark":"light")}.png")))encoder.Save(file);
            report.Add($"PASS {code} {(dark?"dark":"light")} game-log text contrast {contrast:0.0}:1; actions wrap without overlap and preserve a readable viewport");
        }
        foreach(var option in new System.Windows.FrameworkElement[]{
            new MyCheckBox { Text="A long translated option label that must wrap and remain completely readable in a narrow settings row" },
            new MyRadioBox { Text="A long translated radio option that must also wrap without cropping its last line" }})
        {
            option.Measure(new System.Windows.Size(180,double.PositiveInfinity));
            option.Arrange(new System.Windows.Rect(0,0,180,option.DesiredSize.Height));
            if(option.ActualHeight<32)throw new Exception("A long option label did not grow its row");
        }
        report.Add("PASS long checkbox and radio labels grow to their measured multiline height");
    }

    private static async Task AuditSettingsLayouts(FormMain form, string output, List<string> report)
    {
        var issues=new HashSet<string>();
        var categories=new[] { FormMain.PageSubType.SetupLaunch, FormMain.PageSubType.SetupUI,
            FormMain.PageSubType.SetupGameManage, FormMain.PageSubType.SetupJava,
            FormMain.PageSubType.SetupLauncherMisc, FormMain.PageSubType.SetupLauncherLanguage,
            FormMain.PageSubType.SetupAbout, FormMain.PageSubType.SetupLog,
            FormMain.PageSubType.SetupFeedback };
        foreach(var code in new[]{"en-US","zh-CN"})
        foreach(var dark in new[]{false,true})
        foreach(var width in new[]{810d,1280d})
        {
            PCL.Core.App.Localization.LocalizationService.Apply(code,save:false);
            Config.Preference.Theme.ColorMode=dark?ColorMode.Dark:ColorMode.Light;
            ThemeService.RefreshColorMode();
            form.Width=width;form.Height=width==810?470:800;
            form.PageChange(FormMain.PageType.Setup);await Task.Delay(400);
            foreach(var category in categories)
            {
                ModMain.frmSetupLeft.OpenCategory(category);await Task.Delay(450);form.UpdateLayout();
                var page=(MyPageRight)form.PanMainRight.Child;
                var categoryName=page.GetType().Name;
                foreach(var card in VisualDescendants(page).OfType<MyCard>().Where(card=>card.CanSwap && card.IsSwapped).ToArray()) card.IsSwapped=false;
                var conditional=new List<(System.Windows.FrameworkElement Element,System.Windows.Visibility Visibility)>();
                if(page is PageSetupUI appearance)
                    foreach(var element in new System.Windows.FrameworkElement[]{appearance.PanBackgroundSuit,appearance.PanCustomPreset,appearance.PanAccent,appearance.PanBlurValue})
                    { conditional.Add((element,element.Visibility));element.Visibility=System.Windows.Visibility.Visible; }
                await Task.Delay(300);form.UpdateLayout();
                var scroll=page.PanScroll;
                var expectedScroll=VisualDescendants(page).OfType<MyScrollViewer>().FirstOrDefault();
                for(var attempt=0;attempt<20 && (scroll is null || !ReferenceEquals(scroll,expectedScroll));attempt++)
                {
                    await Task.Delay(100);form.UpdateLayout();
                    scroll=page.PanScroll;expectedScroll=VisualDescendants(page).OfType<MyScrollViewer>().FirstOrDefault();
                }
                if(scroll is null || !ReferenceEquals(scroll,expectedScroll))
                    throw new Exception($"{categoryName} does not expose its real scrolling viewport: loaded={page.IsLoaded}, registered={scroll?.Name}, visual={expectedScroll?.Name}");
                var end=scroll.ScrollableHeight;
                var step=Math.Max(100,(scroll?.ViewportHeight ?? 350)*.75);
                var offsets=new List<double>();for(var offset=0d;offset<end;offset+=step)offsets.Add(offset);offsets.Add(end);
                foreach(var offset in offsets)
                {
                    scroll?.ScrollToVerticalOffset(offset);
                    await Task.Delay(70);form.UpdateLayout();
                    foreach(var issue in FindInteractiveOverlaps(page))issues.Add($"{categoryName} {code} {width} {(dark?"dark":"light")}: {issue}");
                }
                scroll?.ScrollToTop();await Task.Delay(50);form.UpdateLayout();
                var bitmap=new RenderTargetBitmap((int)form.ActualWidth,(int)form.ActualHeight,96,96,PixelFormats.Pbgra32);bitmap.Render(form);
                var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));
                for(var attempt=0;attempt<5 && form.PanMsg.Children.OfType<System.Windows.FrameworkElement>().Any(dialog=>dialog.IsVisible && dialog.Opacity>.1);attempt++)await Task.Delay(150);
                if(form.PanMsg.Children.OfType<System.Windows.FrameworkElement>().Any(dialog=>dialog.IsVisible && dialog.Opacity>.1))
                    throw new Exception("Unexpected modal obscures settings layout capture");
                LiquidGlassSmoke.Capture(form,Path.Combine(output,$"material-settings-{categoryName}-{code}-{width}-{(dark?"dark":"light")}.png"));
                using var file=File.Create(Path.Combine(output,$"settings-{categoryName}-{code}-{width}-{(dark?"dark":"light")}.png"));encoder.Save(file);
                File.WriteAllLines(Path.Combine(Root,"layout-audit-progress.txt"),issues.Order());
                if(!issues.Any(i=>i.StartsWith($"{categoryName} {code} {width} {(dark?"dark":"light")}:")))
                    report.Add($"PASS {categoryName} {code} {width} {(dark?"dark":"light")}: {offsets.Count} scroll positions, expanded and conditional controls");
                foreach(var state in conditional)state.Element.Visibility=state.Visibility;
            }
        }
        File.WriteAllLines(Path.Combine(Root,"layout-audit.txt"),issues.Order());
        if(issues.Count>0)throw new Exception($"Settings layout audit found {issues.Count} overlaps; see layout-audit.txt");
    }

    private static IEnumerable<System.Windows.DependencyObject> VisualDescendants(System.Windows.DependencyObject parent)
    {
        for(var i=0;i<VisualTreeHelper.GetChildrenCount(parent);i++)
        {
            var child=VisualTreeHelper.GetChild(parent,i);yield return child;
            foreach(var descendant in VisualDescendants(child))yield return descendant;
        }
    }

    private static IEnumerable<string> FindInteractiveOverlaps(System.Windows.FrameworkElement root)
    {
        var controls=new List<(System.Windows.FrameworkElement Element,System.Windows.Rect Bounds)>();
        var sizingIssues=new List<string>();
        void Visit(System.Windows.DependencyObject parent)
        {
            if(parent is System.Windows.FrameworkElement container && (container.Visibility!=System.Windows.Visibility.Visible || container.Opacity<=0))return;
            for(var i=0;i<VisualTreeHelper.GetChildrenCount(parent);i++)
            {
                var child=VisualTreeHelper.GetChild(parent,i);
                if(child is System.Windows.FrameworkElement e && e.Visibility==System.Windows.Visibility.Visible && e.Opacity>0 &&
                    e is MyButton or MyIconButton or MyIconTextButton or MyTextBox or MyComboBox or MyCheckBox or MyRadioBox or MyRadioButton or MySlider or MySearchBox)
                {
                    var bounds=e.TransformToAncestor(root).TransformBounds(new System.Windows.Rect(e.RenderSize));
                    var raw=bounds;
                    System.Windows.DependencyObject? ancestor=e;
                    while(ancestor!=root && ancestor is not null)
                    {
                        if(ancestor is System.Windows.FrameworkElement clipper && (clipper.ClipToBounds || VisualTreeHelper.GetClip(clipper) is not null))
                            bounds.Intersect(clipper.TransformToAncestor(root).TransformBounds(new System.Windows.Rect(clipper.RenderSize)));
                        ancestor=VisualTreeHelper.GetParent(ancestor);
                    }
                    bounds.Intersect(new System.Windows.Rect(root.RenderSize));
                    if(!bounds.IsEmpty && bounds.Width>2 && bounds.Height>2)
                    {
                        controls.Add((e,bounds));
                        if(raw.Left < -2 || raw.Right > root.ActualWidth+2)
                            sizingIssues.Add($"{e.Name} ({e.GetType().Name}) extends outside horizontal viewport: {raw.Left:0}..{raw.Right:0} / {root.ActualWidth:0}");
                        var label=e is MyCheckBox check ? check.LabText : e is MyRadioBox radio ? radio.LabText : null;
                        if(label is not null && !string.IsNullOrWhiteSpace(label.Text))
                        {
                            var text=new FormattedText(label.Text,PCL.Core.App.Localization.Lang.Culture,label.FlowDirection,
                                new Typeface(label.FontFamily,label.FontStyle,label.FontWeight,label.FontStretch),label.FontSize,label.Foreground,
                                VisualTreeHelper.GetDpi(label).PixelsPerDip);
                            var available=Math.Max(1,e.ActualWidth-label.Margin.Left-label.Margin.Right);
                            if(label.TextWrapping==System.Windows.TextWrapping.NoWrap && text.Width>available+2)
                                sizingIssues.Add($"{e.Name}: option label exceeds available width");
                            text.MaxTextWidth=available;
                            if(text.Height>e.ActualHeight+2)sizingIssues.Add($"{e.Name}: wrapped option label exceeds row height ({text.Height:0} > {e.ActualHeight:0})");
                        }
                    }
                }
                else Visit(child);
            }
        }
        Visit(root);
        foreach(var issue in sizingIssues)yield return issue;
        for(var i=0;i<controls.Count;i++)
        for(var j=i+1;j<controls.Count;j++)
        {
            var overlap=System.Windows.Rect.Intersect(controls[i].Bounds,controls[j].Bounds);
            if(!overlap.IsEmpty && overlap.Width>2 && overlap.Height>2)
                yield return $"{controls[i].Element.Name} ({controls[i].Element.GetType().Name}) overlaps {controls[j].Element.Name} ({controls[j].Element.GetType().Name}) by {overlap.Width:0}x{overlap.Height:0}";
        }
    }

}


