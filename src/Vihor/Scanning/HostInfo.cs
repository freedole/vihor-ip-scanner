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
        public bool IsSelf;
        public bool IsGateway;

        public string MacText => Mac == null ? "" : BitConverter.ToString(Mac).Replace('-', ':');

        public static readonly (Service Svc, int Port, string Label)[] Ports =
        {
            (Service.Ftp, 21, "FTP"),
            (Service.Ssh, 22, "SSH"),
            (Service.Telnet, 23, "Telnet"),
            (Service.Http, 80, "HTTP"),
            (Service.Https, 443, "HTTPS"),
            (Service.Smb, 445, "SMB"),
            (Service.Rdp, 3389, "RDP"),
            (Service.Vnc, 5900, "VNC"),
            (Service.HttpAlt, 8080, "HTTP:8080"),
            (Service.Printer, 9100, "Printer"),
        };

        public string ServicesText
        {
            get
            {
                var list = new List<string>();
                foreach (var p in Ports) if ((Services & p.Svc) != 0) list.Add(p.Label);
                return string.Join(", ", list);
            }
        }

        public bool Has(Service s) => (Services & s) != 0;
    }
}
