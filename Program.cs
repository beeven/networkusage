using System.Runtime.InteropServices;
using System.Diagnostics;
using System.Runtime.CompilerServices;


internal class Program
{
    private static async Task Main(string[] args)
    {
        var collector = new NetworkUsageCollector();

        IAsyncEnumerable<Dictionary<string, NetworkUsageCollector.NetworkSpeedStat>>? statStream = null;
        TimeSpan interval = TimeSpan.FromSeconds(1);

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            statStream = collector.GetOSXNetworkUsage(interval, interfaces: "en0");

        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            statStream = collector.GetLinuxNetworkUsage(interval, interfaces: "eth0");
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            statStream = collector.GetWindowsNetworkUsage(interval);            
        }

        if (statStream is not null)
        {
            System.Console.WriteLine("Network Usage:");
            System.Console.Write("\x1b[s");
            await foreach (var stats in statStream)
            {
                System.Console.Write("\x1b[u\x1b[J");
                foreach (var item in stats)
                {
                    System.Console.WriteLine($"{item.Key}:\t\tRx: {item.Value.RxBytesPerSec.FormatNetworkSpeed()}\tTx: {item.Value.TxBytesPerSec.FormatNetworkSpeed()}");
                }
            }
        }
    }



}