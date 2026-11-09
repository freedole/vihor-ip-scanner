# Changelog

## 0.6.0 - 2026-11-09

- New two-row toolbar with line icons: Scan, My network, Rescan, Favorites and Export on top; tools for the selected device below (Web, RDP, SSH, Shares, Ping, Tracert, Wake, Shut down / restart, Expand / Collapse, Copy). View → *Tools bar* and *Icons only*.
- New columns **Type** (with icon: this PC, gateway, router/firewall, hypervisor, virtual machine, NAS, server, computer, printer, camera, phone, IoT, network device) and **OS** (from known systems, SSH banner and ping TTL).
- Virtual network cards are named: Proxmox VM, Hyper-V VM, VMware VM, VirtualBox VM, QEMU/KVM VM, Xen VM, Docker container.
- Warnings (⚠, orange): the same MAC on two IPs, a possible IP conflict (ARP and NetBIOS report different MACs) and HTTPS certificates that expire in less than 30 days. Type ⚠ in the filter to show only those.
- HTTPS services show the certificate issuer and expiry date.
- IP cameras: RTSP (554) checked by default, Hikvision / Dahua / Reolink / Axis recognised.
- Built-in Ping (continuous, with min/avg/max and loss) and Tracert windows instead of a command prompt.
- Favorites: save the current range or the selected devices and scan them with one click.
- Ctrl+K: all commands, device actions and favorites, searchable by typing.
- Double-click / Enter opens what the device offers: recognised admin panel, RDP, web page, SSH or shares.
- Columns: right-click the header (or View → Columns) to show / hide; widths and order are remembered. Full device details in the row tooltip.
- Export to JSON (also `vihor /scan … /export result.json`), with services and certificates.
- Name source (DNS / NetBIOS) shown in the tooltip; private / random MAC addresses labelled clearly.
- Installer and portable versions check GitHub once a day for a new version and show a link in the status bar (Help → Check for updates). The Store version updates through the Store.
- Recognises common admin and homelab systems out of the box and shows them by name in the Services column: Proxmox VE and Proxmox Backup, Portainer, TrueNAS, Synology DSM, QNAP, Unraid, OpenMediaVault, OPNsense, pfSense, UniFi, Omada, MikroTik, VMware ESXi / vCenter, Dell iDRAC, HPE iLO, Grafana, Zabbix, Webmin, Cockpit, Home Assistant, Pi-hole, Nextcloud and more. Only the page title, Server header and start of the page are used; nothing extra is sent.
- The normal scan now also checks the usual admin web ports: 8006, 8007 (Proxmox), 9443, 9000 (Portainer), 5000, 5001 (Synology), 8080, 8443 (UniFi and others), 8043 (Omada), 10000 (Webmin), 9090 (Cockpit), 3000 (Grafana), 8123 (Home Assistant) and 8291 (MikroTik Winbox). Existing settings get them switched on once.
- New optional services in Options: MS SQL Server (1433), MySQL / MariaDB (3306), PostgreSQL (5432).
- New `vihor` command for the command line in any console: the installer has the option *Add Vihor to PATH* (on by default, removed again on uninstall), the Microsoft Store version and Scoop provide `vihor` automatically.
- Hosts behind a firewall that drops packets (e.g. Windows) get a short timeout for the extra ports, so the scan stays fast.

## 0.5.0 - 2026-10-30

- Names for well-known ports found in "my ports" and port ranges, e.g. "MySQL / MariaDB:3306", "Home Assistant:8123", "MikroTik Winbox:8291".
- Notes on devices (right-click → Note…), remembered by MAC address and shown in the new Note column in every scan; searchable with the filter.
- Export (CSV/HTML) now includes the Note and Status (new / missing) columns and the devices missing since the last scan.
- Command line mode for Task Scheduler: `VihorIPScanner.exe /scan 192.168.1.1-254 /export C:\Reports\lan.csv [/speed 1-5] [/ranges]`.
- SSH client choice in Options: automatic (Windows Terminal, then PuTTY, then command prompt) or a fixed one.

## 0.4.2 - 2026-10-30

- Port ranges: limit raised to 10001 ports, so round ranges like 3000-13000 fit.
- Rescan and Advanced rescan show a progress bar and "ports checked X / Y · seconds" in the status bar.

## 0.4.1 - 2026-10-30

- Port ranges in Options (e.g. 3000-3500, 4000-5000, 8000-9000), checked in parallel, up to 5000 ports. By default they are used only for Rescan, so a normal network scan stays fast.
- Warning when the ranges contain more than 1000 ports, with "Don't show this again".
- Advanced rescan… (right-click a device): the Options window for just that device; the settings are not saved.
- Rescan now uses everything set in Options, including the port ranges, and expands the device to show what was found.
- Printers show "Printer" first in the Services column.
- Filter: ✕ button and Esc clear it.

## 0.4.0 - 2026-10-30

- Options window (Settings → Options): scanning speed slider from fast to thorough (longer timeouts and extra ping/ARP/name attempts on the thorough side), services to detect with checkboxes, and your own TCP ports (web interfaces on them are recognised automatically).
- SSH, FTP and network printers (TCP 9100) are now detected by default; the old "extended" switch is replaced by the service list.
- Scan comparison: devices that are new since the last complete scan of the same range are highlighted, devices that disappeared are listed in grey. Type "new" or "missing" in the filter to show only those.
- Expand all / Collapse all and Rescan selected buttons in the toolbar (Ctrl++, Ctrl+-, Ctrl+R) and a new View menu.
- Workgroup / domain column (can be hidden in View).
- Dark theme: follows Windows, or choose Light / Dark in View → Theme.

## 0.3.2 — 2026-10-20

- SMB service label: renamed "Shared folders" to "SMB — admin connect" (sr/bs/hr: "admin konekcija") in all languages. Port 445 alone does not mean the user shared a folder (default admin shares C$, ADMIN$, IPC$).
