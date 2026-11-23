using System;
using System.Collections.Generic;
using System.Reflection;

namespace Saraff.IoC.Tests {

    public enum MyLifetime {
        Singleton,
        Scoped,
        Transient
    }

    public interface IMyContainer {
        object Resolve(Type serviceType);

        T Resolve<T>();
    }

    public interface IMyRegistry {
        void Register(Type serviceType, Type implementationType, MyLifetime lifetime, string name = null);
    }

    public sealed class MyContainer : IMyContainer, IMyRegistry {
        private readonly IContainerCore _core;

        public MyContainer(IContainerCore core) {
            this._core = core;
        }

        public object Resolve(Type serviceType) => this._core.Resolve(serviceType);

        public T Resolve<T>() => (T)this._core.Resolve(typeof(T));

        public void Register(Type serviceType, Type implementationType, MyLifetime lifetime, string name = null) {
            this._core.Register(serviceType, implementationType, MyLifetimes.ToPolicy(lifetime), name);
        }
    }

    internal static class MyLifetimes {

        public static IServiceLifetime ToPolicy(MyLifetime lifetime) {
            switch(lifetime) {
                case MyLifetime.Scoped:
                    return ServiceLifetimes.Scoped;
                case MyLifetime.Transient:
                    return ServiceLifetimes.Transient;
                default:
                    return ServiceLifetimes.Singleton;
            }
        }
    }

    public sealed class CountingLifetime : IServiceLifetime {
        private readonly object _sync = new object();
        private object _instance;

        public int Created { get; private set; }

        public object Resolve(IServiceLifetimeContext context) {
            lock(this._sync) {
                if(this._instance == null) {
                    this._instance = context.Create();
                    this.Created++;
                }
                return this._instance;
            }
        }
    }

    public sealed class RecordingRegistrationFactory : IServiceRegistrationFactory {
        public int Created { get; private set; }

        public IServiceRegistration Create(Type serviceType, Type implementationType, object instance, Func<IServiceProvider, object> factory, IServiceLifetime lifetime, string name) {
            this.Created++;
            return new MyServiceRegistration(serviceType, implementationType, instance, factory, lifetime, name);
        }
    }

    internal sealed class MyServiceRegistration : IServiceRegistration {
        public MyServiceRegistration(Type serviceType, Type implementationType, object instance, Func<IServiceProvider, object> factory, IServiceLifetime lifetime, string name) {
            this.ServiceType = serviceType;
            this.ImplementationType = implementationType;
            this.Instance = instance;
            this.Factory = factory;
            this.Lifetime = lifetime;
            this.Name = name;
        }

        public Type ServiceType { get; }

        public Type ImplementationType { get; }

        public object Instance { get; }

        public Func<IServiceProvider, object> Factory { get; }

        public IServiceLifetime Lifetime { get; }

        public string Name { get; }

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

    public sealed class MyInvocationResult : IInvocationResult {        public MyInvocationResult(bool handled, object value) {
            this.Handled = handled;
            this.Value = value;
        }

        public bool Handled { get; }

        public object Value { get; }
    }

    public sealed class RecordingLifetimeFactory : IServiceLifetimeFactory {
        public int Created { get; private set; }

        public IServiceLifetime Create(ServiceLifetime lifetime) {
            this.Created++;
            return ServiceLifetimes.Singleton;
        }
    }

    public sealed class LegacyCallbackConfiguration : IConfiguration {
        public Type BindServiceAttributeType => typeof(BindServiceAttribute);

        public BindServiceCallback BindServiceCallback => null;

        public Type ServiceRequiredAttributeType => typeof(ServiceRequiredAttribute);

        public Type ContextBinderType => typeof(IContextBinder<,>);

        public Type ProxyRequiredAttributeType => typeof(ProxyRequiredAttribute);

        public Type ListenerType => typeof(IListener);

        public InvokingCallback InvokingCallback => (listener, method, instance, parameters) => new InvocationResult(true, 321);

        public InvokedCallback InvokedCallback => null;

        public CatchCallback CatchCallback => null;

        public Type LazyCallbackType => typeof(Lazy<>);
    }

    internal static class IsolationAssertions {
        private static readonly HashSet<string> _forbidden = new HashSet<string> {
            "Saraff.IoC.IServiceContainer",
            "Saraff.IoC.IServiceRegistry",
            "Saraff.IoC.ServiceLifetime",
            "Saraff.IoC.ServiceRegistration",
            "Saraff.IoC.InvocationResult",
            "Saraff.IoC.IAsyncInitializable"
        };

        public static void AssertNoLegacyApi(Type type) {
            foreach(var _method in type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)) {
                AssertAllowed(_method.ReturnType);
                foreach(var _parameter in _method.GetParameters()) {
                    AssertAllowed(_parameter.ParameterType);
                }
            }
            foreach(var _property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)) {
                AssertAllowed(_property.PropertyType);
            }
            foreach(var _ctor in type.GetConstructors(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)) {
                foreach(var _parameter in _ctor.GetParameters()) {
                    AssertAllowed(_parameter.ParameterType);
                }
            }
        }

        private static void AssertAllowed(Type type) {
            if(type == null) {
                return;
            }
            while(type.IsByRef || type.IsArray) {
                type = type.GetElementType();
            }
            if(type.IsGenericParameter) {
                return;
            }
            if(type.FullName != null) {
                Xunit.Assert.DoesNotContain(type.FullName, _forbidden);
            }
            if(type.IsGenericType) {
                foreach(var _argument in type.GetGenericArguments()) {
                    AssertAllowed(_argument);
                }
            }
        }
    }
}
