using System;
using System.Reflection;

namespace Saraff.IoC {

    /// <summary>
    /// Неизменяемый снимок функциональных групп конфигурации.
    /// </summary>
    internal sealed class ContainerSettings {

        public static readonly ContainerSettings Default = Create(null);

        public ContainerSettings(
            Type bindServiceAttributeType,
            BindServiceCallback bindServiceCallback,
            Type serviceRequiredAttributeType,
            Type contextBinderType,
            Type lazyCallbackType,
            Type proxyRequiredAttributeType,
            Type listenerType,
            InvokingCallback invokingCallback,
            InvokedCallback invokedCallback,
            CatchCallback catchCallback,
            InvokingResultCallback invokingResultCallback,
            InvokedResultCallback invokedResultCallback,
            CatchResultCallback catchResultCallback,
            IServiceLifetimeFactory serviceLifetimeFactory,
            IServiceRegistrationFactory serviceRegistrationFactory,
            IAsyncInitializationAdapter asyncInitializationAdapter) {
            this.BindServiceAttributeType = bindServiceAttributeType;
            this.BindServiceCallback = bindServiceCallback;
            this.ServiceRequiredAttributeType = serviceRequiredAttributeType;
            this.ContextBinderType = contextBinderType;
            this.LazyCallbackType = lazyCallbackType;
            this.ProxyRequiredAttributeType = proxyRequiredAttributeType;
            this.ListenerType = listenerType;
            this.InvokingCallback = invokingCallback;
            this.InvokedCallback = invokedCallback;
            this.CatchCallback = catchCallback;
            this.InvokingResultCallback = invokingResultCallback;
            this.InvokedResultCallback = invokedResultCallback;
            this.CatchResultCallback = catchResultCallback;
            this.ServiceLifetimeFactory = serviceLifetimeFactory;
            this.ServiceRegistrationFactory = serviceRegistrationFactory;
            this.AsyncInitializationAdapter = asyncInitializationAdapter;
        }

        public Type BindServiceAttributeType { get; }

        public BindServiceCallback BindServiceCallback { get; }

        public Type ServiceRequiredAttributeType { get; }

        public Type ContextBinderType { get; }

        public Type LazyCallbackType { get; }

        public Type ProxyRequiredAttributeType { get; }

        public Type ListenerType { get; }

        public InvokingCallback InvokingCallback { get; }

        public InvokedCallback InvokedCallback { get; }

        public CatchCallback CatchCallback { get; }

        public InvokingResultCallback InvokingResultCallback { get; }

        public InvokedResultCallback InvokedResultCallback { get; }

        public CatchResultCallback CatchResultCallback { get; }

        public IServiceLifetimeFactory ServiceLifetimeFactory { get; }

        public IServiceRegistrationFactory ServiceRegistrationFactory { get; }

        public IAsyncInitializationAdapter AsyncInitializationAdapter { get; }

        public static ContainerSettings Create(IConfiguration configuration) {
            var _binding = configuration as IContainerBindingConfiguration ?? DefaultContainerBindingConfiguration.Instance;
            var _injection = configuration as IContainerInjectionConfiguration ?? DefaultContainerInjectionConfiguration.Instance;
            var _interception = configuration as IContainerInterceptionConfiguration ?? DefaultContainerInterceptionConfiguration.Instance;
            var _invocation = configuration as IContainerInvocationResultConfiguration;
            var _lifetime = configuration as IContainerLifetimeConfiguration ?? DefaultContainerLifetimeConfiguration.Instance;
            var _registration = configuration as IContainerRegistrationConfiguration ?? DefaultContainerRegistrationConfiguration.Instance;
            var _asyncInitialization = configuration as IContainerAsyncInitializationConfiguration ?? DefaultContainerAsyncInitializationConfiguration.Instance;
            return new ContainerSettings(
                _binding.BindServiceAttributeType ?? DefaultContainerBindingConfiguration.Instance.BindServiceAttributeType,
                _binding.BindServiceCallback ?? DefaultContainerBindingConfiguration.Instance.BindServiceCallback,
                _injection.ServiceRequiredAttributeType ?? DefaultContainerInjectionConfiguration.Instance.ServiceRequiredAttributeType,
                _injection.ContextBinderType ?? DefaultContainerInjectionConfiguration.Instance.ContextBinderType,
                _injection.LazyCallbackType ?? DefaultContainerInjectionConfiguration.Instance.LazyCallbackType,
                _interception.ProxyRequiredAttributeType ?? DefaultContainerInterceptionConfiguration.Instance.ProxyRequiredAttributeType,
                _interception.ListenerType ?? DefaultContainerInterceptionConfiguration.Instance.ListenerType,
                _interception.InvokingCallback,
                _interception.InvokedCallback,
                _interception.CatchCallback,
                _invocation?.InvokingResultCallback,
                _invocation?.InvokedResultCallback,
                _invocation?.CatchResultCallback,
                _lifetime.ServiceLifetimeFactory ?? DefaultContainerLifetimeConfiguration.Instance.ServiceLifetimeFactory,
                _registration.ServiceRegistrationFactory ?? DefaultContainerRegistrationConfiguration.Instance.ServiceRegistrationFactory,
                _asyncInitialization.AsyncInitializationAdapter ?? DefaultContainerAsyncInitializationConfiguration.Instance.AsyncInitializationAdapter);
        }
    }

    internal sealed class DefaultContainerBindingConfiguration : IContainerBindingConfiguration {

        public static readonly DefaultContainerBindingConfiguration Instance = new DefaultContainerBindingConfiguration();

        private static readonly BindServiceCallback _callback = (attribute, callback) => {
            if(attribute is BindServiceAttribute _attribute) {
                callback(_attribute.Service, _attribute.ObjectType);
            }
        };

        private DefaultContainerBindingConfiguration() {
        }

        public Type BindServiceAttributeType => typeof(BindServiceAttribute);

        public BindServiceCallback BindServiceCallback => _callback;
    }

    internal sealed class DefaultContainerInjectionConfiguration : IContainerInjectionConfiguration {

        public static readonly DefaultContainerInjectionConfiguration Instance = new DefaultContainerInjectionConfiguration();

        private DefaultContainerInjectionConfiguration() {
        }

        public Type ServiceRequiredAttributeType => typeof(ServiceRequiredAttribute);

        public Type ContextBinderType => typeof(IContextBinder<,>);

        public Type LazyCallbackType => typeof(Lazy<>);
    }

    internal sealed class DefaultContainerInterceptionConfiguration : IContainerInterceptionConfiguration {

        public static readonly DefaultContainerInterceptionConfiguration Instance = new DefaultContainerInterceptionConfiguration();

        private static readonly InvokingCallback _invoking = (listener, method, instance, parameters) => (listener as IListener)?.OnInvoking(method, instance, parameters) ?? InvocationResult.NotHandled;

        private static readonly InvokedCallback _invoked = (listener, method, instance, result) => (listener as IListener)?.OnInvoked(method, instance, result) ?? InvocationResult.NotHandled;

        private static readonly CatchCallback _catch = (listener, method, instance, ex) => (listener as IListener)?.OnCatch(method, instance, ex) ?? InvocationResult.NotHandled;

        private DefaultContainerInterceptionConfiguration() {
        }

        public Type ProxyRequiredAttributeType => typeof(ProxyRequiredAttribute);

        public Type ListenerType => typeof(IListener);

        public InvokingCallback InvokingCallback => _invoking;

        public InvokedCallback InvokedCallback => _invoked;

        public CatchCallback CatchCallback => _catch;
    }

    internal sealed class DefaultContainerInvocationResultConfiguration : IContainerInvocationResultConfiguration {

        public static readonly DefaultContainerInvocationResultConfiguration Instance = new DefaultContainerInvocationResultConfiguration();

        private static readonly InvokingResultCallback _invoking = (listener, method, instance, parameters) => (listener as IListener)?.OnInvoking(method, instance, parameters) ?? InvocationResult.NotHandled;

        private static readonly InvokedResultCallback _invoked = (listener, method, instance, result) => (listener as IListener)?.OnInvoked(method, instance, result) ?? InvocationResult.NotHandled;

        private static readonly CatchResultCallback _catch = (listener, method, instance, ex) => (listener as IListener)?.OnCatch(method, instance, ex) ?? InvocationResult.NotHandled;

        private DefaultContainerInvocationResultConfiguration() {
        }

        public InvokingResultCallback InvokingResultCallback => _invoking;

        public InvokedResultCallback InvokedResultCallback => _invoked;

        public CatchResultCallback CatchResultCallback => _catch;
    }

    internal sealed class DefaultContainerLifetimeConfiguration : IContainerLifetimeConfiguration {

        public static readonly DefaultContainerLifetimeConfiguration Instance = new DefaultContainerLifetimeConfiguration();

        private DefaultContainerLifetimeConfiguration() {
        }

        public IServiceLifetimeFactory ServiceLifetimeFactory => DefaultServiceLifetimeFactory.Instance;
    }

    internal sealed class DefaultContainerRegistrationConfiguration : IContainerRegistrationConfiguration {

        public static readonly DefaultContainerRegistrationConfiguration Instance = new DefaultContainerRegistrationConfiguration();

        private DefaultContainerRegistrationConfiguration() {
        }

        public IServiceRegistrationFactory ServiceRegistrationFactory => DefaultServiceRegistrationFactory.Instance;
    }

    internal sealed class DefaultContainerAsyncInitializationConfiguration : IContainerAsyncInitializationConfiguration {

        public static readonly DefaultContainerAsyncInitializationConfiguration Instance = new DefaultContainerAsyncInitializationConfiguration();

        private DefaultContainerAsyncInitializationConfiguration() {
        }

        public IAsyncInitializationAdapter AsyncInitializationAdapter => AsyncInitializableAdapter.Instance;
    }
}
