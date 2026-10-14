# Microsoft Store listing — Vihor IP Scanner

Package languages: en-us (one listing). The app UI itself is English, Serbian, Croatian and Bosnian.

## Product name
Vihor IP Scanner

## Category
Developer tools → Utilities & tools (alternative: Utilities & tools)

## Short description (max 200 chars)
Free, open-source network scanner. See every device on your LAN with name, IP, MAC, manufacturer and services, and open them with one click.

## Description
Vihor IP Scanner shows what is on your local network in seconds.

It discovers devices with parallel ping and ARP requests, so it also finds computers and IoT devices that ignore ping. For every device you get the host name (DNS or NetBIOS), IP and MAC address, the network card manufacturer from the official IEEE registry and the response time.

Vihor also detects the management services a device offers — web interfaces on ports 80, 443, 8080, 8443 and 8006 with their page title and server, SSH with its version, Remote Desktop, shared folders, FTP, VNC and network printers. Click the arrow next to a device to see its services and double-click to open them in your browser, SSH client, Remote Desktop or File Explorer.

Built for administrators and power users:
• Automatically recognises your network cards and keeps VPN / virtual adapters separate
• Wake-on-LAN and remote shutdown or restart (with confirmation)
• Copy IP, name, MAC or whole rows; export results to CSV (Excel-friendly) or HTML
• Flexible ranges: 192.168.1.1-254, 192.168.1.0/24, lists separated by commas
• Interface in English, Serbian, Croatian and Bosnian

Vihor does not test or exploit vulnerabilities. It collects no data, has no telemetry or ads and contacts only the addresses you choose to scan. The complete source code is available on GitHub under the MIT license.

Made in Bosnia and Herzegovina by cyber-security.ba.

## What's new in this version
Expandable rows with detected services, web page titles and SSH banners; automatic detection of physical network cards.

## Product features (max 20, 200 chars each)
- Finds devices with ping and ARP, including devices that block ping
- Shows host name, IP, MAC address, manufacturer and response time
- Detects web UIs, SSH, RDP, SMB, FTP, VNC and printers with page titles and banners
- Opens services with one click: browser, SSH, Remote Desktop, File Explorer
- Wake-on-LAN, remote shutdown and restart
- Export to CSV and HTML
- Recognises physical network cards and separates VPN / virtual adapters
- English, Serbian, Croatian and Bosnian interface
- Open source (MIT), no telemetry, no ads

## Search terms (max 7)
ip scanner, network scanner, lan scanner, mac address, wake on lan, port scanner, sysadmin

## URLs
- Website: https://cyber-security.ba
- Support: https://github.com/freedole/vihor-ip-scanner/issues
- Privacy policy: https://github.com/freedole/vihor-ip-scanner#privacy-policy

## Copyright
© 2026 Đorđe Kaurinović, cyber-security.ba

## Age rating (IARC questionnaire)
Utility app; no user-generated content, no communication between users, no purchases, no location sharing → expected rating 3+ / Everyone.

## Notes for certification (Submission options → Notes for certification)
Vihor IP Scanner is a LAN inventory tool for network administrators (similar to Advanced IP Scanner). It uses ICMP ping, ARP (iphlpapi SendARP) and TCP connect to well-known ports on addresses the user enters; it does not exploit vulnerabilities. runFullTrust is required because it is a packaged WinForms desktop application that launches built-in Windows tools (mstsc, ssh, explorer, ping). No account or test credentials are needed: start the app and press Scan to scan the local network.

## Screenshots needed (min 1, 1366×768 or larger, PNG)
1. Main window after a scan (device list)
2. Expanded device with services
3. "My network" adapter menu
4. Context menu (Remote Desktop / SSH / Wake-on-LAN)
Blur Tailscale host names and real MAC addresses before uploading.
