using System;
using System.Text.RegularExpressions;

namespace Vihor.Scanning
{
    /// <summary>
    /// Recognises common admin / homelab systems from what a web interface already told us
    /// (page title, Server header, start of the page). Nothing extra is sent to the device.
    /// </summary>
    internal static class Products
    {
        // Checked against title + Server header. Order matters: more specific first.
        private static readonly (Regex Rx, string Name)[] Strong =
        {
            (R(@"proxmox virtual environment|pve-api-daemon"), "Proxmox VE"),
            (R(@"proxmox backup server"), "Proxmox Backup"),
            (R(@"proxmox mail gateway"), "Proxmox Mail Gateway"),
            (R(@"portainer"), "Portainer"),
            (R(@"truenas"), "TrueNAS"),
            (R(@"freenas"), "FreeNAS"),
            (R(@"unraid"), "Unraid"),
            (R(@"openmediavault"), "OpenMediaVault"),
            (R(@"synology|diskstation"), "Synology DSM"),
            (R(@"\bqnap\b"), "QNAP"),
            (R(@"opnsense"), "OPNsense"),
            (R(@"pfsense"), "pfSense"),
            (R(@"\bunifi\b"), "UniFi"),
            (R(@"\bomada\b"), "Omada"),
            (R(@"routeros|mikrotik"), "MikroTik"),
            (R(@"vsphere|vcenter"), "vCenter"),
            (R(@"\besxi\b"), "VMware ESXi"),
            (R(@"idrac|dell remote access"), "Dell iDRAC"),
            (R(@"\bilo\s?\d?\b|integrated lights-out"), "HPE iLO"),
            (R(@"windows admin center"), "Windows Admin Center"),
            (R(@"grafana"), "Grafana"),
            (R(@"zabbix"), "Zabbix"),
            (R(@"prometheus"), "Prometheus"),
            (R(@"checkmk|check_mk"), "Checkmk"),
            (R(@"\bprtg\b"), "PRTG"),
            (R(@"librenms"), "LibreNMS"),
            (R(@"uptime kuma"), "Uptime Kuma"),
            (R(@"webmin|miniserv"), "Webmin"),
            (R(@"\bcockpit\b"), "Cockpit"),
            (R(@"home assistant"), "Home Assistant"),
            (R(@"nextcloud"), "Nextcloud"),
            (R(@"pi-hole"), "Pi-hole"),
            (R(@"adguard home"), "AdGuard Home"),
            (R(@"nginx proxy manager"), "Nginx Proxy Manager"),
            (R(@"casaos"), "CasaOS"),
            (R(@"heimdall"), "Heimdall"),
            (R(@"jellyfin"), "Jellyfin"),
            (R(@"\bplex\b"), "Plex"),
            (R(@"urbackup"), "UrBackup"),
            (R(@"\bveeam\b"), "Veeam"),
            (R(@"gitlab"), "GitLab"),
            (R(@"jenkins"), "Jenkins"),
            (R(@"fortigate|fortinet"), "FortiGate"),
            (R(@"sophos"), "Sophos"),
            (R(@"openwrt|\bluci\b"), "OpenWrt"),
            (R(@"geoserver"), "GeoServer"),
        };

        // Systems whose title is just the host name: recognised by a marker in the page itself.
        private static readonly (Regex Rx, string Name)[] Body =
        {
            (R(@"pve-api|proxmox\.com|/pve2/"), "Proxmox VE"),
            (R(@"syno\.|synology"), "Synology DSM"),
            (R(@"\besxi\b|vmware host client"), "VMware ESXi"),
            (R(@"vsphere-client|vcenter"), "vCenter"),
            (R(@"\bqnap\b"), "QNAP"),
            (R(@"truenas"), "TrueNAS"),
            (R(@"portainer"), "Portainer"),
            (R(@"casaos"), "CasaOS"),
            (R(@"heimdall"), "Heimdall"),
            (R(@"unraid"), "Unraid"),
            (R(@"pi-hole"), "Pi-hole"),
        };

        private static Regex R(string p) => new Regex(p, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

        public static string FromWeb(string title, string server, string html)
        {
            var head = (title ?? "") + " | " + (server ?? "");
            foreach (var (rx, name) in Strong)
                if (rx.IsMatch(head)) return name;
            if (!string.IsNullOrEmpty(html))
            {
                var start = html.Length > 16384 ? html.Substring(0, 16384) : html;
                foreach (var (rx, name) in Body)
                    if (rx.IsMatch(start)) return name;
            }
            return "";
        }

        /// <summary>Ports that belong to one system even without a web page (e.g. MikroTik Winbox).</summary>
        public static string FromPort(Service svc) => svc == Service.Winbox ? "MikroTik" : "";
    }
}
