using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Ae.Dns.Protocol;
using PCL.Core.Utils.Exts;

namespace PCL.Core.IO.Net.Dns;

public sealed class WeightedDnsRacerClient : IDnsClient
{
    private readonly (IDnsClient Client, byte Weight)[] _clients;
    private readonly int _concurrentCount;
    private bool _updateWeigh = true;
    private readonly object _lock = new();

    public WeightedDnsRacerClient(int concurrentCount, params IDnsClient[] clients)
    {
        if (concurrentCount <= 0 || clients is null || clients.Length < concurrentCount)
            throw new ArgumentException("Not enough clients.");

        _concurrentCount = concurrentCount;
        _clients = clients.Select(static c => (Client: c, Weight: (byte)0)).ToArray();
    }

    public async Task<DnsMessage> Query(DnsMessage query, CancellationToken ct = default)
    {
        (IDnsClient Client, byte Weight)[] candidates;
        lock (_lock)
        {
            candidates = _clients
                .OrderByDescending(static x => x.Weight)
                .ToArray();
        }

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var failures = new List<Exception>();
        foreach (var batch in candidates.Chunk(_concurrentCount))
        {
            ct.ThrowIfCancellationRequested();
            async Task<DnsMessage> QueryClient(IDnsClient client) => await client.Query(query, cts.Token).ConfigureAwait(false);
            var tasks = batch.Select(c => QueryClient(c.Client)).ToArray();
            foreach (var task in tasks) task.Forget();
            Task<DnsMessage> winnerTask;
            try
            {
                winnerTask = await tasks.WhenAnySuccessAsync().WaitAsync(ct).ConfigureAwait(false);
            }
            catch (AggregateException ex)
            {
                failures.AddRange(ex.Flatten().InnerExceptions);
                continue;
            }
            catch (OperationCanceledException ex) when (!ct.IsCancellationRequested)
            {
                failures.Add(ex);
                continue;
            }
            catch (OperationCanceledException)
            {
                cts.Cancel();
                throw;
            }

            var winner = batch[Array.IndexOf(tasks, winnerTask)].Client;
            lock (_lock)
            {
                for (var i = 0; _updateWeigh && i < _clients.Length; i++)
                {
                    if (_clients[i].Client == winner)
                    {
                        if (_clients[i].Weight < 255)
                            _clients[i] = (_clients[i].Client, (byte)(_clients[i].Weight + 1));
                        else
                            _updateWeigh = false;
                        break;
                    }
                }
            }
            cts.Cancel();
            return await winnerTask.ConfigureAwait(false);
        }
        throw new AggregateException("All DoH providers failed.", failures);
    }

    public void Dispose()
    {
        foreach (var (client, _) in _clients)
            client?.Dispose();
    }
}
