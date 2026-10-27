<p align="center">
  <img src="https://raw.githubusercontent.com/CyberGems/CyberWall/master/src/CyberWall.UI/Assets/CyberWall.png" width="120" alt="CyberWall">
</p>

## 🛡️ CyberWall {{VERSION}}: Release Notes

### 🚀 What's new in this release

<!-- IMPORTANT FOR AGENTS & MAINTAINERS: Always rewrite this entire section and highlights with the ACTUAL release notes for this specific new version before tagging. NEVER leave or copy previous version notes! Use 25-45 words for the summary, lead with user-facing changes, and do not repeat the app name or version. Add the same changes to CHANGELOG.md. -->
<!-- changelog-summary:start -->
The installer no longer launches command-line tools to register startup or to add firewall rules: the application now creates its scheduled task and its own allow rules. Setup no longer shows the process-creation pattern that antivirus heuristics scored as suspicious.
<!-- changelog-summary:end -->

> **New to CyberWall?** A real-time Windows firewall for viewing per-app traffic and allowing or blocking connections with one click.

---

### ✨ Key Features & Highlights

- 🧰 **An installer that no longer calls system tools**:
  - The startup task is registered by the application through the Task Scheduler API, not by `schtasks.exe`.
  - The allow rules for the application and the service are created by the application, not by `netsh.exe`, and they are refreshed on every start.
  - The result is the same: a logon task with the highest privileges and allow rules for both executables.

- 🛡️ **Fewer heuristic triggers**:
  - Setup no longer spawns the command-line tools that read as attacker behaviour, which is what the antivirus engines scored in the previous release.
  - The uninstaller keeps its own cleanup, so an interrupted installation can still restore the Windows firewall policy.

- ✅ **Verification**:
  - 105 unit tests run before packaging, and the pipeline builds and uploads both artifacts to VirusTotal.
  - Every release publishes the SHA256 of the installer and the portable archive.

### ⚠️ Unsigned build

This build is still unsigned while the SignPath Foundation application is under review. Verify the file with the SHA256 below or with the VirusTotal report. If a browser or Defender blocks the download, the [installation guide](https://github.com/CyberGems/CyberWall/wiki/Installation-and-Setup#antivirus-false-positives) explains how to keep it.

<details>
<summary><b>🌐 Ver notas de la versión en Español</b></summary>

### 🚀 Novedades de esta versión

El instalador ya no lanza herramientas de línea de comandos para registrar el inicio ni para añadir reglas de firewall: la aplicación crea su tarea programada y sus propias reglas de permiso. La instalación deja de mostrar el patrón de creación de procesos que las heurísticas de antivirus marcaban como sospechoso.

---

### ✨ Novedades destacadas

- 🧰 **Un instalador que ya no llama a herramientas del sistema**:
  - La tarea de inicio la registra la aplicación con la API del Programador de tareas, no `schtasks.exe`.
  - Las reglas de permiso de la aplicación y del servicio las crea la propia aplicación, no `netsh.exe`, y se refrescan en cada arranque.
  - El resultado es el mismo: una tarea al iniciar sesión con privilegios máximos y reglas de permiso para ambos ejecutables.

- 🛡️ **Menos disparadores de heurísticas**:
  - La instalación ya no crea los procesos que se leen como comandos de un atacante, que es lo que marcaron los motores de antivirus en la versión anterior.
  - El desinstalador conserva su propia limpieza, así una instalación interrumpida todavía puede restaurar la política del firewall de Windows.

- ✅ **Verificación**:
  - 105 pruebas unitarias se ejecutan antes de empaquetar, y la canalización compila y sube ambos artefactos a VirusTotal.
  - Cada release publica el SHA256 del instalador y del archivo portable.

### ⚠️ Compilación sin firmar

Esta compilación sigue sin firmar mientras se revisa la solicitud a SignPath Foundation. Verifica el archivo con el SHA256 de abajo o con el informe de VirusTotal. Si un navegador o Defender bloquean la descarga, la [guía de instalación](https://github.com/CyberGems/CyberWall/wiki/Installation-and-Setup#antivirus-false-positives) explica cómo conservarla.

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
