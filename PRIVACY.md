# Privacy — CyberWall

CyberWall runs on your PC and keeps your data on your PC. It has **no account, no telemetry, no analytics SDK and no ads**. It never sends your connection history, rules or settings to its author or to anyone else.

Everything it knows is stored in `%ProgramData%\CyberWall\`: settings, rules, notifications, the blocked-connection log, the local country database and backups. The uninstaller restores the firewall policy and removes its scheduled task and rules, but it does not delete that folder. Remove it manually if you want the history gone.

## When CyberWall goes online

These are all the connections CyberWall makes by itself. None of them sends your connection history or rules. Like any request on the internet, the server sees your IP address.

| When | Where | What for | How to turn it off |
|---|---|---|---|
| About 3 seconds after launch | `api.github.com` | Check GitHub Releases for a newer version | `AutoCheckForUpdates` in Settings |
| On launch, and when the log or statistics view opens | `cdn.jsdelivr.net`, with `unpkg.com` as fallback | Download the public IP-to-country table used to label connections. Lookups happen locally; destination IPs are not uploaded | No dedicated setting |
| Every 15 seconds and on network changes | `www.msftconnecttest.com`, `www.google.com`, `1.1.1.1` | Reachability probes for the online indicator | No dedicated setting |

A web search, or a link to the wiki or the website that you click yourself, is your own action and is not a connection the app makes by itself.

## What CyberWall reads on your PC

CyberWall reads the TCP connection table, running processes, network adapters, startup entries and the Windows Firewall policy to show you what is connecting and to apply the rules you approve. That information stays on your screen. Any rule that changes the system is created only after your confirmation.

## Questions

Open an issue: https://github.com/CyberGems/CyberWall/issues

For a privacy-specific question, use the [contact page](https://cybergems.org/contact). The suite-wide statement is at [cybergems.org/privacy](https://cybergems.org/privacy).
