using System;

namespace Saraff.IoC {

    /// <summary>
    /// Конфигурация контейнера по умолчанию. Любое свойство можно переопределить.
    /// </summary>
    public sealed class ServiceContainerConfiguration :
        IConfiguration,
        IContainerInvocationResultConfiguration,
        IContainerLifetimeConfiguration,
        IContainerRegistrationConfiguration,
        IContainerAsyncInitializationConfiguration {

        public Type BindServiceAttributeType { get; set; } = typeof(BindServiceAttribute);

        public BindServiceCallback BindServiceCallback { get; set; } = DefaultContainerBindingConfiguration.Instance.BindServiceCallback;

        public Type ServiceRequiredAttributeType { get; set; } = typeof(ServiceRequiredAttribute);

        public Type ContextBinderType { get; set; } = typeof(IContextBinder<,>);

        public Type ProxyRequiredAttributeType { get; set; } = typeof(ProxyRequiredAttribute);

        public Type ListenerType { get; set; } = typeof(IListener);

        public InvokingCallback InvokingCallback { get; set; } = DefaultContainerInterceptionConfiguration.Instance.InvokingCallback;

        public InvokedCallback InvokedCallback { get; set; } = DefaultContainerInterceptionConfiguration.Instance.InvokedCallback;

        public CatchCallback CatchCallback { get; set; } = DefaultContainerInterceptionConfiguration.Instance.CatchCallback;

        public Type LazyCallbackType { get; set; } = typeof(Lazy<>);

        public InvokingResultCallback InvokingResultCallback { get; set; }

        public InvokedResultCallback InvokedResultCallback { get; set; }

        public CatchResultCallback CatchResultCallback { get; set; }

        public IServiceLifetimeFactory ServiceLifetimeFactory { get; set; } = DefaultServiceLifetimeFactory.Instance;

        public IServiceRegistrationFactory ServiceRegistrationFactory { get; set; } = DefaultServiceRegistrationFactory.Instance;

        public IAsyncInitializationAdapter AsyncInitializationAdapter { get; set; } = AsyncInitializableAdapter.Instance;
    }
}
