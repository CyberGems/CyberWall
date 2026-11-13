<p align="center">
  <img src="https://raw.githubusercontent.com/CyberGems/CyberWall/master/src/CyberWall.UI/Assets/CyberWall.png" width="120" alt="CyberWall">
</p>

## 🛡️ CyberWall {{VERSION}}: Release Notes

### 🚀 What's new in this release

<!-- changelog-summary:start -->
Adds automatic and manual .cwbackup snapshots with selective restore, network adapter selection across live graphs, embedded Notifications and About tabs, a CyberGems tray suite menu, timed alert pauses, sleep-resume rendering protection, mixed-DPI positioning fixes, and refreshed navigation icons.
<!-- changelog-summary:end -->

> **New to CyberWall?** A real-time Windows firewall for viewing per-app traffic and allowing or blocking connections with one click.

---

### ✨ Key Features & Highlights

- 💾 **Automatic & Manual Backups (`.cwbackup`)**:
  - Automatic and on-demand snapshots for firewall rules and preferences with SHA-256 integrity checks, customizable storage folder, and retention between 1 and 7 automatic backups.
  - Selective restore dialog (`BackupRestoreDialog`) with external file browsing and automatic safety snapshots before clearing or restoring.

- 🌐 **Network Adapter Selection & Live Graph Sync**:
  - Choose **Automatic** or pin a specific network interface across Settings, Traffic Monitor, and the Floating Traffic Widget.
  - Switching adapters immediately resets live telemetry series and header graphs.

- 🔔 **Embedded Notifications & About Tabs**:
  - Notifications and About now live cleanly as integrated main-window tabs instead of standalone dialogs.
  - Sort notifications by Date, Type, or App, dismiss individual cards on hover, and check for updates directly inline in About alongside the refreshed CyberGems suite footer.

- 🧭 **Refined Menus, Tray Suite View & Navigation Icons**:
  - Redesigned vector tab icons, neutral-idle More menu items with a grouped Help & Documentation submenu, reliable second-click menu dismissal, and calmer dropdown highlights.
  - Upgraded system tray menu with timed floating-alert pauses, Floating Traffic Widget toggle, and a **More from CyberGems** suite sub-view.

- 🛡️ **Rendering & Multi-DPI Stability**:
  - Added `RenderStabilityManager` protection against `UCEERR_RENDERTHREADFAILURE` on sleep/resume, plus hardened mixed-DPI popup positioning, keyboard shortcuts, and higher-contrast unknown country map flags.

<details>
<summary><b>🌐 Ver notas de la versión en Español</b></summary>

### 🚀 Novedades de esta versión

Incorpora copias de seguridad automáticas y manuales `.cwbackup` con restauración selectiva, selección de adaptador de red en gráficos en vivo, pestañas integradas de Notificaciones y Acerca de, menú de la suite CyberGems en la bandeja, pausas temporizadas de alertas, protección tras suspensión y mejoras multi-DPI.

---

### ✨ Novedades destacadas

- 💾 **Copias de seguridad automáticas y manuales (`.cwbackup`)**:
  - Instantáneas automáticas y bajo demanda para reglas y preferencias con verificación de integridad SHA-256, carpeta personalizable y retención entre 1 y 7 copias automáticas.
  - Diálogo de restauración selectiva con exploración de archivos externos y copias preventivas antes de limpiar o restaurar.

- 🌐 **Selección de adaptador de red y sincronización de gráficos**:
  - Elige **Automático** o fija un adaptador de red específico desde Configuración, Monitor de tráfico o el Widget flotante.
  - Al cambiar de adaptador, los gráficos en vivo y el encabezado reinician su escala de forma inmediata.

- 🔔 **Pestañas integradas de Notificaciones y Acerca de**:
  - Notificaciones y Acerca de pasan a estar integradas dentro de la ventana principal en lugar de ventanas modales independientes.
  - Ordena notificaciones por Fecha, Tipo o Aplicación, descarta tarjetas individuales al pasar el cursor y consulta actualizaciones en línea dentro de la pestaña Acerca de.

- 🧭 **Menús refinados, vista de la suite en bandeja e iconos renovados**:
  - Nuevos iconos vectoriales de pestañas, iconos neutros en el menú Más opciones con submenú de Ayuda y documentación, cierre fiable al segundo clic y desplegables menos estridentes.
  - Menú contextual de bandeja mejorado con pausas temporizadas de alertas flotantes, interruptor del Widget flotante y subvista **Más de CyberGems**.

- 🛡️ **Estabilidad gráfica y soporte multi-DPI**:
  - Protección frente a fallos `UCEERR_RENDERTHREADFAILURE` al reanudar desde suspensión, posicionamiento corregido de popups y bandeja en monitores con distinto DPI, atajos de teclado y mayor contraste en banderas de país no identificado.

</details>

---

### 📦 Downloads & Packages

> ⬇️ **Direct download:** click a file name to download it now. Same files as in **Assets** at the bottom of this page.

| File | Description | Platform |
| :--- | :--- | :--- |
| **[`CyberWall-Setup-{{VERSION}}.exe`](https://github.com/CyberGems/CyberWall/releases/download/{{VERSION}}/CyberWall-Setup-{{VERSION}}.exe)** | 🚀 **Recommended Installer** (Inno Setup with Start Menu, Desktop & Auto-Startup options) | Windows 10 / 11 (x64) |
| **[`CyberWall-{{VERSION}}-Portable-win-x64.zip`](https://github.com/CyberGems/CyberWall/releases/download/{{VERSION}}/CyberWall-{{VERSION}}-Portable-win-x64.zip)** | 💼 **Portable Archive** (Extract and run with Administrator privileges) | Windows 10 / 11 (x64) |

---

### VirusTotal and SHA256

**Installer**: [View VirusTotal report](https://www.virustotal.com/gui/file/{{INSTALLER_HASH}})

```text
{{INSTALLER_HASH}}
```

**Portable archive**: [View VirusTotal report](https://www.virustotal.com/gui/file/{{PORTABLE_HASH}})

```text
{{PORTABLE_HASH}}
```

---

*Crafted with precision by [CyberGems](https://cybergems.org)*
