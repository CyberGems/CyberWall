# CyberWall — Per-App Windows Firewall (WFP)

Robust yet minimal firewall inspired by simplewall. Windows-only. Default-deny (whitelist), popup per **new program** (not per IP).

## Stack (Windows-only)
- **.NET 10 + WPF** (native UI) + `fwpuclnt.dll` (WFP) via P/Invoke
- **Service/Engine** in `CyberWall.Service` (requires Admin) + **UI** in `CyberWall.UI`
- **Per-app rules** in `%ProgramData%\CyberWall\rules.json` — persistent filters like simplewall
- **Bilingual** EN/ES (`CyberWall.Common/I18n/Strings.cs`)
- **IPC** Named Pipe `CyberWall_Engine` (ready for UI↔Service split)

## Structure
```
CyberWall.slnx
src/CyberWall.Common  -> AppRule, ConnectionEvent, i18n, pipe protocol
src/CyberWall.Service -> WfpEngine, RuleStore, FirewallService, PipeServer
src/CyberWall.UI      -> MainWindow (rules list) + ConnectionPopup (allow/block)
```

## Run
```ps
dotnet build
.\dev.ps1              # dev UI (embedded engine, 1 terminal)
.\dev-admin.ps1        # dev UI as Admin (real WFP filtering)
dotnet run --project src/CyberWall.Service # engine only
```

> Real WFP filtering requires Administrator. Without it, runs in simulated mode (Classify → Ask).

## Popup flow per program
1. `WfpEngine.Classify(appPath)` → no rule → `Verdict.Ask`
2. `ConnectionPopup` shows `app.exe wants to connect — Outbound TCP 1.2.3.4:443`
3. User picks Allow/Block (+ Remember) → `RuleStore.Upsert()` + `netsh advfirewall` rule
4. Next connection from same exe no longer asks.

## Features
- Master toggle ON/OFF + mode: `Ask to connect` vs `Block all` (silent)
- Custom frameless popup (bottom-right, stacked, like CyberFeeds)
- System tray + minimize to tray
- Blocked log at `%ProgramData%\CyberWall\blocked.log`
- Installer with Windows Service (`installer/install.ps1`)

## Known limitation
Blocked outbound connections never reach TCP table, so the poller may miss some and not show a popup. Allow `git.exe` / `git-remote-https.exe` manually if needed, or watch `blocked.log`.
