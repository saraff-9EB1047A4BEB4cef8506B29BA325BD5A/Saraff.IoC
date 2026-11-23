using System;

namespace Saraff.IoC {

    /// <summary>
    /// Описание регистрации сервиса. Ядро зависит от этого интерфейса,
    /// а не от конкретного класса <c>ServiceRegistration</c>.
    /// </summary>
    public interface IServiceRegistration {

        Type ServiceType {
            get;
        }

        Type ImplementationType {
            get;
        }

        object Instance {
            get;
        }

        Func<IServiceProvider, object> Factory {
            get;
        }

        IServiceLifetime Lifetime {
            get;
        }

        string Name {
            get;
        }

        Type GetImplementationType(Type requestedServiceType);
    }

    /// <summary>
    /// Создаёт описания регистраций. Позволяет подставить собственный descriptor.
    /// </summary>
    public interface IServiceRegistrationFactory {

        IServiceRegistration Create(Type serviceType, Type implementationType, object instance, Func<IServiceProvider, object> factory, IServiceLifetime lifetime, string name);
    }
}
