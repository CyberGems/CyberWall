<p align="center">
  <a href="./README.md">English</a> · Español
</p>

<p align="center">
  <a href="https://cybergems.org/apps/cyberwall/">
    <img src="https://cybergems.org/banners/es/cyberwall.png" alt="CyberWall: control de red por aplicación para Windows" />
  </a>
</p>

<p align="center">
  <a href="https://github.com/CyberGems/CyberWall/releases/latest"><img src="https://img.shields.io/badge/dynamic/xml?url=https%3A%2F%2Fraw.githubusercontent.com%2FCyberGems%2FCyberWall%2Fmaster%2FDirectory.Build.props&query=%2FProject%2FPropertyGroup%2FVersion&prefix=%20Descargar%20CyberWall%20v&suffix=%20&style=for-the-badge&label=&labelColor=0891B2&color=0891B2" alt="Descargar la última versión" /><img src="https://img.shields.io/badge/Windows_10%2F11_(64--bit)-2563EB?style=for-the-badge" alt="Windows 10/11 (64 bits)" /></a>
  &nbsp;<a href="https://github.com/CyberGems/CyberWall/releases"><img src="https://img.shields.io/badge/Todas_las_versiones-30363D?style=for-the-badge&logo=github&logoColor=white" alt="Todas las versiones" /><img src="https://img.shields.io/badge/Notas_de_la_versi%C3%B3n-475569?style=for-the-badge" alt="Notas de la versión" /></a>
</p>

<p align="center">
  <img src="https://img.shields.io/badge/Licencia-GPL--3.0-1F2428.svg?style=flat-square&color=334155" alt="Licencia" />&nbsp;
  <img src="https://img.shields.io/badge/Plataforma-Windows_10%2F11-1F2428.svg?style=flat-square&color=334155" alt="Plataforma" />&nbsp;
  <img src="https://img.shields.io/badge/.NET-10.0-1F2428.svg?style=flat-square&logo=dotnet&logoColor=white&color=334155" alt=".NET" />&nbsp;
  <a href="https://github.com/CyberGems/CyberWall/wiki"><img src="https://img.shields.io/badge/Wiki-Documentaci%C3%B3n-1F2428?style=flat-square&logo=gitbook&logoColor=white&color=334155" alt="Wiki" /></a>
</p>

---

## ¿Qué es CyberWall?

CyberWall es un cortafuegos moderno y ligero **por aplicación** para Windows, impulsado directamente por la plataforma nativa de **Windows Filtering Platform (WFP)**. Su modelo de denegación por defecto intercepta la actividad de red desconocida y pregunta si cada ejecutable debe permitirse una vez, permitirse siempre o bloquearse con una regla persistente. Los avisos en tiempo real, la monitorización de conexiones, la gestión de reglas, las estadísticas de tráfico y la identificación clara de las aplicaciones hacen que el acceso de salida sea más fácil de entender y controlar sin instalar un controlador de kernel de terceros.

*Gratuito y de código abierto (GPLv3): sin anuncios, sin rastreo y sin recogida de datos. Solo disfrútalo.*

---

## 🛡️ ¿Por qué CyberWall? Protección frente a amenazas reales

La mayoría de los firewalls tradicionales dejan salir todo en silencio o te bombardean con complejas preguntas de IP/puerto. CyberWall adopta un enfoque más sencillo y mucho más efectivo: **nada accede a Internet a menos que tú permitas explícitamente ese programa concreto.**

Así es como CyberWall protege tu equipo frente a amenazas reales, en términos sencillos:

| Categoría de amenaza | Nivel de protección | Qué hace CyberWall |
| :--- | :---: | :--- |
| **Spyware y keyloggers** | 🟢 **10 / 10** | **Bloqueo total de exfiltración**: aunque el malware acabe en tu disco, no puede subir tus contraseñas, pulsaciones o documentos al servidor de un atacante sin un aviso explícito. |
| **Ransomware y balizas C2** | 🟢 **10 / 10** | **Apagón de comunicación**: bloquea scripts y cargas maliciosas no autorizados para que contacten con sus servidores de mando o descarguen claves de cifrado. |
| **Ataques silenciosos por CLI y scripts** | 🟢 **9,5 / 10** | **Intercepción instantánea**: atrapa las herramientas de terminal (`curl`, `powershell`, `ssh`, `git`) en milisegundos si intentan transferencias de red salientes inesperadas. |
| **Sondas entrantes no solicitadas** | 🟢 **10 / 10** | **Rechazo automático**: los escáneres y sondas externos en redes locales se descartan en frío en la capa del kernel. |
| **Shells inversos y acceso remoto** | 🟢 **9,5 / 10** | **Corte de canal**: los atacantes que intentan abrir una puerta trasera interactiva remota encuentran su conexión saliente atascada en el limbo del kernel. |

> [!TIP]
> **Cero riesgo de estabilidad**: a diferencia de otros firewalls que instalan controladores de kernel de terceros intrusivos (a menudo causando pantallas azules / BSOD tras las actualizaciones de Windows), CyberWall depende directamente del motor nativo de Microsoft, la **Windows Filtering Platform (WFP)**. Obtienes protección real a nivel de kernel con un 100 % de estabilidad del sistema operativo.

---

## ✨ Funciones principales

- **Intercepción de descartes WFP en tiempo real**: detección instantánea de descartes en el kernel basada en eventos mediante Windows Filtering Platform (ID de evento `5157`). Intercepta con precisión los procesos CLI de vida corta (`git`, `curl`, `dotnet`, `ssh`) en milisegundos.
- **Reglas inteligentes de toolchain y suite**: resuelve y autoriza automáticamente los ejecutables auxiliares de suites de desarrollo como Git (`git.exe`, `git-remote-https.exe`, `git-remote-http.exe`, `ssh.exe`, etc.).
- **Arquitectura denegar por defecto / lista blanca**: aplica una política estricta de bloqueo por defecto en entrada y salida, permitiendo solo las aplicaciones autorizadas por el usuario.
- **Avisos tipo GlassWire**: elegantes notificaciones de «Primera actividad de red» con insignias gráficas de dirección (`↑ Saliente` / `↓ Entrante`).
- **Visor de registro de conexiones dedicado**: registro de eventos en tiempo real con filtrado instantáneo (por programa, IP, puerto o PID), copia al portapapeles con un clic y acceso directo al archivo de registro.
- **Motor de tema visual (`ThemeCard`)**: tarjetas visuales interactivas con vistas previas en vivo y cambio instantáneo:
  - **CyberWall**: obsidiana y azul marino profundos con acentos de cian neón eléctrico y esmeralda.
  - **Dark**: carbón refinado y pizarra neutra con acentos índigo vibrantes.
  - **Light**: pizarra nítida y blanco puro con acentos azul real.
- **Chrome nativo de Windows 11**: esquinas redondeadas DWM puras con buffers de suavizado de subpíxeles, barra de título personalizada y cuadrícula de posicionamiento multi-monitor.
- **Sistema de actualización automática integrado**: comprobador de actualizaciones de GitHub Releases con un clic, seguimiento de progreso de descarga en vivo e instalador silencioso.
- **Ventana Acerca de dedicada**: resumen interactivo del producto, gestor de actualizaciones y acceso directo a los canales de la comunidad CyberGems.
- **Totalmente bilingüe (EN / ES)**: localización completa de la interfaz en inglés y español con cambio de idioma instantáneo.
- **Almacenamiento persistente de reglas**: las reglas se guardan de forma persistente en `%ProgramData%\CyberWall\rules.json`.
- **Bandeja del sistema y demonio en segundo plano**: integración con la bandeja, minimizar a la bandeja y soporte para ejecutarse como Servicio de Windows en segundo plano (SYSTEM).
---

## 🚀 Primeros pasos

### Instalación (recomendada)

1. Descarga el instalador más reciente desde [Releases](https://github.com/CyberGems/CyberWall/releases/latest)
2. Ejecuta el instalador `.exe` y sigue el asistente de instalación
3. Inicia CyberWall. No necesitas el SDK de .NET para usarlo.

> **Nota**: El filtrado de red real a nivel de kernel y la aplicación de las reglas del cortafuegos requieren privilegios de Administrador.

### 🛡️ Windows SmartScreen

Windows puede mostrar un aviso de SmartScreen la primera vez que ejecutas el instalador de CyberWall: esta es una app de hobby sin firmar, así que Windows aún no ha construido reputación para el archivo. Esto es esperado; el código fuente es público para que puedas inspeccionar exactamente qué hace.

Para continuar:

<details>
<summary><strong>Cómo ejecutar el instalador (paso a paso)</strong></summary>

Windows muestra este aviso para cualquier instalador sin un certificado de firma de código de pago; no significa que el archivo sea inseguro. No hagas clic en "No ejecutar":

1. Ejecuta el instalador. Windows puede mostrar el diálogo azul "Windows protegió tu PC".

![Aviso de Windows SmartScreen](https://cybergems.org/branding/smartscreen-warning.svg)

2. Haz clic en el pequeño enlace **Más información**.

![Diálogo de SmartScreen tras Más información](https://cybergems.org/branding/smartscreen-runanyway.svg)

3. Haz clic en **Ejecutar de todos modos**. El instalador arranca con normalidad.

Puedes verificar el archivo de forma independiente: compara el SHA con el release de GitHub, escanéalo en VirusTotal o compila desde el código fuente. Más detalles: [guía de SmartScreen en el sitio web](https://cybergems.org/download#smartscreen).

</details>
---

## 🛠️ Stack tecnológico y arquitectura

- **Plataforma**: Windows 10 / 11 (x64)
- **Framework**: .NET 10 + WPF (UI nativa)
- **Núcleo de filtrado**: `fwpuclnt.dll` (API de WFP en modo usuario), Cortafuegos Avanzado de Windows (`HNetCfg.FwPolicy2`) y registro de eventos de auditoría de seguridad (`EventLogWatcher`).

```
CyberWall.slnx
├── src/CyberWall.Common/   -> Modelos base (AppRule, ConnectionEvent), cadenas I18n, configuración
├── src/CyberWall.Service/  -> WfpEngine, WfpBlockWatcher, RealFirewall, ConnectionMonitor, RuleStore
└── src/CyberWall.UI/       -> UI WPF sin marco, controles ThemeCard, ConnectionPopup, TrayService, Diálogos
```

### Compilar desde el código fuente (desarrolladores)

Solo necesario si quieres modificar CyberWall o compilarlo tú mismo; los usuarios normales pueden omitir esta sección.

#### Requisitos previos
- Windows 10/11 (x64)
- .NET 10 SDK

#### Compilación y ejecución

```powershell
# Compilar la solución
dotnet build

# Ejecutar con privilegios de Administrador (filtrado WFP real)
.\dev-admin.ps1

# Sesión de desarrollo estándar (filtrado simulado si no está elevado)
.\dev.ps1
```

> **Nota**: El filtrado de red real a nivel de kernel y la aplicación de las reglas del cortafuegos requieren privilegios de Administrador.
---

## 🔍 Cómo funciona

1. **Intercepción en el kernel**: cuando una aplicación sin regla existente intenta una conexión entrante o saliente, Windows Filtering Platform retiene la conexión de forma segura a nivel de kernel.
2. **Captura instantánea de eventos**: `WfpBlockWatcher` recibe el evento de descarte en tiempo real, resuelve la ruta del dispositivo NT del kernel (p. ej. `\device\harddiskvolume3\...`) a una ruta de archivo Win32 estándar (`C:\...`) y elimina los desencadenantes duplicados con debounce.
3. **Aviso interactivo**: aparece una ventana emergente sin marco e intrusiva en la esquina de pantalla de tu elección mostrando el icono de la aplicación, el nombre, el protocolo, el extremo de destino y la insignia gráfica de dirección (`↑ Saliente` / `↓ Entrante`).
4. **Aplicación de reglas**: CyberWall aplica la decisión seleccionada al proceso pendiente. **Permitir siempre** y **Bloquear** guardan una regla persistente en `rules.json` y actualizan el motor del Cortafuegos de Windows, mientras que **Permitir una vez** mantiene la decisión solo para la sesión actual.

---

## ❤️ Donar

Tras incontables horas construyendo y perfeccionando **CyberWall** para mi propio uso, decidí recientemente compartirlo con el mundo junto a mis otras herramientas de código abierto en [CyberGems](https://github.com/CyberGems#-all-apps--repositories).

Si te gustaría apoyar las futuras actualizaciones, te lo agradecería de verdad. Tu donación ayuda a mantener el desarrollo, lanzar nuevas funciones, acelerar la resolución de actualizaciones y errores, y mejorar la calidad de la documentación. También puedes mostrar tu apoyo [poniendo una estrella al repo en GitHub](https://github.com/CyberGems/CyberWall). ¡Gracias! 🙏

<p align="center">
  <a href="https://www.paypal.com/donate/?hosted_button_id=M4PY3UPJA5Y6Q"><img src="https://img.shields.io/badge/Donar-PayPal-0070BA?style=for-the-badge&logo=paypal" alt="Donar con PayPal" /></a>
</p>

<p align="center">
  <a href="https://ko-fi.com/cybergems"><img src="https://img.shields.io/badge/Apóyame_en_Ko--fi-FF5E5B?style=for-the-badge&logo=ko-fi&logoColor=white" alt="Apóyame en Ko-fi" /></a>
</p>

<p align="center">
  <a href="https://buymeacoffee.com/cybergems"><img src="https://img.shields.io/badge/Invítame_a_un_café-FFDD00?style=for-the-badge&logo=buy-me-a-coffee&logoColor=black" alt="Invítame a un café" /></a>
</p>

<div align="center">

<details>
<summary><b>Donaciones cripto (BTC, ETH, USDT, LTC): haz clic para ver las direcciones</b></summary>

| Activo | Dirección | QR |
|---|---|---|
| **BTC** | <pre><code>bc1q5mxzz05nmvsheqzx7970euswta3fksxzcfzag4</code></pre> | <img src="src/CyberWall.UI/Assets/donate/qr-btc.png" width="90" height="90" alt="QR de BTC" /> |
| **ETH** | <pre><code>0x79b703Ec0f77493679Fcd280aF3b983E20c580B8</code></pre> | <img src="src/CyberWall.UI/Assets/donate/qr-eth.png" width="90" height="90" alt="QR de ETH" /> |
| **USDT (ERC20 / BEP20)** | <pre><code>0x79b703Ec0f77493679Fcd280aF3b983E20c580B8</code></pre> | <img src="src/CyberWall.UI/Assets/donate/qr-eth.png" width="90" height="90" alt="QR de USDT" /> |
| **USDT (TRC20)** | <pre><code>TSVbSk1HSyZ1NprCnAYiw56ECwXgH887mD</code></pre> | <img src="src/CyberWall.UI/Assets/donate/qr-usdt-tron.png" width="90" height="90" alt="QR de USDT TRC20" /> |
| **LTC** | <pre><code>LWGnEHgcFCE2BRkzLnsdPDD8Y8ZeDK577X</code></pre> | <img src="src/CyberWall.UI/Assets/donate/qr-ltc.png" width="90" height="90" alt="QR de LTC" /> |

> ⚠️ Envía solo el activo seleccionado en la red indicada. Usar la red incorrecta provocará la pérdida permanente de fondos.

</details>

</div>

---

## Licencia

CyberWall se distribuye bajo los términos de la Licencia Pública General GNU v3.0. Consulta [LICENSE](LICENSE) para el texto completo de la licencia.

Copyright (C) 2026 CyberGems

---

## ❓ Preguntas frecuentes

Para preguntas frecuentes, guías de solución de problemas e instrucciones detalladas de configuración, visita las [Preguntas frecuentes](https://github.com/CyberGems/CyberWall/wiki/FAQ) o la [documentación en línea](https://cybergems.org/docs/cyberwall/FAQ).

---

<p align="center">
  <strong>¡Gracias por usar CyberWall! 🎉</strong><br><br>
  Creado por <a href="https://cybergems.org">CyberGems</a>
</p>

<p align="center">
  <a href="https://www.reddit.com/submit?url=https%3A%2F%2Fcybergems.org%2Fapps%2Fcyberwall%2F&title=CyberWall%3A%20herramienta%20de%20escritorio%20gratuita%20y%20de%20c%C3%B3digo%20abierto%20para%20Windows"><img src="https://img.shields.io/badge/Compartir_en_Reddit-FF4500?style=for-the-badge&logo=reddit&logoColor=white" alt="Compartir en Reddit" /></a>
  &nbsp;<a href="https://twitter.com/intent/tweet?text=CyberWall%3A%20herramienta%20de%20escritorio%20gratuita%20y%20de%20c%C3%B3digo%20abierto%20para%20Windows&url=https%3A%2F%2Fcybergems.org%2Fapps%2Fcyberwall%2F"><img src="https://img.shields.io/badge/Compartir_en_X-1DA1F2?style=for-the-badge&logo=x&logoColor=white" alt="Compartir en X" /></a>
  &nbsp;<a href="https://www.facebook.com/sharer/sharer.php?u=https%3A%2F%2Fcybergems.org%2Fapps%2Fcyberwall%2F"><img src="https://img.shields.io/badge/Compartir_en_Facebook-1877F2?style=for-the-badge&logo=facebook&logoColor=white" alt="Compartir en Facebook" /></a>
  &nbsp;<a href="mailto:?subject=CyberWall%3A%20herramienta%20de%20escritorio%20gratuita%20y%20de%20c%C3%B3digo%20abierto%20para%20Windows&body=CyberWall%3A%20herramienta%20de%20escritorio%20gratuita%20y%20de%20c%C3%B3digo%20abierto%20para%20Windows%20https%3A%2F%2Fcybergems.org%2Fapps%2Fcyberwall%2F"><img src="https://img.shields.io/badge/Compartir_por_Email-EA4335?style=for-the-badge&logo=gmail&logoColor=white" alt="Compartir por correo" /></a>
  &nbsp;<a href="https://t.me/share/url?url=https%3A%2F%2Fcybergems.org%2Fapps%2Fcyberwall%2F&text=CyberWall%3A%20herramienta%20de%20escritorio%20gratuita%20y%20de%20c%C3%B3digo%20abierto%20para%20Windows"><img src="https://img.shields.io/badge/Compartir_en_Telegram-26A5E4?style=for-the-badge&logo=telegram&logoColor=white" alt="Compartir en Telegram" /></a>
  &nbsp;<a href="https://www.linkedin.com/sharing/share-offsite/?url=https%3A%2F%2Fcybergems.org%2Fapps%2Fcyberwall%2F"><img src="https://img.shields.io/badge/Compartir_en_LinkedIn-0A66C2?style=for-the-badge&logo=linkedin&logoColor=white" alt="Compartir en LinkedIn" /></a>
</p>

---

## 🔗 Ver también

Más aplicaciones gratuitas, de código abierto y con la privacidad primero de [**CyberGems**](https://github.com/CyberGems):

| App | Descripción |
|:---:|---|
| 🕐&nbsp;[**CyberClock**](https://github.com/CyberGems/CyberClock#readme) | Reloj de escritorio con analógico y digital, calendario, temporizador, cronómetro y módulo de relajación. |
| 📢&nbsp;[**CyberFeeds**](https://github.com/CyberGems/CyberFeeds#readme) | Lector RSS y Atom de alto rendimiento y local-first, creado para la velocidad, la privacidad y la lectura limpia. |
| 🚀&nbsp;[**CyberLauncher**](https://github.com/CyberGems/CyberLauncher#readme) | Lanzador de aplicaciones de Windows con esquinas calientes, programador, monitor de sistema y terminal integrada. |
| 💻&nbsp;[**CyberManager**](https://github.com/CyberGems/CyberManager#readme) | Gestor de tareas ligero y de alto rendimiento, virtualizado y nativo de NT, una potente alternativa al Administrador de Tareas. |
| 📝&nbsp;[**CyberNotes**](https://github.com/CyberGems/CyberNotes#readme) | App de notas centrada en la privacidad con texto enriquecido, carpetas, pestañas y almacenamiento local protegido con bcrypt. |
| ⚡&nbsp;[**CyberPaste**](https://github.com/CyberGems/CyberPaste#readme) | Gestor de portapapeles con la privacidad primero para texto, código, imágenes, HTML y archivos. |
| 📸&nbsp;[**CyberSnap**](https://github.com/CyberGems/CyberSnap#readme) | Suite de captura y anotación de pantalla con herramientas vectoriales, OCR de alta velocidad, grabación de pantalla y selector de color. |
| ⭐&nbsp;[**CyberTray**](https://github.com/CyberGems/CyberTray#readme) | Lanzador de bandeja de alto rendimiento con hotspots, monitoreo de sistema, gestor de procesos y bóveda de archivos protegida con PIN. |
| 💫&nbsp;[**CyberViewer**](https://github.com/CyberGems/CyberViewer#readme) | Visor y editor de imágenes completo diseñado para usuarios casuales y avanzados. |

➡️ **[Todas las aplicaciones en cybergems.org](https://cybergems.org)**