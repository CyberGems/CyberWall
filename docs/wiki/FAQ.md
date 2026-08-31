# Frequently Asked Questions

General questions about CyberWall features, configuration, and troubleshooting.

---

## General

### What is CyberWall?
CyberWall is a per-application firewall for Windows powered by the Windows Filtering Platform (WFP). It uses a default-deny architecture that intercepts unknown network connections and prompts you to allow or deny them.

### Is CyberWall free?
Yes. CyberWall is completely free and open source under the GPLv3 license. You can help keep it free [here](https://github.com/CyberGems/CyberWall#-donate).

### Why does CyberWall need Administrator privileges?
WFP operates at the kernel level and requires elevated privileges to install firewall filters and monitor network traffic.

### Does CyberWall replace Windows Defender Firewall?
No. CyberWall works alongside Windows Defender Firewall. It uses the same WFP layer but provides per-application prompts and a more user-friendly interface.

---

## Firewall Modes

### What are the firewall modes?
| Mode | Description |
|---|---|
| **Ask to Connect** | Prompt for each unknown app |
| **Block All** | Silently block unknown apps |
| **Killswitch** | Block all network traffic |
| **Disabled** | No filtering |

### Which mode should I use?
**Ask to Connect** is recommended for most apps. It provides security while letting you control which apps can access the network.

### What is Killswitch?
Killswitch is total network lockdown. All traffic is blocked except for apps you've explicitly allowed. Useful for emergency situations.

---

## Rules

### How do I create rules?
Rules are created automatically when you respond to prompts. You can also manually add rules in Settings → Rules.

### What are Smart Toolchain Rules?
CyberWall automatically resolves companion executables. For example, if you allow Git, it will also allow git-remote-https, ssh, and other Git helper programs.

### Can I edit rules?
Yes. Go to Settings → Rules, right-click any rule to edit its properties.

### How do I backup rules?
Export rules to JSON: Settings → Rules → Export. You can import them later or on another machine.

---

## Prompts

### Why do I keep seeing prompts for the same app?
Check these settings:
- **Remember decision** — Make sure to check "Remember" when responding
- **App version change** — CyberWall re-prompts if the app updates
- **Different connection type** — Inbound vs outbound may have separate rules

### What happens if I don't respond to a prompt?
After the auto-block timeout (default: 5 minutes), the connection is automatically blocked. You can adjust the timeout in Settings.

### Can I disable prompts for specific apps?
Yes. Create a rule for the app with "Allow" verdict and "Remember" enabled. No more prompts for that app.

---

## Connection Monitoring

### What is logged?
Every blocked or allowed connection is logged with: timestamp, app name, protocol, remote IP/port, direction, and action.

### How do I view the connection log?
Click the Log button in the main window, or right-click the tray icon → View Log.

### Can I export logs?
Yes. The log viewer has an export function to save logs as CSV.

---

## Troubleshooting

### CyberWall doesn't start
- Ensure you're running as Administrator
- Check if .NET 10 runtime is installed
- Verify Windows Event Log service is running

### No prompts appearing
- Check if firewall is enabled in Settings
- Verify mode is set to "Ask to Connect"
- Ensure the app doesn't already have a rule

### Legitimate apps can't connect
- Check if "Block All" or "Killswitch" mode is active
- Look for the app in the rules list and verify it's allowed
- Temporarily switch to "Disabled" mode to test

### High CPU usage
- Reduce TCP polling frequency in advanced settings
- Clear old connection logs
- Restart CyberWall

### Conflicts with other firewalls
CyberWall is designed to coexist with Windows Defender Firewall. Third-party firewalls may conflict — test with only one enabled.

---

## Performance

### Does CyberWall slow down my internet?
No. WFP filtering operates at kernel level with minimal overhead. Network speed impact is negligible.

### How much memory does CyberWall use?
Typically 50-100 MB RAM, depending on the number of rules and log entries.

### Does it affect gaming?
CyberWall has minimal performance impact. You can whitelist your game apps to avoid prompts during gameplay.

---

## Contributing

### How can I report a bug?
Open an issue on [GitHub Issues](https://github.com/CyberGems/CyberWall/issues) with:
- CyberWall version
- Windows version
- Steps to reproduce
- Expected vs actual behavior

### How can I contribute code?
1. Fork the repository
2. Create a feature branch
3. Submit a pull request
4. Describe your changes in the PR description

### How can I help with translations?
UI strings are in `src/CyberWall.Common/I18n/`. Submit a PR with your translation.

### How can I donate?
See the [Donate section](https://github.com/CyberGems/CyberWall#-donate) on the main README.
