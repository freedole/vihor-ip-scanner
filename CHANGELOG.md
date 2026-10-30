# Changelog

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
