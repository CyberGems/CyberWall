<p align="center">
  <img src="https://raw.githubusercontent.com/CyberGems/CyberWall/master/src/CyberWall.UI/Assets/CyberWall.png" width="120" alt="CyberWall">
</p>

## 🛡️ CyberWall {{VERSION}}: Release Notes

### 🚀 What's new in this release

<!-- IMPORTANT FOR AGENTS & MAINTAINERS: Always rewrite this entire section and highlights with the ACTUAL release notes for this specific new version before tagging. NEVER leave or copy previous version notes! Use 25-45 words for the summary, lead with user-facing changes, and do not repeat the app name or version. Keep the Spanish mirror inside the details block in sync: translate the summary and rewrite the highlights. -->
<!-- changelog-summary:start -->
The Windows uninstall entry now shows the app icon. Releases ship winget manifests and feed an automated Windows Package Manager submission pipeline, and dependencies are refreshed.
<!-- changelog-summary:end -->

> **New to CyberWall?** A real-time Windows firewall for viewing per-app traffic and allowing or blocking connections with one click.

---

### ✨ Key Features & Highlights

- 🖥️ **Recognizable in Windows**: The Apps uninstall entry displays the CyberWall icon.
- 📦 **Windows Package Manager ready**: Every release ships validated winget manifests alongside the installer and portable archive, and an automated workflow submits them once the submission pipeline is armed.
- 🧰 **Maintenance**: Microsoft.Extensions.Hosting and three other dependencies are updated.

<details>
<summary><b>🇪🇸 Ver notas de la versión en Español</b></summary>

### 🚀 Novedades de esta versión

La entrada de desinstalación de Windows ya muestra el icono de la app. Los releases publican manifiestos winget y alimentan un envío automatizado al Windows Package Manager, y se actualizan dependencias.

> **¿Nuevo en CyberWall?** Un firewall de Windows en tiempo real para ver el tráfico por aplicación y permitir o bloquear conexiones con un clic.

---

### ✨ Novedades destacadas

- 🖥️ **Reconocible en Windows**: La entrada de desinstalación en Aplicaciones muestra el icono de CyberWall.
- 📦 **Listo para el Windows Package Manager**: Cada release publica manifiestos winget validados junto al instalador y el archivo portable, y un workflow automatizado los envía cuando el pipeline se arme.
- 🧰 **Mantenimiento**: Se actualizan Microsoft.Extensions.Hosting y otras tres dependencias.

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
