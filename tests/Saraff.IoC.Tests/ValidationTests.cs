using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Saraff.IoC.Tests {

    public class ValidationTests {

        [Fact]
        public void ArgumentValidationThrows() {
            using(var container = new ServiceContainer()) {
                Assert.Throws<ArgumentNullException>(() => container.Bind((Type)null, typeof(GreetingService)));
                Assert.Throws<ArgumentNullException>(() => container.Bind(typeof(IGreeting), (Type)null));
                Assert.Throws<ArgumentNullException>(() => container.Bind(typeof(IGreeting), (object)null));
                Assert.Throws<ArgumentNullException>(() => container.Bind<IGreeting>(null));
                Assert.Throws<ArgumentNullException>(() => container.Register((Type)null, typeof(GreetingService)));
                Assert.Throws<ArgumentNullException>(() => container.Register(typeof(IGreeting), (Type)null));
                Assert.Throws<ArgumentNullException>(() => container.Register((Type)null, (Func<IServiceProvider, object>)null));
                Assert.Throws<ArgumentNullException>(() => container.Register(typeof(IGreeting), (Func<IServiceProvider, object>)null));
                Assert.Throws<ArgumentNullException>(() => container.Register<IGreeting>((Func<IServiceProvider, IGreeting>)null));
                Assert.Throws<ArgumentNullException>(() => container.RegisterInstance((Type)null, new GreetingService()));
                Assert.Throws<ArgumentNullException>(() => container.RegisterInstance(typeof(IGreeting), (object)null));
                Assert.Throws<ArgumentNullException>(() => container.RegisterInstance<IGreeting>(null));
                Assert.Throws<ArgumentNullException>(() => { container.Resolve(null); });
                Assert.Throws<ArgumentNullException>(() => { container.ResolveNamed(null, "name"); });
                Assert.Throws<ArgumentNullException>(() => { container.ResolveNamed(typeof(IGreeting), null); });
                Assert.Throws<ArgumentNullException>(() => { container.IsRegistered(null); });
                Assert.Throws<ArgumentNullException>(() => { container.CreateInstance((Type)null); });
                Assert.Throws<ArgumentNullException>(() => container.Load(null));
            }
        }

        [Fact]
        public void LoadBindsAssemblyAttributes() {
            using(var container = new ServiceContainer()) {
                container.Load(typeof(ILoadedService).Assembly);

                Assert.True(container.IsRegistered(typeof(ILoadedService)));
                var service = container.Resolve<ILoadedService>();
                Assert.Equal("loaded", service.Name);
                Assert.Same(service, container.Resolve<ILoadedService>());
            }
        }

        [Fact]
        public void ConfigurationDefaultsAndCallback() {
            var configuration = new ServiceContainerConfiguration();

            Assert.Equal(typeof(BindServiceAttribute), configuration.BindServiceAttributeType);
            Assert.Equal(typeof(ServiceRequiredAttribute), configuration.ServiceRequiredAttributeType);
            Assert.Equal(typeof(IContextBinder<,>), configuration.ContextBinderType);
            Assert.Equal(typeof(ProxyRequiredAttribute), configuration.ProxyRequiredAttributeType);
            Assert.Equal(typeof(IListener), configuration.ListenerType);
            Assert.Equal(typeof(Saraff.IoC.Lazy<>), configuration.LazyCallbackType);
            Assert.NotNull(configuration.BindServiceCallback);
            Assert.NotNull(configuration.InvokingCallback);
            Assert.NotNull(configuration.InvokedCallback);
            Assert.NotNull(configuration.CatchCallback);

            var attribute = new BindServiceAttribute(typeof(IGreeting), typeof(GreetingService));
            Type service = null;
            Type implementation = null;
            configuration.BindServiceCallback(attribute, (s, o) => {
                service = s;
                implementation = o;
            });

            Assert.Equal(typeof(IGreeting), service);
            Assert.Equal(typeof(GreetingService), implementation);
        }

        [Fact]
        public void ConfigurationViaConstructor() {
            using(var container = new ServiceContainer(new ServiceContainerConfiguration())) {
                container.Register<IGreeting, GreetingService>();

                Assert.NotNull(container.Resolve<IGreeting>());
            }
        }

        [Fact]
        public void ConfigurationViaUseConfiguration() {
            using(var container = new ServiceContainer()) {
                container.UseConfiguration(new ServiceContainerConfiguration { ServiceRequiredAttributeType = typeof(CustomServiceRequiredAttribute) });
                container.Register<IGreeting, GreetingService>();

                var consumer = container.CreateInstance<CustomConsumer>();

                Assert.NotNull(consumer.Greeting);
            }
        }

        [Fact]
        public void ConfigurationViaRegisteredInstance() {
            using(var container = new ServiceContainer()) {
                container.RegisterInstance<IConfiguration>(new ServiceContainerConfiguration { ServiceRequiredAttributeType = typeof(CustomServiceRequiredAttribute) });
                container.Register<IGreeting, GreetingService>();

                var consumer = container.CreateInstance<CustomConsumer>();

                Assert.NotNull(consumer.Greeting);
            }
        }

        [Fact]
        public void ConfigurationViaRegisteredImplementation() {
            using(var container = new ServiceContainer()) {
                container.Register<IConfiguration, CustomConfiguration>();
                container.Register<IGreeting, GreetingService>();

                var consumer = container.CreateInstance<CustomConsumer>();

                Assert.NotNull(consumer.Greeting);
            }
        }

        [Fact]
        public void ConfigurationViaBoundInstance() {
            using(var container = new ServiceContainer()) {
                container.Bind(typeof(IConfiguration), new CustomConfiguration());
                container.Register<IGreeting, GreetingService>();

                var consumer = container.CreateInstance<BoundConfigConsumer>();

                Assert.NotNull(consumer.Greeting);
            }
        }

        [Fact]
        public void NamedRegistrationMissingReturnsNull() {
            using(var container = new ServiceContainer()) {
                container.Register<IPlugin, PluginA>(ServiceLifetime.Transient, "a");

                Assert.Null(container.ResolveNamed<IPlugin>("missing"));
                Assert.Equal("a", container.ResolveNamed<IPlugin>("a").Name);
            }
        }

        [Fact]
        public void OpenGenericResolveAllIncludesExactAndOpenRegistrations() {
            using(var container = new ServiceContainer()) {
                container.Register<IRepository<Customer>, Repository<Customer>>(ServiceLifetime.Transient);
                container.Register(typeof(IRepository<>), typeof(Repository<>), ServiceLifetime.Transient);

                var all = container.ResolveAll<IRepository<Customer>>().ToList();
                var viaEnumerable = ((IEnumerable<IRepository<Customer>>)container.Resolve(typeof(IEnumerable<IRepository<Customer>>))).ToList();

                Assert.Equal(2, all.Count);
                Assert.Equal(2, viaEnumerable.Count);
                Assert.All(all, item => Assert.Equal(typeof(Customer), item.ItemType));
            }
        }

        [Fact]
        public void InvalidOpenGenericRegistrationsThrow() {
            using(var container = new ServiceContainer()) {
                Assert.Throws<ArgumentException>(() => container.Register(typeof(IRepository<>), typeof(Repository<Customer>)));
                Assert.Throws<ArgumentException>(() => container.Register(typeof(IRepository<>), typeof(TwoArgumentRepository<,>)));
                Assert.Throws<ArgumentException>(() => container.Register(typeof(Repository<Customer>), typeof(IRepository<>)));
            }
        }

        [Fact]
        public void ServiceWithoutSuitableConstructorThrows() {
            using(var container = new ServiceContainer()) {
                Assert.Throws<ServiceResolutionException>(() => { container.CreateInstance<NoCtorService>(); });
            }
        }

        [Fact]
        public void AbstractDependencyIsResolvedFromRegistration() {
            using(var container = new ServiceContainer()) {
                container.Register<AbstractDependency, ConcreteDependency>();

                var consumer = container.CreateInstance<AbstractConsumer>();

                Assert.Equal("concrete", consumer.Dependency.Name);
            }
        }

        [Fact]
        public void UnregisteredConcreteDependencyIsCreated() {
            using(var container = new ServiceContainer()) {
                var consumer = container.CreateInstance<ConcreteConsumer>();

                Assert.NotNull(consumer.Dependency);
            }
        }

        [Fact]
        public void FactoryReturningNullIsResolvedAsNull() {
            using(var container = new ServiceContainer()) {
                container.Register<IGreeting>(provider => (IGreeting)null);

                Assert.Null(container.Resolve<IGreeting>());
            }
        }

        [Fact]
        public void ProtectedGetServiceRejectsNull() {
            using(var container = new ExposedContainer()) {
                Assert.Throws<ArgumentNullException>(() => { container.GetServiceExposed(null); });
            }
        }

        [Fact]
        public void ContextBinderMarkerIsNotInjectable() {
            using(var container = new ServiceContainer()) {
                var consumer = container.CreateInstance<ContextBinderConsumer>();

                Assert.Null(consumer.Binder);
            }
        }

        [Fact]
        public void LazyContextBinderIsNull() {
            using(var container = new ServiceContainer()) {
                var consumer = container.CreateInstance<LazyContextConsumer>();

                Assert.Null(consumer.Lazy);
            }
        }

        [Fact]
        public void CreateInstanceTypeOverload() {
            using(var container = new ServiceContainer()) {
                var consumer = container.CreateInstance(typeof(CtorConsumer));

                Assert.IsType<CtorConsumer>(consumer);
            }
        }

        [Fact]
        public void ExceptionConstructors() {
            _ = new CircularDependencyException();
            _ = new CircularDependencyException("message");
            _ = new CircularDependencyException("message", new InvalidOperationException());
            _ = new ServiceResolutionException();
            _ = new ServiceResolutionException("message");
            _ = new ServiceResolutionException("message", new InvalidOperationException());
        }

        [Fact]
        public void ScopeProviderAndDisposedState() {
            var container = new ServiceContainer();
            var scope = container.CreateScope();

            Assert.NotNull(scope.ServiceProvider);

            scope.Dispose();

            Assert.Throws<ObjectDisposedException>(() => { var _ = scope.ServiceProvider; });
            Assert.Throws<ObjectDisposedException>(() => { scope.Resolve<IGreeting>(); });
            container.Dispose();
        }

        private sealed class ExposedContainer : ServiceContainer {
            public object GetServiceExposed(Type serviceType) => base.GetService(serviceType);
        }
    }
}
