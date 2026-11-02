using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace Lumen.Core;

public sealed class PingMonitor : IDisposable
{
	private readonly CancellationTokenSource stop = new CancellationTokenSource();

	private readonly object gate = new object();

	private readonly Queue<double?> recent = new Queue<double?>();

	private double? last;

	private double? gatewayLast;

	private string target;

	public string Target
	{
		get
		{
			return target;
		}
		set
		{
			lock (gate)
			{
				target = value;
				recent.Clear();
				last = null;
			}
		}
	}

	public PingMonitor(string target)
	{
		this.target = target;
		Task.Run((Func<Task?>)Loop);
	}

	private async Task Loop()
	{
		Ping ping = new Ping();
		try
		{
			Ping gw = new Ping();
			try
			{
				while (!stop.IsCancellationRequested)
				{
					string host;
					lock (gate)
					{
						host = target;
					}
					double? ms = await Once(ping, host);
					IPAddress iPAddress = Gateway();
					double? num = ((iPAddress != null) ? (await Once(gw, iPAddress.ToString())) : ((double?)null));
					double? num2 = num;
					lock (gate)
					{
						last = ms;
						gatewayLast = num2;
						recent.Enqueue(ms);
						while (recent.Count > 30)
						{
							recent.Dequeue();
						}
					}
					try
					{
						await Task.Delay(2000, stop.Token);
					}
					catch
					{
						break;
					}
				}
			}
			finally
			{
				((IDisposable)gw)?.Dispose();
			}
		}
		finally
		{
			((IDisposable)ping)?.Dispose();
		}
	}

	private static async Task<double?> Once(Ping p, string host)
	{
		try
		{
			PingReply pingReply = await p.SendPingAsync(host, 1500);
			return (pingReply.Status == IPStatus.Success) ? new double?(Math.Max(0.1, pingReply.RoundtripTime)) : ((double?)null);
		}
		catch
		{
			return null;
		}
	}

	private static IPAddress? Gateway()
	{
		try
		{
			return (from g in (from n in NetworkInterface.GetAllNetworkInterfaces()
					where n.OperationalStatus == OperationalStatus.Up
					select n).SelectMany((NetworkInterface n) => n.GetIPProperties().GatewayAddresses)
				select g.Address).FirstOrDefault((IPAddress a) => a.AddressFamily == AddressFamily.InterNetwork && !a.Equals(IPAddress.Any));
		}
		catch
		{
			return null;
		}
	}

	public PingInfo? Read()
	{
		lock (gate)
		{
			if (recent.Count == 0)
			{
				return null;
			}
			List<double> list = (from x in recent
				where x.HasValue
				select x.Value).ToList();
			double? jitterMs = ((list.Count > 1) ? new double?(list.Zip(list.Skip(1), (double a, double b) => Math.Abs(a - b)).Average()) : ((double?)null));
			return new PingInfo(target, last, gatewayLast, jitterMs, 100.0 * (double)recent.Count((double? x) => !x.HasValue) / (double)recent.Count);
		}
	}

	public void Dispose()
	{
		stop.Cancel();
	}
}
