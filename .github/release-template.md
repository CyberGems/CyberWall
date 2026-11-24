<p align="center">
  <img src="https://raw.githubusercontent.com/CyberGems/CyberWall/master/src/CyberWall.UI/Assets/CyberWall.png" width="120" alt="CyberWall">
</p>

## 🛡️ CyberWall {{VERSION}}: Release Notes

### 🚀 What's new in this release

<!-- IMPORTANT FOR AGENTS & MAINTAINERS: Always rewrite this entire section and highlights with the ACTUAL release notes for this specific new version before tagging. NEVER leave or copy previous version notes! Use 25-45 words for the summary, lead with user-facing changes, and do not repeat the app name or version. Keep the Spanish mirror inside the details block in sync: translate the summary and rewrite the highlights. -->
<!-- changelog-summary:start -->
Cuts idle CPU usage by caching active network adapter lookups, pausing hidden telemetry refreshes, and idling flat traffic graphs. Adds a unified floating update notice with in-app downloads, customizable global hotkeys, and standardized titlebar shortcuts.
<!-- changelog-summary:end -->

> **New to CyberWall?** A real-time Windows firewall for viewing per-app traffic and allowing or blocking connections with one click.

---

### ✨ Key Features & Highlights

- ⚡ **Lower idle CPU & adapter polling**: Replaced full per-second adapter enumeration and route socket probes with LUID-cached `GetIfEntry2` lookups, suspended hidden/minimized main-window telemetry refreshes, and paused 30 FPS traffic graph animations while series are flat.
- 🔄 **Unified floating update notice**: Replaced modal update dialogs with a non-intrusive floating notice featuring bilingual highlights, live download progress, SHA-256 verification, skip-version persistence, one-click installer launch, and a Settings toggle for automatic notices.
- ⌨️ **Global & titlebar shortcuts**: Added customizable global hotkeys (`Alt+Shift+W` window toggle, `Alt+Shift+F2` floating widget) in Settings → System, standardized `Win+Up`/`Win+Down`/`Alt+F4`/`Ctrl+W` window controls, and aligned the tray tooltip with the suite format.

<details>
<summary><b>🇪🇸 Ver notas de la versión en Español</b></summary>

### 🚀 Novedades de esta versión

Reduce el uso de CPU en reposo mediante caché de adaptadores de red activos, pausa de telemetría con ventana oculta y gráficas sin repintado en reposo. Añade un aviso flotante de actualización con descarga integrada, atajos globales y controles de ventana unificados.

> **¿Nuevo en CyberWall?** Un firewall de Windows en tiempo real para ver el tráfico por aplicación y permitir o bloquear conexiones con un clic.

---

### ✨ Novedades destacadas

- ⚡ **Menor consumo de CPU en reposo**: Se reemplazó la enumeración completa de adaptadores cada segundo por consultas `GetIfEntry2` cacheadas por LUID, se pausó la telemetría y actualización de filas cuando la ventana está oculta o minimizada, y se detuvo la animación de 30 FPS en gráficas planas.
- 🔄 **Aviso flotante unificado de actualizaciones**: Nueva ventana flotante no intrusiva con novedades en inglés y español, progreso de descarga, verificación SHA-256, omisión de versión, instalación directa e interruptor en Configuración → Sistema para avisos automáticos.
- ⌨️ **Atajos globales y barra de título estandarizada**: Nuevos atajos globales configurables (`Alt+Shift+W` para la ventana y `Alt+Shift+F2` para el widget flotante) en Configuración → Sistema, controles unificados (`Win+Up`, `Win+Down`, `Alt+F4`, `Ctrl+W`) y tooltip de bandeja alineado con la suite.

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
