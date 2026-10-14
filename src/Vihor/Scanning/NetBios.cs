using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace Vihor.Scanning
{
    internal sealed class NetBiosInfo
    {
        public string ComputerName;
        public string Workgroup;
        public byte[] Mac;
    }

    /// <summary>NetBIOS Node Status (NBSTAT) query over UDP/137 — returns computer name, workgroup and MAC.</summary>
    internal static class NetBios
    {
        private static readonly byte[] Query = BuildQuery();

        private static byte[] BuildQuery()
        {
            var q = new byte[50];
            var rnd = new Random();
            q[0] = (byte)rnd.Next(256); q[1] = (byte)rnd.Next(256); // transaction id
            q[2] = 0x00; q[3] = 0x00;                                 // flags
            q[4] = 0x00; q[5] = 0x01;                                 // QDCOUNT = 1
            q[12] = 0x20;                                             // name length 32
            // "*" encoded (first-level encoding): '*' = 0x2A -> 'C','K' then 15x 0x00 -> 'A','A'
            q[13] = (byte)'C'; q[14] = (byte)'K';
            for (int i = 15; i < 45; i++) q[i] = (byte)'A';
            q[45] = 0x00;                                             // name terminator
            q[46] = 0x00; q[47] = 0x21;                               // type NBSTAT
            q[48] = 0x00; q[49] = 0x01;                               // class IN
            return q;
        }

        public static async Task<NetBiosInfo> QueryAsync(IPAddress ip, int timeoutMs)
        {
            using var udp = new UdpClient(AddressFamily.InterNetwork);
            try
            {
                udp.Client.ReceiveTimeout = timeoutMs;
                await udp.SendAsync(Query, Query.Length, new IPEndPoint(ip, 137)).ConfigureAwait(false);

                var recv = udp.ReceiveAsync();
                var done = await Task.WhenAny(recv, Task.Delay(timeoutMs)).ConfigureAwait(false);
                if (done != recv) return null;

                var res = await recv.ConfigureAwait(false);
                return Parse(res.Buffer);
            }
            catch
            {
                return null;
            }
        }

        private static NetBiosInfo Parse(byte[] b)
        {
            if (b == null || b.Length < 13) return null;

            // Skip the answer RR name: either a compression pointer or a sequence of labels
            int p = 12;
            if ((b[p] & 0xC0) == 0xC0) p += 2;
            else
            {
                while (p < b.Length && b[p] != 0) p += b[p] + 1;
                p++; // terminator
            }
            // type(2) + class(2) + ttl(4) + rdlength(2)
            int namesOffset = p + 10;
            if (b.Length < namesOffset + 1) return null;

            int count = b[namesOffset];
            int pos = namesOffset + 1;
            var info = new NetBiosInfo();

            for (int i = 0; i < count && pos + 18 <= b.Length; i++, pos += 18)
            {
                string name = Encoding.ASCII.GetString(b, pos, 15).TrimEnd(' ', '\0');
                byte suffix = b[pos + 15];
                bool group = (b[pos + 16] & 0x80) != 0;

                if (suffix == 0x00 && !group && info.ComputerName == null) info.ComputerName = name;
                else if (suffix == 0x00 && group && info.Workgroup == null) info.Workgroup = name;
            }

            if (pos + 6 <= b.Length)
            {
                var mac = new byte[6];
                Array.Copy(b, pos, mac, 0, 6);
                bool zero = true;
                foreach (var x in mac) if (x != 0) { zero = false; break; }
                if (!zero) info.Mac = mac;
            }

            return info.ComputerName == null && info.Mac == null ? null : info;
        }
    }
}
