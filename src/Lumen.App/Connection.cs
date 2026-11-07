using System;
using System.Linq;
using System.Net.NetworkInformation;

namespace Lumen.App;

// How the PC is connected (Ethernet or WLAN) and whether a VPN is up. Asking Windows is cheap but not free,
// so the answer is kept for ten seconds.
public static class Connection
{
	private static DateTime checkedAt = DateTime.MinValue;

	private static string adapter = "";

	private static (string Medium, bool Vpn) last = ("", false);

	private static readonly string[] VpnWords = { "VPN", "TAP-", "WireGuard", "Wintun", "OpenVPN", "AnyConnect", "FortiClient", "GlobalProtect", "NordLynx", "ProtonVPN", "Tailscale", "ZeroTier" };

	public static (string Medium, bool Vpn) Probe(string adapterName)
	{
		if (adapterName == adapter && DateTime.UtcNow - checkedAt < TimeSpan.FromSeconds(10))
		{
			return last;
		}
		adapter = adapterName;
		checkedAt = DateTime.UtcNow;
		try
		{
			var all = NetworkInterface.GetAllNetworkInterfaces().Where(n => n.OperationalStatus == OperationalStatus.Up).ToList();
			var own = all.FirstOrDefault(n => n.Description.Equals(adapterName, StringComparison.OrdinalIgnoreCase) || n.Name.Equals(adapterName, StringComparison.OrdinalIgnoreCase))
				?? all.FirstOrDefault(n => adapterName.Length > 3 && (n.Description.Contains(adapterName, StringComparison.OrdinalIgnoreCase) || adapterName.Contains(n.Description, StringComparison.OrdinalIgnoreCase)));
			string medium = own?.NetworkInterfaceType switch
			{
				NetworkInterfaceType.Wireless80211 => "WLAN",
				NetworkInterfaceType.Ethernet or NetworkInterfaceType.GigabitEthernet or NetworkInterfaceType.FastEthernetT or NetworkInterfaceType.FastEthernetFx or NetworkInterfaceType.Ethernet3Megabit => "Ethernet",
				null => "",
				_ => own.NetworkInterfaceType.ToString()
			};
			bool vpn = all.Any(n => n.NetworkInterfaceType == NetworkInterfaceType.Ppp || VpnWords.Any(w => n.Description.Contains(w, StringComparison.OrdinalIgnoreCase) || n.Name.Contains(w, StringComparison.OrdinalIgnoreCase)));
			last = (medium, vpn);
		}
		catch
		{
			last = ("", false);
		}
		return last;
	}
}
