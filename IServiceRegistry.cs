using System;

namespace Saraff.IoC {

    /// <summary>
    /// Реестр сервисов контейнера.
    /// </summary>
    public interface IServiceRegistry {

        /// <summary>
        /// Регистрирует сервис с указанной реализацией.
        /// </summary>
        void Register(Type serviceType, Type implementationType, ServiceLifetime lifetime = ServiceLifetime.Singleton, string name = null);

        /// <summary>
        /// Регистрирует сервис с указанной реализацией.
        /// </summary>
        void Register<TService, TImplementation>(ServiceLifetime lifetime = ServiceLifetime.Singleton, string name = null) where TImplementation : TService;

        /// <summary>
        /// Регистрирует фабрику сервиса.
        /// </summary>
        void Register(Type serviceType, Func<IServiceProvider, object> factory, ServiceLifetime lifetime = ServiceLifetime.Singleton, string name = null);

        /// <summary>
        /// Регистрирует фабрику сервиса.
        /// </summary>
        void Register<TService>(Func<IServiceProvider, TService> factory, ServiceLifetime lifetime = ServiceLifetime.Singleton, string name = null);

        /// <summary>
        /// Регистрирует готовый экземпляр сервиса. Экземпляр не освобождается контейнером.
        /// </summary>
        void RegisterInstance(Type serviceType, object instance, string name = null);

        /// <summary>
        /// Регистрирует готовый экземпляр сервиса. Экземпляр не освобождается контейнером.
        /// </summary>
        void RegisterInstance<TService>(TService instance, string name = null);
    }
}
