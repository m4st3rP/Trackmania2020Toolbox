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

    public LogConsole(Action<string> logAction, Action<string>? writeAction = null)
    {
        _logAction = logAction;
        _writeAction = writeAction;
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

    public Task<int> SelectItemAsync(string title, IEnumerable<string> items)
    {
        // For GUI, we just take the first item if multiple are found for now.
        // Implementing a popup dialog would be better but is more complex.
        WriteLine($"Note: Multiple items found for '{title}'. Picking the first one in non-interactive GUI mode.");
        return Task.FromResult(1);
    }
}
