using PCL.Network;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace PCL;

internal static class LanUpdate
{
    internal const int Revision = 7;
    private const string OnlineManifestUrl = "https://github.com/Alec-Apu/PCL_Alec/releases/latest/download/latest.json";
    private const string LanManifestUrl = "http://192.168.5.7:19997/latest.json";
#if PCL_ADMIN
    internal static bool IsAdminBuild => true;
#else
    internal static bool IsAdminBuild => false;
#endif
    private const string PublicKey = """
-----BEGIN PUBLIC KEY-----
MIIBojANBgkqhkiG9w0BAQEFAAOCAY8AMIIBigKCAYEAw7u6/k2sGS03PYO1G5G6
PQcSNht4Zvm8BOTois+sppmyAcGayoHFNFgbLhubjyiUKPM8Lj9bdBexAeGcUtej
kXQl8yVHhXzEe3MVWWpCP3JkJR50cjXkoQE9ywk2p0d5WQPfyiy2EpKcHCpCodHK
F19nYqd5Id8qDC2X+LOr4wC9RTjIcRi3SPslJBWm9H+Uy3cJLoN9U9hXB7xb7DRg
XOEqg9cpxKIl+AVFhG+r81dkRVV8C2TvukAQSMjVB+sCQEKan7j5CkUvSUKBzPMQ
jDtVeWzOeGExWJwJNNniy+JUW1vN0kIuRLvc2Hx04TiclMFTIkpGQAkMXbzZA+i+
76XoM9OVx+RlmJSWLZd0kc+IF/S4Rhq4jHhD4Y00KYz2ooGBotWDYCtWu332Gvqh
/DeW07C+tdXaI2m3m8ahf4cwDZ17SV47ug4/NlHub8Rl/0EdIg+HO44OxvF2JlqJ
0DoZQJTYrBc12P+e4Dwhm390dU2T6X+pMVhJyAR3yyNHAgMBAAE=
-----END PUBLIC KEY-----
""";
    private static int _checking;
    private sealed record Envelope(string Payload, string Signature);
    private sealed record Release(int Revision, string Url, string Sha256, string Notes);

    internal static async Task CheckAsync(bool manual = false, bool? useLan = null)
    {
        var lan = useLan ?? IsAdminBuild;
        if (lan && !IsAdminBuild) return;
        var manifestUrl = lan ? LanManifestUrl : OnlineManifestUrl;
        var channel = lan ? "内网" : "在线";
        if (Interlocked.Exchange(ref _checking, 1) != 0) return;
        try
        {
            using var client = new HttpClient(new HttpClientHandler { UseProxy = !lan }) { Timeout = TimeSpan.FromSeconds(20) };
            var envelope = JsonSerializer.Deserialize<Envelope>(await client.GetStringAsync(manifestUrl))
                ?? throw new InvalidDataException("更新信息为空");
            var payload = Convert.FromBase64String(envelope.Payload);
            using var rsa = RSA.Create();
            rsa.ImportFromPem(PublicKey);
            if (!rsa.VerifyData(payload, Convert.FromBase64String(envelope.Signature), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1))
                throw new InvalidDataException("更新签名验证失败");
            var release = JsonSerializer.Deserialize<Release>(payload) ?? throw new InvalidDataException("更新信息无效");
            if (release.Revision <= Revision)
            {
                if (manual) HintService.Hint($"当前已是最新{channel}版本（{Revision}）。", HintType.Success);
                return;
            }
            if (ModMain.MyMsgBox($"发现{channel}更新（版本 {release.Revision}）\n\n{release.Notes}\n\n下载完成后启动器将自动重启。", $"PCL Ap {channel}更新", "立即更新", "稍后") != 1) return;
            if (ModNet.HasDownloadingTask())
            {
                HintService.Hint("请等待下载任务完成，再检查更新。", HintType.Info);
                return;
            }
            var temp = Path.Combine(Path.GetTempPath(), "PCL-Ap-Update", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(temp);
            var archive = Path.Combine(temp, "update.zip");
            HintService.Hint($"正在下载{channel}更新……");
            using (var download = new HttpClient(new HttpClientHandler { UseProxy = !lan }) { Timeout = TimeSpan.FromMinutes(5) })
            using (var response = await download.GetAsync(release.Url, HttpCompletionOption.ResponseHeadersRead))
            {
                response.EnsureSuccessStatusCode();
                await using var file = File.Create(archive);
                await response.Content.CopyToAsync(file);
            }
            await using (var file = File.OpenRead(archive))
            {
                var hash = Convert.ToHexString(await SHA256.HashDataAsync(file));
                if (!hash.Equals(release.Sha256, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("更新包校验失败");
            }
            using (var zip = System.IO.Compression.ZipFile.OpenRead(archive))
            {
                if (!zip.Entries.Any(e => e.FullName == "PCL Ap.exe")) throw new InvalidDataException("缺少启动器文件");
                foreach (var entry in zip.Entries)
                {
                    var name = entry.FullName.Replace('\\', '/');
                    if (name.StartsWith('/') || name.Contains(':') || name.Split('/').Contains("..") ||
                        (name != "PCL Ap.exe" && !name.StartsWith("UI/"))) throw new InvalidDataException("更新包路径无效");
                }
            }
            var stage = Path.Combine(temp, "stage");
            System.IO.Compression.ZipFile.ExtractToDirectory(archive, stage);
            var exe = Environment.ProcessPath ?? throw new InvalidOperationException("无法定位启动器");
            var target = Path.GetDirectoryName(exe)!;
            var probe = Path.Combine(target, ".pcl-update-" + Guid.NewGuid().ToString("N"));
            await File.WriteAllTextAsync(probe, ""); File.Delete(probe);
            static string Quote(string s) => "'" + s.Replace("'", "''") + "'";
            var script = UpdaterScript.Replace("__PID__", Environment.ProcessId.ToString())
                .Replace("__STAGE__", Quote(stage)).Replace("__EXE__", Quote(exe)).Replace("__TARGET__", Quote(target));
            var scriptPath = Path.Combine(temp, "install.ps1");
            await File.WriteAllTextAsync(scriptPath, script, new UTF8Encoding(true));
            Process.Start(new ProcessStartInfo("powershell.exe")
            {
                Arguments = "-NoProfile -ExecutionPolicy Bypass -File \"" + scriptPath + "\"",
                UseShellExecute = false, CreateNoWindow = true
            });
            ModMain.frmMain.EndProgram(false);
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "[Update] 在线更新检查失败", ModBase.LogLevel.Debug);
            if (manual) HintService.Hint(channel + "更新失败：" + ex.Message, HintType.Error);
        }
        finally { Interlocked.Exchange(ref _checking, 0); }
    }

    private const string UpdaterScript = """
$ErrorActionPreference = 'Stop'
$stage = __STAGE__
$exe = __EXE__
$target = __TARGET__
$backup = $exe + '.update-backup'
$ui = Join-Path $target 'UI'
$uiBackup = Join-Path $target ('UI.update-backup-' + [guid]::NewGuid().ToString('N'))
$movedExe = $false
$movedUi = $false
$installedUi = $false
try {
    $launcher = Get-Process -Id __PID__ -ErrorAction SilentlyContinue
    if ($launcher -and -not $launcher.WaitForExit(120000)) { throw '等待启动器退出超时' }
    for ($attempt = 0; $attempt -lt 30; $attempt++) {
        try { Move-Item -LiteralPath $exe -Destination $backup -Force; $movedExe = $true; break }
        catch { if ($attempt -eq 29) { throw }; Start-Sleep -Milliseconds 500 }
    }
    Copy-Item -LiteralPath (Join-Path $stage 'PCL Ap.exe') -Destination $exe
    if (Test-Path -LiteralPath $ui) { Move-Item -LiteralPath $ui -Destination $uiBackup; $movedUi = $true }
    if (Test-Path -LiteralPath (Join-Path $stage 'UI')) {
        $installedUi = $true
        Copy-Item -LiteralPath (Join-Path $stage 'UI') -Destination $ui -Recurse
    }
    Start-Process -FilePath $exe -WorkingDirectory $target -WindowStyle Normal
} catch {
    $_ | Out-String | Set-Content -LiteralPath (Join-Path $target 'update-error.log')
    if ($movedExe) {
        Move-Item -LiteralPath $backup -Destination $exe -Force
        if ($installedUi -and (Test-Path -LiteralPath $ui)) { Move-Item -LiteralPath $ui -Destination ($ui + '.failed-' + [guid]::NewGuid().ToString('N')) }
        if ($movedUi) { Move-Item -LiteralPath $uiBackup -Destination $ui }
        Start-Process -FilePath $exe -WorkingDirectory $target -WindowStyle Normal
    }
}
""";
}
