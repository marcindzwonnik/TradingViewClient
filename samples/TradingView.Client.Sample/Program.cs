using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TradingView.Client;
using TradingView.Client.DependencyInjection;

// Usage:
//   dotnet run --project samples/TradingView.Client.Sample -- status
//   dotnet run --project samples/TradingView.Client.Sample -- quote [SYMBOL]
//   dotnet run --project samples/TradingView.Client.Sample -- ohlcv [COUNT]
//   dotnet run --project samples/TradingView.Client.Sample -- stream quote
//   dotnet run --project samples/TradingView.Client.Sample -- stream "CME_MINI:ES1!@1" "NASDAQ:AAPL@15"
//   add --for SECONDS to stop a stream by itself (e.g. --for 10)
// Ctrl+C stops a stream and kills the node process behind it.

var verbose = args.Contains("--verbose");
args = args.Where(a => a != "--verbose").ToArray();
var forIndex = Array.IndexOf(args, "--for");
int? runSeconds = null;
if (forIndex >= 0 && forIndex + 1 < args.Length)
{
    runSeconds = int.Parse(args[forIndex + 1]);
    args = args.Where((_, i) => i != forIndex && i != forIndex + 1).ToArray();
}

var services = new ServiceCollection();
services.AddLogging(b => b.AddSimpleConsole(o => o.SingleLine = true).SetMinimumLevel(verbose ? LogLevel.Debug : LogLevel.Warning));
services.AddTradingViewClient();
await using var provider = services.BuildServiceProvider();
var tv = provider.GetRequiredService<ITradingViewClient>();

using var cts = new CancellationTokenSource();
if (runSeconds is { } seconds)
{
    cts.CancelAfter(TimeSpan.FromSeconds(seconds));
}

Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true; // let the stream end gracefully so the CLI process is killed by the transport
    cts.Cancel();
};

try
{
    var command = args.Length > 0 ? args[0] : "status";
    switch (command)
    {
        case "status":
            var status = await tv.Health.GetStatusAsync(cts.Token);
            Console.WriteLine($"{status.ChartSymbol} @ {status.ChartResolution} (CDP connected: {status.CdpConnected})");
            break;

        case "quote":
            var quote = await tv.Data.GetQuoteAsync(args.Length > 1 ? args[1] : null, cts.Token);
            Console.WriteLine($"{quote.Symbol}: last {quote.Last} (O {quote.Open} H {quote.High} L {quote.Low} V {quote.Volume})");
            break;

        case "ohlcv":
            var count = args.Length > 1 ? int.Parse(args[1]) : 10;
            var ohlcv = await tv.Data.GetOhlcvAsync(count, cts.Token);
            foreach (var bar in ohlcv.Bars)
            {
                Console.WriteLine($"{bar.Time:u}  O {bar.Open}  H {bar.High}  L {bar.Low}  C {bar.Close}  V {bar.Volume}");
            }

            break;

        case "stream" when args.Length > 1 && args[1] == "quote":
            await foreach (var tick in tv.Streams.QuotesAsync(cancellationToken: cts.Token))
            {
                Console.WriteLine($"{tick.Timestamp:HH:mm:ss.fff} {tick.Symbol} close {tick.Close} vol {tick.Volume}");
            }

            break;

        case "stream" when args.Length > 1:
            var feeds = args.Skip(1).Select(FeedSpec.Parse).ToList();
            await foreach (var tick in tv.Streams.OhlcvAsync(feeds, cancellationToken: cts.Token))
            {
                Console.WriteLine($"{tick.ObservedAt:HH:mm:ss.fff} pane {tick.PaneIndex} {tick.Symbol}@{tick.Timeframe} {tick.BarTime:u} close {tick.Close} vol {tick.Volume}");
            }

            break;

        default:
            Console.Error.WriteLine("Unknown command. Use: status | quote [SYMBOL] | ohlcv [COUNT] | stream quote | stream SYMBOL@TF...");
            return 2;
    }
}
catch (OperationCanceledException)
{
    Console.WriteLine("Stopped.");
}
catch (TvConnectionException ex)
{
    Console.Error.WriteLine($"TradingView is not reachable over CDP: {ex.Message}\nStart it with `tv launch` and try again.");
    return 2;
}
catch (TvException ex)
{
    Console.Error.WriteLine($"{ex.GetType().Name}: {ex.Message}");
    return 1;
}

return 0;
