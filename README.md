<p align="center">
  <img src="src/CyberWall.UI/Assets/CyberWall.png" width="140" alt="CyberWall logo" />
</p>

# <p align="center">CyberWall — Application Layer Firewall &amp; Real-Time Network Filter</p>

<p align="center">
  <img src="https://img.shields.io/badge/license-GPL--3.0-blue.svg" alt="License" />
  <img src="https://img.shields.io/badge/platform-Windows%2010%20%7C%2011-0078D4.svg?logo=windows&logoColor=white" alt="Platform" />
  <img src="https://img.shields.io/badge/.NET-10.0-512BD4.svg?logo=dotnet&logoColor=white" alt=".NET" />
  <img src="https://img.shields.io/badge/version-1.7.0-00F0FF.svg" alt="Version" />
</p>

<p align="center">
  <a href="https://github.com/CyberGems/CyberWall/releases/latest">
    <img src="https://img.shields.io/badge/⚡_Download_Latest_Release-(Windows_64--bit)-00F2FF?style=for-the-badge&logo=windows&logoColor=000000" alt="Download Latest Release" />
  </a>
  <a href="https://github.com/CyberGems/CyberWall/releases">
    <img src="https://img.shields.io/badge/All_Releases-Changelog-18181B?style=for-the-badge&logo=github&logoColor=white" alt="All Releases" />
  </a>
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

- **Platform**: Windows 10 / 11 (x64 / ARM64)
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
- Windows 10/11 (x64 / ARM64)
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

---

## 🔍 How It Works

1. **Kernel Interception**: When an application without an existing rule attempts to make an outbound or inbound connection, Windows Filtering Platform safely holds the connection at the kernel level.
2. **Instant Event Capture**: `WfpBlockWatcher` receives the drop event in real-time, resolves the NT kernel device path (e.g. `\device\harddiskvolume3\...`) to a standard Win32 file path (`C:\...`), and debounces duplicate triggers.
3. **Interactive Prompt**: A non-intrusive frameless popup appears in your chosen monitor corner showing the application icon, name, protocol, destination endpoint, and graphic direction badge (`↑ Outbound` / `↓ Inbound`).
4. **Rule Enforcement**: CyberWall applies the selected decision to the pending process. **Allow always** and **Block** save a persistent rule to `rules.json` and update the Windows Firewall engine, while **Allow once** keeps the decision for the current session only.

---

## ❓ Frequently Asked Questions

### What is CyberWall?

CyberWall is a per-application firewall for Windows. It uses the Windows Filtering Platform (WFP), the built-in Windows Firewall engine, and Windows security audit events to monitor and enforce network decisions for individual executable files.

### Does CyberWall replace Windows Firewall?

No. CyberWall uses Windows Filtering Platform and configures rules through the Windows Firewall engine; it does not install a third-party kernel driver or replace the Windows networking stack. While protection is enabled, it changes the default firewall policy and adds its own per-program or packaged-app rules. Other firewall rules can still affect the final result.

### What does “default-deny” mean?

When CyberWall is enabled, incoming and outgoing traffic from applications without a matching rule is blocked. In **Ask to connect** mode, CyberWall shows a prompt so you can decide. In **Block all** mode, unknown applications are blocked silently and recorded in the notification center.

### What is the difference between the two modes?

- **Ask to connect**: a new, unknown application triggers a prompt with **Block**, **Allow once**, and **Allow always** actions.
- **Block all**: unknown applications are blocked without a permission prompt. Existing allowed rules continue to work.

### What does “Allow once” do?

It allows the application for the current session only. The temporary decision expires after approximately 10 minutes or when the process identity changes. Use **Allow always** to save a persistent application rule.

### Are decisions made per IP address or port?

No. CyberWall currently makes decisions at the application level. A saved allow or block decision applies to the executable, including both inbound and outbound traffic. The prompt still shows the destination address, port, protocol, direction, and approximate country so you can make an informed decision.

### Does CyberWall support Microsoft Store applications?

Yes. Packaged applications are identified by their package identity and receive package-aware Windows Firewall rules. Companion executables inside a package are handled together when CyberWall can resolve them.

### Do rules survive a restart?

Yes. CyberWall stores its application rules in:

```text
%ProgramData%\CyberWall\rules.json
```

The corresponding Windows Firewall rules are also persistent. Removing an application from CyberWall removes its stored rule and the associated CyberWall firewall rules. If an application connects again afterwards, it can appear as a new request.

### Does protection continue when CyberWall is closed?

If CyberWall is minimized to the system tray, it remains running and continues filtering. If the desktop application is fully exited, its active filtering session is stopped; persistent program rules may remain in Windows Firewall, but unknown applications are not covered by CyberWall's default-deny session until protection is enabled again. For background operation, use the included CyberWall Windows Service where appropriate.

### Why do I need Administrator privileges?

Administrator privileges are required to configure Windows Firewall rules, access the Security event log used for real-time WFP drop events, and apply kernel-level filtering. Without elevation, the application can run for UI and development purposes, but the status will show **Simulated** and real enforcement is not guaranteed.

### Does CyberWall inspect the contents of network traffic?

No. CyberWall decides based on the application identity and connection metadata such as direction, protocol, remote address, port, and process ID. It is not an antivirus, malware scanner, intrusion-prevention system, or TLS/deep-packet inspection tool.

### Where can I find the connection log?

The log is stored at:

```text
%ProgramData%\CyberWall\blocked.log
```

It records the time, action, direction, executable path, remote endpoint, and process ID. The in-app **Connection Log** can filter entries by application, IP, port, PID, country, or date, and can open, copy, refresh, or clear the file.

### What happens if I do not answer a prompt?

When **Auto-block unanswered prompts** is enabled, the application is blocked automatically after the configured timeout. The timeout can be changed in Settings from 30 seconds to 30 minutes. If the option is disabled, closing the prompt still applies a temporary block for that session.

### Can CyberWall run alongside Windows Defender Firewall?

Yes. CyberWall is designed to work through the built-in Windows Firewall rather than alongside a separate replacement firewall. Avoid overlapping products that independently rewrite the same firewall policies, because their rules and default actions can conflict.

### How can I recover network access?

Turn the **Firewall** switch off in the main window, or remove the application's block rule and allow it again. Disabling CyberWall stops its active monitoring and restores the Windows profile baseline used by the application—currently **block inbound / allow outbound**—while persistent per-application rules are not automatically deleted.

### Why did an allowed application still fail to connect?

Check that you allowed the correct executable, including any helper process it uses, and confirm that another Windows Firewall rule or security product is not blocking it. Some applications use packaged identities or launch helper binaries; CyberWall handles common packaged apps and Git companion binaries, but not every third-party launcher or helper automatically.

---

## ❤️ Donate

**CyberWall** is a personal open-source project within the **CyberGems** suite. I've spent thousands of hours building and refining it — both for my own use and to share premium-quality software with the world for free.

If you'd like to support this work, a donation would mean a lot. Thank you! 🙏

<p align="center">
  <a href="https://www.paypal.com/donate/?hosted_button_id=M4PY3UPJA5Y6Q"><img src="https://img.shields.io/badge/Donate-PayPal-0070BA?style=for-the-badge&logo=paypal" alt="Donate via PayPal" /></a>
  <a href="https://ko-fi.com/cybergems"><img src="https://img.shields.io/badge/Support_me_on_Ko--fi-FF5E5B?style=for-the-badge&logo=ko-fi&logoColor=white" alt="Support me on Ko-fi" /></a>
  <a href="https://buymeacoffee.com/cybergems"><img src="https://img.shields.io/badge/Buy%20Me%20a%20Coffee-FFDD00?style=for-the-badge&logo=buy-me-a-coffee&logoColor=black" alt="Buy Me a Coffee" /></a>
</p>

<details>
<summary><b>Crypto donations (BTC, ETH, USDT, LTC) — choose the correct network</b> &nbsp;<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 32 32" width="18" height="18" style="vertical-align:middle" role="img" aria-label="BTC"><circle cx="16" cy="16" r="16" fill="#F7931A"/><path fill="#FFF" fill-rule="nonzero" d="M23.189 14.02c.314-2.096-1.283-3.223-3.465-3.975l.708-2.84-1.728-.43-.69 2.765c-.454-.114-.92-.22-1.385-.326l.695-2.783L15.596 6l-.708 2.839c-.376-.086-.746-.17-1.104-.26l.002-.009-2.384-.595-.46 1.846s1.283.294 1.256.312c.7.175.826.638.805 1.006l-.806 3.235c.048.012.11.03.18.057l-.183-.045-1.13 4.532c-.086.212-.303.531-.793.41.018.025-1.256-.313-1.256-.313l-.858 1.978 2.25.561c.418.105.828.215 1.231.318l-.715 2.872 1.727.43.708-2.84c.472.127.93.245 1.378.357l-.706 2.828 1.728.43.715-2.866c2.948.558 5.164.333 6.097-2.333.752-2.146-.037-3.385-1.588-4.192 1.13-.26 1.98-1.003 2.207-2.538zm-3.95 5.538c-.535 2.147-4.151.986-5.325.694l.95-3.81c1.174.293 4.929.872 4.375 3.116zm.535-5.567c-.487 1.953-3.495.96-4.47.717l.86-3.45c.977.243 4.118.697 3.61 2.733z"/></svg> <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 32 32" width="18" height="18" style="vertical-align:middle" role="img" aria-label="ETH"><circle cx="16" cy="16" r="16" fill="#627EEA"/><g fill="#FFF" fill-rule="nonzero"><path fill-opacity=".602" d="M16.498 4v8.87l7.497 3.35z"/><path d="M16.498 4L9 16.22l7.498-3.35z"/><path fill-opacity=".602" d="M16.498 21.968v6.027L24 17.616z"/><path d="M16.498 27.995v-6.028L9 17.616z"/><path fill-opacity=".2" d="M16.498 20.573l7.497-4.353-7.497-3.348z"/><path fill-opacity=".602" d="M9 16.22l7.498 4.353v-7.701z"/></g></svg> <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 32 32" width="18" height="18" style="vertical-align:middle" role="img" aria-label="USDT"><circle cx="16" cy="16" r="16" fill="#26A17B"/><path fill="#FFF" fill-rule="nonzero" d="M17.922 17.383c-.11.008-.68.048-1.924.048-1.055 0-1.732-.04-1.875-.048C10.824 17.147 8 16.142 8 14.92c0-1.22 2.824-2.224 6.123-2.46v3.073c.147.01.828.05 1.889.05 1.233 0 1.804-.042 1.91-.05V12.46c3.295.236 6.115 1.24 6.115 2.46 0 1.222-2.82 2.226-6.115 2.463m0-5.347V9.013h4.996V6h-13.84v3.013h4.996v3.023C10.22 12.33 7 13.567 7 15.02c0 1.455 3.22 2.69 7.078 2.986v6.99h3.844v-6.99c3.854-.296 7.074-1.53 7.074-2.986 0-1.453-3.22-2.69-7.074-2.984"/></svg> <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 32 32" width="18" height="18" style="vertical-align:middle" role="img" aria-label="LTC"><circle cx="16" cy="16" r="16" fill="#345D9D"/><path fill="#FFF" fill-rule="nonzero" d="M10.427 19.161L9 19.768l.942-3.784 1.43-.599 2.062-8.24h4.636l-1.579 6.332 1.428-.598-.944 3.784-1.43.599-1.272 5.093H23.5L22.618 26H12.008z"/></svg></summary>

| Asset | Network | Address | QR |
|---|---|---|---|
| <img src="docs/donate/btc.svg" width="18" height="18" valign="middle" alt="BTC" /> **BTC** | Bitcoin | `bc1q5mxzz05nmvsheqzx7970euswta3fksxzcfzag4` | ![BTC QR](docs/donate/qr-btc.png) |
| <img src="docs/donate/eth.svg" width="18" height="18" valign="middle" alt="ETH" /> **ETH** | Ethereum (ERC20) | `0x79b703Ec0f77493679Fcd280aF3b983E20c580B8` | ![ETH QR](docs/donate/qr-eth.png) |
| <img src="docs/donate/usdt.svg" width="18" height="18" valign="middle" alt="USDT" /> **USDT** | Ethereum (ERC20) | `0x79b703Ec0f77493679Fcd280aF3b983E20c580B8` | ![USDT ERC20 QR](docs/donate/qr-eth.png) |
| <img src="docs/donate/usdt.svg" width="18" height="18" valign="middle" alt="USDT" /> **USDT** | BNB Smart Chain (BEP20) | `0x79b703Ec0f77493679Fcd280aF3b983E20c580B8` | ![USDT BEP20 QR](docs/donate/qr-eth.png) |
| <img src="docs/donate/usdt.svg" width="18" height="18" valign="middle" alt="USDT" /> **USDT** | Tron (TRC20) | `TSVbSk1HSyZ1NprCnAYiw56ECwXgH887mD` | ![USDT TRC20 QR](docs/donate/qr-usdt-tron.png) |
| <img src="docs/donate/ltc.svg" width="18" height="18" valign="middle" alt="LTC" /> **LTC** | Litecoin | `LWGnEHgcFCE2BRkzLnsdPDD8Y8ZeDK577X` | ![LTC QR](docs/donate/qr-ltc.png) |

> ⚠️ Send only the selected asset on the indicated network. Using the wrong network will result in permanent loss of funds.

</details>

## License

CyberWall is distributed under the terms of the GNU General Public License v3.0. See [LICENSE](LICENSE) for the full license text.

---

<p align="center">
  <strong>Thanks for using CyberWall! 🎉</strong><br><br>
  Made by <a href="https://cybergems.org">CyberGems</a>
</p>

