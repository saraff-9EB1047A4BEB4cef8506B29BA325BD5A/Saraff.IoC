using System;
using System.ComponentModel;
using Xunit;

namespace Saraff.IoC.Tests {

    public class ResolutionTests {

        private static T Resolve<T>(ServiceContainer container) => (T)((IServiceProvider)container).GetService(typeof(T));

        [Fact]
        public void ResolveBoundServiceReturnsSameInstance() {
            using(var container = new ServiceContainer()) {
                container.Bind<IGreeting, GreetingService>();

                var first = Resolve<IGreeting>(container);
                var second = Resolve<IGreeting>(container);

                Assert.Same(first, second);
            }
        }

        [Fact]
        public void ConstructorInjectionResolvesDependency() {
            using(var container = new ServiceContainer()) {
                container.Bind<IGreeting, GreetingService>();

                var consumer = container.CreateInstance<CtorConsumer>();

                Assert.NotNull(consumer.Greeting);
                Assert.Equal("hello", consumer.Greeting.Greet());
            }
        }

        [Fact]
        public void PropertyInjectionResolvesDependency() {
            using(var container = new ServiceContainer()) {
                container.Bind<IGreeting, GreetingService>();

                var consumer = container.CreateInstance<PropertyConsumer>();

                Assert.NotNull(consumer.Greeting);
            }
        }

        [Fact]
        public void ConstructorCallbackOverridesDependency() {
            using(var container = new ServiceContainer()) {
                container.Bind<IGreeting, GreetingService>();
                var fake = new GreetingService();

                var consumer = container.CreateInstance<CtorConsumer>(callback => callback("greeting", fake));

                Assert.Same(fake, consumer.Greeting);
            }
        }

        [Fact]
        public void NestedContainerResolvesFromParent() {
            using(var container = new ServiceContainer()) {
                container.Bind<IGreeting, GreetingService>();
                using(var nested = container.CreateNestedContainer()) {
                    Assert.Same(Resolve<IGreeting>(container), Resolve<IGreeting>(nested));
                }
            }
        }

        [Fact]
        public void NestedContainerBindingIsNotVisibleToParent() {
            using(var container = new ServiceContainer()) {
                using(var nested = container.CreateNestedContainer()) {
                    nested.Bind<IGreeting, GreetingService>();

                    Assert.Null(((IServiceProvider)container).GetService(typeof(IGreeting)));
                    Assert.NotNull(((IServiceProvider)nested).GetService(typeof(IGreeting)));
                }
            }
        }

        [Fact]
        public void ContextBindingIsUsedForConsumer() {
            using(var container = new ServiceContainer()) {
                container.Bind<IContextService, DefaultContextService>();
                container.Bind<IContextBinder<IContextService, ContextConsumer>, SpecialContextService>();

                var consumer = container.CreateInstance<ContextConsumer>();
                var other = container.CreateInstance<OtherContextConsumer>();

                Assert.Equal("special", consumer.Service.Name);
                Assert.Equal("default", other.Service.Name);
            }
        }

        [Fact]
        public void CyclicDependencyThrowsWithTrace() {
            using(var container = new ServiceContainer()) {
                container.Bind<ICycleA, CycleA>();
                container.Bind<ICycleB, CycleB>();

                var ex = Assert.Throws<CircularDependencyException>(() => ((IServiceProvider)container).GetService(typeof(ICycleA)));

                Assert.Contains("Трассировка", ex.Message);
                Assert.Contains(typeof(CycleA).FullName, ex.Message);
            }
        }

        [Fact]
        public void LazyDependencyIsResolvedOnDemand() {
            using(var container = new ServiceContainer()) {
                container.Bind<IGreeting, GreetingService>();

                var consumer = container.CreateInstance<LazyConsumer>();

                Assert.NotNull(consumer.Greeting);
                Assert.Equal("hello", consumer.Greeting().Greet());
            }
        }

        [Fact]
        public void CreateInstanceDoesNotRequireRegistration() {
            using(var container = new ServiceContainer()) {
                var consumer = container.CreateInstance<CtorConsumer>();

                Assert.Null(consumer.Greeting);
            }
        }

        [Fact]
        public void UnboundConcreteTypeIsNotResolvedThroughServiceProvider() {
            using(var container = new ServiceContainer()) {
                Assert.Null(((IServiceProvider)container).GetService(typeof(GreetingService)));
            }
        }

        [Fact]
        public void DuplicateBindingThrows() {
            using(var container = new ServiceContainer()) {
                container.Bind<IGreeting, GreetingService>();

                Assert.Throws<ArgumentException>(() => container.Bind<IGreeting, GreetingService>());
            }
        }

        [Fact]
        public void BindingNonInterfaceThrows() {
            using(var container = new ServiceContainer()) {
                Assert.Throws<InvalidOperationException>(() => container.Bind<GreetingService, GreetingService>());
            }
        }

        [Fact]
        public void BindingImplementationWithoutIComponentThrows() {
            using(var container = new ServiceContainer()) {
                Assert.Throws<InvalidOperationException>(() => container.Bind<IGreeting, PlainGreetingService>());
            }
        }
    }
}
