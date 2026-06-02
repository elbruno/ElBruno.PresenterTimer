using System.Windows.Input;

namespace ElBruno.PresenterTimer.ViewModels;

/// <summary>
/// A lightweight <see cref="ICommand"/> implementation that delegates Execute and CanExecute
/// to caller-supplied delegates. Supports both parameterless and parameter-carrying variants.
/// </summary>
public sealed class RelayCommand : ICommand
{
    private readonly Action<object?> _execute;
    private readonly Func<object?, bool>? _canExecute;

    public RelayCommand(Action execute, Func<bool>? canExecute = null)
        : this(_ => execute(), canExecute is null ? null : _ => canExecute())
    {
    }

    public RelayCommand(Action<object?> execute, Func<object?, bool>? canExecute = null)
    {
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        _canExecute = canExecute;
    }

    public event EventHandler? CanExecuteChanged
    {
        add => CommandManager.RequerySuggested += value;
        remove => CommandManager.RequerySuggested -= value;
    }

    public bool CanExecute(object? parameter) => _canExecute is null || _canExecute(parameter);

    public void Execute(object? parameter) => _execute(parameter);

    /// <summary>Forces WPF to re-evaluate <see cref="CanExecute"/> for all commands.</summary>
    public static void RaiseCanExecuteChanged()
        => System.Windows.Application.Current?.Dispatcher.Invoke(
            CommandManager.InvalidateRequerySuggested);
}
