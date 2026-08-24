namespace CyberWall.Common.I18n;

public enum Lang { En, Es }

public static class Strings
{
    public static Lang Current { get; set; } = Lang.Es;

    private static readonly Dictionary<string, (string En, string Es)> Map = new()
    {
        ["AppTitle"] = ("CyberWall — Per-app firewall", "CyberWall — Firewall por programa"),
        ["MasterOn"] = ("Firewall ON", "Firewall ACTIVADO"),
        ["MasterOff"] = ("Firewall OFF", "Firewall DESACTIVADO"),
        ["ProtectionActive"] = ("PROTECTION ACTIVE", "PROTECCIÓN ACTIVA"),
        ["ProtectionDisabled"] = ("PROTECTION DISABLED", "PROTECCIÓN DESACTIVADA"),
        ["ModeBlockAll"] = ("Block all", "Bloquear todo"),
        ["ModeAsk"] = ("Ask to connect", "Preguntar para conectar"),
        ["ModeBlockAllDesc"] = ("Silent block — nothing goes in/out without a rule", "Bloqueo silencioso — nada entra/sale sin regla"),
        ["ModeAskDesc"] = ("Popup for each new program", "Popup por cada programa nuevo"),
        ["StatusEnabledAsk"] = ("Default block • Asking for new connections • WFP Real-time", "Bloqueo por defecto • Preguntando por nuevas conexiones • WFP Tiempo Real"),
        ["StatusEnabledBlock"] = ("Strict block • Silent blocking for all unknown apps • WFP Real-time", "Bloqueo estricto • Bloqueo silencioso para apps desconocidas • WFP Tiempo Real"),
        ["StatusDisabled"] = ("Disabled — all incoming/outgoing connections allowed", "Desactivado — todas las conexiones entrantes/salientes permitidas"),
        ["Allow"] = ("Allow", "Permitir"),
        ["Block"] = ("Block", "Bloquear"),
        ["AllowOnce"] = ("Allow once", "Permitir una vez"),
        ["NewConnection"] = ("New Connection Request", "Nueva Solicitud de Conexión"),
        ["AppWantsToConnect"] = ("{0} wants to access the network", "{0} quiere acceder a la red"),
        ["Direction"] = ("Direction", "Dirección"),
        ["Inbound"] = ("Inbound", "Entrada"),
        ["Outbound"] = ("Outbound", "Salida"),
        ["Both"] = ("Both", "Ambas"),
        ["Remember"] = ("Remember my choice", "Recordar mi elección"),
        ["Settings"] = ("Settings", "Configuración"),
        ["Rules"] = ("Rules", "Reglas"),
        ["Search"] = ("Search program or path...", "Buscar programa o ruta..."),
        ["NoRules"] = ("No rules yet", "Sin reglas aún"),
        ["TestPopup"] = ("Test popup", "Probar popup"),
        ["RemoveRule"] = ("Remove rule", "Quitar regla"),
        ["Allowed"] = ("Allowed", "Permitidas"),
        ["Blocked"] = ("Blocked", "Bloqueadas"),
        ["Program"] = ("Program", "Programa"),
        ["Path"] = ("Path", "Ruta"),
        ["Verdict"] = ("Verdict", "Veredicto"),
        ["Mode"] = ("Mode:", "Modo:"),
        ["HintRules"] = ("Per-program rules — each new .exe triggers a real-time prompt", "Reglas por programa — cada .exe nuevo dispara un aviso en tiempo real"),
        ["OpenLog"] = ("Open log", "Abrir registro"),
        ["ThemeCyberWall"] = ("CyberWall — Cyber Navy & Neon Cyan", "CyberWall — Azul Cyber y Cyan Neón"),
        ["ThemeDark"] = ("Dark — Neutral Charcoal & Indigo", "Dark — Carbón Neutro e Índigo"),
        ["ThemeLight"] = ("Light — Slate & Royal Blue", "Light — Pizarra y Azul Real"),
    };

    public static string T(string key, params object[] args)
    {
        if (!Map.TryGetValue(key, out var v)) return key;
        var s = Current == Lang.Es ? v.Es : v.En;
        return args.Length == 0 ? s : string.Format(s, args);
    }
}
