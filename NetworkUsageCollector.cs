
using System.Diagnostics;
using System.Runtime.CompilerServices;

public class NetworkUsageCollector
{
    public async IAsyncEnumerable<Dictionary<string, NetworkSpeedStat>> GetOSXNetworkUsage(TimeSpan interval = default(TimeSpan), [EnumeratorCancellation] CancellationToken cancellationToken = default(CancellationToken), params string[] interfaces)
    {
        if (interval == TimeSpan.Zero) { interval = TimeSpan.FromSeconds(1); }
        string netstats;

        NetworkBytesStat stat, lastStat;
        using (var p = new Process())
        {
            p.StartInfo = new ProcessStartInfo("netstat", "-ib")
            {
                RedirectStandardOutput = true,
                CreateNoWindow = true
            };
            p.Start();
            await p.WaitForExitAsync(cancellationToken);

            netstats = await p.StandardOutput.ReadToEndAsync();



            List<NetworkBytesStat> currentNetworkStats = new List<NetworkBytesStat>();
            List<NetworkBytesStat> lastNetworkStats;
            Dictionary<string, NetworkSpeedStat> networkSpeedStats = new Dictionary<string, NetworkSpeedStat>();

            foreach (var line in netstats.Split("\n", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Skip(1))
            {
                var s = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (interfaces.Length > 0 && !interfaces.Contains(s[0])) continue;
                if (currentNetworkStats.Any(x => x.Interface == s[0])) continue;
                stat = new NetworkBytesStat(s[0], Convert.ToInt64(s[6]), Convert.ToInt64(s[9]));
                currentNetworkStats.Add(stat);
            }

            TimeSpan elapsed;

            while (!cancellationToken.IsCancellationRequested)
            {
                lastNetworkStats = currentNetworkStats;
                currentNetworkStats = new List<NetworkBytesStat>();


                var startingTime = Stopwatch.GetTimestamp();
                await Task.Delay(interval);
                p.Start();
                await p.WaitForExitAsync(cancellationToken);
                elapsed = Stopwatch.GetElapsedTime(startingTime);
                netstats = await p.StandardOutput.ReadToEndAsync();
                //System.Console.WriteLine(netstats);


                foreach (var line in netstats.Split("\n", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Skip(1))
                {
                    var s = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);

                    if (interfaces.Length > 0 && !interfaces.Contains(s[0])) continue;
                    if (currentNetworkStats.Any(x => x.Interface == s[0])) continue;
                    stat = new NetworkBytesStat(s[0], Convert.ToInt64(s[6]), Convert.ToInt64(s[9]));
                    currentNetworkStats.Add(stat);
                    lastStat = lastNetworkStats.Where(x => x.Interface == stat.Interface).SingleOrDefault();
                    //System.Console.WriteLine($"{stat.RxBytes} {lastStat.RxBytes}");
                    var rxBps = (stat.RxBytes - lastStat.RxBytes) / elapsed.TotalSeconds;
                    var txBps = (stat.TxBytes - lastStat.TxBytes) / elapsed.TotalSeconds;
                    networkSpeedStats[stat.Interface] = new NetworkSpeedStat(rxBps, txBps);
                }
                yield return networkSpeedStats;
            }
        }

    }


    public record struct NetworkSpeedStat(double RxBytesPerSec, double TxBytesPerSec);

    public record struct NetworkBytesStat(string Interface, long RxBytes, long TxBytes);
}

public static class NetworkUsageCollectorExtensions
{
    public static string FormatNetworkSpeed(this double bytesPerSec)
    {
        if (bytesPerSec < 1024)
        {
            return $"{bytesPerSec:0.} B/s";
        }
        else if (bytesPerSec < 1024 * 1024)
        {
            return $"{bytesPerSec / 1024:#,0.##} KB/s";
        }
        else
        {
            return $"{bytesPerSec / 1024 / 1024:#,0.##} MB/s";
        }
    }
}