## 🛡️ CyberWall {{VERSION}} — Release Notes

Welcome to the official **CyberWall {{VERSION}}** release! CyberWall is a high-performance, real-time application-layer firewall powered by the Windows Filtering Platform (WFP) kernel engine.

---

### ✨ Key Features & Highlights

- 🗂️ **GlassWire-Style Multi-Tab Navigation**:
  - Unified main window interface embedding **Firewall**, **Traffic Monitor**, **Connections Log**, and **Security Statistics** directly into seamlessly switchable tabs.
  - Zero-overhead lifecycle management (< 0.1% idle CPU) that automatically suspends background graphics rendering and periodic timers when views are inactive.

- ⚡ **Real-Time Per-Process Bandwidth Telemetry**:
  - High-precision kernel ETW listener (`Microsoft-Windows-Kernel-Network`) capturing IPv4/IPv6 TCP and UDP throughput per executable in real time.
  - Live row-level bandwidth throughput badges in the Rules table with one-click sort-by-speed.
  - Ranked Top Bandwidth Consumers list with visual transfer progress bars and a 1-click **Quick Block** action.
  - Contextual process actions on right-click (*Search online*, *Copy path to clipboard*, and *Open executable folder*) with source-row highlighting, interaction freeze, and auto-dismiss on traffic completion.

- 🌊 **Continuous 60-Second Telemetry Canvas Graph**:
  - DirectX-accelerated `StreamGeometry` real-time wave graph displaying live download and upload traffic history.
  - Dynamic bandwidth summary cards displaying current speeds, 60-second peak throughput, session transfer totals, and active network adapter.

- 📋 **Embedded Real-Time Connections Log**:
  - Live WFP network event log with instant text search, verdict filter pills (*All*, *Blocked*, *Allowed*), country flags, and direct log file management.

- 📊 **Security & Traffic Statistics Dashboard**:
  - 5 KPI summary cards: Total Events, Blocked, Allowed, Block Ratio, and Active Apps.
  - Top 10 Destination Countries with flag badges, Top Applications proportional consumption bar chart, and Inbound vs Outbound traffic flow distribution.
  - Multi-range timeframe selector (*1 Hour*, *24 Hours*, *7 Days*, *30 Days*, and *All Time*).

- 🎨 **Visual & Ergonomic Polish**:
  - High-contrast rules loading indicator with illuminated empty vector shield and neon cyan glow.
  - Mathematically exact High-DPI PerMonitorV2 window centering engine fixing multi-monitor scaling offsets.
  - Expanded default window width for optimal title bar breathing room.

- ⚡ **WFP Kernel-Level Filtering & Asynchronous Pipeline**:
  - Real-time packet interception with ultra-low latency and persistent rules.
  - Asynchronous background rule loading with icon cache pre-warming, eliminating UI delays.
  - Strict Block, Allow, and Interactive Prompt modes with inbound/outbound directional control.

- 🌐 **100% Bilingual Interface**:
  - Complete native support for **English** and **Spanish** across all tabs, badges, dialogs, and tooltips.

---

### 📦 Downloads & Packages

| File | Description | Platform |
| :--- | :--- | :--- |
| **`CyberWall-Setup-{{VERSION}}.exe`** | 🚀 **Recommended Installer** (Inno Setup with Start Menu, Desktop & Auto-Startup options) | Windows 10 / 11 (x64) |
| **`CyberWall-{{VERSION}}-Portable-win-x64.zip`** | 💼 **Portable Archive** (Extract and run with Administrator privileges) | Windows 10 / 11 (x64) |

---

### 🔍 VirusTotal Scan Results (70+ Antivirus Engines)

- 🛡️ **Setup Installer**: [View VirusTotal Inspection Report](https://www.virustotal.com/gui/file/{{INSTALLER_HASH}})  
  *(SHA256: `{{INSTALLER_HASH}}`)*
- 💼 **Portable Archive**: [View VirusTotal Inspection Report](https://www.virustotal.com/gui/file/{{PORTABLE_HASH}})  
  *(SHA256: `{{PORTABLE_HASH}}`)*

---

*Crafted with precision by [CyberGems](https://cybergems.org)*
