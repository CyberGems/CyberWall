# CyberWall — Firewall Windows por programa (WFP)

Scaffolding robusto + simplificado inspirado en simplewall. Solo Windows. Bloqueo por defecto (whitelist), popup por cada **programa nuevo** (no por IP).

## Stack elegido (Windows-only)
- **.NET 10 + WPF** (UI nativa) + `fwpuclnt.dll` (WFP) vía P/Invoke
- **Servicio/Engine** en `CyberWall.Service` (requiere Admin) + **UI** en `CyberWall.UI`
- **Reglas por app** en `%ProgramData%\CyberWall\rules.json` — filtros persistentes como simplewall
- **Bilingüe** ES/EN (`CyberWall.Common/I18n/Strings.cs`)
- **IPC** Named Pipe `CyberWall_Engine` (preparado para separar UI↔Service)

## Estructura
```
CyberWall.sln
src/CyberWall.Common  -> modelos AppRule, ConnectionEvent, i18n, pipe protocol
src/CyberWall.Service -> WfpEngine, RuleStore, FirewallService, PipeServer
src/CyberWall.UI      -> MainWindow (lista reglas) + ConnectionPopup (allow/block)
```

## Ejecutar
```ps
dotnet build
dotnet run --project src/CyberWall.UI      # UI (WPF)
dotnet run --project src/CyberWall.Service # engine (admin para WFP real)
```

> WFP real requiere ejecutar como Administrador. Sin admin corre en modo simulado (Classify → Ask).

## Flujo popup por programa
1. `WfpEngine.Classify(appPath)` → si no hay regla → `Verdict.Ask`
2. `ConnectionPopup` muestra `app.exe quiere conectarse — Outbound TCP 1.2.3.4:443`
3. Usuario elige Permitir/Bloquear (+ Recordar) → `RuleStore.Upsert()`
4. Próxima conexión del mismo exe ya no pregunta.

## Próximos pasos
- Callout driver real + `FwpmFilterAdd0` por app (hash/path)
- Tray icon + autostart + modo temporal/permanente como simplewall
- Log de paquetes bloqueados/permitidos
- Instalador MSIX
