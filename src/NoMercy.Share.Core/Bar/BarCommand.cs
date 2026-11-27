using System.Windows.Input;

namespace NoMercy.Share.Core.Bar;

/// <summary>A command that runs a plain action with the button's parameter.</summary>
public sealed class BarCommand(Action<object?> run) : ICommand
{
    public event EventHandler? CanExecuteChanged
    {
        add { }
        remove { }
    }

    public bool CanExecute(object? parameter) => true;

    public void Execute(object? parameter) => run(parameter);
}
