<p align="center">
  <img src="src/CyberWall.UI/Assets/CyberWall.png" width="140" alt="CyberWall logo" />
</p>

# <p align="center">CyberWall — Application Layer Firewall &amp; Real-Time Network Filter</p>

<p align="center">
  <a href="https://github.com/CyberGems/CyberWall/releases/latest"><img src="https://img.shields.io/badge/dynamic/xml?url=https%3A%2F%2Fraw.githubusercontent.com%2FCyberGems%2FCyberWall%2Fmaster%2FDirectory.Build.props&query=%2FProject%2FPropertyGroup%2FVersion&prefix=%E2%9A%A1%20RELEASE%20v&style=for-the-badge&label=&labelColor=555555&color=555555" alt="Download Latest Release" /><img src="https://img.shields.io/badge/-(WINDOWS_64--BIT)-0047B3?style=for-the-badge&logo=windows&logoColor=white" alt="Windows 64-bit" /></a>
  &nbsp;<a href="https://github.com/CyberGems/CyberWall/releases"><img src="https://img.shields.io/badge/All_Releases-Changelog-18181B?style=for-the-badge&logo=github&logoColor=white" alt="All Releases" /></a>
</p>

<p align="center">
  <img src="https://img.shields.io/badge/license-GPL--3.0-blue.svg" alt="License" height="24" />&nbsp;
  <img src="https://img.shields.io/badge/platform-Windows%2010%20%7C%2011-0078D4.svg?logo=windows&logoColor=white" alt="Platform" height="24" />&nbsp;
  <img src="https://img.shields.io/badge/.NET-10.0-512BD4.svg?logo=dotnet&logoColor=white" alt=".NET" height="24" />&nbsp;
  <a href="https://github.com/CyberGems/CyberWall/wiki"><img src="https://img.shields.io/badge/%F0%9F%93%96_Wiki-Documentation-222222?style=flat-square&logo=github&logoColor=white" alt="Wiki" height="24" /></a>
</p>

A modern, high-performance, and lightweight **per-application firewall** for Windows, powered by the **Windows Filtering Platform (WFP)**. Built on a strict **default-deny (whitelist)** architecture, CyberWall intercepts unknown network connections and displays interactive real-time prompts per **application executable** — turning your PC into an impenetrable network fortress.

*Free and open source (GPLv3) — no ads, no tracking, and no data collection. Just enjoy it.*

---

## 🛡️ Why CyberWall? Real-World Threat Protection

Most traditional firewalls either let everything out silently or bombard you with complex IP/port questions. CyberWall takes a simpler, much more effective approach: **nothing accesses the internet unless you explicitly allow that specific program.**

Here is how CyberWall protects your computer against real-world threats in plain terms:

| Threat Category | Protection Level | What CyberWall Does |
| :--- | :---: | :--- |
| **Spyware & Keyloggers** | 🟢 **10 / 10** | **Total Exfiltration Lock**: Even if malware ends up on your disk, it cannot upload your passwords, keystrokes, or documents to an attacker's server without an explicit prompt. |
| **Ransomware & C2 Beacons** | 🟢 **10 / 10** | **Communication Blackout**: Blocks unauthorized background scripts and payloads from contacting their command servers or downloading encryption keys. |
| **Silent CLI & Script Attacks** | 🟢 **9.5 / 10** | **Instant Interception**: Catches terminal tools (`curl`, `powershell`, `ssh`, `git`) within milliseconds if they attempt unexpected outbound network transfers. |
| **Unsolicited Inbound Probes** | 🟢 **10 / 10** | **Automatic Rejection**: External scanners and probes on local networks are dropped cold at the kernel layer. |
| **Reverse Shells & Remote Access** | 🟢 **9.5 / 10** | **Channel Severance**: Hackers attempting to open a remote interactive backdoor find their outbound connection stuck in kernel limbo. |

> [!TIP]
> **Zero Stability Risk**: Unlike other firewalls that install invasive third-party kernel drivers (often causing Blue Screens / BSODs after Windows updates), CyberWall relies directly on Microsoft's native **Windows Filtering Platform (WFP)** engine. You get true kernel-level protection with 100% operating system stability.

---

## ✨ Key Features

- **Real-Time WFP Drop Interception**: Instant event-driven kernel drop detection via Windows Filtering Platform (`Event ID 5157`). Accurately intercepts short-lived CLI processes (`git`, `curl`, `dotnet`, `ssh`) within milliseconds.
- **Smart Toolchain & Suite Rules**: Automatically resolves and authorizes companion helper executables for developer suites like Git (`git.exe`, `git-remote-https.exe`, `git-remote-http.exe`, `ssh.exe`, etc.).
- **Default-Deny / Whitelist Architecture**: Enforces a strict inbound and outbound block-by-default policy, allowing only user-authorized applications.
- **GlassWire-Style Toast Alerts**: Sleek "First Network Activity" notification toasts with graphic direction badges (`↑ Outbound` / `↓ Inbound`).
- **Dedicated Connection Log Viewer**: Real-time event log with instant filtering (by program, IP, port, or PID), one-click clipboard copying, and direct log file access.
- **Visual Theme Engine (`ThemeCard`)**: Interactive visual cards with live UI previews and instant switching:
  - **CyberWall**: Deep obsidian/navy with electric neon cyan and emerald accents.
  - **Dark**: Refined charcoal and neutral slate with vibrant indigo accents.
  - **Light**: Crisp slate and pure white with royal blue accents.
- **Native Windows 11 Chrome**: Pure DWM-rounded corners with subpixel anti-aliasing buffers, custom title bar, and multi-monitor positioning grid.
- **Built-in Auto-Update System**: One-click GitHub Releases update checker with live download progress tracking and silent installer execution.
- **Dedicated About Modal**: Interactive product overview, update manager, and direct access to CyberGems community channels.
- **Fully Bilingual (EN / ES)**: Complete UI localization in English and Spanish with instant language switching.
- **Persistent Rule Store**: Rules are stored persistently in `%ProgramData%\CyberWall\rules.json`.
- **System Tray & Background Daemon**: System tray integration, minimize-to-tray, and support for running as a background Windows Service (SYSTEM).

---

## 🛠️ Tech Stack & Architecture

- **Platform**: Windows 10 / 11 (x64)
- **Framework**: .NET 10 + WPF (Native UI)
- **Filtering Core**: `fwpuclnt.dll` (WFP User-Mode API), Windows Advanced Firewall (`HNetCfg.FwPolicy2`), and Security Audit Event Log (`EventLogWatcher`).

```
CyberWall.slnx
├── src/CyberWall.Common/   -> Core models (AppRule, ConnectionEvent), I18n strings, settings
├── src/CyberWall.Service/  -> WfpEngine, WfpBlockWatcher, RealFirewall, ConnectionMonitor, RuleStore
└── src/CyberWall.UI/       -> Frameless WPF UI, ThemeCard controls, ConnectionPopup, TrayService, Dialogs
```

---

## 🚀 Getting Started

### Prerequisites
- Windows 10/11 (x64)
- .NET 10 SDK

### Building & Running

```powershell
# Build solution
dotnet build

# Run with Administrator privileges (Real WFP filtering)
.\dev-admin.ps1

# Run standard dev session (Simulated filtering if non-elevated)
.\dev.ps1
```

> **Note**: Real kernel-level network filtering and firewall rule enforcement require Administrator privileges.

### 🛡️ Windows SmartScreen

Windows may show a SmartScreen warning the first time you run the CyberWall installer — this is an unsigned hobby app, so Windows hasn't built reputation for the file yet. This is expected; the source is public so you can inspect exactly what it does.

To continue:

1. Click **More info**.
2. Click **Run anyway**.

---

## 🔍 How It Works

1. **Kernel Interception**: When an application without an existing rule attempts to make an outbound or inbound connection, Windows Filtering Platform safely holds the connection at the kernel level.
2. **Instant Event Capture**: `WfpBlockWatcher` receives the drop event in real-time, resolves the NT kernel device path (e.g. `\device\harddiskvolume3\...`) to a standard Win32 file path (`C:\...`), and debounces duplicate triggers.
3. **Interactive Prompt**: A non-intrusive frameless popup appears in your chosen monitor corner showing the application icon, name, protocol, destination endpoint, and graphic direction badge (`↑ Outbound` / `↓ Inbound`).
4. **Rule Enforcement**: CyberWall applies the selected decision to the pending process. **Allow always** and **Block** save a persistent rule to `rules.json` and update the Windows Firewall engine, while **Allow once** keeps the decision for the current session only.

---

## ❤️ Donate

I’ve spent countless hours building and refining **CyberWall** for my own use. I recently decided to share it with the world as part of the [CyberGems](https://github.com/CyberGems#-all-apps--repositories) set of free and open-source tools.

If you’d like to support future updates, I’d truly appreciate it. You can also show your support by [starring the repo on GitHub](https://github.com/CyberGems/CyberWall). Thank you! 🙏

<p align="center">
  <a href="https://www.paypal.com/donate/?hosted_button_id=M4PY3UPJA5Y6Q"><img src="https://img.shields.io/badge/Donate-PayPal-0070BA?style=for-the-badge&logo=paypal" alt="Donate via PayPal" /></a>
</p>

<p align="center">
  <a href="https://ko-fi.com/cybergems"><img src="https://img.shields.io/badge/Support_me_on_Ko--fi-FF5E5B?style=for-the-badge&logo=ko-fi&logoColor=white" alt="Support me on Ko-fi" /></a>
</p>

<p align="center">
  <a href="https://buymeacoffee.com/cybergems"><img src="https://img.shields.io/badge/Buy%20Me%20a%20Coffee-FFDD00?style=for-the-badge&logo=buy-me-a-coffee&logoColor=black" alt="Buy Me a Coffee" /></a>
</p>

<div align="center">

<details>
<summary><b>Crypto donations (BTC, ETH, USDT, LTC) — click to view addresses</b></summary>

| Asset | Address | QR |
|---|---|---|
| **BTC** | <pre><code>bc1q5mxzz05nmvsheqzx7970euswta3fksxzcfzag4</code></pre> | <img src="src/CyberWall.UI/Assets/donate/qr-btc.png" width="90" height="90" alt="BTC QR" /> |
| **ETH** | <pre><code>0x79b703Ec0f77493679Fcd280aF3b983E20c580B8</code></pre> | <img src="src/CyberWall.UI/Assets/donate/qr-eth.png" width="90" height="90" alt="ETH QR" /> |
| **USDT (ERC20 / BEP20)** | <pre><code>0x79b703Ec0f77493679Fcd280aF3b983E20c580B8</code></pre> | <img src="src/CyberWall.UI/Assets/donate/qr-eth.png" width="90" height="90" alt="USDT QR" /> |
| **USDT (TRC20)** | <pre><code>TSVbSk1HSyZ1NprCnAYiw56ECwXgH887mD</code></pre> | <img src="src/CyberWall.UI/Assets/donate/qr-usdt-tron.png" width="90" height="90" alt="USDT TRC20 QR" /> |
| **LTC** | <pre><code>LWGnEHgcFCE2BRkzLnsdPDD8Y8ZeDK577X</code></pre> | <img src="src/CyberWall.UI/Assets/donate/qr-ltc.png" width="90" height="90" alt="LTC QR" /> |

> ⚠️ Send only the selected asset on the indicated network. Using the wrong network will result in permanent loss of funds.

</details>

</div>

## License

CyberWall is distributed under the terms of the GNU General Public License v3.0. See [LICENSE](LICENSE) for the full license text.

Copyright (C) 2026 CyberGems

---

## ❓ FAQ

For frequently asked questions, troubleshooting guides, and detailed configuration instructions, visit the [FAQ](https://github.com/CyberGems/CyberWall/wiki/FAQ) or the [online documentation](https://cybergems.org/docs/cyberwall/FAQ).

---

<p align="center">
  <strong>Thanks for using CyberWall! 🎉</strong><br><br>
  Made by <a href="https://cybergems.org">CyberGems</a>
</p>

---

## 🔗 See also

More free, open-source, privacy-first apps from [**CyberGems**](https://github.com/CyberGems):

| App | Description |
|:---:|---|
| 🕐&nbsp;[**CyberClock**](https://github.com/CyberGems/CyberClock#readme) | Desktop clock with analog & digital display, calendar, timer, stopwatch and relaxation module. |
| 📢&nbsp;[**CyberFeeds**](https://github.com/CyberGems/CyberFeeds#readme) | High-performance, local-first RSS and Atom reader built for speed, privacy and clean reading. |
| 🚀&nbsp;[**CyberLauncher**](https://github.com/CyberGems/CyberLauncher#readme) | Windows application launcher with hot corners, scheduler, system monitor and integrated terminal. |
| 💻&nbsp;[**CyberManager**](https://github.com/CyberGems/CyberManager#readme) | Lightweight, high-performance task manager, virtualized and NT-native — a powerful Task Manager alternative. |
| 📝&nbsp;[**CyberNotes**](https://github.com/CyberGems/CyberNotes#readme) | Privacy-focused note-taking app with rich text, folders, tabs and bcrypt-protected local storage. |
| ⚡&nbsp;[**CyberPaste**](https://github.com/CyberGems/CyberPaste#readme) | Privacy-first clipboard manager for text, code, images, HTML and files. |
| 📸&nbsp;[**CyberSnap**](https://github.com/CyberGems/CyberSnap#readme) | Screen capture and annotation suite with vector tools, high-speed OCR, screen recording and color picker. |
| ⭐&nbsp;[**CyberTray**](https://github.com/CyberGems/CyberTray#readme) | High-performance tray launcher with hotspots, system monitoring, process manager and PIN-protected file vault. |
| 💫&nbsp;[**CyberViewer**](https://github.com/CyberGems/CyberViewer#readme) | Full-featured image viewer and editor engineered for casual and power users. |

➡️ **[Browse all apps at cybergems.org](https://cybergems.org)**
