using System.Windows;
using System.Windows.Controls;
using CyberWall.Common.I18n;
using CyberWall.Common.Settings;
using CyberWall.UI.Services;
using ContextMenu = System.Windows.Controls.ContextMenu;
using MenuItem = System.Windows.Controls.MenuItem;

namespace CyberWall.UI.Controls;

/// <summary>
/// Shared builder for the network adapter selection ContextMenu used by both the
/// Traffic Monitor tab and the Floating Traffic Widget.
/// </summary>
internal static class NetworkAdapterMenuHelper
{
    public static void Populate(
        FrameworkElement resourceHost,
        ItemCollection items,
        AppSettings? settings,
        Action? onSelected = null,
        bool persistChanges = true)
    {
        ArgumentNullException.ThrowIfNull(resourceHost);
        ArgumentNullException.ThrowIfNull(items);

        items.Clear();

        var itemStyle = resourceHost.TryFindResource("ModernMenuItem") as Style;
        var sepStyle = resourceHost.TryFindResource("ModernMenuSeparator") as Style;
        var currentId = settings?.PreferredNetworkAdapterId ?? string.Empty;

        items.Add(CreateItem(
            Strings.T("NetworkAdapterAuto"),
            string.Empty,
            string.Empty,
            isSelected: string.IsNullOrEmpty(currentId),
            itemStyle,
            settings,
            onSelected,
            persistChanges));

        var adapters = NetworkSpeedService.Instance.GetSelectableAdapters();
        if (adapters.Count > 0 || !string.IsNullOrEmpty(currentId))
        {
            items.Add(sepStyle != null ? new Separator { Style = sepStyle } : new Separator());
        }

        bool foundSaved = false;
        foreach (var adapter in adapters)
        {
            bool selected = !string.IsNullOrEmpty(currentId) &&
                            string.Equals(adapter.Id, currentId, StringComparison.OrdinalIgnoreCase);
            if (selected) foundSaved = true;

            var label = adapter.IsUp
                ? adapter.DisplayName
                : Strings.T("NetworkAdapterDisconnected", adapter.DisplayName);
            items.Add(CreateItem(
                label,
                adapter.Id,
                adapter.DisplayName,
                selected,
                itemStyle,
                settings,
                onSelected,
                persistChanges));
        }

        if (!string.IsNullOrEmpty(currentId) && !foundSaved)
        {
            var fallbackName = string.IsNullOrWhiteSpace(settings?.PreferredNetworkAdapterName)
                ? currentId
                : settings!.PreferredNetworkAdapterName;
            items.Add(CreateItem(
                Strings.T("NetworkAdapterDisconnected", fallbackName),
                currentId,
                fallbackName,
                isSelected: true,
                itemStyle,
                settings,
                onSelected,
                persistChanges));
        }
    }

    private static MenuItem CreateItem(
        string header,
        string adapterId,
        string adapterName,
        bool isSelected,
        Style? style,
        AppSettings? settings,
        Action? onSelected,
        bool persistChanges)
    {
        var item = new MenuItem
        {
            Header = header,
            IsCheckable = true,
            IsChecked = isSelected,
            Tag = (Id: adapterId, Name: adapterName)
        };
        if (style != null) item.Style = style;
        item.Click += (_, _) =>
        {
            if (settings != null)
            {
                settings.PreferredNetworkAdapterId = adapterId;
                settings.PreferredNetworkAdapterName = adapterName;
                if (persistChanges)
                {
                    settings.Save();
                }
            }

            if (persistChanges)
            {
                NetworkSpeedService.Instance.SetPreferredAdapter(adapterId, adapterName);
            }

            onSelected?.Invoke();
        };
        return item;
    }
}

