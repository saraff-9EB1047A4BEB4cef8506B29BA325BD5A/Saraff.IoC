using System;
using System.Linq;
using Xunit;

namespace Saraff.IoC.Tests {

    public class LifetimeTests {

        private static T Resolve<T>(ServiceContainer container) => (T)((IServiceProvider)container).GetService(typeof(T));

        [Fact]
        public void CreatedComponentIsDisposedWithContainer() {
            var container = new ServiceContainer();
            container.Bind<IGreeting, GreetingService>();
            var service = (GreetingService)Resolve<IGreeting>(container);

            container.Dispose();

            Assert.True(service.DisposedFlag);
        }

        [Fact]
        public void BoundInstanceIsNotDisposedWithContainer() {
            var container = new ServiceContainer();
            var instance = new GreetingService();
            container.Bind<IGreeting>(instance);

            Assert.Same(instance, Resolve<IGreeting>(container));

            container.Dispose();

            Assert.False(instance.DisposedFlag);
        }

        [Fact]
        public void DisposeIsIdempotent() {
            var container = new ServiceContainer();
            container.Bind<IGreeting, GreetingService>();

            container.Dispose();

            var exception = Record.Exception(() => container.Dispose());

            Assert.Null(exception);
        }

        [Fact]
        public void ServiceProviderThrowsAfterDispose() {
            var container = new ServiceContainer();
            container.Bind<IGreeting, GreetingService>();
            container.Dispose();

            Assert.Throws<ObjectDisposedException>(() => ((IServiceProvider)container).GetService(typeof(IGreeting)));
        }

        [Fact]
        public void CreateInstanceTracksComponentByDefault() {
            var container = new ServiceContainer();
            var created = container.CreateInstance<GreetingService>();

            Assert.Contains(created, container.Components.Cast<object>());

            container.Dispose();

            Assert.True(created.DisposedFlag);
        }

        [Fact]
        public void CreateInstanceCanSkipComponentTracking() {
            var container = new ServiceContainer();
            var created = container.CreateInstance<GreetingService>(false);

            Assert.DoesNotContain(created, container.Components.Cast<object>());

            container.Dispose();

            Assert.False(created.DisposedFlag);
        }

        [Fact]
        public void NestedContainerIsDisposedWithParent() {
            var parent = new ServiceContainer();
            var nested = parent.CreateNestedContainer();
            nested.Bind<IDependency, DependencyService>();
            var service = (DependencyService)Resolve<IDependency>(nested);

            parent.Dispose();

            Assert.True(service.DisposedFlag);
        }
    }
}
