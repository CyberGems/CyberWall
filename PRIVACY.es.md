# Privacidad — CyberWall

CyberWall funciona en tu PC y guarda tus datos en tu PC. **No tiene cuenta, telemetría, SDK de analítica ni anuncios.** Nunca envía tu historial de conexiones, tus reglas ni tus ajustes a su autor ni a nadie más.

Todo lo que sabe se guarda en `%ProgramData%\CyberWall\`: ajustes, reglas, notificaciones, el registro de conexiones bloqueadas, la base de datos local de países y las copias de seguridad. El desinstalador restaura la política del firewall y elimina su tarea programada y sus reglas, pero no borra esa carpeta. Bórrala manualmente si quieres que desaparezca el historial.

## Cuándo CyberWall se conecta a internet

Estas son todas las conexiones que CyberWall hace por su cuenta. Ninguna envía tu historial de conexiones ni tus reglas. Como en cualquier petición de internet, el servidor ve tu dirección IP.

| Cuándo | Dónde | Para qué | Cómo desactivarlo |
|---|---|---|---|
| Unos 3 segundos después de iniciar | `api.github.com` | Consultar GitHub Releases para detectar una versión nueva | `AutoCheckForUpdates` en Configuración |
| Al iniciar y al abrir la vista de registros o estadísticas | `cdn.jsdelivr.net`, con `unpkg.com` como alternativa | Descargar la tabla pública de IP a país para etiquetar las conexiones. Las consultas se hacen en local; las IP de destino no se envían | Sin ajuste específico |
| Cada 15 segundos y al cambiar la red | `www.msftconnecttest.com`, `www.google.com`, `1.1.1.1` | Sondas de alcance para el indicador de conexión | Sin ajuste específico |

Los enlaces que abres tú (una búsqueda web, un enlace a la wiki o al sitio) no están en esta lista.

## Qué lee CyberWall de tu PC

CyberWall lee la tabla de conexiones TCP, los procesos en ejecución, los adaptadores de red, las entradas de inicio y la política de Windows Firewall para mostrarte qué se conecta y aplicar las reglas que aprobaste. Esa información se queda en pantalla. Cualquier regla que cambia el sistema se crea solo tras tu confirmación.

## Dudas

Abre un issue: https://github.com/CyberGems/CyberWall/issues

Para una pregunta concreta sobre privacidad, usa la [página de contacto](https://cybergems.org/contact). La declaración general de la suite está en [cybergems.org/es/privacy](https://cybergems.org/es/privacy).
