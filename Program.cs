using System.Runtime.InteropServices;
using System.Diagnostics;
using System.Runtime.CompilerServices;


internal class Program
{
    private static async Task Main(string[] args)
    {
        var collector = new NetworkUsageCollector();

        System.Console.WriteLine("Network Usage:");
        System.Console.Write("\x1b[s");
        
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            await foreach (var stats in collector.GetOSXNetworkUsage(TimeSpan.FromSeconds(1), interfaces: "en0"))
            {
                System.Console.Write("\x1b[u\x1b[J");
                foreach (var item in stats)
                {
                    System.Console.WriteLine($"{item.Key}:\t\tRx: {item.Value.RxBytesPerSec.FormatNetworkSpeed()}\tTx: {item.Value.TxBytesPerSec.FormatNetworkSpeed()}");
                }
                //System.Console.Write($"\x1b[{stats.Count}A");
            }
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {

        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {

        }
    }

    
   
}