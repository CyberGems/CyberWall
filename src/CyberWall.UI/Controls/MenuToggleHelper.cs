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
/// Coordinates click-to-toggle behavior between a trigger button and its ContextMenu so that
/// a second click on the trigger button closes an open menu instead of letting WPF's outside-click
/// auto-dismiss close it on mouse-down and Button.Click immediately reopen it on mouse-up,
/// regardless of how long the mouse button is held down.
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

    internal static void ArmExternalDismissForTest(ButtonBase button)
    {
        if (Controllers.TryGetValue(button, out var controller))
        {
            controller.ArmSuppression();
        }
    }

    private sealed class ToggleController
    {
        private const long ClickThroughWindowMs = 60;

        private readonly ButtonBase _button;
        private readonly MouseButtonEventHandler _windowMouseUpHandler;
        private ContextMenu _menu;
        private Action? _beforeOpen;
        private PlacementMode _placement;
        private Window? _hookedWindow;

        private bool _isOpen;
        private bool _closingFromSelf;
        private bool _suppressCurrentPress;
        private long _armedAtTick;

        public ToggleController(ButtonBase button, ContextMenu menu, Action? beforeOpen, PlacementMode placement)
        {
            _button = button;
            _menu = menu;
            _beforeOpen = beforeOpen;
            _placement = placement;
            _windowMouseUpHandler = OnWindowPreviewMouseLeftButtonUp;

            _button.PreviewMouseLeftButtonDown += OnButtonPreviewMouseLeftButtonDown;
            _button.MouseEnter += OnButtonMouseEnter;
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
        }

        private void UnhookMenu(ContextMenu menu)
        {
            menu.Opened -= OnMenuOpened;
            menu.Closed -= OnMenuClosed;
        }

        private void OnMenuOpened(object sender, RoutedEventArgs e)
        {
            _isOpen = true;
            _closingFromSelf = false;
            _suppressCurrentPress = false;
            _armedAtTick = 0;
            UnhookWindowMouseUp();
        }

        private void OnMenuClosed(object sender, RoutedEventArgs e)
        {
            bool wasOpen = _isOpen;
            _isOpen = false;
            bool selfClose = _closingFromSelf;
            _closingFromSelf = false;

            if (selfClose)
                return;

            // WPF's MenuBase class handler closes the ContextMenu synchronously inside WM_LBUTTONDOWN
            // before instance handlers or Button.PreviewMouseLeftButtonDown run. Detect that press
            // right here using the physical cursor and primary button state (both immune to capture).
            if (wasOpen && IsLeftButtonDown() && IsCursorOverButton(_button))
            {
                ArmSuppression();
            }
            else
            {
                _suppressCurrentPress = false;
                _armedAtTick = 0;
                UnhookWindowMouseUp();
            }
        }

        internal void ArmSuppression()
        {
            _suppressCurrentPress = true;
            _armedAtTick = Environment.TickCount64;
            HookWindowMouseUp();
        }

        private void OnButtonPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (_isOpen || _menu.IsOpen)
            {
                _closingFromSelf = true;
                ArmSuppression();
                _menu.IsOpen = false;
                e.Handled = true;
                return;
            }

            if (_suppressCurrentPress)
            {
                bool sameClickThroughPress = _hookedWindow != null &&
                    (Environment.TickCount64 - _armedAtTick) <= ClickThroughWindowMs;
                if (sameClickThroughPress)
                {
                    // Swallow the pass-through MouseDown from the press that just dismissed the menu
                    // so ButtonBase never captures, animates IsPressed, or raises Click on MouseUp.
                    e.Handled = true;
                    return;
                }

                // Previous press was released outside the window; allow this new click to open.
                ClearSuppression();
            }
        }

        private void OnButtonMouseEnter(object sender, MouseEventArgs e)
        {
            if (_suppressCurrentPress && !IsLeftButtonDown())
            {
                ClearSuppression();
            }
        }

        private void HookWindowMouseUp()
        {
            var window = Window.GetWindow(_button);
            if (window == null || ReferenceEquals(window, _hookedWindow))
                return;

            UnhookWindowMouseUp();
            _hookedWindow = window;
            _hookedWindow.AddHandler(UIElement.PreviewMouseLeftButtonUpEvent, _windowMouseUpHandler, handledEventsToo: true);
        }

        private void UnhookWindowMouseUp()
        {
            if (_hookedWindow == null) return;
            _hookedWindow.RemoveHandler(UIElement.PreviewMouseLeftButtonUpEvent, _windowMouseUpHandler);
            _hookedWindow = null;
        }

        private void OnWindowPreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            UnhookWindowMouseUp();
            // Clear suppression after the current MouseLeftButtonUp + Button.Click dispatch completes.
            _button.Dispatcher.BeginInvoke(DispatcherPriority.Input, ClearSuppression);
        }

        private void ClearSuppression()
        {
            _suppressCurrentPress = false;
            _armedAtTick = 0;
            UnhookWindowMouseUp();
        }

        public bool Toggle()
        {
            if (_isOpen || _menu.IsOpen)
            {
                _closingFromSelf = true;
                ClearSuppression();
                _menu.IsOpen = false;
                return false;
            }

            if (_suppressCurrentPress)
            {
                ClearSuppression();
                return false;
            }

            _beforeOpen?.Invoke();
            _menu.PlacementTarget = _button;
            _menu.Placement = _placement;
            _menu.IsOpen = true;
            return true;
        }

        private static bool IsLeftButtonDown() =>
            Mouse.LeftButton == MouseButtonState.Pressed ||
            (WF.Control.MouseButtons & WF.MouseButtons.Left) != 0;

        private static bool IsCursorOverButton(ButtonBase button)
        {
            if (!button.IsVisible || button.ActualWidth <= 0 || button.ActualHeight <= 0)
                return false;
            if (PresentationSource.FromVisual(button) == null)
                return false;

            try
            {
                var cursor = WF.Control.MousePosition;
                var topLeft = button.PointToScreen(new System.Windows.Point(0, 0));
                var bottomRight = button.PointToScreen(new System.Windows.Point(button.ActualWidth, button.ActualHeight));
                var minX = Math.Min(topLeft.X, bottomRight.X);
                var maxX = Math.Max(topLeft.X, bottomRight.X);
                var minY = Math.Min(topLeft.Y, bottomRight.Y);
                var maxY = Math.Max(topLeft.Y, bottomRight.Y);
                return cursor.X >= minX && cursor.X <= maxX
                    && cursor.Y >= minY && cursor.Y <= maxY;
            }
            catch
            {
                return false;
            }
        }
    }
}
