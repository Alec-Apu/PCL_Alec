using System;
using System.Linq;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Runtime.Caching;
using System.Threading;
using System.Threading.Tasks;
using Ae.Dns.Client;
using Ae.Dns.Protocol;
using Ae.Dns.Protocol.Enums;
using Ae.Dns.Protocol.Records;
using PCL.Core.IO.Net.Http;
using PCL.Core.Logging;

namespace PCL.Core.IO.Net.Dns;

public class DnsQuery : IDisposable
{
    private const string ModuleName = "DoH query";
    public static DnsQuery Instance { get; } = new();

    private readonly DnsCachingClient _resolver;
    private readonly HttpClient[] _httpClients;

    private DnsQuery()
    {
        // 使用Ae.Dns创建DoH客户端，支持多个DoH服务器
        _httpClients =
        [
            CreateClient("https://dns.alidns.com/")
        ];
        _resolver = new DnsCachingClient(
            new WeightedDnsRacerClient(1, _httpClients.Select(static x => new DnsHttpClient(x)).ToArray<IDnsClient>()),
            new MemoryCache("DoH Query Cache"));
    }

    private static HttpClient CreateClient(string address) => new(new DiagnosticHandler(
        new HttpClientHandler { Proxy = HttpProxyManager.Instance })) { BaseAddress = new Uri(address) };

    private sealed class DiagnosticHandler(HttpMessageHandler inner) : DelegatingHandler(inner)
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var elapsed = Stopwatch.StartNew();
            try
            {
                var response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
                LogWrapper.Debug(ModuleName, $"DoH transport; provider={request.RequestUri?.Host}; status={(int)response.StatusCode}; elapsed={elapsed.ElapsedMilliseconds}ms");
                return response;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
            catch (Exception ex)
            {
                LogWrapper.Debug(ex, ModuleName, $"DoH transport failed; provider={request.RequestUri?.Host}; elapsed={elapsed.ElapsedMilliseconds}ms");
                throw;
            }
        }
    }

    public async Task<DnsMessage?> QueryAsync(string host, DnsQueryType qType, CancellationToken cts = default)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cts);
        timeout.CancelAfter(TimeSpan.FromSeconds(5));
        try
        {
            return await _resolver.Query(DnsQueryFactory.CreateQuery(host, qType), timeout.Token)
                .WaitAsync(timeout.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cts.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            LogWrapper.Debug(ex, ModuleName, $"DoH query failed; host={host}; type={qType}");
        }

        return null;
    }

    public async Task<IPAddress[]?> QueryForIpAsync(string host, CancellationToken cts = default)
    {
        async Task<IPAddress[]?> QueryAddresses(DnsQueryType type, CancellationToken token)
        {
            var answer = await QueryAsync(host, type, token).ConfigureAwait(false);
            if (answer is not null && answer.Header.ResponseCode != DnsResponseCode.NoError)
            {
                LogWrapper.Debug(ModuleName, $"DNS answer failed; host={host}; type={type}; code={answer.Header.ResponseCode}");
                return null;
            }
            return answer?.Answers.Where(a => a.Type == type)
                .Select(a => a.Resource).OfType<DnsIpAddressResource>()
                .Select(a => a.IPAddress).Distinct().ToArray();
        }
        var addresses = await DnsAddressResolver.ResolveAsync(host,
            token => QueryAddresses(DnsQueryType.A, token),
            token => QueryAddresses(DnsQueryType.AAAA, token),
            token => System.Net.Dns.GetHostAddressesAsync(host, token), cts).ConfigureAwait(false);
        LogWrapper.Debug(ModuleName, $"Resolved; host={host}; addresses={string.Join(",", addresses.Select(ip => ip.ToString()))}");
        return addresses;
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        foreach (var client in _httpClients)
        {
            client.Dispose();
        }
        _resolver.Dispose(); // 好像这个包的 Dispose 并没有做什么 lol
    }
}
