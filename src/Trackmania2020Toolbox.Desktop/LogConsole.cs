using System;
using System.Collections.Generic;
using System.Text;
using Avalonia.Threading;
using Trackmania2020Toolbox;

namespace Trackmania2020Toolbox.Desktop;

public class LogConsole : IConsole
{
    private readonly Action<string> _logAction;
    private readonly Action<string>? _writeAction;
    private readonly Func<string, IEnumerable<string>, Task<int>>? _selectionFunc;

    public LogConsole(Action<string> logAction, Action<string>? writeAction = null, Func<string, IEnumerable<string>, Task<int>>? selectionFunc = null)
    {
        _logAction = logAction;
        _writeAction = writeAction;
        _selectionFunc = selectionFunc;
    }

    public void WriteLine(string? value = null)
    {
        Dispatcher.UIThread.Post(() => _logAction((value ?? "") + Environment.NewLine));
    }

    public void Write(string? value = null)
    {
        if (_writeAction != null)
        {
            Dispatcher.UIThread.Post(() => _writeAction(value ?? ""));
        }
        else
        {
            Dispatcher.UIThread.Post(() => _logAction(value ?? ""));
        }
    }

    public string? ReadLine()
    {
        // For now, return null as we don't have interactive input in GUI yet
        return null;
    }

    public async Task<int> SelectItemAsync(string title, IEnumerable<string> items)
    {
        if (_selectionFunc != null)
        {
            return await _selectionFunc(title, items);
        }

        WriteLine($"Note: Multiple items found for '{title}'. Picking the first one (fallback).");
        return 1;
    }
}
