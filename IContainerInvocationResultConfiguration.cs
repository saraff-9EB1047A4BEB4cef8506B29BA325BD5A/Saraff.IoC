namespace Saraff.IoC {

    /// <summary>
    /// Настройки результата перехваченного вызова. Позволяет подсистеме
    /// использовать собственный тип результата вместо <see cref="InvocationResult"/>.
    /// </summary>
    public interface IContainerInvocationResultConfiguration {

        InvokingResultCallback InvokingResultCallback {
            get;
        }

        InvokedResultCallback InvokedResultCallback {
            get;
        }

        CatchResultCallback CatchResultCallback {
            get;
        }
    }
}
