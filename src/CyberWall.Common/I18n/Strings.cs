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
        ["ModeBlockAll"] = ("Block all", "Bloquear todo"),
        ["ModeAsk"] = ("Ask to connect", "Preguntar para conectar"),
        ["ModeBlockAllDesc"] = ("Silent block — nothing goes in/out without a rule", "Bloqueo silencioso — nada entra/sale sin regla"),
        ["ModeAskDesc"] = ("Popup for each new program", "Popup por cada programa nuevo"),
        ["StatusEnabledAsk"] = ("Enabled — asking for unknown apps", "Activado — preguntando por apps desconocidas"),
        ["StatusEnabledBlock"] = ("Enabled — blocking all unknown apps", "Activado — bloqueando todo lo desconocido"),
        ["StatusDisabled"] = ("Disabled — all connections allowed", "Desactivado — todo permitido"),
        ["Allow"] = ("Allow", "Permitir"),
        ["Block"] = ("Block", "Bloquear"),
        ["AllowOnce"] = ("Allow once", "Permitir una vez"),
        ["NewConnection"] = ("New connection", "Nueva conexión"),
        ["AppWantsToConnect"] = ("{0} wants to connect", "{0} quiere conectarse"),
        ["Direction"] = ("Direction", "Dirección"),
        ["Inbound"] = ("Inbound", "Entrada"),
        ["Outbound"] = ("Outbound", "Salida"),
        ["Remember"] = ("Remember my choice", "Recordar mi elección"),
        ["Settings"] = ("Settings", "Ajustes"),
        ["Rules"] = ("Rules", "Reglas"),
        ["Search"] = ("Search...", "Buscar..."),
        ["NoRules"] = ("No rules yet", "Sin reglas aún"),
        ["TestPopup"] = ("Test popup", "Probar popup"),
    };

    public static string T(string key, params object[] args)
    {
        if (!Map.TryGetValue(key, out var v)) return key;
        var s = Current == Lang.Es ? v.Es : v.En;
        return args.Length == 0 ? s : string.Format(s, args);
    }
}
