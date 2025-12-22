using System.Threading;

namespace SnapMark.Core;

public class SingleInstanceManager : IDisposable
{
    private static Mutex? _mutex;
    private bool _disposed = false;

    public bool IsFirstInstance { get; private set; }

    public SingleInstanceManager(string appName)
    {
        _mutex = new Mutex(true, appName, out bool createdNew);
        IsFirstInstance = createdNew;
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            if (IsFirstInstance && _mutex != null)
            {
                _mutex.ReleaseMutex();
                _mutex.Dispose();
            }
            _disposed = true;
        }
    }
}


