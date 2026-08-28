using System.Windows;
using CyberWall.Common.Models;
using CyberWall.Common.Settings;
using CyberWall.Service.Engine;
using CyberWall.UI.Popup;
using Application = System.Windows.Application;

namespace CyberWall.UI.Services;

public class PromptManager
{
    private static PromptManager? _instance;
    public static PromptManager Instance => _instance ??= new PromptManager();

    private FirewallService? _svc;
    private MainWindow? _mainWindow;
    private PromptStackWindow? _activeStack;
    private readonly HashSet<string> _pendingKeys = new(StringComparer.OrdinalIgnoreCase);

    public void Initialize(FirewallService svc, MainWindow mainWindow)
    {
        _svc = svc;
        _mainWindow = mainWindow;
    }

    public bool HasOpenPrompts()
    {
        return _activeStack != null && _activeStack.IsVisible && _activeStack.ActiveCount > 0;
    }

    public void Enqueue(ConnectionEvent ev)
    {
        var app = Application.Current;
        if (app == null) return;

        app.Dispatcher.BeginInvoke(() =>
        {
            try
            {
                var key = AppRule.Normalize(ev.AppPath);
                if (!_pendingKeys.Add(key)) return;

                AutoBlockToast.CloseAll();

                if (_activeStack == null || !_activeStack.IsLoaded)
                {
                    _activeStack = new PromptStackWindow();
                    _activeStack.CardResolved += OnCardResolved;
                    _activeStack.Closed += (_, _) =>
                    {
                        _activeStack = null;
                        _pendingKeys.Clear();
                    };
                    _activeStack.AddCard(ev);
                    _activeStack.Show();
                }
                else
                {
                    _activeStack.AddCard(ev);
                }

                PromptSoundService.PlayPromptSound();
            }
            catch
            {
                // Safety fallback
            }
        });
    }

    private void OnCardResolved(PromptCardControl card, PopupDecision decision, bool timedOut)
    {
        var key = AppRule.Normalize(card.Event.AppPath);
        _pendingKeys.Remove(key);

        if (_svc == null) return;

        var ev = card.Event;
        Task.Run(() =>
        {
            try
            {
                switch (decision)
                {
                    case PopupDecision.AllowAlways:
                        _svc.SetVerdict(ev.AppPath, Verdict.Allow, true, ev);
                        _mainWindow?.Dispatcher.BeginInvoke(() => _mainWindow.RefreshRules());
                        break;
                    case PopupDecision.AllowOnce:
                        _svc.SetVerdict(ev.AppPath, Verdict.Allow, false, ev);
                        break;
                    case PopupDecision.BlockAlways:
                        _svc.SetVerdict(ev.AppPath, Verdict.Block, true, ev);
                        _mainWindow?.Dispatcher.BeginInvoke(() => _mainWindow.RefreshRules());
                        if (timedOut)
                        {
                            _mainWindow?.Dispatcher.BeginInvoke(() => _mainWindow.RecordAutoBlock(ev));
                        }
                        break;
                    default:
                        _svc.SetVerdict(ev.AppPath, Verdict.Block, false, ev);
                        break;
                }
            }
            catch { }
        });
    }

    public void ShowPreview(PopupPosition position, int monitorIndex)
    {
        PromptStackWindow.ShowPreview(position, monitorIndex);
    }

    public void DismissPreview()
    {
        PromptStackWindow.DismissPreview();
    }

    public void CloseAll()
    {
        var app = Application.Current;
        if (app == null) return;

        app.Dispatcher.BeginInvoke(() =>
        {
            if (_activeStack != null)
            {
                _activeStack.DismissAll();
                _activeStack = null;
            }
            _pendingKeys.Clear();
        });
    }
}
