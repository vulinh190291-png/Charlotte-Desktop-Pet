using System.Windows.Input;

namespace Charlotte.Windows.ViewModels;

public sealed class RelayCommand(Action<object?> execute,Func<Exception,bool>? handleException=null) : ICommand
{
    public bool CanExecute(object? parameter)=>true;

    public void Execute(object? parameter)
    {
        try { execute(parameter); }
        catch(Exception error) when(handleException?.Invoke(error)==true) { }
    }

    public event EventHandler? CanExecuteChanged
    {
        add { }
        remove { }
    }
}
