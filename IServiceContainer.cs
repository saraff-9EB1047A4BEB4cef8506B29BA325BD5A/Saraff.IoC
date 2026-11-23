using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Saraff.IoC {

    /// <summary>
    /// Контейнер сервисов.
    /// </summary>
    public interface IServiceContainer : IServiceProvider, IDisposable
#if NETSTANDARD
        , IAsyncDisposable
#endif
    {

        /// <summary>
        /// Создаёт область (scope) с собственными scoped-экземплярами.
        /// </summary>
        IServiceScope CreateScope();

        /// <summary>
        /// Разрешает сервис или возвращает <c>null</c>, если сервис не зарегистрирован.
        /// </summary>
        object Resolve(Type serviceType);

        /// <summary>
        /// Разрешает сервис или возвращает <c>null</c>, если сервис не зарегистрирован.
        /// </summary>
        T Resolve<T>();

        /// <summary>
        /// Разрешает все регистрации указанного сервиса.
        /// </summary>
        IEnumerable<T> ResolveAll<T>();

        /// <summary>
        /// Разрешает именованную регистрацию сервиса.
        /// </summary>
        object ResolveNamed(Type serviceType, string name);

        /// <summary>
        /// Разрешает именованную регистрацию сервиса.
        /// </summary>
        T ResolveNamed<T>(string name);

        /// <summary>
        /// Проверяет наличие регистрации сервиса.
        /// </summary>
        bool IsRegistered(Type serviceType);

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
