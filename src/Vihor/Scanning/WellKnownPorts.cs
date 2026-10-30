using System.Collections.Generic;

namespace Vihor.Scanning
{
    /// <summary>Names for common TCP ports, shown for "my ports" and port ranges (e.g. "MySQL :3306").</summary>
    internal static class WellKnownPorts
    {
        private static readonly Dictionary<int, string> Names = new Dictionary<int, string>
        {
            [7] = "Echo", [25] = "SMTP", [53] = "DNS", [69] = "TFTP", [79] = "Finger", [88] = "Kerberos",
            [110] = "POP3", [111] = "RPC", [119] = "NNTP", [135] = "MS RPC", [139] = "NetBIOS", [143] = "IMAP",
            [161] = "SNMP", [179] = "BGP", [389] = "LDAP", [427] = "SLP", [465] = "SMTPS", [500] = "IKE",
            [515] = "LPD", [548] = "AFP", [554] = "RTSP", [587] = "SMTP Submission", [631] = "IPP (CUPS)",
            [636] = "LDAPS", [873] = "rsync", [902] = "VMware", [990] = "FTPS", [993] = "IMAPS", [995] = "POP3S",
            [1080] = "SOCKS", [1194] = "OpenVPN", [1433] = "MS SQL", [1434] = "MS SQL Browser", [1521] = "Oracle DB",
            [1723] = "PPTP", [1883] = "MQTT", [1900] = "UPnP", [2049] = "NFS", [2082] = "cPanel", [2083] = "cPanel SSL",
            [2086] = "WHM", [2087] = "WHM SSL", [2375] = "Docker", [2376] = "Docker TLS", [2379] = "etcd",
            [3000] = "Grafana / Node", [3001] = "Uptime Kuma", [3128] = "Squid proxy", [3260] = "iSCSI",
            [3268] = "LDAP GC", [3306] = "MySQL / MariaDB", [3478] = "STUN/TURN", [3690] = "SVN",
            [4369] = "Erlang EPMD", [4443] = "HTTPS alt.", [5000] = "Synology DSM", [5001] = "Synology DSM SSL",
            [5060] = "SIP", [5061] = "SIP TLS", [5353] = "mDNS", [5432] = "PostgreSQL", [5555] = "ADB",
            [5601] = "Kibana", [5672] = "RabbitMQ", [5683] = "CoAP", [5900] = "VNC", [5938] = "TeamViewer",
            [5985] = "WinRM", [5986] = "WinRM HTTPS", [6379] = "Redis", [6443] = "Kubernetes API",
            [6881] = "BitTorrent", [7070] = "AnyDesk", [7474] = "Neo4j", [8000] = "HTTP alt.", [8008] = "HTTP alt.",
            [8009] = "AJP / Chromecast", [8081] = "HTTP alt.", [8086] = "InfluxDB", [8088] = "HTTP alt.",
            [8096] = "Jellyfin", [8123] = "Home Assistant", [8200] = "Vault", [8291] = "MikroTik Winbox",
            [8333] = "Bitcoin", [8384] = "Syncthing", [8443] = "HTTPS alt.", [8728] = "MikroTik API",
            [8729] = "MikroTik API SSL", [8834] = "Nessus", [8880] = "HTTP alt.", [8883] = "MQTT TLS",
            [8888] = "HTTP alt.", [9000] = "Portainer", [9001] = "Tor / Supervisor", [9042] = "Cassandra",
            [9090] = "Prometheus / Cockpit", [9091] = "Transmission", [9092] = "Kafka", [9200] = "Elasticsearch",
            [9443] = "Portainer HTTPS", [9999] = "HTTP alt.", [10000] = "Webmin", [10050] = "Zabbix agent",
            [10051] = "Zabbix server", [11211] = "Memcached", [25565] = "Minecraft", [27017] = "MongoDB",
            [32400] = "Plex", [51820] = "WireGuard",
        };

        /// <summary>Name for a port, or null if unknown.</summary>
        public static string Name(int port) => Names.TryGetValue(port, out var n) ? n : null;
    }
}
