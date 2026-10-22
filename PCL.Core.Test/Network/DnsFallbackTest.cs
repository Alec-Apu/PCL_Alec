using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Ae.Dns.Protocol;
using Ae.Dns.Protocol.Enums;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.IO.Net.Dns;

namespace PCL.Core.Test.Network;

[TestClass]
public class DnsFallbackTest
{
    private static readonly IPAddress V4 = IPAddress.Parse("192.0.2.1");
    private static readonly IPAddress V6 = IPAddress.Parse("2001:db8::1");
    private static readonly IPAddress SystemIp = IPAddress.Parse("192.0.2.2");
    private static Task<IPAddress[]?> Answer(params IPAddress[] addresses) => Task.FromResult<IPAddress[]?>(addresses);

    [TestMethod]
    public async Task SuccessfulDoHDoesNotCallSystemDns()
    {
        var result = await DnsAddressResolver.ResolveAsync("example.test", _ => Answer(V4), _ => Answer(V6),
            _ => throw new AssertFailedException("Unexpected system DNS call"), default);
        CollectionAssert.AreEquivalent(new[] { V4, V6 }, result);
    }

    [TestMethod]
    [DataRow(true, false)]
    [DataRow(false, true)]
    [DataRow(true, true)]
    public async Task EitherFailedFamilyUsesSystemDns(bool failV4, bool failV6)
    {
        var calls = 0;
        var result = await DnsAddressResolver.ResolveAsync("example.test",
            _ => failV4 ? Task.FromException<IPAddress[]?>(new HttpRequestException("TLS failure")) : Answer(V4),
            _ => failV6 ? Task.FromResult<IPAddress[]?>(null) : Answer(V6),
            _ => { calls++; return Task.FromResult(new[] { SystemIp }); }, default);
        Assert.AreEqual(1, calls);
        CollectionAssert.AreEqual(new[] { SystemIp }, result);
    }

    [TestMethod]
    public async Task EmptyDoHAnswersUseSystemDns()
    {
        var result = await DnsAddressResolver.ResolveAsync("example.test", _ => Answer(), _ => Answer(),
            _ => Task.FromResult(new[] { SystemIp }), default);
        CollectionAssert.AreEqual(new[] { SystemIp }, result);
    }

    [TestMethod]
    public async Task ProviderTimeoutUsesSystemDnsWithoutCancellingCaller()
    {
        var result = await DnsAddressResolver.ResolveAsync("example.test",
            _ => Task.FromException<IPAddress[]?>(new TaskCanceledException("DoH timeout")), _ => Answer(V6),
            _ => Task.FromResult(new[] { SystemIp }), default);
        CollectionAssert.AreEqual(new[] { SystemIp }, result);
    }

    [TestMethod]
    public async Task MissingIpv6RecordIsNotATransportFailure()
    {
        var result = await DnsAddressResolver.ResolveAsync("example.test", _ => Answer(V4), _ => Answer(),
            _ => throw new AssertFailedException("No AAAA record should not trigger fallback"), default);
        CollectionAssert.AreEqual(new[] { V4 }, result);
    }

    [TestMethod]
    public async Task SystemFailurePreservesUsableDoHAnswer()
    {
        var result = await DnsAddressResolver.ResolveAsync("example.test", _ => Answer(V4),
            _ => Task.FromResult<IPAddress[]?>(null),
            _ => Task.FromException<IPAddress[]>(new HttpRequestException("System DNS unavailable")), default);
        CollectionAssert.AreEqual(new[] { V4 }, result);
    }

    [TestMethod]
    public async Task AllFailuresPropagateSystemError()
    {
        await Assert.ThrowsExactlyAsync<HttpRequestException>(() => DnsAddressResolver.ResolveAsync("example.test",
            _ => Task.FromResult<IPAddress[]?>(null), _ => Task.FromResult<IPAddress[]?>(null),
            _ => Task.FromException<IPAddress[]>(new HttpRequestException("System DNS unavailable")), default));
    }

    [TestMethod]
    public async Task CallerCancellationDoesNotTriggerFallback()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsExactlyAsync<OperationCanceledException>(() => DnsAddressResolver.ResolveAsync("example.test",
            _ => Answer(V4), _ => Answer(V6),
            _ => throw new AssertFailedException("Cancellation is not DNS failure"), cancellation.Token));
    }

    [TestMethod]
    public async Task RacerTriesThirdProviderAfterFirstTwoFail()
    {
        var query = DnsQueryFactory.CreateQuery("example.test", DnsQueryType.A);
        var first = new FakeClient(_ => Task.FromException<DnsMessage>(new HttpRequestException("TLS")));
        var second = new FakeClient(_ => Task.FromException<DnsMessage>(new HttpRequestException("Offline")));
        var third = new FakeClient(_ => Task.FromResult(query));
        using var racer = new WeightedDnsRacerClient(2, first, second, third);
        Assert.AreSame(query, await racer.Query(query));
        Assert.AreEqual(1, third.Calls);
    }

    [TestMethod]
    public async Task RacerPreservesAllProviderErrors()
    {
        var query = DnsQueryFactory.CreateQuery("example.test", DnsQueryType.A);
        using var racer = new WeightedDnsRacerClient(2,
            new FakeClient(_ => throw new HttpRequestException("sync failure")),
            new FakeClient(_ => Task.FromException<DnsMessage>(new HttpRequestException("second"))),
            new FakeClient(_ => Task.FromException<DnsMessage>(new HttpRequestException("third"))));
        var exception = await Assert.ThrowsExactlyAsync<AggregateException>(() => racer.Query(query));
        Assert.AreEqual(3, exception.Flatten().InnerExceptions.Count);
    }

    [TestMethod]
    public async Task RacerCancellationDoesNotStartBackup()
    {
        using var cancellation = new CancellationTokenSource();
        var query = DnsQueryFactory.CreateQuery("example.test", DnsQueryType.A);
        var backup = new FakeClient(_ => Task.FromResult(query));
        using var racer = new WeightedDnsRacerClient(1,
            new FakeClient(async token => { cancellation.Cancel(); await Task.Delay(Timeout.Infinite, token); return query; }), backup);
        try { await racer.Query(query, cancellation.Token); Assert.Fail("Expected cancellation"); }
        catch (OperationCanceledException) { }
        Assert.AreEqual(0, backup.Calls);
    }

    private sealed class FakeClient(Func<CancellationToken, Task<DnsMessage>> query) : IDnsClient
    {
        public int Calls { get; private set; }
        public Task<DnsMessage> Query(DnsMessage message, CancellationToken token = default)
        {
            Calls++;
            return query(token);
        }
        public void Dispose() { }
    }
}
