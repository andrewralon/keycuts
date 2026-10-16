using System;
using System.Threading.Tasks;
using System.Windows.Input;

namespace keycuts.GUI.Mvvm
{
    public class RelayCommand : ICommand
    {
        private readonly Action execute;

        private readonly Func<bool>? canExecute;

        public RelayCommand(Action execute, Func<bool>? canExecute = null)
        {
            this.execute = execute ?? throw new ArgumentNullException(nameof(execute));
            this.canExecute = canExecute;
        }

        public event EventHandler? CanExecuteChanged;

        public bool CanExecute(object? parameter)
        {
            return canExecute == null || canExecute();
        }

        public void Execute(object? parameter)
        {
            if (CanExecute(parameter))
            {
                execute();
            }
        }

        public void RaiseCanExecuteChanged()
        {
            CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>
    /// Runs one invocation at a time; CanExecute is false while a run is in flight.
    /// </summary>
    public class AsyncRelayCommand : ICommand
    {
        private readonly Func<Task> execute;

        private readonly Func<bool>? canExecute;

        private bool isRunning;

        public AsyncRelayCommand(Func<Task> execute, Func<bool>? canExecute = null)
        {
            this.execute = execute ?? throw new ArgumentNullException(nameof(execute));
            this.canExecute = canExecute;
        }

        public event EventHandler? CanExecuteChanged;

        public bool IsRunning
        {
            get { return isRunning; }
        }

        public bool CanExecute(object? parameter)
        {
            return !isRunning && (canExecute == null || canExecute());
        }

        public async void Execute(object? parameter)
        {
            await ExecuteAsync();
        }

        public async Task ExecuteAsync()
        {
            if (!CanExecute(null))
            {
                return;
            }

            isRunning = true;
            RaiseCanExecuteChanged();

            try
            {
                await execute();
            }
            finally
            {
                isRunning = false;
                RaiseCanExecuteChanged();
            }
        }

        public void RaiseCanExecuteChanged()
        {
            CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
