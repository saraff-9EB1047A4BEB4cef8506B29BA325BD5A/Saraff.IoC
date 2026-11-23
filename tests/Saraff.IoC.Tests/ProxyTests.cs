using System;
using System.Reflection;
using Xunit;

namespace Saraff.IoC.Tests {

    public class ProxyTests {

        private static T Resolve<T>(ServiceContainer container) => (T)((IServiceProvider)container).GetService(typeof(T));

        [Fact]
        public void InterceptionCallbacksAreInvoked() {
            using(var container = new ServiceContainer()) {
                var listener = new RecordingListener();
                container.Bind<IListener>(listener);
                container.Bind<IProxyService, ProxyService>();

                var proxy = Resolve<IProxyService>(container);
                var result = proxy.Execute("a");

                Assert.Equal("impl:a", result);
                Assert.Equal(1, listener.InvokingCount);
                Assert.Equal(1, listener.InvokedCount);
            }
        }

        [Fact]
        public void InvokingCallbackCanShortCircuit() {
            using(var container = new ServiceContainer()) {
                var listener = new RecordingListener { InvokingHandler = (method, instance, parameters) => InvocationResult.FromResult("short") };
                container.Bind<IListener>(listener);
                container.Bind<IProxyService, ProxyService>();

                var proxy = Resolve<IProxyService>(container);

                Assert.Equal("short", proxy.Execute("a"));
            }
        }

        [Fact]
        public void InvokingCallbackCanShortCircuitWithNullResult() {
            using(var container = new ServiceContainer()) {
                var service = new ProxyService();
                var listener = new RecordingListener { InvokingHandler = (method, instance, parameters) => InvocationResult.FromResult(null) };
                container.Bind<IListener>(listener);
                container.Bind<IProxyService>(service);

                var proxy = Resolve<IProxyService>(container);

                Assert.Null(proxy.Execute("a"));
                Assert.Equal(0, service.ExecuteCount);
            }
        }

        [Fact]
        public void InvokingCallbackCanShortCircuitVoidMethod() {
            using(var container = new ServiceContainer()) {
                var service = new ProxyService();
                var listener = new RecordingListener { InvokingHandler = (method, instance, parameters) => InvocationResult.FromResult(null) };
                container.Bind<IListener>(listener);
                container.Bind<IProxyService>(service);

                var proxy = Resolve<IProxyService>(container);
                proxy.Touch();

                Assert.False(service.Touched);
            }
        }

        [Fact]
        public void InvokedCallbackCanOverrideResult() {
            using(var container = new ServiceContainer()) {
                var listener = new RecordingListener { InvokedHandler = (method, instance, result) => InvocationResult.FromResult("post") };
                container.Bind<IListener>(listener);
                container.Bind<IProxyService, ProxyService>();

                var proxy = Resolve<IProxyService>(container);

                Assert.Equal("post", proxy.Execute("a"));
            }
        }

        [Fact]
        public void InvokedCallbackCanOverrideResultWithNull() {
            using(var container = new ServiceContainer()) {
                var service = new ProxyService();
                var listener = new RecordingListener { InvokedHandler = (method, instance, result) => InvocationResult.FromResult(null) };
                container.Bind<IListener>(listener);
                container.Bind<IProxyService>(service);

                var proxy = Resolve<IProxyService>(container);

                Assert.Null(proxy.Execute("a"));
                Assert.Equal(1, service.ExecuteCount);
            }
        }

        [Fact]
        public void OutAndRefParametersArePropagated() {
            using(var container = new ServiceContainer()) {
                container.Bind<IProxyService, ProxyService>();

                var proxy = Resolve<IProxyService>(container);
                string text = "a";
                proxy.Assign(out var value, ref text);

                Assert.Equal(42, value);
                Assert.Equal("a!", text);
            }
        }

        [Fact]
        public void GenericMethodsAreProxied() {
            using(var container = new ServiceContainer()) {
                container.Bind<IProxyService, ProxyService>();

                var proxy = Resolve<IProxyService>(container);

                Assert.Equal("value", proxy.Echo("value"));
            }
        }

        [Fact]
        public void ExplicitInterfaceImplementationsAreProxied() {
            using(var container = new ServiceContainer()) {
                container.Bind<IExplicitService, ExplicitService>();

                var proxy = Resolve<IExplicitService>(container);

                Assert.Equal(7, proxy.GetValue());
            }
        }

        [Fact]
        public void CatchCallbackCanMapException() {
            using(var container = new ServiceContainer()) {
                var listener = new RecordingListener { CatchHandler = (method, instance, ex) => InvocationResult.FromResult(new InvalidOperationException("mapped")) };
                container.Bind<IListener>(listener);
                container.Bind<IProxyService, ProxyService>();

                var proxy = Resolve<IProxyService>(container);
                var ex = Assert.Throws<InvalidOperationException>(() => proxy.Fail());

                Assert.Equal("mapped", ex.Message);
            }
        }

        [Fact]
        public void CatchCallbackCanSuppressException() {
            using(var container = new ServiceContainer()) {
                var listener = new RecordingListener { CatchHandler = (method, instance, ex) => InvocationResult.FromResult(null) };
                container.Bind<IListener>(listener);
                container.Bind<IProxyService, ProxyService>();

                var proxy = Resolve<IProxyService>(container);

                Assert.Null(proxy.Fail());
            }
        }

        [Fact]
        public void CatchCallbackCanSuppressExceptionForValueType() {
            using(var container = new ServiceContainer()) {
                var listener = new RecordingListener { CatchHandler = (method, instance, ex) => InvocationResult.FromResult(null) };
                container.Bind<IListener>(listener);
                container.Bind<IProxyService, ProxyService>();

                var proxy = Resolve<IProxyService>(container);

                Assert.Equal(0, proxy.FailValue());
            }
        }

        [Fact]
        public void CatchCallbackNotHandledRethrowsOriginal() {
            using(var container = new ServiceContainer()) {
                var listener = new RecordingListener { CatchHandler = (method, instance, ex) => InvocationResult.NotHandled };
                container.Bind<IListener>(listener);
                container.Bind<IProxyService, ProxyService>();

                var proxy = Resolve<IProxyService>(container);
                var ex = Assert.Throws<TargetInvocationException>(() => proxy.Fail());

                Assert.IsType<InvalidOperationException>(ex.InnerException);
                Assert.Equal("original", ex.InnerException.Message);
            }
        }

        [Fact]
        public void ConfiguredServiceRequiredAttributeInjectsMethodParameter() {
            using(var container = new ServiceContainer()) {
                container.Bind<IConfiguration, CustomConfiguration>();
                container.Bind<IDependency, DependencyService>();
                container.Bind<IProxyService, ProxyService>();

                var proxy = Resolve<IProxyService>(container);
                proxy.InjectDependency(null);

                Assert.NotNull(proxy.Injected);
            }
        }

        [Fact]
        public void ConfiguredServiceRequiredAttributeInjectsConstructor() {
            using(var container = new ServiceContainer()) {
                container.Bind<IConfiguration, CustomConfiguration>();
                container.Bind<IGreeting, GreetingService>();

                var consumer = container.CreateInstance<CustomConsumer>();

                Assert.NotNull(consumer.Greeting);
            }
        }

        [Fact]
        public void ProxyIsCachedAsSingleton() {
            using(var container = new ServiceContainer()) {
                container.Bind<IProxyService, ProxyService>();

                Assert.Same(Resolve<IProxyService>(container), Resolve<IProxyService>(container));
            }
        }
    }
}
