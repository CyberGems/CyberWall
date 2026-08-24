<p align="center">
  <img src="src/CyberWall.UI/Assets/CyberWall.png" width="140" alt="CyberWall logo" />
</p>

# <p align="center">CyberWall — Per-App Windows Firewall (WFP)</p>

<p align="center">
  <img src="https://img.shields.io/badge/license-GPL--3.0-blue.svg" alt="License" />
  <img src="https://img.shields.io/badge/platform-Windows%2010%20%7C%2011-0078D4.svg?logo=windows&logoColor=white" alt="Platform" />
  <img src="https://img.shields.io/badge/.NET-10.0-512BD4.svg?logo=dotnet&logoColor=white" alt=".NET" />
  <img src="https://img.shields.io/badge/version-1.1.1-00F0FF.svg" alt="Version" />
</p>

A modern, ultra-fast, and lightweight application-layer firewall for Windows, powered by the **Windows Filtering Platform (WFP)**. Built on a strict **default-deny (whitelist)** architecture, CyberWall intercepts unknown network connections and displays interactive real-time prompts per **application executable** — turning your PC into an impenetrable network fortress.

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
4. **Rule Enforcement**: Clicking **Allow** or **Block** (with *Remember my choice* checked) writes the rule to `rules.json` and updates the Windows Firewall engine. The waiting application connection is immediately authorized and resumes seamlessly.

---

## 📄 License

This project is licensed under the **GNU General Public License v3.0 (GPL-3.0)**. See the [LICENSE](LICENSE) file for details.

