using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Threading;
using ButtonBase = System.Windows.Controls.Primitives.ButtonBase;
using ContextMenu = System.Windows.Controls.ContextMenu;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using WF = System.Windows.Forms;

namespace CyberWall.UI.Controls;

/// <summary>
/// Coordinates click-to-toggle behavior between a button and its ContextMenu so that a
/// second click on the trigger button closes an open menu instead of closing and immediately
/// reopening it regardless of click hold duration.
/// </summary>
public static class MenuToggleHelper
{
    private static readonly ConditionalWeakTable<ButtonBase, ToggleController> Controllers = new();

    public static void Attach(
        ButtonBase button,
        ContextMenu menu,
        Action? beforeOpen = null,
        PlacementMode placement = PlacementMode.Bottom)
    {
        ArgumentNullException.ThrowIfNull(button);
        ArgumentNullException.ThrowIfNull(menu);

        if (Controllers.TryGetValue(button, out var existing))
        {
            existing.Update(menu, beforeOpen, placement);
            return;
        }

        var controller = new ToggleController(button, menu, beforeOpen, placement);
        Controllers.Add(button, controller);
    }

    public static bool Toggle(ButtonBase button)
    {
        ArgumentNullException.ThrowIfNull(button);
        if (Controllers.TryGetValue(button, out var controller))
            return controller.Toggle();

        if (button.ContextMenu is not { } menu)
            return false;

        if (menu.IsOpen)
        {
            menu.IsOpen = false;
            return false;
        }

        menu.PlacementTarget = button;
        menu.Placement = PlacementMode.Bottom;
        menu.IsOpen = true;
        return true;
    }

    private sealed class ToggleController
    {
        private const long FallbackGuardMs = 250;

        private readonly ButtonBase _button;
        private ContextMenu _menu;
        private Action? _beforeOpen;
        private PlacementMode _placement;
        private Window? _hookedWindow;
        private bool _suppressUntilRelease;
        private long _dismissedOverButtonTick;

        public ToggleController(ButtonBase button, ContextMenu menu, Action? beforeOpen, PlacementMode placement)
        {
            _button = button;
            _menu = menu;
            _beforeOpen = beforeOpen;
            _placement = placement;

            _button.PreviewMouseLeftButtonDown += OnButtonPreviewMouseLeftButtonDown;
            HookMenu(_menu);
        }

        public void Update(ContextMenu menu, Action? beforeOpen, PlacementMode placement)
        {
            if (!ReferenceEquals(_menu, menu))
            {
                UnhookMenu(_menu);
                _menu = menu;
                HookMenu(_menu);
            }

            _beforeOpen = beforeOpen;
            _placement = placement;
        }

        private void HookMenu(ContextMenu menu)
        {
            menu.Opened += OnMenuOpened;
            menu.Closed += OnMenuClosed;
            Mouse.AddPreviewMouseDownOutsideCapturedElementHandler(menu, OnOutsideMouseDown);
        }

        private void UnhookMenu(ContextMenu menu)
        {
            menu.Opened -= OnMenuOpened;
            menu.Closed -= OnMenuClosed;
            Mouse.RemovePreviewMouseDownOutsideCapturedElementHandler(menu, OnOutsideMouseDown);
        }

        private void OnMenuOpened(object sender, RoutedEventArgs e)
        {
            _suppressUntilRelease = false;
            _dismissedOverButtonTick = 0;
        }

        private void OnMenuClosed(object sender, RoutedEventArgs e)
        {
            UnhookWindowMouseUp();
        }

        private void OnOutsideMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton != MouseButton.Left || !_menu.IsOpen)
                return;

            if (!IsCursorOverButton(_button))
                return;

            _suppressUntilRelease = true;
            _dismissedOverButtonTick = Environment.TickCount64;
            HookWindowMouseUp();
            _menu.IsOpen = false;
        }

        private void OnButtonPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (_menu.IsOpen)
            {
                _suppressUntilRelease = true;
                _dismissedOverButtonTick = Environment.TickCount64;
                HookWindowMouseUp();
                _menu.IsOpen = false;
                e.Handled = true;
                return;
            }

            // A fresh mouse press when the menu is already closed should open cleanly.
            _suppressUntilRelease = false;
            _dismissedOverButtonTick = 0;
        }

        private void HookWindowMouseUp()
        {
            var window = Window.GetWindow(_button);
            if (window == null || ReferenceEquals(window, _hookedWindow))
                return;

            UnhookWindowMouseUp();
            _hookedWindow = window;
            _hookedWindow.AddHandler(UIElement.PreviewMouseLeftButtonUpEvent, new MouseButtonEventHandler(OnWindowPreviewMouseLeftButtonUp), handledEventsToo: true);
        }

        private void UnhookWindowMouseUp()
        {
            if (_hookedWindow == null) return;
            _hookedWindow.RemoveHandler(UIElement.PreviewMouseLeftButtonUpEvent, new MouseButtonEventHandler(OnWindowPreviewMouseLeftButtonUp));
            _hookedWindow = null;
        }

        private void OnWindowPreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            UnhookWindowMouseUp();
            // Clear suppression after the current MouseLeftButtonUp + Button.Click dispatch completes.
            _button.Dispatcher.BeginInvoke(DispatcherPriority.Input, () =>
            {
                _suppressUntilRelease = false;
            });
        }

        public bool Toggle()
        {
            var elapsedMs = _dismissedOverButtonTick > 0
                ? Environment.TickCount64 - _dismissedOverButtonTick
                : long.MaxValue;

            if (_menu.IsOpen || _suppressUntilRelease || elapsedMs < FallbackGuardMs)
            {
                _suppressUntilRelease = false;
                _dismissedOverButtonTick = 0;
                UnhookWindowMouseUp();
                _menu.IsOpen = false;
                return false;
            }

            _beforeOpen?.Invoke();
            _menu.PlacementTarget = _button;
            _menu.Placement = _placement;
            _menu.IsOpen = true;
            return true;
        }

        private static bool IsCursorOverButton(ButtonBase button)
        {
            if (!button.IsVisible) return false;
            if (PresentationSource.FromVisual(button) == null) return false;

            try
            {
                var screen = WF.Control.MousePosition;
                var local = button.PointFromScreen(new System.Windows.Point(screen.X, screen.Y));
                return local.X >= 0 && local.Y >= 0 && local.X <= button.ActualWidth && local.Y <= button.ActualHeight;
            }
            catch
            {
                return false;
            }
        }
    }
}

