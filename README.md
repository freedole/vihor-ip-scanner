<p align="center">
  <img src="assets/vihor-256.png" width="112" alt="Vihor IP Scanner logo">
</p>

<h1 align="center">Vihor IP Scanner</h1>

<p align="center">
  Fast, free &amp; open-source network scanner for Windows — by <a href="https://cyber-security.ba">cyber-security.ba</a>
</p>

<p align="center">
  <a href="https://github.com/freedole/vihor-ip-scanner/releases/latest"><img alt="Latest release" src="https://img.shields.io/github/v/release/freedole/vihor-ip-scanner?label=download"></a>
  <a href="https://github.com/freedole/vihor-ip-scanner/actions/workflows/build.yml"><img alt="Build" src="https://github.com/freedole/vihor-ip-scanner/actions/workflows/build.yml/badge.svg"></a>
  <a href="LICENSE"><img alt="MIT License" src="https://img.shields.io/badge/license-MIT-blue"></a>
</p>

> **Vihor** /ˈviː.hɔr/ — Slavic for *whirlwind*. Made in Bosnia and Herzegovina.

## Features

- **Fast discovery** — parallel ICMP ping + ARP, so devices that block ping are found too
- **Device details** — name (DNS / NetBIOS), IP, MAC address, manufacturer (offline IEEE OUI database), ping time
- **Service detection** — HTTP/HTTPS (incl. 8080, 8443, Proxmox 8006) with page title and server, SSH/FTP/VNC banners, SMB, RDP, Telnet, printers (9100)
- **Expandable rows** — click the arrow next to a device to list its services; double-click a service to open it
- **One-click actions** — Remote Desktop, SSH, open in browser, shared folders (`\\IP`), FTP, ping, traceroute
- **Wake-on-LAN** and remote shutdown / restart (with confirmation)
- **Copy** IP, name, MAC, manufacturer or whole rows · **Export** to CSV (Excel-friendly) and HTML
- **Flexible ranges** — `192.168.1.1-254`, `192.168.1.0/24`, `10.0.0.1-10.0.3.254`, comma-separated lists
- **Languages** — English, Srpski, Hrvatski, Bosanski (installer in 40+ languages)
- **Lightweight** — ~1 MB, no admin rights needed to run, no telemetry, no ads, no internet connection required

## Download

Get the installer or the portable ZIP from the **[latest release](https://github.com/freedole/vihor-ip-scanner/releases/latest)**.
Every release lists SHA-256 checksums in `SHA256SUMS.txt`.

**Requirements:** Windows 10 / 11 (or Windows 7 SP1+ with .NET Framework 4.8).

> Until code signing is in place, Windows SmartScreen may show *"Windows protected your PC"*.
> Click **More info → Run anyway**. You can verify the file against the published SHA-256 checksum and review the source code here.

## Keyboard shortcuts

| Key | Action |
|---|---|
| `F5` | Start / stop scan |
| `Esc` | Stop scan |
| `Enter` / double-click | Default action (RDP → HTTPS → HTTP → shared folders → SSH → ping) |
| `Ctrl+C` | Copy selected IP addresses |
| `Ctrl+F` | Filter results |

## Building from source

```powershell
dotnet build src/Vihor/Vihor.csproj -c Release
iscc /DAppVersion=0.1.0 installer\vihor.iss     # optional: installer (Inno Setup 6.5+)
```

Releases are built by GitHub Actions from tagged commits (`v*`). Nothing is built on a developer machine.

## Translations

UI strings live in [`src/Vihor/Lang`](src/Vihor/Lang) as simple `key=value` files.
To add a language, copy `en.lang` to `<iso-code>.lang` (e.g. `de.lang`), translate the values and open a pull request.

## Privacy

Vihor IP Scanner does not collect or send any data. It only contacts the addresses you choose to scan,
and opens web pages (cyber-security.ba, GitHub) only when you click the corresponding menu item.

## Code signing policy

Code signing for release builds is being requested from the [SignPath Foundation](https://signpath.org).
Until approved, release binaries are unsigned.

- Releases are built only by GitHub Actions from this repository — no local builds are published.
- Committers and approvers: [Đorđe Kaurinović](https://github.com/freedole)

## License

[MIT](LICENSE) © 2026 Đorđe Kaurinović · [cyber-security.ba](https://cyber-security.ba)

MAC vendor data © IEEE Registration Authority.
