
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

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

                foreach (var line in netstats.Split("\n", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Skip(1))
                {
                    var s = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);

                    if (interfaces.Length > 0 && !interfaces.Contains(s[0])) continue;
                    if (currentNetworkStats.Any(x => x.Interface == s[0])) continue;
                    stat = new NetworkBytesStat(s[0], Convert.ToInt64(s[6]), Convert.ToInt64(s[9]));
                    currentNetworkStats.Add(stat);
                    lastStat = lastNetworkStats.Where(x => x.Interface == stat.Interface).SingleOrDefault();
                    var rxBps = (stat.RxBytes - lastStat.RxBytes) / elapsed.TotalSeconds;
                    var txBps = (stat.TxBytes - lastStat.TxBytes) / elapsed.TotalSeconds;
                    networkSpeedStats[stat.Interface] = new NetworkSpeedStat(rxBps, txBps);
                }
                yield return networkSpeedStats;
            }
        }

    }


    public async IAsyncEnumerable<Dictionary<string, NetworkSpeedStat>> GetLinuxNetworkUsage(TimeSpan interval = default(TimeSpan), [EnumeratorCancellation] CancellationToken cancellationToken = default(CancellationToken), params string[] interfaces)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (interval == TimeSpan.Zero) { interval = TimeSpan.FromSeconds(1); }
        var netstat = await File.ReadAllTextAsync("/proc/net/dev", cancellationToken);
        var lines = netstat.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        List<NetworkUsageCollector.NetworkBytesStat> currentNetworkStats = new List<NetworkUsageCollector.NetworkBytesStat>();
        List<NetworkUsageCollector.NetworkBytesStat> lastNetworkStats;
        Dictionary<string, NetworkUsageCollector.NetworkSpeedStat> networkSpeedStats = new Dictionary<string, NetworkUsageCollector.NetworkSpeedStat>();

        foreach (var line in lines.Skip(2))
        {
            var s = line.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var ifname = s[0].Substring(0, s[0].Length - 1);
            if(interfaces.Length > 0 && !interfaces.Contains(ifname)) continue;
            currentNetworkStats.Add(new NetworkUsageCollector.NetworkBytesStat(s[0].Substring(0, s[0].Length - 1), Convert.ToInt64(s[1]), Convert.ToInt64(s[9])));
        }

        while (!cancellationToken.IsCancellationRequested)
        {
            lastNetworkStats = currentNetworkStats;
            currentNetworkStats = new List<NetworkUsageCollector.NetworkBytesStat>();
            var startingTime = Stopwatch.GetTimestamp();
            await Task.Delay(interval, cancellationToken);
            netstat = await File.ReadAllTextAsync("/proc/net/dev", cancellationToken);
            var elasped = Stopwatch.GetElapsedTime(startingTime);
            lines = netstat.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            foreach (var line in lines.Skip(2))
            {
                var s = line.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                var ifname = s[0].Substring(0, s[0].Length - 1);
                if(interfaces.Length > 0 && !interfaces.Contains(ifname)) continue;
                var stat = new NetworkUsageCollector.NetworkBytesStat(ifname, Convert.ToInt64(s[1]), Convert.ToInt64(s[9]));
                currentNetworkStats.Add(stat);
                var lastStat = lastNetworkStats.Where(x => x.Interface == ifname).SingleOrDefault();
                var rxBps = (stat.RxBytes - lastStat.RxBytes) / elasped.TotalSeconds;
                var txBps = (stat.TxBytes - lastStat.TxBytes) / elasped.TotalSeconds;
                networkSpeedStats[ifname] = new NetworkUsageCollector.NetworkSpeedStat(rxBps, txBps);
            }
            yield return networkSpeedStats;
        }
    }

    public async IAsyncEnumerable<Dictionary<string, NetworkSpeedStat>> GetWindowsNetworkUsage(TimeSpan interval = default(TimeSpan), [EnumeratorCancellation] CancellationToken cancellationToken = default(CancellationToken), params string[] interfaces)
    {
        if(!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            throw new PlatformNotSupportedException();

        if(interval == default(TimeSpan))
        {
            interval = TimeSpan.FromSeconds(1);
        }

        if(interfaces.Length == 0)
        {
            var category = new PerformanceCounterCategory("Network Interface");
            interfaces = category.GetInstanceNames();
        }

        var ifnames = interfaces.Where(x => PerformanceCounterCategory.InstanceExists(x, "Network Interface")).ToArray();

        var counters = new Dictionary<string, (PerformanceCounter rx,PerformanceCounter tx)>();
        foreach(var ifname in ifnames)
        {
            counters[ifname] = (new PerformanceCounter("Network Interface", "Bytes Received/sec", ifname, true), new PerformanceCounter("Network Interface", "Bytes Sent/sec", ifname, true));
        }

        var networkSpeedStats = new Dictionary<string, NetworkSpeedStat>();

        while(!cancellationToken.IsCancellationRequested)
        {
            await Task.Delay(interval, cancellationToken);
            foreach(var counter in counters)
            {
                networkSpeedStats[counter.Key] = new NetworkSpeedStat(counter.Value.rx.NextValue(), counter.Value.tx.NextValue());
            }
            yield return networkSpeedStats;
            
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