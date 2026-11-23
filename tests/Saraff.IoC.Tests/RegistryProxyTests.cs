using System;
using Xunit;

namespace Saraff.IoC.Tests {

    public class RegistryProxyTests {

        [Fact]
        public void RegistryServiceIsProxied() {
            using(var container = new ServiceContainer()) {
                var listener = new RecordingListener();
                container.Bind<IListener>(listener);
                container.Register<IProxyService, ProxyService>();

                var proxy = container.Resolve<IProxyService>();

                Assert.Equal("impl:a", proxy.Execute("a"));
                Assert.Equal(1, listener.InvokingCount);
                Assert.Equal(1, listener.InvokedCount);
            }
        }

        [Fact]
        public void ContextBinderServiceIsProxied() {
            using(var container = new ServiceContainer()) {
                var listener = new RecordingListener();
                container.Bind<IListener>(listener);
                container.Register(typeof(IContextBinder<IContextService, ContextConsumer>), typeof(ContextProxyService));

                var consumer = container.CreateInstance<ContextConsumer>();

                Assert.Equal("special", consumer.Service.Name);
                Assert.True(listener.InvokingCount > 0);
            }
        }

        [Fact]
        public void InvalidCatchCallbackResultThrows() {
            using(var container = new ServiceContainer()) {
                var listener = new RecordingListener {
                    CatchHandler = (method, instance, ex) => InvocationResult.FromResult("not an exception")
                };
                container.Bind<IListener>(listener);
                container.Register<IProxyService, ProxyService>();

                var proxy = container.Resolve<IProxyService>();
                var exception = Assert.Throws<InvalidOperationException>(() => { proxy.Fail(); });

                Assert.Contains("CatchCallback", exception.Message);
            }
        }
    }
}
