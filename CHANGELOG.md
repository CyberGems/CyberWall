# Changelog

All notable changes to CyberWall are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html). Published builds and their download links live on the [Releases](https://github.com/CyberGems/CyberWall/releases) page.

## 2.2.0 - 2026-10-20

### Added

- A scheduled watchdog task restores the original firewall policy and removes leftover rules when the application stops unexpectedly, so a crash can no longer leave the host without connectivity.
- The notification area icon turns grey while the protection is off, and its tooltip reports whether filtering is active, including when the Windows service is the one enforcing it.
- Downloaded installers are verified against the published SHA256 hash and discarded when it does not match.
- 75 unit tests cover the rule store, package identity resolution, settings migration, the translation catalog, and firewall rule naming. The pipeline now runs them before packaging.
- The release pipeline validates the release summary and the translation catalog before publishing.

### Changed

- Windows Firewall rule names now carry a stable hash of the executable path, so two programs that share a file name no longer overwrite each other's rule. Rules created by previous versions are recognized and cleaned up on the first run.
- Turning the protection off also removes the per-application rules, so a blocked program is not left blocked after the master switch is turned off.
- The Windows service applies the mode and state stored in the settings, steps aside while the desktop window is open, and only one host owns the firewall engine at a time.
- Rules, settings, and notifications are written atomically, so an interrupted write cannot truncate them.
- The protection state and the selected mode are stored by name, so the chosen mode survives a restart even when it was picked while the protection was off.
- The application migrated to the .NET 10 hosting packages, and the installer targets x64 without the per-user registry entries that duplicated the application manifest.
- The uninstaller restores the firewall policy and removes every rule the application owns.
- Closing the window to the tray explains once that traffic keeps being filtered in the background.

### Fixed

- The firewall policy is verified after being restored, and a machine left in the default-deny state by an unclean shutdown is always returned to an allowed state.
- A failed policy restore now warns the user with the exact recovery command instead of failing silently.
- Every window title is translated, and the missing translations that rendered a raw key in the connections log and the statistics view are fixed.
- The WFP audit policy enabled to detect blocked traffic is restored when the application stops, so the Security log no longer grows indefinitely.
- The activity log no longer corrupts itself and stays capped at a fraction of the disk space it used to take.

## 2.1.0 - 2026-09-12

### Added

- Settings moved into a dedicated fifth tab that replaces the modal dialog, with a centered container limited to 960 px for wide displays.
- `Ctrl+,` and `Ctrl+5` open Settings, and `Ctrl+1` to `Ctrl+4` switch tabs.
- Title bar "more options" menu with Settings, rule refresh, traffic monitor, connection log, statistics, wiki, FAQ, changelog, website, and update check.
- Sorting by live network activity and transfer speed in the rules table.

### Changed

- Bandwidth sampling was reworked to ignore duplicate NDIS filter counter reads, with improved timer thread affinity and high-precision per-process calculations.
- The application icon cache detects when an executable is rebuilt or replaced and updates the icon in place.
- Rounded pill headers for the rules columns, flattened rule expander groups, vector icons in the traffic monitor and connection cards, and refined toast cards.
- Typography and contrast were raised for settings titles, descriptions, and category headings.

### Fixed

- Exaggerated throughput figures caused by duplicated counter reads.

## 2.0.0 - 2026-09-04

### Added

- Unified window with Firewall, Traffic Monitor, Connections Log, and Statistics tabs, with lifecycle hooks that suspend timers and rendering when a view is inactive.
- Per-process bandwidth telemetry through the `Microsoft-Windows-Kernel-Network` ETW provider, with throughput badges per rule row, sorting by speed, and a ranked top consumers list with quick block.
- 60-second download and upload graph with current speed, peak, session total, and active adapter cards.
- Embedded connections log with search, verdict filters, country flags, and log file management.
- Statistics dashboard with five KPIs, the top ten destination countries, top applications, an inbound and outbound split, and a range selector from one hour to all time.
- Context actions on processes (search online, copy path, open executable folder) with row highlighting and auto-dismiss.

### Changed

- Rules load asynchronously with icon cache pre-warming to keep the window responsive.
- High-DPI PerMonitorV2 centering was corrected for multi-monitor setups, and the default window width was increased.

### Fixed

- Startup delay, socket drops when an allow rule was re-applied, and connectivity for loopback applications, Defender, SmartScreen, and Store apps.
- `NullReferenceException` raised while the views initialized.

## 1.9.0 - 2026-08-28

### Added

- Adaptive traffic telemetry stream with auto-calibrated scaling, particle density driven by live saturation, and Fluid Stream, Glow Pulse, and Static modes.
- Application monitor that detects version changes and modified executables.
- On-screen alerts for internet disconnection and restoration, protection state changes, and automatic blocks, each with its own toggle.

### Changed

- Theme engine with the CyberWall, Dark, and Light palettes and themed scrollbars across windows and dialogs.
- Version toasts display clean commit identifiers instead of raw hashes.

## 1.8.0 - 2026-08-28

### Added

- Separate inbound and outbound verdicts per application (Allow, Block, Inherit), enforced by dedicated WFP sub-filters per direction.
- Directional badges on rule rows, a rule editor for changing verdicts at any time, and sorting by country and by direction.
- Country flag badge in the application dialog.

### Changed

- Quick search was renamed to "Search online" and its default query became the executable and display name.

### Fixed

- The GeoIP resolver no longer lets LAN, multicast, or broadcast addresses overwrite verified public country flags.

## 1.7.0 - 2026-08-28

### Added

- Built-in harmonic chime for network prompts, with support for custom WAV and MP3 files, sound preview, and burst throttling when several prompts appear at once.
- Authenticode verification through the native Win32 APIs (`CryptQueryObject`, `CertFindCertificateInStore`) and an "Unsigned" badge for unsigned binaries.
- Start minimized to the notification area.
- Automatic cleanup of obsolete update notifications.

### Changed

- Rule application, WFP enforcement, and Windows Firewall COM rules run on background threads to keep the interface responsive.
- Press feedback on prompt and confirmation buttons, and protection against duplicate verdicts.

### Fixed

- False positives produced by the legacy certificate table parsing.

## 1.6.1 - 2026-08-28

### Changed

- Startup moved from the registry to the Windows Task Scheduler.

### Fixed

- Loopback traffic is allowed again.

## 1.6.0 - 2026-08-27

### Added

- Traffic and security statistics dashboard, reachable from the toolbar and the tray, with theme-adaptive vector graphics and summary export.
- Activity column with a real-time traffic LED per process and bilingual tooltips, sortable by activity.

### Changed

- Statistics are computed in memory over the connection logs, without external charting dependencies.

## 1.5.0 - 2026-08-27

### Added

- Real-time network speed meter with a traffic telemetry stream and adapter detection.
- Keyboard workflow for the rules list, including row navigation and `Space` to toggle a rule.
- Inline quick action buttons on rule rows.
- Stacked notification alerts and exact or relative timestamps in activity tooltips.

### Changed

- The search bar gained a clear button, a hotkey, and keyboard navigation. Light theme badges use a high-contrast royal blue.

## 1.4.0 - 2026-08-27

### Added

- "Clear all rules" in Settings, with a confirmation step that resets the WFP engine.
- Action-specific icons in the modal dialogs.

### Changed

- Keyboard navigation in the rules list (`Home`, `End`, `Page Up`, and `Page Down`), and tooltips dismiss on wheel scroll.
- Centered Settings footer styled after CyberSnap, and 256x256 icon decoding from multi-resolution ICO streams.

### Fixed

- Column header alignment against the scrollbar, and the extra padding between the rule expanders and the first rule.

## 1.3.3 - 2026-08-26

### Fixed

- Post-install launch failed with Inno Setup error 740. The installer now launches through `ShellExec` with `RunAsCurrentUser`.

## 1.3.2 - 2026-08-26

### Changed

- The application manifest requires Administrator, and core DNS and DHCP rules plus setup allow rules are enforced.

## 1.3.1 - 2026-08-26

### Fixed

- Firewall rule registration for install paths that contain spaces.
- The connectivity probe is more resilient.

## 1.3.0 - 2026-08-26

### Added

- Killswitch firewall mode that enforces global override block rules and tears down active TCP sessions.

### Changed

- Silent block mode was renamed, and the live network indicator became a telemetry capsule badge.

## 1.2.0 - 2026-08-25

### Added

- Persistent notification center for missed blocks and protection events.
- Configurable handling of unanswered prompts, including automatic blocking after a wait from 30 seconds to 30 minutes.
- Allow once for the current session.
- Country indicators in prompts, rules, and logs through the local GeoIP database.
- FAQ documentation covering WFP, Windows Firewall integration, modes, rules, logs, permissions, limitations, and troubleshooting.

### Changed

- Package-aware rules keep Store applications under control while prompts are open, and helper processes such as the Git companion binaries resolve more consistently.
- Permission popups were refined, and WFP drop monitoring handles short-lived processes.

## 1.1.1 - 2026-08-24

### Added

- Minimize to tray on close and minimize to taskbar, with a close button tooltip that follows the setting.

### Fixed

- The firewall master toggle state was not retained, and the engine was disposed when minimizing to the tray.
- The DPI awareness manifest and the tray menu clamping were corrected.

## 1.1.0 - 2026-08-24

### Added

- Auto-update system with a dedicated About window.
- Run at Windows startup toggle.
- First network activity toast, direction badges, and DWM rounded window corners.
- Crisp themed vector icons in place of emoji.

## 1.0.1 - 2026-08-23

Initial follow-up tag, published to Git only.

### Added

- Tray icon with minimize to tray.
- Connection block log.

### Fixed

- The permission prompt no longer blocks the interface.
- Ambiguity between the WinForms and WPF namespaces.

## 1.0.0 - 2026-08-23

Initial tag, published to Git only.

### Added

- Per-application WFP whitelist firewall with a custom non-blocking prompt and a Windows service.

---

## Maintaining this file

- Add the entry for a new version in the same commit as the version bump, before tagging, using the same changes as the release notes.
- Describe what users will notice, not internal commits, and keep the newest version at the top.
