using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

namespace Lumen.Core;

public static class ExternalIp
{
	private static readonly string[] Services = new string[3] { "https://api.ipify.org", "https://icanhazip.com", "https://ifconfig.me/ip" };

	public static string? Address { get; private set; }

	public static DateTime? At { get; private set; }

	public static bool Busy { get; private set; }

	public static bool Failed { get; private set; }

	public static event Action? Changed;

	public static async Task FetchAsync()
	{
		if (Busy)
		{
			return;
		}
		Busy = true;
		Failed = false;
		ExternalIp.Changed?.Invoke();
		try
		{
			using HttpClient http = new HttpClient
			{
				Timeout = TimeSpan.FromSeconds(6.0)
			};
			http.DefaultRequestHeaders.UserAgent.ParseAdd("Lumen");
			string[] services = Services;
			foreach (string requestUri in services)
			{
				try
				{
					string text = (await http.GetStringAsync(requestUri)).Trim();
					if (IPAddress.TryParse(text, out IPAddress _))
					{
						Address = text;
						At = DateTime.Now;
						return;
					}
				}
				catch
				{
				}
			}
			Failed = true;
		}
		finally
		{
			Busy = false;
			ExternalIp.Changed?.Invoke();
		}
	}
}
