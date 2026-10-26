<p align="center">
  <img src="https://raw.githubusercontent.com/CyberGems/CyberWall/master/src/CyberWall.UI/Assets/CyberWall.png" width="120" alt="CyberWall">
</p>

## 🛡️ CyberWall {{VERSION}}: Release Notes

### 🚀 What's new in this release

<!-- IMPORTANT FOR AGENTS & MAINTAINERS: Always rewrite this entire section and highlights with the ACTUAL release notes for this specific new version before tagging. NEVER leave or copy previous version notes! Use 25-45 words for the summary, lead with user-facing changes, and do not repeat the app name or version. Add the same changes to CHANGELOG.md. -->
<!-- changelog-summary:start -->
Firewall protection can now be turned off without losing the core service rules, so localhost applications keep working. The firewall policy is restored in a verified order, the About window shows the rest of the CyberGems suite, and the installer carries the app artwork.
<!-- changelog-summary:end -->

> **New to CyberWall?** A real-time Windows firewall for viewing per-app traffic and allowing or blocking connections with one click.

---

### ✨ Key Features & Highlights

- 🧩 **Turning the protection off keeps the core rules**:
  - The DNS, DHCP, NTP and loopback rules now survive switching the protection off, so applications that talk to localhost keep working after the master switch is used.
  - Only the per-application rules are removed, which is what the legacy cleanup and the crash recovery script already did.

- 🧱 **A policy restore that verifies itself**:
  - The reset now runs before the per-profile writes, so the inbound action configured by the user is no longer overwritten with block inbound.
  - The effective policy is read back, and the reset is applied a second time when a profile still blocks outbound traffic.
  - If the restore cannot be confirmed, the application rules are kept instead of leaving the host without filtering.

- 🧩 **The rest of the suite, one click away**:
  - The About window now shows five applications from the CyberGems suite with their icons and a link to the full catalog.
  - The icons are refreshed from each application's own repository for every release, and a workflow verifies that they stay in sync.

- 🎨 **An installer with its own identity**:
  - The setup wizard shows the application icon in the corner and the CyberGems plate on the side of every page, generated from the app artwork.

- ⚡ **A connection log that costs less**:
  - The connection log is read once and incrementally, so the log viewer, the statistics view and the connection tabs no longer parse the whole file on every refresh.

- ✅ **Verification**:
  - New tests cover the core rule cleanup, the policy restore order, and the incremental log reader.
  - The release pipeline stops when the changelog has no entry for the version being released, and the notes are checked against the branch and the files they link.

<details>
<summary><b>🌐 Ver notas de la versión en Español</b></summary>

### 🚀 Novedades de esta versión

La protección se puede apagar ahora sin perder las reglas core del servicio, así las aplicaciones que usan localhost siguen funcionando. La política del firewall se restaura en un orden verificado, la ventana Acerca de muestra el resto de la suite de CyberGems y el instalador lleva el arte de la aplicación.

---

### ✨ Novedades destacadas

- 🧩 **Apagar la protección conserva las reglas core**:
  - Las reglas de DNS, DHCP, NTP y loopback se mantienen al apagar la protección, así las aplicaciones que se comunican con localhost siguen funcionando después de usar el interruptor maestro.
  - Solo se eliminan las reglas por aplicación, que es lo que ya hacían la limpieza heredada y el script de recuperación tras un fallo.

- 🧱 **Una restauración de la política que se verifica sola**:
  - El restablecimiento se ejecuta ahora antes de las escrituras por perfil, de modo que la acción de entrada configurada por el usuario ya no se sobrescribe con "bloquear entrada".
  - La política efectiva se relee y el restablecimiento se aplica una segunda vez cuando un perfil sigue bloqueando el tráfico de salida.
  - Si la restauración no se puede confirmar, se conservan las reglas de las aplicaciones en lugar de dejar el equipo sin filtrado.

- 🧩 **El resto de la suite, a un clic**:
  - La ventana Acerca de muestra ahora cinco aplicaciones de la suite de CyberGems con sus iconos y un enlace al catálogo completo.
  - Los iconos se actualizan desde el repositorio de cada aplicación en cada versión, y un flujo de trabajo verifica que se mantengan sincronizados.

- 🎨 **Un instalador con identidad propia**:
  - El asistente de instalación muestra el icono de la aplicación en la esquina y la placa de CyberGems en el lateral de cada página, generados a partir del arte de la aplicación.

- ⚡ **Un registro de conexiones más barato**:
  - El registro de conexiones se lee una sola vez y de forma incremental, así el visor de registros, la vista de estadísticas y las pestañas de conexiones ya no analizan el archivo completo en cada actualización.

- ✅ **Verificación**:
  - Nuevas pruebas cubren la limpieza de las reglas core, el orden de restauración de la política y el lector incremental de registros.
  - La integración continua del release se detiene si el registro de cambios no tiene una entrada para la versión publicada, y las notas se validan contra la rama y los archivos a los que enlazan.

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
