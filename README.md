# CyberWall — Per-App Windows Firewall (WFP)

A robust, modern, and lightweight application-layer firewall for Windows, built on the **Windows Filtering Platform (WFP)**. Designed with a strict **default-deny (whitelist)** architecture, CyberWall intercepts unknown network connections and displays interactive real-time prompts per **application executable** (rather than per IP address).

---

## Key Features

- **Real-Time WFP Drop Interception**: Instant event-driven kernel drop detection via Windows Filtering Platform (`Event ID 5157`). Accurately intercepts short-lived CLI processes (such as `git push`, `curl`, `dotnet`, `ssh`) within milliseconds.
- **Smart Toolchain & Suite Rules**: Automatically resolves and authorizes companion helper executables for developer suites like Git (`git.exe`, `git-remote-https.exe`, `git-remote-http.exe`, `ssh.exe`, etc.).
- **Default-Deny / Whitelist Architecture**: Enforces a strict inbound and outbound block-by-default policy, allowing only user-authorized applications.
- **Modern Frameless Window Chrome**: Custom dark-themed titlebar with smooth dragging, custom window controls, and crisp typography.
- **Visual Theme Selector (`ThemeCard`)**: Interactive visual cards with live UI previews and instant switching:
  - **CyberWall**: Deep obsidian/navy with electric neon cyan and emerald accents.
  - **Dark**: Refined charcoal and neutral slate with vibrant indigo accents.
  - **Light**: Crisp slate and pure white with royal blue accents.
- **Fully Bilingual (EN / ES)**: Complete UI localization in English and Spanish with instant language switching.
- **Persistent Rule Store**: Rules are stored persistently in `%ProgramData%\CyberWall\rules.json`.
- **System Tray & Background Daemon**: System tray integration, minimize-to-tray, and support for running as a background Windows Service (SYSTEM).

---

## Tech Stack & Architecture

- **Platform**: Windows 10 / 11 (x64 / ARM64)
- **Framework**: .NET 10 + WPF (Native UI)
- **Filtering Core**: `fwpuclnt.dll` (WFP User-Mode API), Windows Advanced Firewall (`HNetCfg.FwPolicy2`), and Security Audit Event Log (`EventLogWatcher`).

```
CyberWall.slnx
├── src/CyberWall.Common/   -> Core models (AppRule, ConnectionEvent), I18n strings, settings
├── src/CyberWall.Service/  -> WfpEngine, WfpBlockWatcher, RealFirewall, ConnectionMonitor, RuleStore
└── src/CyberWall.UI/       -> Frameless WPF UI, ThemeCard controls, ConnectionPopup, TrayService
```

---

## Getting Started

### Prerequisites
- Windows 10/11
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

## How It Works

1. **Interception**: When an application without an existing rule attempts to make an outbound or inbound connection, Windows Filtering Platform blocks the connection.
2. **Instant Event Capture**: `WfpBlockWatcher` receives the drop event in real-time, resolves the NT kernel device path (e.g. `\device\harddiskvolume3\...`) to a standard Win32 file path (`C:\...`), and debounces duplicate triggers.
3. **Interactive Prompt**: A non-intrusive frameless popup appears at the bottom-right of the screen displaying the application icon, name, protocol, and destination.
4. **Rule Application**: Upon clicking **Allow** or **Block** (with *Remember my choice* checked), the rule is saved to `rules.json` and applied to Windows Firewall rules. Future connection attempts will be handled automatically without prompting.

---

## License

This project is licensed under the **GNU General Public License v3.0 (GPL-3.0)**. See the [LICENSE](LICENSE) file for details.
