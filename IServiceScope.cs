using System;
using System.Threading;
using System.Threading.Tasks;

namespace Saraff.IoC {

    /// <summary>
    /// Область (scope) разрешения сервисов.
    /// </summary>
    public interface IServiceScope : IDisposable
#if NETSTANDARD
        , IAsyncDisposable
#endif
    {

        /// <summary>
        /// Провайдер сервисов области.
        /// </summary>
        IServiceProvider ServiceProvider { get; }

        /// <summary>
        /// Разрешает сервис в области.
        /// </summary>
        object Resolve(Type serviceType);

        /// <summary>
        /// Разрешает сервис в области.
        /// </summary>
        T Resolve<T>();

        /// <summary>
        /// Разрешает именованную регистрацию сервиса в области.
        /// </summary>
        object ResolveNamed(Type serviceType, string name);

        /// <summary>
        /// Разрешает именованную регистрацию сервиса в области.
        /// </summary>
        T ResolveNamed<T>(string name);

#if NETSTANDARD

        /// <summary>
        /// Разрешает сервис и дожидается асинхронной инициализации созданных сервисов.
        /// </summary>
        Task<object> ResolveAsync(Type serviceType, CancellationToken cancellationToken = default(CancellationToken));

        /// <summary>
        /// Разрешает сервис и дожидается асинхронной инициализации созданных сервисов.
        /// </summary>
        Task<T> ResolveAsync<T>(CancellationToken cancellationToken = default(CancellationToken));

        /// <summary>
        /// Инициализирует все созданные, но ещё не инициализированные сервисы.
        /// </summary>
        Task InitializeAsync(CancellationToken cancellationToken = default(CancellationToken));

#endif
    }
}
