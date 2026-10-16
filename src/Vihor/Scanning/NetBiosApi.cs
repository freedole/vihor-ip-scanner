using System;
using System.Collections.Generic;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace Vihor.Scanning
{
    /// <summary>
    /// NetBIOS adapter status through the Windows NetBIOS API (netapi32 Netbios, NCBASTAT) — the same
    /// mechanism as "nbtstat -A". The query is sent by the NetBT driver from UDP/137 of the OS, so it is
    /// not affected by per-application firewall rules. Used as a fallback when our own UDP query gets no answer.
    /// </summary>
    internal static class NetBiosApi
    {
        private const byte NCBENUM = 0x37, NCBRESET = 0x32, NCBASTAT = 0x33;
        private const int NameCount = 64;

        [DllImport("netapi32.dll")]
        private static extern byte Netbios(IntPtr pncb);

        private static readonly object InitLock = new object();
        private static byte[] lanas;
        private static readonly SemaphoreSlim Gate = new SemaphoreSlim(8);

        // NCB layout: command(1) retcode(1) lsn(1) num(1) [pad] buffer(ptr) length(2) callname(16) name(16)
        // rto(1) sto(1) [pad] post(ptr) lana_num(1) cmd_cplt(1) reserve(10 on x86 / 18 on x64) [pad] event(ptr)
        private static int Ptr => IntPtr.Size;
        private static int OffBuffer => Ptr;                      // 4 / 8
        private static int OffLength => OffBuffer + Ptr;          // 8 / 16
        private static int OffCallname => OffLength + 2;          // 10 / 18
        private static int OffName => OffCallname + 16;          // 26 / 34
        private static int OffRto => OffName + 16;               // 42 / 50
        private static int OffPost => Align(OffRto + 2, Ptr);    // 44 / 56
        private static int OffLana => OffPost + Ptr;             // 48 / 64
        private static int OffCplt => OffLana + 1;
        private static int OffReserve => OffLana + 2;
        private static int OffEvent => Align(OffReserve + (Ptr == 8 ? 18 : 10), Ptr);  // 60 / 88
        private static int NcbSize => OffEvent + Ptr;                                  // 64 / 96

        private static int Align(int v, int a) => (v + a - 1) / a * a;

        private static byte Call(byte command, byte lana, byte[] callname, IntPtr buffer, int length)
        {
            IntPtr ncb = Marshal.AllocHGlobal(NcbSize);
            try
            {
                for (int i = 0; i < NcbSize; i++) Marshal.WriteByte(ncb, i, 0);
                Marshal.WriteByte(ncb, 0, command);
                Marshal.WriteIntPtr(ncb, OffBuffer, buffer);
                Marshal.WriteInt16(ncb, OffLength, (short)length);
                if (callname != null)
                    for (int i = 0; i < 16; i++) Marshal.WriteByte(ncb, OffCallname + i, callname[i]);
                Marshal.WriteByte(ncb, OffLana, lana);
                return Netbios(ncb);
            }
            finally
            {
                Marshal.FreeHGlobal(ncb);
            }
        }

        private static void Init()
        {
            lock (InitLock)
            {
                if (lanas != null) return;
                var list = new List<byte>();
                try
                {
                    const int enumSize = 256; // LANA_ENUM: length + lana[MAX_LANA+1]
                    IntPtr buf = Marshal.AllocHGlobal(enumSize);
                    try
                    {
                        for (int i = 0; i < enumSize; i++) Marshal.WriteByte(buf, i, 0);
                        if (Call(NCBENUM, 0, null, buf, enumSize) == 0)
                        {
                            int n = Marshal.ReadByte(buf, 0);
                            for (int i = 0; i < n; i++)
                            {
                                byte lana = Marshal.ReadByte(buf, 1 + i);
                                if (Call(NCBRESET, lana, null, IntPtr.Zero, 0) == 0) list.Add(lana);
                            }
                        }
                    }
                    finally { Marshal.FreeHGlobal(buf); }
                }
                catch { /* NetBIOS API not available */ }
                lanas = list.ToArray();
                Diag.Log("netbios api: LANA " + (lanas.Length == 0 ? "none" : string.Join(",", lanas)));
            }
        }

        /// <summary>Blocking; call from a worker thread. Returns null when the host does not answer.</summary>
        public static NetBiosInfo Query(IPAddress ip)
        {
            Init();
            if (lanas.Length == 0) return null;

            Gate.Wait();
            try
            {
                var callname = Encoding.ASCII.GetBytes(ip.ToString().PadRight(16, ' ').Substring(0, 16));
                int size = 60 + 18 * NameCount; // ADAPTER_STATUS + NAME_BUFFER[]
                IntPtr buf = Marshal.AllocHGlobal(size);
                try
                {
                    foreach (var lana in lanas)
                    {
                        for (int i = 0; i < size; i++) Marshal.WriteByte(buf, i, 0);
                        if (Call(NCBASTAT, lana, callname, buf, size) != 0) continue;

                        var info = new NetBiosInfo();
                        var mac = new byte[6];
                        Marshal.Copy(buf, mac, 0, 6);
                        bool zero = true;
                        foreach (var b in mac) if (b != 0) { zero = false; break; }
                        if (!zero) info.Mac = mac;

                        int count = Math.Min((int)(ushort)Marshal.ReadInt16(buf, 58), NameCount);
                        for (int i = 0; i < count; i++)
                        {
                            int off = 60 + i * 18;
                            var raw = new byte[16];
                            Marshal.Copy(buf + off, raw, 0, 16);
                            string name = Encoding.ASCII.GetString(raw, 0, 15).TrimEnd(' ', '\0');
                            byte suffix = raw[15];
                            bool group = (Marshal.ReadByte(buf, off + 17) & 0x80) != 0;
                            if (suffix == 0 && !group && info.ComputerName == null) info.ComputerName = name;
                            else if (suffix == 0 && group && info.Workgroup == null) info.Workgroup = name;
                        }
                        if (info.ComputerName != null || info.Mac != null) return info;
                    }
                }
                finally { Marshal.FreeHGlobal(buf); }
            }
            catch { }
            finally { Gate.Release(); }
            return null;
        }
    }
}
