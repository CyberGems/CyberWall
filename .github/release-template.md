<p align="center">
  <img src="https://raw.githubusercontent.com/CyberGems/CyberWall/master/src/CyberWall.UI/Assets/CyberWall.png" width="120" alt="CyberWall">
</p>

## 🛡️ CyberWall {{VERSION}}: Release Notes

### 🚀 What's new in this release

<!-- IMPORTANT FOR AGENTS & MAINTAINERS: Always rewrite this entire section and highlights with the ACTUAL release notes for this specific new version before tagging. NEVER leave or copy previous version notes! Use 25-45 words for the summary, lead with user-facing changes, and do not repeat the app name or version. Add the same changes to CHANGELOG.md. -->
<!-- changelog-summary:start -->
Firewall policy handling is now crash-safe: a watchdog restores connectivity if the app stops unexpectedly, the tray icon shows the protection state, and rule names no longer collide between programs sharing a file name. Updates now verify their checksum before installing.
<!-- changelog-summary:end -->

> **New to CyberWall?** A real-time Windows firewall for viewing per-app traffic and allowing or blocking connections with one click.

---

### ✨ Key Features & Highlights

- 🛡️ **Crash-safe firewall policy**:
  - The firewall policy is verified after being restored, and a machine left in the default-deny state by an unclean shutdown is always restored to an allowed state instead of staying locked down.
  - A scheduled watchdog task now restores the original policy and removes the leftover rules if the application stops unexpectedly, so a crash can never leave the host without connectivity.
  - Turning the protection off removes the per-application rules as well, so a blocked program is no longer left blocked after the master switch is turned off.
  - If the policy cannot be restored, the user is warned with the exact command to recover instead of the failure going unnoticed.

- 🏷️ **Rules that no longer collide**:
  - Windows Firewall rule names now carry a stable hash of the executable path, so two programs with the same file name (two browsers, two installers) no longer overwrite each other's rule.
  - Rules created by previous versions are recognized and cleaned up on the first run, and the uninstaller now restores the firewall policy and removes every rule the application owns.

- 🖥️ **The tray icon tells the truth**:
  - The notification area icon turns grey while the protection is off and its tooltip states whether the filtering is active, including when the Windows service is the one enforcing it.
  - Closing the window to the tray now explains once that traffic keeps being filtered in the background.
  - The protection state and the selected mode are stored by name, so the chosen mode survives a restart even when it was picked while the protection was off.

- 🔧 **The Windows service behaves**:
  - The service now applies the mode and the state saved in the settings instead of always starting in "ask" mode, and it steps aside while the desktop window is open.
  - Only one host owns the firewall engine at a time, so the window and the service can no longer fight over the same rules.

- 🌍 **A fully bilingual interface**:
  - Every window title is now translated, and the missing translations that rendered a raw key in the connections log and the statistics view are fixed.
  - Unused translation entries were removed and the catalog is now verified by the test suite on every build, in both English and Spanish.

- 🔒 **Verified updates and durable state**:
  - The downloaded installer is checked against the published SHA256 before it is executed, and it is discarded when the checksum does not match.
  - Rules, settings and notifications are written atomically, so an interrupted write can no longer truncate them.
  - The WFP audit policy enabled to detect blocked traffic is restored when the application stops, so the Security log no longer grows indefinitely.
  - The activity log no longer corrupts itself, and it stays capped at a fraction of the disk space it used to take.

- 📦 **.NET 10 and a cleaner installer**:
  - Migrated to the .NET 10 hosting packages and the CI pipeline now runs the unit tests before packaging.
  - The installer compiles without warnings, targets x64 properly, and no longer writes per-user registry entries that duplicated the application manifest.

- ✅ **Verification**:
  - 75 unit tests cover the rule store, the package identity resolution, the settings migration, the translation catalog and the firewall rule naming.
  - The build and the installer both compile with zero warnings, and the release pipeline validates the translation summary before packaging.

<details>
<summary><b>🌐 Ver notas de la versión en Español</b></summary>

### 🚀 Novedades de esta versión

La gestión de la política del firewall ahora es resistente a fallos: un watchdog restaura la conectividad si la aplicación se detiene de forma inesperada, el icono de la bandeja muestra el estado de la protección y los nombres de las reglas ya no chocan entre programas que comparten nombre de archivo. Las actualizaciones verifican su suma de comprobación antes de instalarse.

---

### ✨ Novedades destacadas

- 🛡️ **Política del firewall a prueba de fallos**:
  - La política del firewall se verifica después de restaurarse, y una máquina que quedó en modo denegar por defecto tras un cierre irregular siempre vuelve a un estado permitido en lugar de quedarse bloqueada.
  - Una tarea programada de vigilancia restaura la política original y elimina las reglas sobrantes si la aplicación se detiene de forma inesperada, así que un fallo nunca puede dejar el equipo sin conectividad.
  - Al desactivar la protección también se eliminan las reglas por aplicación, de modo que una aplicación bloqueada ya no se queda bloqueada después de apagar el interruptor maestro.
  - Si la política no se puede restaurar, se avisa al usuario con el comando exacto para recuperarla, en lugar de que el fallo pase inadvertido.

- 🏷️ **Reglas que ya no chocan**:
  - Los nombres de las reglas de Windows Firewall incluyen ahora un hash estable de la ruta del ejecutable, así dos programas con el mismo nombre de archivo (dos navegadores, dos instaladores) ya no sobrescriben la regla del otro.
  - Las reglas creadas por versiones anteriores se reconocen y se limpian en la primera ejecución, y el desinstalador restaura la política del firewall y elimina todas las reglas de la aplicación.

- 🖥️ **El icono de la bandeja dice la verdad**:
  - El icono del área de notificación se pone gris cuando la protección está desactivada y su descripción indica si el filtrado está activo, incluso cuando el servicio de Windows es quien lo aplica.
  - Al cerrar la ventana a la bandeja se explica una vez que el tráfico sigue filtrándose en segundo plano.
  - El estado de la protección y el modo seleccionado se guardan por nombre, así que el modo elegido sobrevive a un reinicio incluso si se eligió con la protección apagada.

- 🔧 **El servicio de Windows se comporta**:
  - El servicio ahora aplica el modo y el estado guardados en la configuración en lugar de arrancar siempre en modo "preguntar", y se aparta mientras la ventana de escritorio está abierta.
  - Solo un host es propietario del motor del firewall a la vez, así que la ventana y el servicio ya no discuten por las mismas reglas.

- 🌍 **Interfaz totalmente bilingüe**:
  - Todos los títulos de las ventanas están traducidos, y se corrigieron las traducciones que mostraban una clave en crudo en el registro de conexiones y en las estadísticas.
  - Se eliminaron las entradas de traducción sin usar, y el catálogo se verifica ahora con la suite de pruebas en cada compilación, en inglés y en español.

- 🔒 **Actualizaciones verificadas y estado duradero**:
  - El instalador descargado se contrasta con el SHA256 publicado antes de ejecutarlo, y se descarta cuando la suma no coincide.
  - Las reglas, la configuración y las notificaciones se escriben de forma atómica, así una escritura interrumpida ya no puede truncarlas.
  - La política de auditoría de WFP que se activa para detectar el tráfico bloqueado se restaura al detener la aplicación, de modo que el registro de Seguridad deja de crecer sin control.
  - El registro de actividad ya no se corrompe y se mantiene limitado a una fracción del espacio que ocupaba.

- 📦 **.NET 10 y un instalador más limpio**:
  - Migración a los paquetes de hosting de .NET 10, y la integración continua ahora ejecuta las pruebas unitarias antes de empaquetar.
  - El instalador compila sin avisos, apunta correctamente a x64 y ya no escribe entradas de registro por usuario que duplicaban el manifiesto de la aplicación.

- ✅ **Verificación**:
  - 75 pruebas unitarias cubren el almacén de reglas, la resolución de identidad de paquetes, la migración de la configuración, el catálogo de traducciones y el nombre de las reglas del firewall.
  - La compilación y el instalador compilan sin ningún aviso, y la integración continua valida el resumen de traducciones antes de empaquetar.

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
