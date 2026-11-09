using System;
using System.Linq;

namespace Vihor.Scanning
{
    internal enum DeviceType
    {
        Unknown, ThisPc, Gateway, Router, Network, Hypervisor, VirtualMachine, Nas, Server, Computer, Printer, Camera, Phone, Iot
    }

    /// <summary>Best-effort device type and OS from what the scan already learned (no extra traffic).</summary>
    internal static class DeviceKind
    {
        /// <summary>MAC prefixes of virtual network cards. Checked before the IEEE list, so VMs are named clearly.</summary>
        private static readonly (string Prefix, string Name)[] VirtualNics =
        {
            ("BC2411", "Proxmox VM"),
            ("00155D", "Hyper-V VM"),
            ("005056", "VMware VM"), ("000C29", "VMware VM"), ("000569", "VMware VM"), ("001C14", "VMware VM"),
            ("080027", "VirtualBox VM"), ("0A0027", "VirtualBox host-only"),
            ("525400", "QEMU/KVM VM"),
            ("00163E", "Xen VM"),
            ("001C42", "Parallels VM"),
            ("0242", "Docker container"),
        };

        public static string VirtualVendor(byte[] mac)
        {
            if (mac == null || mac.Length < 6) return null;
            var hex = BitConverter.ToString(mac).Replace("-", "");
            foreach (var v in VirtualNics) if (hex.StartsWith(v.Prefix, StringComparison.Ordinal)) return v.Name;
            return null;
        }

        public static bool IsVirtual(HostInfo h) => VirtualVendor(h.Mac) != null;

        private static bool Any(string text, params string[] words) =>
            !string.IsNullOrEmpty(text) && words.Any(w => text.IndexOf(w, StringComparison.OrdinalIgnoreCase) >= 0);

        private static bool HasProduct(HostInfo h, params string[] names) =>
            h.Details.Any(d => d.Product.Length > 0 && names.Any(n => d.Product.IndexOf(n, StringComparison.OrdinalIgnoreCase) >= 0));

        public static DeviceType Detect(HostInfo h)
        {
            if (h.IsMissing) return DeviceType.Unknown;
            if (h.IsSelf) return DeviceType.ThisPc;
            if (h.IsGateway) return DeviceType.Gateway;
            var v = h.Vendor ?? "";

            if (HasProduct(h, "Proxmox VE", "ESXi", "vCenter", "XCP-ng", "Hyper-V")) return DeviceType.Hypervisor;
            if (HasProduct(h, "TrueNAS", "FreeNAS", "Unraid", "OpenMediaVault", "Synology", "QNAP")) return DeviceType.Nas;
            if (HasProduct(h, "OPNsense", "pfSense", "MikroTik", "RouterOS")) return DeviceType.Router;
            if (h.Has(Service.Printer) || Any(v, "ricoh", "brother", "kyocera", "lexmark", "xerox", "konica", "epson", "canon", "zebra")) return DeviceType.Printer;
            if (h.Has(Service.Rtsp) || HasProduct(h, "Hikvision", "Dahua", "Reolink", "Axis") ||
                Any(v, "hikvision", "dahua", "axis comm", "reolink", "uniview", "hanwha")) return DeviceType.Camera;
            if (HasProduct(h, "iDRAC", "iLO", "Proxmox Backup")) return DeviceType.Server;
            if (HasProduct(h, "UniFi", "Omada") || h.Has(Service.Winbox) ||
                Any(v, "ubiquiti", "cisco", "routerboard", "mikrotik", "aruba", "juniper", "fortinet", "zyxel", "netgear", "tp-link", "d-link", "ruckus", "cambium"))
                return h.Has(Service.Rdp) || h.Has(Service.Smb) ? DeviceType.Computer : DeviceType.Network;
            if (IsVirtual(h)) return DeviceType.VirtualMachine;
            if (h.Has(Service.Rdp) || h.Has(Service.Smb) || !string.IsNullOrEmpty(h.Workgroup)) return DeviceType.Computer;
            if (Any(v, "espressif", "tuya", "shelly", "itead", "sonoff", "tasmota", "silicon labs", "signify", "philips lighting", "nest", "ring", "amazon"))
                return DeviceType.Iot;
            if (h.Has(Service.Ssh) || h.Has(Service.MsSql) || h.Has(Service.MySql) || h.Has(Service.Postgres) || h.Details.Any(d => d.Product.Length > 0))
                return DeviceType.Server;
            bool noServices = h.Services == Service.None;
            if (noServices && (Any(v, "apple", "samsung", "xiaomi", "huawei device", "oppo", "oneplus", "vivo", "motorola", "honor", "realme") ||
                               (h.Mac != null && (h.Mac[0] & 0x02) != 0)))
                return DeviceType.Phone;
            if (h.Has(Service.Http) || h.Has(Service.Https)) return DeviceType.Network;
            return DeviceType.Unknown;
        }

        public static string Text(DeviceType t) => t == DeviceType.Unknown ? "" : Lang.T("type." + t.ToString().ToLowerInvariant());

        /// <summary>Icon key used by the list (see MainForm.BuildIcons).</summary>
        public static string Icon(DeviceType t) => "type_" + t.ToString().ToLowerInvariant();

        /// <summary>OS guess: known systems first, then SSH banner, then the TTL of the ping reply.</summary>
        public static string Os(HostInfo h)
        {
            if (h.IsMissing) return "";
            if (h.IsSelf) return "Windows";
            if (HasProduct(h, "ESXi")) return "VMware ESXi";
            if (HasProduct(h, "MikroTik", "RouterOS")) return "RouterOS";
            if (HasProduct(h, "OPNsense", "pfSense", "TrueNAS Core", "FreeNAS")) return "FreeBSD";
            if (HasProduct(h, "Proxmox", "TrueNAS", "Unraid", "OpenMediaVault", "Synology", "QNAP", "Portainer")) return "Linux";

            var ssh = h.Details.FirstOrDefault(d => d.Def.Svc == Service.Ssh)?.Banner ?? "";
            if (Any(ssh, "ubuntu")) return "Linux (Ubuntu)";
            if (Any(ssh, "debian")) return "Linux (Debian)";
            if (Any(ssh, "raspbian")) return "Linux (Raspberry Pi OS)";
            if (Any(ssh, "freebsd")) return "FreeBSD";
            if (Any(ssh, "dropbear")) return "Linux (embedded)";
            if (Any(ssh, "for_windows", "windows")) return "Windows";

            if (h.Ttl <= 0) return "";
            if (h.Ttl <= 64)
            {
                if (Any(h.Vendor, "apple")) return "macOS / iOS";
                return h.Has(Service.Ssh) || IsVirtual(h) ? "Linux" : "Linux / Unix";
            }
            if (h.Ttl <= 128) return "Windows";
            return Lang.T("os.network");
        }
    }
}
