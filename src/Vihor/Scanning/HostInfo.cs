using System;
using System.Collections.Generic;

namespace Vihor.Scanning
{
    [Flags]
    internal enum Service
    {
        None = 0,
        Ftp = 1 << 0,
        Ssh = 1 << 1,
        Telnet = 1 << 2,
        Http = 1 << 3,
        Https = 1 << 4,
        Smb = 1 << 5,
        Rdp = 1 << 6,
        Vnc = 1 << 7,
        HttpAlt = 1 << 8,
        Printer = 1 << 9,
        Https8443 = 1 << 10,
        Proxmox = 1 << 11,
    }

    internal sealed class PortDef
    {
        public Service Svc;
        public int Port;
        public string Label;
        public string Scheme; // "http" / "https" for web services, otherwise null

        public PortDef(Service svc, int port, string label, string scheme = null)
        {
            Svc = svc; Port = port; Label = label; Scheme = scheme;
        }

        public bool IsWeb => Scheme != null;
    }

    /// <summary>One detected service with whatever the probe could learn (page title, server, banner).</summary>
    internal sealed class ServiceDetail
    {
        public PortDef Def;
        public string Title = "";   // HTML <title>
        public string Server = "";  // HTTP "Server" header
        public string Banner = "";  // SSH / FTP / VNC greeting

        public string Text
        {
            get
            {
                var name = Def.IsWeb && Def.Port != 80 && Def.Port != 443 ? $"{Def.Label} :{Def.Port}" : Def.Label;
                string info;
                if (!string.IsNullOrEmpty(Title)) info = string.IsNullOrEmpty(Server) ? Title : $"{Title} ({Server})";
                else if (!string.IsNullOrEmpty(Server)) info = Server;
                else if (!string.IsNullOrEmpty(Banner)) info = Banner;
                else
                {
                    var key = "svc." + Def.Svc.ToString().ToLowerInvariant();
                    info = Lang.T(key);
                    if (info == key) info = "";
                }
                return info.Length == 0 ? name : $"{name} — {info}";
            }
        }
    }

    internal sealed class HostInfo
    {
        public uint Ip;
        public string IpText => IpRange.ToStr(Ip);
        public bool Alive;
        public long PingMs = -1;
        public string Name = "";
        public string Workgroup = "";
        public byte[] Mac;
        public string Vendor = "";
        public Service Services;
        public List<ServiceDetail> Details = new List<ServiceDetail>();
        public bool IsSelf;
        public bool IsGateway;

        public string MacText => Mac == null ? "" : BitConverter.ToString(Mac).Replace('-', ':');

        public static readonly PortDef[] Ports =
        {
            new PortDef(Service.Http, 80, "HTTP", "http"),
            new PortDef(Service.Https, 443, "HTTPS", "https"),
            new PortDef(Service.HttpAlt, 8080, "HTTP", "http"),
            new PortDef(Service.Https8443, 8443, "HTTPS", "https"),
            new PortDef(Service.Proxmox, 8006, "HTTPS", "https"),
            new PortDef(Service.Ssh, 22, "SSH"),
            new PortDef(Service.Rdp, 3389, "RDP"),
            new PortDef(Service.Smb, 445, "SMB"),
            new PortDef(Service.Ftp, 21, "FTP"),
            new PortDef(Service.Telnet, 23, "Telnet"),
            new PortDef(Service.Vnc, 5900, "VNC"),
            new PortDef(Service.Printer, 9100, "Printer"),
        };

        public string ServicesText
        {
            get
            {
                var list = new List<string>();
                foreach (var p in Ports)
                    if ((Services & p.Svc) != 0)
                        list.Add(p.IsWeb && p.Port != 80 && p.Port != 443 ? $"{p.Label}:{p.Port}" : p.Label);
                return string.Join(", ", list);
            }
        }

        public bool Has(Service s) => (Services & s) != 0;
    }
}
