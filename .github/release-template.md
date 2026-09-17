## 🛡️ CyberWall {{VERSION}} — Release Notes

Welcome to the official **CyberWall {{VERSION}}** release! CyberWall is a high-performance, real-time firewall for Windows: see live traffic per app, allow or block connections in one click, and stay in control.

---

### ✨ Key Features & Highlights

- ⚙️ **Integrated In-Window Settings Tab**:
  - Migrated the configuration interface into a dedicated 5th navigation tab (`SettingsView`), replacing the modal dialog with a modern, non-blocking experience.
  - **Responsive Centered Container**: Smart `MaxWidth="960"` layout ensures optimal readability and eliminates wide gaps between settings and controls on maximized and ultrawide displays.
  - **Refined Typography & Visual Hierarchy**: Increased font sizes and contrast for option titles, descriptions, and category headings for effortless reading on Full HD, 2K, and 4K displays.
  - **Quick Keyboard Shortcuts**: Jump directly to Settings with `Ctrl+,` or `Ctrl+5` (along with `Ctrl+1..4` for instant tab switching).

- 🎛️ **Modern Title Bar "More Options" Menu**:
  - Added a sleek dropdown menu (`...`) next to notifications for instant access to Settings, Rule Refresh, Traffic Monitor, Connection Log, Statistics, Documentation Wiki, FAQ, Changelog, Website, and Update checks.

- ⚡ **Precision Network Telemetry & Real-Time Bandwidth Engine**:
  - Overhauled speed sampling algorithms to eliminate duplicate NDIS filter counter readings and exaggerated throughput figures.
  - Improved timer thread affinity and high-precision calculations for per-process live bandwidth tracking.
  - Added full column sorting by real-time network activity and transfer speed in the firewall rules table.

- 🖼️ **Reactive Application Icon Cache**:
  - Automatic background detection of executable file changes or re-compilations with reactive, in-place UI icon updates.

- 🎨 **Firewall & UI Visual Refinements**:
  - Restyled the rules column headers with a modern rounded pill container (`CornerRadius="8"`).
  - Flattened rule expander groups ("Allowed" and "Blocked") with transparent backgrounds to eliminate visual noise and prevent header collision.
  - Replaced emoji icons with crisp vector geometry across traffic monitor and connection cards.
  - Refined popup notification and toast cards with improved button alignment and tooltip placement.

- 🗂️ **GlassWire-Style Multi-Tab Navigation**:
  - Seamlessly switch between **Firewall**, **Traffic Monitor**, **Connections Log**, **Statistics**, and **Settings**.
  - Zero-overhead lifecycle hooks (< 0.1% idle CPU) automatically suspending timers and rendering when tabs are inactive.

- 🌐 **100% Bilingual Interface**:
  - Complete native support for **English** and **Spanish** across all tabs, dialogs, menus, and tooltips.

---

### 📦 Downloads & Packages

> ⬇️ **Direct download:** click a file name to download it now. Same files as in **Assets** at the bottom of this page.

| File | Description | Platform |
| :--- | :--- | :--- |
| **[`CyberWall-Setup-{{VERSION}}.exe`](https://github.com/CyberGems/CyberWall/releases/download/{{VERSION}}/CyberWall-Setup-{{VERSION}}.exe)** | 🚀 **Recommended Installer** (Inno Setup with Start Menu, Desktop & Auto-Startup options) | Windows 10 / 11 (x64) |
| **[`CyberWall-{{VERSION}}-Portable-win-x64.zip`](https://github.com/CyberGems/CyberWall/releases/download/{{VERSION}}/CyberWall-{{VERSION}}-Portable-win-x64.zip)** | 💼 **Portable Archive** (Extract and run with Administrator privileges) | Windows 10 / 11 (x64) |

---

### 🔍 VirusTotal Scan Results (70+ Antivirus Engines)

> ℹ️ These links open the security report on VirusTotal in a new tab, they do not download the app.

- 🛡️ **Setup Installer**: [View security report on VirusTotal](https://www.virustotal.com/gui/file/{{INSTALLER_HASH}})  
  *(SHA256: `{{INSTALLER_HASH}}`)*
- 💼 **Portable Archive**: [View security report on VirusTotal](https://www.virustotal.com/gui/file/{{PORTABLE_HASH}})  
  *(SHA256: `{{PORTABLE_HASH}}`)*

---

*Crafted with precision by [CyberGems](https://cybergems.org)*
