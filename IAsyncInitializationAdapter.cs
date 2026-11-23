using System;
using System.Threading;
using System.Threading.Tasks;

namespace Saraff.IoC {

    /// <summary>
    /// Определяет, требует ли созданный сервис асинхронной инициализации,
    /// и выполняет её. Позволяет не завязываться на <c>IAsyncInitializable</c>.
    /// </summary>
    public interface IAsyncInitializationAdapter {

        bool CanInitialize(object instance);

        Task InitializeAsync(object instance, CancellationToken cancellationToken);
    }

    /// <summary>
    /// Адаптер по умолчанию: инициализирует сервисы, реализующие <c>IAsyncInitializable</c>.
    /// </summary>
    public sealed class AsyncInitializableAdapter : IAsyncInitializationAdapter {

        public static readonly AsyncInitializableAdapter Instance = new AsyncInitializableAdapter();

        private AsyncInitializableAdapter() {
        }

        public bool CanInitialize(object instance) {
#if NETSTANDARD
            return instance is IAsyncInitializable;
#else
            return false;
#endif
        }

        public Task InitializeAsync(object instance, CancellationToken cancellationToken) {
#if NETSTANDARD
            return ((IAsyncInitializable)instance).InitializeAsync(cancellationToken);
#else
            var _task = new TaskCompletionSource<object>();
            _task.SetResult(null);
            return _task.Task;
#endif
        }
    }
}
