# Changelog

## 0.4.0 - 2026-10-30

- Options window (Settings → Options): scanning speed slider from fast to thorough (longer timeouts and extra ping/ARP/name attempts on the thorough side), services to detect with checkboxes, and your own TCP ports (web interfaces on them are recognised automatically).
- SSH, FTP and network printers (TCP 9100) are now detected by default; the old "extended" switch is replaced by the service list.
- Scan comparison: devices that are new since the last complete scan of the same range are highlighted, devices that disappeared are listed in grey. Type "new" or "missing" in the filter to show only those.
- Expand all / Collapse all and Rescan selected buttons in the toolbar (Ctrl++, Ctrl+-, Ctrl+R) and a new View menu.
- Workgroup / domain column (can be hidden in View).
- Dark theme: follows Windows, or choose Light / Dark in View → Theme.

## 0.3.2 — 2026-10-20

- SMB service label: renamed "Shared folders" to "SMB — admin connect" (sr/bs/hr: "admin konekcija") in all languages. Port 445 alone does not mean the user shared a folder (default admin shares C$, ADMIN$, IPC$).
