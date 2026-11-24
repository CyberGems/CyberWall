<p align="center">
  <img src="https://raw.githubusercontent.com/CyberGems/CyberWall/master/src/CyberWall.UI/Assets/CyberWall.png" width="120" alt="CyberWall">
</p>

## 🛡️ CyberWall {{VERSION}}: Release Notes

### 🚀 What's new in this release

<!-- IMPORTANT FOR AGENTS & MAINTAINERS: Always rewrite this entire section and highlights with the ACTUAL release notes for this specific new version before tagging. NEVER leave or copy previous version notes! Use 25-45 words for the summary, lead with user-facing changes, and do not repeat the app name or version. Keep the Spanish mirror inside the details block in sync: translate the summary and rewrite the highlights. -->
<!-- changelog-summary:start -->
Fixes a cross-thread UI guard regression that froze the main Firewall traffic graph, speed gauges, and activity dots, repositions the paused chart badge below the header label, and keeps the close button's Alt+F4 shortcut chip visible in tray mode.
<!-- changelog-summary:end -->

> **New to CyberWall?** A real-time Windows firewall for viewing per-app traffic and allowing or blocking connections with one click.

---

### ✨ Key Features & Highlights

- 📈 **Live Firewall telemetry restored**: Background speed and activity callbacks now check thread-safe visibility and active-tab flags before marshaling to the UI dispatcher, keeping the Firewall live graph, download/upload speed gauges, and activity dots updating continuously.
- ⏸️ **Clearer paused badge placement**: Added a configurable vertical offset (`PausedBadgeTopOffset`) to `TrafficGraphControl` so the paused badge on the Firewall header graph sits cleanly below the chart title instead of overlapping it.
- ⌨️ **Consistent close button hotkey chip**: Kept the `Alt+F4` shortcut hint visible on the close button whether `Send to tray on close` is enabled or disabled.

<details>
<summary><b>🇪🇸 Ver notas de la versión en Español</b></summary>

### 🚀 Novedades de esta versión

Corrige una regresión de acceso entre hilos que congelaba la gráfica principal, los velocímetros y los puntos de actividad, reubica el indicador de pausa bajo el título del gráfico y conserva el chip Alt+F4 al enviar a la bandeja.

> **¿Nuevo en CyberWall?** Un firewall de Windows en tiempo real para ver el tráfico por aplicación y permitir o bloquear conexiones con un clic.

---

### ✨ Novedades destacadas

- 📈 **Telemetría en vivo restaurada**: Los callbacks de velocidad y actividad ahora comprueban indicadores seguros de visibilidad y pestaña activa antes de despachar al hilo de interfaz, manteniendo en movimiento la gráfica principal, los velocímetros y los puntos de actividad.
- ⏸️ **Mejor ubicación del badge de pausa**: Se añadió un desplazamiento vertical configurable (`PausedBadgeTopOffset`) en `TrafficGraphControl` para situar el indicador de pausa debajo del título sin superponerse.
- ⌨️ **Atajo de cierre siempre visible**: Mantiene visible el indicador `Alt+F4` en el botón de cierre tanto al minimizar a la bandeja como al salir directamente.

### ℹ️ Antes de instalar

- **Windows:** esta versión aún no está firmada digitalmente. SmartScreen puede avisar antes de ejecutarla: **Más información › Ejecutar de todas formas**. Cuando Smart App Control está activado, Windows rechaza programas sin firmar; consulta la [política de firma de código](https://github.com/CyberGems/.github/blob/main/CODE_SIGNING.md). Verifica la descarga con los checksums SHA256 que aparecen más abajo.

</details>

---

### ℹ️ Before you install

- **Windows:** this release is not code-signed yet. SmartScreen may ask before it runs: **More info › Run anyway**. Where Smart App Control is on, Windows refuses unsigned programs; see the [code signing policy](https://github.com/CyberGems/.github/blob/main/CODE_SIGNING.md). Verify the download with the SHA256 checksums below.

---

### 📦 Downloads & Packages

> ⬇️ **Direct download:** click a file name to download it now. Same files as in **Assets** at the bottom of this page.

| File | Description | Platform |
| :--- | :--- | :--- |
| **[`CyberWall-Setup-{{VERSION}}.exe`](https://github.com/CyberGems/CyberWall/releases/download/{{VERSION}}/CyberWall-Setup-{{VERSION}}.exe)** | 🚀 **Recommended Installer** (Inno Setup with Start Menu, Desktop & Auto-Startup options) | Windows 10 / 11 (x64) |
| **[`CyberWall-{{VERSION}}-Portable-win-x64.zip`](https://github.com/CyberGems/CyberWall/releases/download/{{VERSION}}/CyberWall-{{VERSION}}-Portable-win-x64.zip)** | 💼 **Portable Archive** (Extract and run with Administrator privileges) | Windows 10 / 11 (x64) |

---

### 🔐 VirusTotal and SHA256

> 🛡️ The report opens on VirusTotal and does not download anything.

**Installer**: [View VirusTotal report](https://www.virustotal.com/gui/file/{{INSTALLER_HASH}})

```text
{{INSTALLER_HASH}}
```

**Portable**: [View VirusTotal report](https://www.virustotal.com/gui/file/{{PORTABLE_HASH}})

```text
{{PORTABLE_HASH}}
```

---

📜 **Full changelog:** browse every version in [CHANGELOG.md](https://github.com/CyberGems/CyberWall/blob/master/CHANGELOG.md).

*Crafted with precision by [CyberGems](https://cybergems.org)*
