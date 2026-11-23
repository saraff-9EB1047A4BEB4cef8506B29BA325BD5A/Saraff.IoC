using System;
using System.Reflection;

namespace Saraff.IoC {

    /// <summary>
    /// Результат перехваченного вызова. Позволяет использовать собственный тип
    /// результата, не завязываясь на <see cref="InvocationResult"/>.
    /// </summary>
    public interface IInvocationResult {

        bool Handled {
            get;
        }

        object Value {
            get;
        }
    }

    public delegate IInvocationResult InvokingResultCallback(object listener, MethodBase method, object instance, object[] parameters);

    public delegate IInvocationResult InvokedResultCallback(object listener, MethodBase method, object instance, object result);

    public delegate IInvocationResult CatchResultCallback(object listener, MethodBase method, object instance, Exception ex);
}
