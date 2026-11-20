# Changelog

All notable changes to CyberWall are documented here.

## [Unreleased]

### 🇺🇸 English

- Add `Alt+Shift+W` as the global shortcut that shows or hides the CyberWall window from any application, and `Alt+Shift+F2` for the floating traffic widget. Both are configurable in Settings → System, with restore, clear and a per-field notice when another application owns the combination.
- Show the current global shortcut next to the show/hide row of the tray menu.
- Apply restored shortcuts immediately after restoring a backup, without restarting the app.

### 🇪🇸 Español

- Añade `Alt+Shift+W` como atajo global que muestra u oculta la ventana de CyberWall desde cualquier aplicación, y `Alt+Shift+F2` para el widget flotante de tráfico. Ambos se configuran en Configuración → Sistema, con restablecer, limpiar y un aviso por campo cuando la combinación ya pertenece a otra aplicación.
- Muestra el atajo global actual junto a la fila de mostrar/ocultar del menú de la bandeja.
- Aplica los atajos restaurados al instante tras restaurar un respaldo, sin reiniciar la aplicación.

## [2.5.3] - 2026-11-19

### 🇺🇸 English

- Tighten the horizontal spacing between the main tray card and its side-by-side flyout panels from `8px` to `4px` to match the rest of the suite.
- Remove the active accent tint from branding elements.

### 🇪🇸 Español

- Reduce la separación horizontal entre la tarjeta principal de la bandeja y los paneles flyout laterales de `8px` a `4px` para igualar el resto de la suite.
- Elimina el tinte de acento activo en los elementos de marca.

## [2.5.2] - 2026-11-18

### 🇺🇸 English

- Convert the tray submenus (`Pause floating alerts`, `Help`, and `More from CyberGems`) into side-by-side flyout panels with dwell hover, click toggling, automatic left/right taskbar orientation, matching chevron flips, and arrow-key navigation.
- Rename the Dark theme to Gray (`Gris` in Spanish) across settings and palettes to match suite naming without changing stored user preferences.

### 🇪🇸 Español

- Convierte los submenús de la bandeja (`Pausar avisos flotantes`, `Ayuda` y `Más de CyberGems`) en paneles flyout laterales con retardo hover, alternancia por clic, orientación automática según la barra de tareas, inversión del indicador chevron y navegación por teclado.
- Renombra el tema Oscuro a Gris (`Gray` en inglés) en ajustes y paletas para unificar nombres con la suite sin afectar la preferencia guardada.

## [2.5.1] - 2026-11-16

### 🇺🇸 English

- Show the CyberWall app icon on the Windows uninstall entry.
- Ship winget manifests with every release and automate Windows Package Manager submissions.
- Update Microsoft.Extensions.Hosting and three other dependencies.

### 🇪🇸 Español

- Muestra el icono de CyberWall en la entrada de desinstalación de Windows.
- Publica manifiestos winget con cada release y automatiza los envíos al Windows Package Manager.
- Actualiza Microsoft.Extensions.Hosting y otras tres dependencias.

## [2.5.0] - 2026-11-13

### 🇺🇸 English

- Automatic and manual `.cwbackup` snapshots for firewall rules and settings (`BackupService`, `BackupCoordinator`) with canonical SHA-256 integrity verification, deduplication of transient UI state, customizable destination folder, and bounded retention between 1 and 7 automatic copies (default 3).
- Selective restore modal (`BackupRestoreDialog`) supporting rules and/or preferences, external `.cwbackup` file browsing, pre-clear and pre-restore safety snapshots, and Block All / Killswitch warnings.
- Network adapter selector (`NetworkSpeedService`, `NetworkAdapterMenuHelper`) with an Automatic default and per-adapter selection persisted across Settings, Traffic Monitor, and the Floating Traffic Widget, resetting live and header graphs immediately on adapter switch.
- Embedded `NotificationsView` and `AboutView` tabs inside the main window, including notification sorting (Date, Type, Apps), hover dismiss buttons on notification cards, inline update status in About, and a suite-aligned CyberGems footer.
- Timed floating-alert pauses (15m, 1h, Until Restart), direct Floating Traffic Widget toggles, title bar keyboard shortcuts with keycap tooltips, and a "More from CyberGems" suite sub-view with animated hover icons in the tray menu.
- Grouped "Help & Documentation" submenu and an "Exit CyberWall" action in the title bar More menu.
- Unified `ContextMenu` and `MenuItem` styles into shared `App.xaml` resources, softened ComboBox selection highlights, and redesigned the navigation tab and More menu vector icons.
- Improved contrast of the unidentified country world-map flag mark (`CountryFlagMark`).
- Restored the Floating Traffic Widget automatically when starting minimized to the tray if previously pinned open.
- Prevented `UCEERR_RENDERTHREADFAILURE` crashes on sleep/resume (`RenderStabilityManager`) and decoupled animated shadows for visual stability.
- Fixed toast, popup, and tray context menu positioning across mixed-DPI multi-monitor setups and eliminated clipped menu drop shadows.
- Fixed second-click toggle dismissal on title bar menus (`MenuToggleHelper`), eliminated tray submenu size jumps, and fixed tray Exit icon geometry and red hover styling.
- Resolved pending prompt cards cleanly when `PromptStackWindow` closes externally and guarded `BackupRestoreDialog` state updates during XAML initialization.
- Added null checks in `ThemeManager` during startup resource application.

### 🇪🇸 Español

- Instantáneas automáticas y manuales `.cwbackup` para las reglas y los ajustes del firewall (`BackupService`, `BackupCoordinator`) con verificación canónica de integridad SHA-256, deduplicación del estado transitorio de la interfaz, carpeta de destino personalizable y retención limitada entre 1 y 7 copias automáticas (3 por defecto).
- Modal de restauración selectiva (`BackupRestoreDialog`) que admite reglas y/o preferencias, exploración de archivos `.cwbackup` externos, copias de seguridad preventivas antes de limpiar o restaurar y avisos de Bloquear todo / Killswitch.
- Selector de adaptador de red (`NetworkSpeedService`, `NetworkAdapterMenuHelper`) con Automático por defecto y una selección por adaptador persistida en Configuración, el Monitor de tráfico y el Widget flotante, reiniciando al instante los gráficos en vivo y del encabezado al cambiar de adaptador.
- Pestañas `NotificationsView` y `AboutView` integradas en la ventana principal, con ordenación de notificaciones (Fecha, Tipo, Aplicaciones), botones para descartar tarjetas al pasar el cursor, estado de actualización en línea en Acerca de y un pie de CyberGems alineado con la suite.
- Pausas temporizadas de alertas flotantes (15 m, 1 h, Hasta reiniciar), interruptores directos del Widget flotante, atajos de teclado en la barra de título con tooltips de teclas y una subvista de suite "Más de CyberGems" con iconos animados al pasar el cursor en el menú de la bandeja.
- Submenú agrupado "Ayuda y documentación" y una acción "Salir de CyberWall" en el menú Más opciones de la barra de título.
- Estilos de `ContextMenu` y `MenuItem` unificados en recursos compartidos de `App.xaml`, resaltados de selección de ComboBox más suaves e iconos vectoriales de las pestañas de navegación y del menú Más opciones rediseñados.
- Mayor contraste en la marca de bandera del mapa mundial para países no identificados (`CountryFlagMark`).
- El Widget flotante se restaura automáticamente al iniciar minimizado en la bandeja si antes estaba fijado abierto.
- Prevención de fallos `UCEERR_RENDERTHREADFAILURE` al suspender y reanudar (`RenderStabilityManager`) y sombras animadas desacopladas para una mayor estabilidad visual.
- Corregido el posicionamiento de toasts, popups y del menú contextual de la bandeja en equipos con varios monitores de DPI distinto, y eliminadas las sombras de los menús recortadas.
- Corregido el cierre al segundo clic en los menús de la barra de título (`MenuToggleHelper`), eliminados los saltos de tamaño en los submenús de la bandeja y corregida la geometría del icono Salir de la bandeja y su estilo rojo al pasar el cursor.
- Las tarjetas de avisos pendientes se resuelven limpiamente cuando `PromptStackWindow` se cierra desde fuera, y las actualizaciones de estado de `BackupRestoreDialog` están protegidas durante la inicialización de XAML.
- Añadidas comprobaciones de valores nulos en `ThemeManager` durante la aplicación de recursos al arranque.

## [2.4.0] - 2026-11-10

### 🇺🇸 English

- A floating desktop companion widget (`FloatingTrafficWidget`) with real-time spline throughput graphs, session totals, compact mode, always-on-top toggle, and one-click access to the Traffic Monitor.
- Unified executive hero dashboard row combining the master firewall switch, mode dropdown, live network wave graph, scale indicator, and throughput pills with 1m, 5m, and 10m time ranges.
- Quick country filtering: clicking a country flag filters rules by destination country with a dedicated filter chip and match count.
- Animated expandable search bar in the rules header with real-time keyword highlighting, match counters, and recent search history.
- Context menus with app-themed edit actions (Undo, Redo, Cut, Copy, Paste, Select All, Delete) and "Copy executable name".
- Space and P keyboard shortcuts to pause and resume the live traffic graphs.
- Reset window size and position option in the title bar More menu.
- Unanswered connection prompts no longer save a persistent block rule by default (auto-block is now opt-in in Settings, with a one-time migration disabling it on existing setups); expired or dismissed prompts close without saving a verdict, record a pending decision notification in Notification Center with a 60-second reprompt cooldown, and keep the app blocked under the default-deny policy until answered.
- Disabled window transparency to enable full GPU rendering and hardware acceleration across Windows versions.
- Optimized startup and rule loading with non-blocking background initialization and DataGrid row virtualization.
- Streamlined protection status labels and title bar tooltips for improved layout clarity and multi-monitor scaling.
- Uninstaller now waits for the CyberWall application and Windows service to stop cleanly before removing files or firewall rules.
- Provisioned allow rules and armed the anti-lockout watchdog before switching the Windows Firewall to default-deny, preventing connectivity drops for already-allowed programs during activation.
- Fixed sticky tooltip persistence when moving the cursor quickly between title bar buttons.
- Fixed bracketed IPv6 endpoint parsing with ports.
- Fixed country flag alignment in expanded rule lists.

### 🇪🇸 Español

- Un widget compañero flotante de escritorio (`FloatingTrafficWidget`) con gráficos spline de velocidad en tiempo real, totales de sesión, modo compacto, fijación siempre encima y acceso al Monitor de tráfico en un clic.
- Fila de panel ejecutivo unificada que combina el interruptor maestro del firewall, el selector de modo, la onda de red en vivo, el indicador de escala y las píldoras de velocidad con rangos de 1 min, 5 min y 10 min.
- Filtro rápido por país: al hacer clic en una bandera de país se filtran las reglas por país de destino, con un chip de filtro dedicado y recuento de coincidencias.
- Barra de búsqueda expandible animada en el encabezado de reglas, con resaltado de palabras clave en tiempo real, contadores de coincidencias e historial de búsquedas recientes.
- Menús contextuales con acciones de edición al tema de la app (Deshacer, Rehacer, Cortar, Copiar, Pegar, Seleccionar todo, Eliminar) y "Copiar nombre del ejecutable".
- Atajos de teclado Espacio y P para pausar y reanudar los gráficos de tráfico en vivo.
- Opción de restablecer tamaño y posición de la ventana en el menú Más opciones de la barra de título.
- Los avisos de conexión sin respuesta ya no guardan una regla de bloqueo permanente por defecto (el auto-bloqueo ahora es opcional en Configuración, con una migración única que lo desactiva en instalaciones existentes); los avisos expirados o descartados se cierran sin guardar veredicto, registran una notificación de decisión pendiente en el centro de notificaciones con un cooldown de 60 s para volver a preguntar y mantienen la app bloqueada bajo la política default-deny hasta que se respondan.
- Transparencia de la ventana desactivada para habilitar el renderizado completo por GPU y la aceleración por hardware en todas las versiones de Windows.
- Inicio y carga de reglas optimizados con inicialización en segundo plano sin bloqueos y virtualización de filas del DataGrid.
- Etiquetas de estado de protección y tooltips de la barra de título simplificados para mayor claridad del diseño y mejor escalado multi-monitor.
- El desinstalador ahora espera a que la aplicación CyberWall y el servicio de Windows se detengan limpiamente antes de eliminar archivos o reglas del firewall.
- Las reglas de permiso se aprovisionan y el watchdog anti-bloqueo se arma antes de poner Windows Firewall en default-deny, evitando cortes de conectividad en programas ya permitidos durante la activación.
- Corregida la persistencia de tooltips pegajosos al mover el cursor rápidamente entre botones de la barra de título.
- Corregido el análisis de direcciones IPv6 con corchetes y puertos.
- Corregida la alineación de las banderas de país en las listas de reglas expandidas.

## [2.3.1] - 2026-10-27

### 🇺🇸 English

- The installer no longer runs `schtasks.exe` or `netsh.exe`. Startup registration is delegated to the application through `--register-startup`, which creates the scheduled task with the Task Scheduler COM API, and the application ensures its own firewall allow rules on every start. The resulting task and rules are the same, but setup no longer spawns command-line tools, which is the behaviour that heuristic engines scored as a dropper. The uninstaller keeps its cleanup commands so a broken installation can still restore the firewall policy.

### 🇪🇸 Español

- El instalador ya no ejecuta `schtasks.exe` ni `netsh.exe`. El registro de inicio se delega en la aplicación mediante `--register-startup`, que crea la tarea programada con la API COM del Programador de tareas, y la aplicación asegura sus propias reglas de permiso del firewall en cada arranque. La tarea y las reglas resultantes son las mismas, pero la instalación ya no lanza herramientas de línea de comandos, que es el comportamiento que los motores heurísticos puntuaban como dropper. El desinstalador conserva sus comandos de limpieza, así una instalación rota todavía puede restaurar la política del firewall.

## [2.3.0] - 2026-10-26

### 🇺🇸 English

- The About window now shows five applications from the CyberGems suite with their icons, a link to the full catalog, and icons refreshed from each application's repository on every release. A workflow verifies that they stay in sync.
- The release pipeline now refreshes the suite icons before building and refuses to publish a version that has no changelog entry.
- Turning the protection off keeps the DNS, DHCP, NTP, and loopback rules, so applications that talk to localhost keep working. Only the per-application rules are removed, which matches what the legacy cleanup and the crash recovery script already did.
- The firewall policy is restored in a verified order: the reset runs first, the captured inbound action is applied on top of it, the effective policy is read back, and the reset is repeated when a profile still blocks outbound traffic.
- The installer wizard now shows the application icon and the CyberGems plate, generated from the app artwork by `scripts/generate-installer-art.py`.
- The connection log is read once and incrementally, so the log viewer, the statistics view, and the connection tabs no longer parse the whole file on every refresh.
- Restoring the policy overwrote the inbound action configured by the user with block inbound, because the reset ran after the per-profile writes.
- Switching the protection off deleted the core service rules, which broke localhost traffic for applications that depend on an explicit allow rule.
- The release notes logo pointed at the `main` branch, which does not exist, so the header image of the published notes was broken.

### 🇪🇸 Español

- La ventana Acerca de muestra ahora cinco aplicaciones de la suite de CyberGems con sus iconos, un enlace al catálogo completo e iconos refrescados desde el repositorio de cada aplicación en cada versión. Un flujo de trabajo verifica que se mantengan sincronizados.
- El pipeline de release ahora refresca los iconos de la suite antes de compilar y se niega a publicar una versión sin entrada en el changelog.
- Apagar la protección conserva las reglas de DNS, DHCP, NTP y loopback, así las aplicaciones que se comunican con localhost siguen funcionando. Solo se eliminan las reglas por aplicación, igual que ya hacían la limpieza heredada y el script de recuperación tras un fallo.
- La política del firewall se restaura en un orden verificado: el restablecimiento se ejecuta primero, la acción de entrada capturada se aplica encima, la política efectiva se relee y el restablecimiento se repite cuando un perfil sigue bloqueando el tráfico de salida.
- El asistente de instalación muestra ahora el icono de la aplicación y la placa de CyberGems, generados a partir del arte de la app por `scripts/generate-installer-art.py`.
- El registro de conexiones se lee una sola vez y de forma incremental, así el visor de registros, la vista de estadísticas y las pestañas de conexiones ya no analizan el archivo completo en cada actualización.
- Restaurar la política sobrescribía la acción de entrada configurada por el usuario con bloquear entrada, porque el restablecimiento se ejecutaba después de las escrituras por perfil.
- Apagar la protección eliminaba las reglas core del servicio, lo que rompía el tráfico localhost de las aplicaciones que dependen de una regla de permiso explícita.
- El logo de las notas de la versión apuntaba a la rama `main`, que no existe, así la imagen de cabecera de las notas publicadas salía rota.

## [2.2.0] - 2026-10-20

### 🇺🇸 English

- A scheduled watchdog task restores the original firewall policy and removes leftover rules when the application stops unexpectedly, so a crash can no longer leave the host without connectivity.
- The notification area icon turns grey while the protection is off, and its tooltip reports whether filtering is active, including when the Windows service is the one enforcing it.
- Downloaded installers are verified against the published SHA256 hash and discarded when it does not match.
- 75 unit tests cover the rule store, package identity resolution, settings migration, the translation catalog, and firewall rule naming. The pipeline now runs them before packaging.
- The release pipeline validates the release summary and the translation catalog before publishing.
- Windows Firewall rule names now carry a stable hash of the executable path, so two programs that share a file name no longer overwrite each other's rule. Rules created by previous versions are recognized and cleaned up on the first run.
- Turning the protection off also removes the per-application rules, so a blocked program is not left blocked after the master switch is turned off.
- The Windows service applies the mode and state stored in the settings, steps aside while the desktop window is open, and only one host owns the firewall engine at a time.
- Rules, settings, and notifications are written atomically, so an interrupted write cannot truncate them.
- The protection state and the selected mode are stored by name, so the chosen mode survives a restart even when it was picked while the protection was off.
- The application migrated to the .NET 10 hosting packages, and the installer targets x64 without the per-user registry entries that duplicated the application manifest.
- The uninstaller restores the firewall policy and removes every rule the application owns.
- Closing the window to the tray explains once that traffic keeps being filtered in the background.
- The firewall policy is verified after being restored, and a machine left in the default-deny state by an unclean shutdown is always returned to an allowed state.
- A failed policy restore now warns the user with the exact recovery command instead of failing silently.
- Every window title is translated, and the missing translations that rendered a raw key in the connections log and the statistics view are fixed.
- The WFP audit policy enabled to detect blocked traffic is restored when the application stops, so the Security log no longer grows indefinitely.
- The activity log no longer corrupts itself and stays capped at a fraction of the disk space it used to take.

### 🇪🇸 Español

- Una tarea programada de vigilancia (watchdog) restaura la política original del firewall y elimina las reglas sobrantes cuando la aplicación se detiene de forma inesperada, así un fallo ya no puede dejar el equipo sin conectividad.
- El icono del área de notificación se pone gris mientras la protección está desactivada, y su tooltip indica si el filtrado está activo, incluso cuando es el servicio de Windows quien lo aplica.
- Los instaladores descargados se verifican contra el hash SHA256 publicado y se descartan cuando no coincide.
- 75 pruebas unitarias cubren el almacén de reglas, la resolución de identidad de paquetes, la migración de la configuración, el catálogo de traducciones y el nombre de las reglas del firewall. El pipeline ahora las ejecuta antes de empaquetar.
- El pipeline de release valida el resumen del release y el catálogo de traducciones antes de publicar.
- Los nombres de las reglas de Windows Firewall incluyen ahora un hash estable de la ruta del ejecutable, así dos programas que comparten nombre de archivo ya no sobrescriben la regla del otro. Las reglas creadas por versiones anteriores se reconocen y se limpian en la primera ejecución.
- Al desactivar la protección también se eliminan las reglas por aplicación, de modo que un programa bloqueado no se queda bloqueado tras apagar el interruptor maestro.
- El servicio de Windows aplica el modo y el estado guardados en la configuración, se aparta mientras la ventana de escritorio está abierta y solo un host es propietario del motor del firewall a la vez.
- Las reglas, la configuración y las notificaciones se escriben de forma atómica, así una escritura interrumpida no puede truncarlas.
- El estado de la protección y el modo seleccionado se guardan por nombre, así el modo elegido sobrevive a un reinicio incluso si se eligió con la protección apagada.
- La aplicación migró a los paquetes de hosting de .NET 10, y el instalador apunta a x64 sin las entradas de registro por usuario que duplicaban el manifiesto de la aplicación.
- El desinstalador restaura la política del firewall y elimina todas las reglas de la aplicación.
- Al cerrar la ventana a la bandeja se explica una vez que el tráfico sigue filtrándose en segundo plano.
- La política del firewall se verifica después de restaurarse, y una máquina que quedó en default-deny tras un cierre irregular siempre vuelve a un estado permitido.
- Si la restauración de la política falla, se avisa al usuario con el comando exacto de recuperación en lugar de fallar en silencio.
- Todos los títulos de las ventanas están traducidos, y se corrigieron las traducciones que faltaban y que mostraban una clave en crudo en el registro de conexiones y en las estadísticas.
- La política de auditoría de WFP activada para detectar el tráfico bloqueado se restaura al detener la aplicación, así el registro de Seguridad deja de crecer sin control.
- El registro de actividad ya no se corrompe y se mantiene limitado a una fracción del espacio en disco que ocupaba.

## [2.1.0] - 2026-09-12

### 🇺🇸 English

- Settings moved into a dedicated fifth tab that replaces the modal dialog, with a centered container limited to 960 px for wide displays.
- `Ctrl+,` and `Ctrl+5` open Settings, and `Ctrl+1` to `Ctrl+4` switch tabs.
- Title bar "more options" menu with Settings, rule refresh, traffic monitor, connection log, statistics, wiki, FAQ, changelog, website, and update check.
- Sorting by live network activity and transfer speed in the rules table.
- Bandwidth sampling was reworked to ignore duplicate NDIS filter counter reads, with improved timer thread affinity and high-precision per-process calculations.
- The application icon cache detects when an executable is rebuilt or replaced and updates the icon in place.
- Rounded pill headers for the rules columns, flattened rule expander groups, vector icons in the traffic monitor and connection cards, and refined toast cards.
- Typography and contrast were raised for settings titles, descriptions, and category headings.
- Fixed exaggerated throughput figures caused by duplicated counter reads.

### 🇪🇸 Español

- Los ajustes pasan a una quinta pestaña dedicada que sustituye al diálogo modal, con un contenedor centrado limitado a 960 px para pantallas anchas.
- `Ctrl+,` y `Ctrl+5` abren Ajustes, y `Ctrl+1` a `Ctrl+4` cambian de pestaña.
- Menú "más opciones" en la barra de título con Ajustes, refresco de reglas, monitor de tráfico, registro de conexiones, estadísticas, wiki, FAQ, changelog, sitio web y comprobación de actualizaciones.
- Ordenación por actividad de red en vivo y velocidad de transferencia en la tabla de reglas.
- El muestreo de ancho de banda se rehízo para ignorar lecturas duplicadas de los contadores del filtro NDIS, con mejor afinidad del hilo del temporizador y cálculos de alta precisión por proceso.
- La caché de iconos de la aplicación detecta cuando un ejecutable se recompila o se sustituye y actualiza el icono in situ.
- Encabezados de las columnas de reglas en píldoras redondeadas, grupos expansores de reglas planos, iconos vectoriales en el monitor de tráfico y las tarjetas de conexión, y tarjetas toast refinadas.
- Tipografía y contraste aumentados en títulos, descripciones y encabezados de categoría de los ajustes.
- Corregidas las cifras de velocidad exageradas causadas por lecturas duplicadas de contadores.

## [2.0.0] - 2026-09-05

### 🇺🇸 English

- Unified window with Firewall, Traffic Monitor, Connections Log, and Statistics tabs, with lifecycle hooks that suspend timers and rendering when a view is inactive.
- Per-process bandwidth telemetry through the `Microsoft-Windows-Kernel-Network` ETW provider, with throughput badges per rule row, sorting by speed, and a ranked top consumers list with quick block.
- 60-second download and upload graph with current speed, peak, session total, and active adapter cards.
- Embedded connections log with search, verdict filters, country flags, and log file management.
- Statistics dashboard with five KPIs, the top ten destination countries, top applications, an inbound and outbound split, and a range selector from one hour to all time.
- Context actions on processes (search online, copy path, open executable folder) with row highlighting and auto-dismiss.
- Rules load asynchronously with icon cache pre-warming to keep the window responsive.
- High-DPI PerMonitorV2 centering was corrected for multi-monitor setups, and the default window width was increased.
- Fixed startup delay, socket drops when an allow rule was re-applied, and connectivity for loopback applications, Defender, SmartScreen, and Store apps.
- Fixed the `NullReferenceException` raised while the views initialized.

### 🇪🇸 Español

- Ventana unificada con pestañas de Firewall, Monitor de tráfico, Registro de conexiones y Estadísticas, con hooks de ciclo de vida que suspenden los temporizadores y el renderizado cuando una vista está inactiva.
- Telemetría de ancho de banda por proceso mediante el proveedor ETW `Microsoft-Windows-Kernel-Network`, con insignias de velocidad por fila de regla, ordenación por velocidad y una lista de los mayores consumidores con bloqueo rápido.
- Gráfico de 60 segundos de descarga y subida con tarjetas de velocidad actual, pico, total de sesión y adaptador activo.
- Registro de conexiones integrado con búsqueda, filtros por veredicto, banderas de país y gestión del archivo de registro.
- Panel de estadísticas con cinco KPIs, los diez principales países de destino, las aplicaciones principales, el reparto entre entrada y salida y un selector de rango de una hora a todo el histórico.
- Acciones contextuales sobre los procesos (buscar en línea, copiar ruta, abrir la carpeta del ejecutable) con resaltado de fila y autodescarte.
- Las reglas se cargan de forma asíncrona con precarga de la caché de iconos para mantener la ventana fluida.
- El centrado High-DPI PerMonitorV2 se corrigió para configuraciones multi-monitor, y se amplió el ancho por defecto de la ventana.
- Corregidos el retraso al iniciar, los cortes de socket al reaplicar una regla de permiso y la conectividad de las aplicaciones de loopback, Defender, SmartScreen y las apps de la Store.
- Corregida la `NullReferenceException` que se producía al inicializar las vistas.

## [1.9.0] - 2026-08-29

### 🇺🇸 English

- Adaptive traffic telemetry stream with auto-calibrated scaling, particle density driven by live saturation, and Fluid Stream, Glow Pulse, and Static modes.
- Application monitor that detects version changes and modified executables.
- On-screen alerts for internet disconnection and restoration, protection state changes, and automatic blocks, each with its own toggle.
- Theme engine with the CyberWall, Dark, and Light palettes and themed scrollbars across windows and dialogs.
- Version toasts display clean commit identifiers instead of raw hashes.

### 🇪🇸 Español

- Flujo de telemetría de tráfico adaptativo con escalado autocalibrado, densidad de partículas según la saturación en vivo y modos Fluid Stream, Glow Pulse y Static.
- Monitor de aplicaciones que detecta cambios de versión y ejecutables modificados.
- Alertas en pantalla al perder y recuperar internet, ante cambios del estado de protección y bloqueos automáticos, cada una con su interruptor.
- Motor de temas con las paletas CyberWall, Dark y Light y barras de desplazamiento temáticas en ventanas y diálogos.
- Los toasts de versión muestran identificadores de commit limpios en lugar de hashes en crudo.

## [1.8.0] - 2026-08-28

### 🇺🇸 English

- Separate inbound and outbound verdicts per application (Allow, Block, Inherit), enforced by dedicated WFP sub-filters per direction.
- Directional badges on rule rows, a rule editor for changing verdicts at any time, and sorting by country and by direction.
- Country flag badge in the application dialog.
- Quick search was renamed to "Search online" and its default query became the executable and display name.
- The GeoIP resolver no longer lets LAN, multicast, or broadcast addresses overwrite verified public country flags.

### 🇪🇸 Español

- Veredictos separados de entrada y salida por aplicación (Permitir, Bloquear, Heredar), aplicados mediante sub-filtros WFP dedicados por dirección.
- Insignias direccionales en las filas de reglas, un editor de reglas para cambiar veredictos en cualquier momento y ordenación por país y por dirección.
- Insignia de bandera de país en el diálogo de aplicación.
- La búsqueda rápida pasó a llamarse "Buscar en línea" y su consulta por defecto pasa a ser el ejecutable y el nombre visible.
- El resolutor GeoIP ya no permite que direcciones LAN, multicast o broadcast sobrescriban las banderas de país públicas verificadas.

## [1.7.0] - 2026-08-28

### 🇺🇸 English

- Built-in harmonic chime for network prompts, with support for custom WAV and MP3 files, sound preview, and burst throttling when several prompts appear at once.
- Authenticode verification through the native Win32 APIs (`CryptQueryObject`, `CertFindCertificateInStore`) and an "Unsigned" badge for unsigned binaries.
- Start minimized to the notification area.
- Automatic cleanup of obsolete update notifications.
- Rule application, WFP enforcement, and Windows Firewall COM rules run on background threads to keep the interface responsive.
- Press feedback on prompt and confirmation buttons, and protection against duplicate verdicts.
- Fixed false positives produced by the legacy certificate table parsing.

### 🇪🇸 Español

- Campana armónica integrada para los avisos de red, con soporte de archivos WAV y MP3 personalizados, vista previa de sonido y limitación de ráfagas cuando aparecen varios avisos a la vez.
- Verificación Authenticode mediante las APIs nativas de Win32 (`CryptQueryObject`, `CertFindCertificateInStore`) y una insignia "Sin firmar" para los binarios sin firma.
- Inicio minimizado en el área de notificación.
- Limpieza automática de las notificaciones de actualización obsoletas.
- La aplicación de reglas, la aplicación de WFP y las reglas COM de Windows Firewall se ejecutan en hilos en segundo plano para mantener la interfaz fluida.
- Feedback de pulsación en los botones de aviso y confirmación, y protección contra veredictos duplicados.
- Corregidos los falsos positivos que producía el parseo heredado de la tabla de certificados.

## [1.6.1] - 2026-08-28

### 🇺🇸 English

- Startup moved from the registry to the Windows Task Scheduler.
- Loopback traffic is allowed again.

### 🇪🇸 Español

- El arranque con Windows se movió del registro al Programador de tareas de Windows.
- El tráfico loopback vuelve a estar permitido.

## [1.6.0] - 2026-08-27

### 🇺🇸 English

- Traffic and security statistics dashboard, reachable from the toolbar and the tray, with theme-adaptive vector graphics and summary export.
- Activity column with a real-time traffic LED per process and bilingual tooltips, sortable by activity.
- Statistics are computed in memory over the connection logs, without external charting dependencies.

### 🇪🇸 Español

- Panel de estadísticas de tráfico y seguridad, accesible desde la barra de herramientas y la bandeja, con gráficos vectoriales adaptados al tema y exportación de resumen.
- Columna de actividad con un LED de tráfico en tiempo real por proceso y tooltips bilingües, ordenable por actividad.
- Las estadísticas se calculan en memoria sobre los registros de conexiones, sin dependencias de gráficos externas.

## [1.5.0] - 2026-08-27

### 🇺🇸 English

- Real-time network speed meter with a traffic telemetry stream and adapter detection.
- Keyboard workflow for the rules list, including row navigation and `Space` to toggle a rule.
- Inline quick action buttons on rule rows.
- Stacked notification alerts and exact or relative timestamps in activity tooltips.
- The search bar gained a clear button, a hotkey, and keyboard navigation. Light theme badges use a high-contrast royal blue.

### 🇪🇸 Español

- Medidor de velocidad de red en tiempo real con flujo de telemetría de tráfico y detección de adaptadores.
- Flujo de trabajo con teclado para la lista de reglas, con navegación por filas y `Espacio` para alternar una regla.
- Botones de acción rápida integrados en las filas de reglas.
- Alertas de notificación apiladas y marcas de tiempo exactas o relativas en los tooltips de actividad.
- La barra de búsqueda estrena botón de limpiar, atajo de teclado y navegación por teclado. Las insignias del tema claro usan un azul royal de alto contraste.

## [1.4.0] - 2026-08-27

### 🇺🇸 English

- "Clear all rules" in Settings, with a confirmation step that resets the WFP engine.
- Action-specific icons in the modal dialogs.
- Keyboard navigation in the rules list (`Home`, `End`, `Page Up`, and `Page Down`), and tooltips dismiss on wheel scroll.
- Centered Settings footer styled after CyberSnap, and 256x256 icon decoding from multi-resolution ICO streams.
- Fixed column header alignment against the scrollbar, and the extra padding between the rule expanders and the first rule.

### 🇪🇸 Español

- "Borrar todas las reglas" en Ajustes, con un paso de confirmación que reinicia el motor WFP.
- Iconos específicos de la acción en los diálogos modales.
- Navegación por teclado en la lista de reglas (`Home`, `End`, `Page Up` y `Page Down`), y los tooltips se descartan al mover la rueda.
- Pie de Ajustes centrado al estilo CyberSnap y decodificación de iconos 256x256 desde flujos ICO multirresolución.
- Corregida la alineación de los encabezados de columna con la barra de desplazamiento, y el padding extra entre los expansores de reglas y la primera regla.

## [1.3.3] - 2026-08-27

### 🇺🇸 English

- Post-install launch failed with Inno Setup error 740. The installer now launches through `ShellExec` with `RunAsCurrentUser`.

### 🇪🇸 Español

- El arranque tras instalar fallaba con el error 740 de Inno Setup. El instalador ahora se lanza mediante `ShellExec` con `RunAsCurrentUser`.

## [1.3.2] - 2026-08-27

### 🇺🇸 English

- The application manifest requires Administrator, and core DNS and DHCP rules plus setup allow rules are enforced.

### 🇪🇸 Español

- El manifiesto de la aplicación requiere Administrador, y se aplican las reglas core de DNS y DHCP además de las reglas de permiso de la instalación.

## [1.3.1] - 2026-08-27

### 🇺🇸 English

- Firewall rule registration for install paths that contain spaces.
- The connectivity probe is more resilient.

### 🇪🇸 Español

- Registro de reglas del firewall en rutas de instalación que contienen espacios.
- La sonda de conectividad es más resistente.

## [1.3.0] - 2026-08-26

### 🇺🇸 English

- Killswitch firewall mode that enforces global override block rules and tears down active TCP sessions.
- Silent block mode was renamed, and the live network indicator became a telemetry capsule badge.

### 🇪🇸 Español

- Modo de firewall killswitch que aplica reglas globales de bloqueo por encima de todo y derriba las sesiones TCP activas.
- El modo de bloqueo silencioso cambió de nombre, y el indicador de red en vivo pasó a ser una insignia cápsula de telemetría.

## [1.2.0] - 2026-08-25

### 🇺🇸 English

- Persistent notification center for missed blocks and protection events.
- Configurable handling of unanswered prompts, including automatic blocking after a wait from 30 seconds to 30 minutes.
- Allow once for the current session.
- Country indicators in prompts, rules, and logs through the local GeoIP database.
- FAQ documentation covering WFP, Windows Firewall integration, modes, rules, logs, permissions, limitations, and troubleshooting.
- Package-aware rules keep Store applications under control while prompts are open, and helper processes such as the Git companion binaries resolve more consistently.
- Permission popups were refined, and WFP drop monitoring handles short-lived processes.

### 🇪🇸 Español

- Centro de notificaciones persistente para los bloqueos perdidos y los eventos de protección.
- Gestión configurable de los avisos sin responder, incluido el bloqueo automático tras una espera de 30 segundos a 30 minutos.
- Permitir una vez para la sesión actual.
- Indicadores de país en avisos, reglas y registros mediante la base de datos GeoIP local.
- Documentación FAQ que cubre WFP, la integración con Windows Firewall, modos, reglas, registros, permisos, limitaciones y solución de problemas.
- Las reglas conscientes de paquetes mantienen controladas las apps de la Store mientras hay avisos abiertos, y los procesos auxiliares como los binarios acompañantes de Git se resuelven de forma más consistente.
- Los popups de permisos se refinaron, y la monitorización de descartes de WFP gestiona los procesos de vida corta.

## [1.1.1] - 2026-08-24

### 🇺🇸 English

- Minimize to tray on close and minimize to taskbar, with a close button tooltip that follows the setting.
- The firewall master toggle state was not retained, and the engine was disposed when minimizing to the tray.
- The DPI awareness manifest and the tray menu clamping were corrected.

### 🇪🇸 Español

- Minimizar a la bandeja al cerrar y minimizar a la barra de tareas, con un tooltip del botón de cierre que sigue la opción.
- El estado del interruptor maestro del firewall no se retenía, y el motor se eliminaba al minimizar a la bandeja.
- Se corrigieron el manifiesto de reconocimiento de DPI y el ajuste del menú de la bandeja para que no se salga de la pantalla.

## [1.1.0] - 2026-08-24

### 🇺🇸 English

- Auto-update system with a dedicated About window.
- Run at Windows startup toggle.
- First network activity toast, direction badges, and DWM rounded window corners.
- Crisp themed vector icons in place of emoji.

### 🇪🇸 Español

- Sistema de auto-actualización con una ventana Acerca de dedicada.
- Interruptor para arrancar con Windows.
- Toast de primera actividad de red, insignias de dirección y esquinas de ventana redondeadas por DWM.
- Iconos vectoriales temáticos nítidos en lugar de emojis.

## [1.0.1] - 2026-08-23

### 🇺🇸 English

- Initial follow-up tag, published to Git only.
- Tray icon with minimize to tray.
- Connection block log.
- The permission prompt no longer blocks the interface.
- Resolved the ambiguity between the WinForms and WPF namespaces.

### 🇪🇸 Español

- Etiqueta de seguimiento inicial, publicada solo en Git.
- Icono de bandeja con minimizar a la bandeja.
- Registro de bloqueos de conexiones.
- El aviso de permisos ya no bloquea la interfaz.
- Resuelta la ambigüedad entre los espacios de nombres WinForms y WPF.

## [1.0.0] - 2026-08-23

### 🇺🇸 English

- Initial tag, published to Git only.
- Per-application WFP whitelist firewall with a custom non-blocking prompt and a Windows service.

### 🇪🇸 Español

- Etiqueta inicial, publicada solo en Git.
- Firewall de lista blanca WFP por aplicación con un aviso personalizado que no bloquea y un servicio de Windows.
