using System.Diagnostics;
using System.Threading;

namespace CyberWall.Service.Engine;

/// <summary>
/// Single owner guard for the firewall engine.
///
/// CyberWall can run the engine from the desktop application or from the Windows
/// service. Both would otherwise create, remove and rewrite the same Windows Firewall
/// rules, fighting over the machine-wide profile policy. Only the owner of the engine
/// is allowed to touch the firewall.
/// </summary>
public static class EngineOwnership
{
    private const string OwnershipMutexName = @"Global\CyberWall_Engine_Ownership";

    /// <summary>Mutex the desktop application holds while it is running.</summary>
    private const string UiMutexName = @"Global\CyberWall_SingleInstance_Mutex";

    private static Mutex? _mutex;

    /// <summary>
    /// Tries to become the owner of the engine. Returns false when another host
    /// (the Windows service, for instance) already owns it.
    /// </summary>
    public static bool TryAcquire(TimeSpan? timeout = null)
    {
        lock (typeof(EngineOwnership))
        {
            if (_mutex != null) return true;

            try
            {
                var mutex = new Mutex(false, OwnershipMutexName);
                bool acquired;
                try
                {
                    acquired = timeout.HasValue
                        ? mutex.WaitOne(timeout.Value)
                        : mutex.WaitOne(0);
                }
                catch (AbandonedMutexException)
                {
                    // The previous owner died without releasing: the mutex is ours now.
                    acquired = true;
                }

                if (!acquired)
                {
                    mutex.Dispose();
                    return false;
                }

                _mutex = mutex;
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"EngineOwnership: could not acquire the engine mutex: {ex.Message}");
                return false;
            }
        }
    }

    public static void Release()
    {
        lock (typeof(EngineOwnership))
        {
            if (_mutex == null) return;
            try { _mutex.ReleaseMutex(); } catch { }
            try { _mutex.Dispose(); } catch { }
            _mutex = null;
        }
    }

    public static bool IsOwned => _mutex != null;

    /// <summary>
    /// Reports whether the desktop application is running. The service uses it to
    /// step aside and let the interactive session own the engine.
    /// </summary>
    public static bool IsUiRunning()
    {
        try
        {
            using var uiMutex = new Mutex(false, UiMutexName);
            try
            {
                // The mutex is free when the desktop application is not running.
                return !uiMutex.WaitOne(0);
            }
            catch (AbandonedMutexException)
            {
                return false;
            }
        }
        catch
        {
            return false;
        }
    }
}
