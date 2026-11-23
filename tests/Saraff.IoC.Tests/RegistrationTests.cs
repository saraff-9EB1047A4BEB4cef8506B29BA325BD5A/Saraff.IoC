using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Saraff.IoC.Tests {

    public class RegistrationTests {

        [Fact]
        public void SingletonRegistrationReturnsSameInstance() {
            using(var container = new ServiceContainer()) {
                container.Register<ISingletonService, SingletonService>();

                Assert.Same(container.Resolve<ISingletonService>(), container.Resolve<ISingletonService>());
            }
        }

        [Fact]
        public void TransientRegistrationReturnsNewInstance() {
            using(var container = new ServiceContainer()) {
                container.Register<ITransientService, TransientService>(ServiceLifetime.Transient);

                Assert.NotSame(container.Resolve<ITransientService>(), container.Resolve<ITransientService>());
            }
        }

        [Fact]
        public void ScopedRegistrationIsSharedWithinScope() {
            using(var container = new ServiceContainer()) {
                container.Register<IScopedService, ScopedService>(ServiceLifetime.Scoped);
                container.Register<ISingletonService, SingletonService>();
                using(var first = container.CreateScope())
                using(var second = container.CreateScope()) {
                    var a1 = first.Resolve<IScopedService>();
                    var a2 = first.Resolve<IScopedService>();
                    var b1 = second.Resolve<IScopedService>();

                    Assert.Same(a1, a2);
                    Assert.NotSame(a1, b1);
                    Assert.Same(first.Resolve<ISingletonService>(), second.Resolve<ISingletonService>());
                }
            }
        }

        [Fact]
        public void ScopedDisposableIsDisposedWithScope() {
            var container = new ServiceContainer();
            container.Register<ITransientService, DisposablePocoService>(ServiceLifetime.Scoped);
            var scope = container.CreateScope();
            var service = (DisposablePocoService)scope.Resolve<ITransientService>();

            scope.Dispose();

            Assert.True(service.DisposedFlag);
            container.Dispose();
        }

        [Fact]
        public void SingletonDisposableIsDisposedWithRoot() {
            var container = new ServiceContainer();
            container.Register<ITransientService, DisposablePocoService>();
            var service = (DisposablePocoService)container.Resolve<ITransientService>();

            container.Dispose();

            Assert.True(service.DisposedFlag);
        }

        [Fact]
        public void RegisteredInstanceIsReturnedAndNotDisposed() {
            var container = new ServiceContainer();
            var instance = new DisposablePocoService();
            container.RegisterInstance<ITransientService>(instance);

            Assert.Same(instance, container.Resolve<ITransientService>());

            container.Dispose();

            Assert.False(instance.DisposedFlag);
        }

        [Fact]
        public void FactoryRegistrationIsUsed() {
            var calls = 0;
            using(var container = new ServiceContainer()) {
                container.Register<ISingletonService>(provider => {
                    calls++;
                    return new SingletonService();
                });

                Assert.NotNull(container.Resolve<ISingletonService>());
                Assert.NotNull(container.Resolve<ISingletonService>());
                Assert.Equal(1, calls);
            }
        }

        [Fact]
        public void MultipleRegistrationsAreResolvedAsEnumerable() {
            using(var container = new ServiceContainer()) {
                container.Register<IPlugin, PluginA>(ServiceLifetime.Transient, "a");
                container.Register<IPlugin, PluginB>(ServiceLifetime.Transient, "b");

                var plugins = container.ResolveAll<IPlugin>().ToList();
                var resolved = (IEnumerable<IPlugin>)container.Resolve(typeof(IEnumerable<IPlugin>));

                Assert.Equal(2, plugins.Count);
                Assert.Contains("a", plugins.Select(x => x.Name));
                Assert.Contains("b", plugins.Select(x => x.Name));
                Assert.Equal(2, resolved.Count());
                Assert.Null(container.Resolve<IPlugin>());
                Assert.Equal("b", container.ResolveNamed<IPlugin>("b").Name);
            }
        }

        [Fact]
        public void UnnamedRegistrationWinsOverNamed() {
            using(var container = new ServiceContainer()) {
                container.Register<IPlugin, PluginA>(ServiceLifetime.Transient, "a");
                container.Register<IPlugin, PluginB>(ServiceLifetime.Transient);

                Assert.Equal("b", container.Resolve<IPlugin>().Name);
            }
        }

        [Fact]
        public void OpenGenericRegistrationIsClosedOnResolve() {
            using(var container = new ServiceContainer()) {
                container.Register(typeof(IRepository<>), typeof(Repository<>));

                var repository = container.Resolve<IRepository<Customer>>();

                Assert.IsType<Repository<Customer>>(repository);
                Assert.Equal(typeof(Customer), repository.ItemType);
            }
        }

        [Fact]
        public void PocoRegistrationWorksWithoutIComponent() {
            using(var container = new ServiceContainer()) {
                container.Register<ITransientService, TransientService>(ServiceLifetime.Transient);
                container.Register(typeof(RegisteredConsumer), typeof(RegisteredConsumer), ServiceLifetime.Transient);

                var consumer = container.Resolve<RegisteredConsumer>();

                Assert.NotNull(consumer);
                Assert.NotNull(consumer.Service);
            }
        }

        [Fact]
        public void EnumerableDependencyIsInjected() {
            using(var container = new ServiceContainer()) {
                container.Register<IPlugin, PluginA>(ServiceLifetime.Transient);
                container.Register<IPlugin, PluginB>(ServiceLifetime.Transient);

                var consumer = container.CreateInstance<PluginConsumer>();

                Assert.Equal(2, consumer.Plugins.Count());
            }
        }

        [Fact]
        public void IsRegisteredReflectsRegistrationsAndBindings() {
            using(var container = new ServiceContainer()) {
                Assert.False(container.IsRegistered(typeof(IGreeting)));

                container.Bind<IGreeting, GreetingService>();
                Assert.True(container.IsRegistered(typeof(IGreeting)));

                container.Register<ISingletonService, SingletonService>();
                Assert.True(container.IsRegistered(typeof(ISingletonService)));
            }
        }

        [Fact]
        public void InvalidRegistrationThrows() {
            using(var container = new ServiceContainer()) {
                Assert.Throws<ArgumentException>(() => container.Register(typeof(IGreeting), typeof(SingletonService)));
                Assert.Throws<ArgumentException>(() => container.RegisterInstance(typeof(IGreeting), new SingletonService()));
            }
        }

        [Fact]
        public void RegisteredCycleThrowsCircularDependency() {
            using(var container = new ServiceContainer()) {
                container.Register<ICycleA, CycleA>(ServiceLifetime.Transient);
                container.Register<ICycleB, CycleB>(ServiceLifetime.Transient);

                Assert.Throws<CircularDependencyException>(() => container.Resolve<ICycleA>());
            }
        }

        [Fact]
        public void ContextRegistrationIsUsedForConsumer() {
            using(var container = new ServiceContainer()) {
                container.Register<IContextService, DefaultContextService>();
                container.Register(typeof(IContextBinder<IContextService, ContextConsumer>), typeof(SpecialContextService));

                var consumer = container.CreateInstance<ContextConsumer>();

                Assert.Equal("special", consumer.Service.Name);
            }
        }

        [Fact]
        public void ResolveAfterDisposeThrows() {
            var container = new ServiceContainer();
            container.Register<ISingletonService, SingletonService>();
            container.Dispose();

            Assert.Throws<ObjectDisposedException>(() => container.Resolve<ISingletonService>());
            Assert.Throws<ObjectDisposedException>(() => container.CreateScope());
        }
    }
}
