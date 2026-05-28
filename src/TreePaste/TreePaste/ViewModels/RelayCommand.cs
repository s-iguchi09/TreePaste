using System.Windows.Input;

namespace TreePaste.ViewModels;

/// <summary>
/// アクションとガードをデリゲートで受け取る汎用の <see cref="System.Windows.Input.ICommand"/> 実装。
/// General-purpose <see cref="System.Windows.Input.ICommand"/> implementation that accepts action and guard delegates.
/// </summary>
public class RelayCommand : ICommand
{
    private readonly Action<object?> _execute;
    private readonly Func<object?, bool>? _canExecute;

    /// <summary>
    /// コンストラクター。実行アクションと実行可能条件を指定する。
    /// Constructor. Specifies the execute action and optional can-execute condition.
    /// </summary>
    /// <param name="execute">コマンド実行時のアクション。 / Action to execute.</param>
    /// <param name="canExecute">実行可能条件（オプション）。 / Optional can-execute condition.</param>
    public RelayCommand(Action<object?> execute, Func<object?, bool>? canExecute = null)
    {
        _execute = execute;
        _canExecute = canExecute;
    }

    /// <summary>
    /// コンストラクター・オーバーロード。パラメーターなしの Action を受け取る。
    /// Constructor overload. Accepts a parameterless Action.
    /// </summary>
    /// <param name="execute">コマンド実行時のアクション。 / Action to execute.</param>
    /// <param name="canExecute">実行可能条件（オプション）。 / Optional can-execute condition.</param>
    public RelayCommand(Action execute, Func<bool>? canExecute = null)
        : this(_ => execute(), canExecute == null ? null : _ => canExecute())
    {
    }

    /// <summary>
    /// 実行可能状態の変化を通知するイベント。
    /// Event that notifies changes in the can-execute state.
    /// </summary>
    public event EventHandler? CanExecuteChanged
    {
        add => CommandManager.RequerySuggested += value;
        remove => CommandManager.RequerySuggested -= value;
    }

    /// <summary>
    /// コマンドが実行可能かどうかを返す。
    /// Returns whether the command can execute.
    /// </summary>
    public bool CanExecute(object? parameter) => _canExecute?.Invoke(parameter) ?? true;

    /// <summary>
    /// コマンドを実行する。
    /// Executes the command.
    /// </summary>
    public void Execute(object? parameter) => _execute(parameter);
}

/// <summary>
/// 型パラメーター付きの汎用の <see cref="System.Windows.Input.ICommand"/> 実装。
/// Generic <see cref="System.Windows.Input.ICommand"/> implementation with a typed parameter.
/// </summary>
public class RelayCommand<T> : ICommand
{
    private readonly Action<T?> _execute;
    private readonly Func<T?, bool>? _canExecute;

    /// <summary>
    /// コンストラクター。実行アクションと実行可能条件を指定する。
    /// Constructor. Specifies the execute action and optional can-execute condition.
    /// </summary>
    /// <param name="execute">コマンド実行時のアクション。 / Action to execute.</param>
    /// <param name="canExecute">実行可能条件（オプション）。 / Optional can-execute condition.</param>
    public RelayCommand(Action<T?> execute, Func<T?, bool>? canExecute = null)
    {
        _execute = execute;
        _canExecute = canExecute;
    }

    /// <summary>
    /// 実行可能状態の変化を通知するイベント。
    /// Event that notifies changes in the can-execute state.
    /// </summary>
    public event EventHandler? CanExecuteChanged
    {
        add => CommandManager.RequerySuggested += value;
        remove => CommandManager.RequerySuggested -= value;
    }

    /// <summary>
    /// コマンドが実行可能かどうかを返す。
    /// Returns whether the command can execute.
    /// </summary>
    public bool CanExecute(object? parameter) => _canExecute?.Invoke((T?)parameter) ?? true;

    /// <summary>
    /// コマンドを実行する。
    /// Executes the command.
    /// </summary>
    public void Execute(object? parameter) => _execute((T?)parameter);
}

