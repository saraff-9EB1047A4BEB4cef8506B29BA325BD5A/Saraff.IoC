using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Saraff.IoC {

    /// <summary>
    /// Нейтральный API ядра контейнера. Не зависит от legacy-типов
    /// <c>IServiceContainer</c>, <c>IServiceRegistry</c>, <c>ServiceLifetime</c>,
    /// <c>ServiceRegistration</c>, <c>InvocationResult</c>, <c>IAsyncInitializable</c>.
    /// </summary>
    public interface IContainerCore : IDisposable
#if NETSTANDARD
        , IAsyncDisposable
#endif
    {

        IContainerCore Parent {
            get;
        }

        IContainerCore CreateNested();

        object Resolve(Type serviceType);

        object ResolveNamed(Type serviceType, string name);

        bool IsRegistered(Type serviceType);

        object CreateInstance(Type type, bool trackComponents, IDictionary<string, object> args);

        void Register(Type serviceType, Type implementationType, IServiceLifetime lifetime, string name);

        void Register(Type serviceType, Func<IServiceProvider, object> factory, IServiceLifetime lifetime, string name);

        void RegisterInstance(Type serviceType, object instance, string name);

#if NETSTANDARD

        Task<object> ResolveAsync(Type serviceType, CancellationToken cancellationToken);

        Task InitializeAsync(CancellationToken cancellationToken);

#endif
    }
}
