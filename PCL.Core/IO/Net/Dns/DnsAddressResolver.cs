using System;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using PCL.Core.Logging;

namespace PCL.Core.IO.Net.Dns;

internal static class DnsAddressResolver
{
    internal static async Task<IPAddress[]> ResolveAsync(string host,
        Func<CancellationToken, Task<IPAddress[]?>> ipv4,
        Func<CancellationToken, Task<IPAddress[]?>> ipv6,
        Func<CancellationToken, Task<IPAddress[]>> system,
        CancellationToken cancellationToken)
    {
        async Task<IPAddress[]?> Query(Func<CancellationToken, Task<IPAddress[]?>> query)
        {
            try { return await query(cancellationToken).ConfigureAwait(false); }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
            catch (Exception ex)
            {
                LogWrapper.Debug(ex, "DoH query", $"Address query failed; host={host}");
                return null;
            }
        }

        var answers = await Task.WhenAll(Query(ipv4), Query(ipv6)).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        var addresses = answers.Where(a => a is not null).SelectMany(a => a!).Distinct().ToArray();
        if (answers.All(a => a is not null) && addresses.Length > 0) return addresses;

        LogWrapper.Debug("DoH query", $"Falling back to system DNS; host={host}; A={(answers[0] is null ? "failed" : "answered")}; AAAA={(answers[1] is null ? "failed" : "answered")}");
        try
        {
            var fallback = await system(cancellationToken).ConfigureAwait(false);
            if (fallback.Length > 0)
            {
                LogWrapper.Debug("DoH query", $"System DNS; host={host}; addresses={string.Join(",", fallback.Select(ip => ip.ToString()))}");
                return fallback.Distinct().ToArray();
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception ex) when (addresses.Length > 0)
        {
            LogWrapper.Debug(ex, "DoH query", $"System DNS failed; retaining available DoH answers; host={host}");
        }
        return addresses;
    }
}
