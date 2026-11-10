using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using CyberWall.Common.I18n;
using ContextMenu = System.Windows.Controls.ContextMenu;
using MenuItem = System.Windows.Controls.MenuItem;
using Path = System.Windows.Shapes.Path;
using TextBox = System.Windows.Controls.TextBox;

namespace CyberWall.UI.Controls;

/// <summary>
/// Gives a text box the app's own themed edit menu (Undo, Redo, Cut, Copy, Paste, Select all, Delete)
/// instead of the generic Windows one. Usage: ctrl:TextFieldContextMenu.Enabled="True".
/// The menu reuses the ModernContextMenu / ModernMenuItem / ModernMenuSeparator styles, so it follows
/// every theme through the shared DynamicResource brushes, and its labels follow the active language.
/// </summary>
public static class TextFieldContextMenu
{
    private const string MenuTag = "TextFieldContextMenu";

    /// <summary>Deletes the current selection; disabled when there is nothing selected.</summary>
    public static readonly RoutedCommand DeleteSelectionCommand = new("DeleteSelection", typeof(TextFieldContextMenu));

    public static readonly DependencyProperty EnabledProperty = DependencyProperty.RegisterAttached(
        "Enabled", typeof(bool), typeof(TextFieldContextMenu),
        new PropertyMetadata(false, OnEnabledChanged));

    public static void SetEnabled(DependencyObject element, bool value) => element.SetValue(EnabledProperty, value);

    public static bool GetEnabled(DependencyObject element) => (bool)element.GetValue(EnabledProperty);

    private static void OnEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not TextBox box) return;

        if (e.NewValue is true)
        {
            Attach(box);
        }
        else
        {
            box.ContextMenu = null;
        }
    }

    private static void Attach(TextBox box)
    {
        if (box.ContextMenu is ContextMenu current && Equals(current.Tag, MenuTag)) return;

        var menu = new ContextMenu
        {
            Tag = MenuTag,
            PlacementTarget = box,
            Placement = PlacementMode.MousePoint
        };
        ApplyStyle(menu, box, "ModernContextMenu");

        var labels = new List<(MenuItem Item, string Key)>();

        MenuItem Add(string key, ICommand? command, string gesture, UIElement icon)
        {
            var item = new MenuItem
            {
                Icon = icon,
                InputGestureText = gesture,
                Command = command,
                CommandTarget = command is null ? null : box
            };
            ApplyStyle(item, box, "ModernMenuItem");
            labels.Add((item, key));
            menu.Items.Add(item);
            return item;
        }

        Add("CtxUndo", ApplicationCommands.Undo, "Ctrl+Z", StrokeIcon("M 4 5 H 8.5 C 10.5 5 12 6.5 12 8.5 C 12 10.5 10.5 12 8.5 12 H 5 M 4 5 L 6.5 2.8 M 4 5 L 6.5 7.2"));
        Add("CtxRedo", ApplicationCommands.Redo, "Ctrl+Y", StrokeIcon("M 12 5 H 7.5 C 5.5 5 4 6.5 4 8.5 C 4 10.5 5.5 12 7.5 12 H 11 M 12 5 L 9.5 2.8 M 12 5 L 9.5 7.2"));
        Separator(box, menu);

        Add("CtxCut", ApplicationCommands.Cut, "Ctrl+X", StrokeIcon("M 3.4 3.4 L 8 8 M 3.4 12.6 L 8 8 M 1.4 3.4 A 1.7 1.7 0 1 1 4.8 3.4 A 1.7 1.7 0 1 1 1.4 3.4 M 1.4 12.6 A 1.7 1.7 0 1 1 4.8 12.6 A 1.7 1.7 0 1 1 1.4 12.6"));
        Add("CtxCopy", ApplicationCommands.Copy, "Ctrl+C", FillIcon("M4.2,3.4 H9.6 V9.2 H4.2 Z M6.2,1.6 H11.6 V7.4 H10.2 V3 H6.2 Z"));
        Add("CtxPaste", ApplicationCommands.Paste, "Ctrl+V", StrokeIcon("M4.4 3.4 H7.6 V2.2 H4.4 Z M2.6 3.6 H3.4 V2.8 H8.6 V3.6 H9.4 V11.4 H2.6 Z"));
        Separator(box, menu);

        Add("CtxSelectAll", ApplicationCommands.SelectAll, "Ctrl+A", StrokeIcon("M1.8 3.8 V2.6 C1.8 2.2 2.2 1.8 2.6 1.8 H3.8 M8.2 1.8 H9.4 C9.8 1.8 10.2 2.2 10.2 2.6 V3.8 M10.2 8.2 V9.4 C10.2 9.8 9.8 10.2 9.4 10.2 H8.2 M3.8 10.2 H2.6 C2.2 10.2 1.8 9.8 1.8 9.4 V8.2 M3.6 4.6 H8.4 M3.6 6 H8.4 M3.6 7.4 H6.8"));

        Add("CtxDelete", DeleteSelectionCommand, "Del", StrokeIcon("M 3 6 H 17 M 7 6 V 4 C 7 3.4 7.4 3 8 3 H 12 C 12.6 3 13 3.4 13 4 V 6 M 5 6 L 6 17 C 6 17.6 6.4 18 7 18 H 13 C 13.6 18 14 17.6 14 17 L 15 6"));

        if (!box.CommandBindings.OfType<CommandBinding>().Any(b => ReferenceEquals(b.Command, DeleteSelectionCommand)))
        {
            box.CommandBindings.Add(new CommandBinding(
                DeleteSelectionCommand,
                (_, _) =>
                {
                    if (box.SelectionLength > 0) EditingCommands.Delete.Execute(null, box);
                },
                (_, e) => e.CanExecute = box.SelectionLength > 0));
        }

        void Refresh()
        {
            foreach (var (item, key) in labels) item.Header = Strings.T(key);
        }

        // Labels and enabled states are resolved when the menu opens, so language and theme
        // changes made while the app is running are picked up without rebuilding the menu.
        menu.Opened += (_, _) => Refresh();

        // Styles live in the window resources, which may not be reachable until the box is loaded.
        box.Loaded += (_, _) =>
        {
            if (menu.Style is null) ApplyStyle(menu, box, "ModernContextMenu");
            foreach (var (item, _) in labels)
                if (item.Style is null) ApplyStyle(item, box, "ModernMenuItem");
            Refresh();
        };

        Refresh();
        box.ContextMenu = menu;
    }

    private static void Separator(TextBox source, ContextMenu menu)
    {
        var separator = new Separator();
        ApplyStyle(separator, source, "ModernMenuSeparator");
        menu.Items.Add(separator);
    }

    private static void ApplyStyle(FrameworkElement element, FrameworkElement source, string key)
    {
        if (source.TryFindResource(key) is Style style) element.Style = style;
    }

    private static Path StrokeIcon(string data) => PathIcon(data, filled: false);

    private static Path FillIcon(string data) => PathIcon(data, filled: true);

    private static Path PathIcon(string data, bool filled)
    {
        var path = new Path
        {
            Data = Geometry.Parse(data),
            Width = 12,
            Height = 12,
            Stretch = Stretch.Uniform,
            StrokeThickness = 1.3,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round,
            StrokeLineJoin = PenLineJoin.Round,
            Fill = filled ? null : System.Windows.Media.Brushes.Transparent
        };

        // DynamicResource equivalent: icons follow the active theme instead of hardcoded colors.
        if (filled) path.SetResourceReference(Shape.FillProperty, "SubTextBrush");
        else path.SetResourceReference(Shape.StrokeProperty, "SubTextBrush");

        return path;
    }
}
