using System;

namespace Saraff.IoC {

    /// <summary>
    /// Описание регистрации сервиса.
    /// </summary>
    internal sealed class ServiceRegistration : IServiceRegistration {

        internal ServiceRegistration(Type serviceType, Type implementationType, object instance, Func<IServiceProvider, object> factory, IServiceLifetime lifetime, string name) {
            this.ServiceType = serviceType;
            this.ImplementationType = implementationType;
            this.Instance = instance;
            this.Factory = factory;
            this.Lifetime = lifetime;
            this.Name = name;
        }

        public Type ServiceType { get; private set; }

        public Type ImplementationType { get; private set; }

        public object Instance { get; private set; }

        public Func<IServiceProvider, object> Factory { get; private set; }

        public IServiceLifetime Lifetime { get; private set; }

        public string Name { get; private set; }

        public Type GetImplementationType(Type requestedServiceType) {
            if(this.ImplementationType == null) {
                return null;
            }
            if(!this.ImplementationType.IsGenericTypeDefinition) {
                return this.ImplementationType;
            }
            return this.ImplementationType.MakeGenericType(requestedServiceType.GetGenericArguments());
        }
    }

    internal sealed class DefaultServiceRegistrationFactory : IServiceRegistrationFactory {

        public static readonly DefaultServiceRegistrationFactory Instance = new DefaultServiceRegistrationFactory();

        private DefaultServiceRegistrationFactory() {
        }

        public IServiceRegistration Create(Type serviceType, Type implementationType, object instance, Func<IServiceProvider, object> factory, IServiceLifetime lifetime, string name) {
            return new ServiceRegistration(serviceType, implementationType, instance, factory, lifetime, name);
        }
    }
}
