using System;

namespace Saraff.IoC {

    /// <summary>
    /// Политика времени жизни сервиса.
    /// </summary>
    public interface IServiceLifetime {

        object Resolve(IServiceLifetimeContext context);
    }

    /// <summary>
    /// Контекст разрешения, доступный политике времени жизни.
    /// </summary>
    public interface IServiceLifetimeContext {

        Type ServiceType {
            get;
        }

        string Name {
            get;
        }

        IContainerCore Owner {
            get;
        }

        IContainerCore Scope {
            get;
        }

        object GetOrAddSingleton();

        object GetOrAddScoped();

        object Create();
    }

    /// <summary>
    /// Сопоставляет legacy-значение <see cref="ServiceLifetime"/> политике.
    /// </summary>
    public interface IServiceLifetimeFactory {

        IServiceLifetime Create(ServiceLifetime lifetime);
    }

    /// <summary>
    /// Стандартные политики времени жизни.
    /// </summary>
    public static class ServiceLifetimes {

        public static IServiceLifetime Singleton { get; } = new SingletonServiceLifetime();

        public static IServiceLifetime Scoped { get; } = new ScopedServiceLifetime();

        public static IServiceLifetime Transient { get; } = new TransientServiceLifetime();

        private sealed class SingletonServiceLifetime : IServiceLifetime {

            public object Resolve(IServiceLifetimeContext context) => context.GetOrAddSingleton();
        }

        private sealed class ScopedServiceLifetime : IServiceLifetime {

            public object Resolve(IServiceLifetimeContext context) => context.GetOrAddScoped();
        }

        private sealed class TransientServiceLifetime : IServiceLifetime {

            public object Resolve(IServiceLifetimeContext context) => context.Create();
        }
    }

    /// <summary>
    /// Фабрика политик по умолчанию.
    /// </summary>
    public sealed class DefaultServiceLifetimeFactory : IServiceLifetimeFactory {

        public static readonly DefaultServiceLifetimeFactory Instance = new DefaultServiceLifetimeFactory();

        private DefaultServiceLifetimeFactory() {
        }

        public IServiceLifetime Create(ServiceLifetime lifetime) {
            switch(lifetime) {
                case ServiceLifetime.Scoped:
                    return ServiceLifetimes.Scoped;
                case ServiceLifetime.Transient:
                    return ServiceLifetimes.Transient;
                default:
                    return ServiceLifetimes.Singleton;
            }
        }
    }
}
