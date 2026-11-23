using System;
using System.Collections.Generic;
using Xunit;

namespace Saraff.IoC.Tests {

    public class IsolationTests {

        [Fact]
        public void CustomContainerFacadeIsolatesLegacyTypes() {
            using(var container = new ServiceContainer()) {
                var facade = new MyContainer(container);
                facade.Register(typeof(IGreeting), typeof(PlainGreetingService), MyLifetime.Singleton);

                Assert.Equal("hello", facade.Resolve<IGreeting>().Greet());

                IsolationAssertions.AssertNoLegacyApi(typeof(MyContainer));
                IsolationAssertions.AssertNoLegacyApi(typeof(IMyContainer));
                IsolationAssertions.AssertNoLegacyApi(typeof(IMyRegistry));
            }
        }

        [Fact]
        public void CustomLifetimeControlsCaching() {            using(var container = new ServiceContainer()) {
                var lifetime = new CountingLifetime();
                container.Register(typeof(IGreeting), typeof(PlainGreetingService), lifetime, null);

                var first = container.Resolve(typeof(IGreeting));
                var second = container.Resolve(typeof(IGreeting));

                Assert.Same(first, second);
                Assert.Equal(1, lifetime.Created);
            }
        }

        [Fact]
        public void CustomRegistrationFactoryIsUsed() {
            var factory = new RecordingRegistrationFactory();
            var configuration = new ServiceContainerConfiguration { ServiceRegistrationFactory = factory };
            using(var container = new ServiceContainer(configuration)) {
                container.Register(typeof(IGreeting), typeof(PlainGreetingService), ServiceLifetimes.Singleton, null);

                Assert.Equal(1, factory.Created);
                Assert.NotNull(container.Resolve(typeof(IGreeting)));
            }
        }

        [Fact]
        public void CustomInvocationResultIsUsed() {
            var configuration = new ServiceContainerConfiguration {
                InvokingResultCallback = (listener, method, instance, parameters) => new MyInvocationResult(true, 123)
            };
            using(var container = new ServiceContainer(configuration)) {
                container.Register<IExplicitService, ExplicitService>(ServiceLifetime.Transient);

                var service = container.Resolve<IExplicitService>();

                Assert.Equal(123, service.GetValue());
            }
        }

        [Fact]
        public void ConfigurationSnapshotRequiresUseConfiguration() {            var configuration = new ServiceContainerConfiguration();
            using(var container = new ServiceContainer(configuration)) {
                container.Register<IGreeting, PlainGreetingService>(ServiceLifetime.Singleton);

                var factory = new RecordingLifetimeFactory();
                configuration.ServiceLifetimeFactory = factory;

                container.Register<IPlugin, PluginA>(ServiceLifetime.Singleton);
                Assert.Equal(0, factory.Created);

                container.UseConfiguration(configuration);
                container.Register<IPlugin, PluginB>(ServiceLifetime.Singleton);

                Assert.Equal(1, factory.Created);
                Assert.NotNull(container.Resolve<IGreeting>());
            }
        }

        [Fact]
        public void LegacyInvocationCallbackIsAdapted() {
            using(var container = new ServiceContainer(new LegacyCallbackConfiguration())) {
                container.Register<IExplicitService, ExplicitService>(ServiceLifetime.Transient);

                Assert.Equal(321, container.Resolve<IExplicitService>().GetValue());
            }
        }

        [Fact]
        public void AggregateConfigurationLegacyCallbackIsAdapted() {
            var configuration = new ServiceContainerConfiguration {
                InvokingCallback = (listener, method, instance, parameters) => new InvocationResult(true, 77)
            };
            using(var container = new ServiceContainer(configuration)) {
                container.Register<IExplicitService, ExplicitService>(ServiceLifetime.Transient);

                Assert.Equal(77, container.Resolve<IExplicitService>().GetValue());
            }
        }

        [Fact]
        public void CoreRegisterFactoryIsUsed() {            using(var container = new ServiceContainer()) {
                var core = (IContainerCore)container;
                core.Register(typeof(IGreeting), provider => (IGreeting)new PlainGreetingService(), ServiceLifetimes.Transient, null);

                Assert.Equal("hello", ((IGreeting)core.Resolve(typeof(IGreeting))).Greet());
            }
        }

        [Fact]
        public void CoreCreateInstanceAcceptsOverrides() {
            using(var container = new ServiceContainer()) {
                var core = (IContainerCore)container;
                var args = new Dictionary<string, object> { { "greeting", new PlainGreetingService() } };

                var consumer = (CtorConsumer)core.CreateInstance(typeof(CtorConsumer), false, args);

                Assert.NotNull(consumer.Greeting);
                Assert.Equal("hello", consumer.Greeting.Greet());
            }
        }

        [Fact]
        public void CoreNestedContainerIsIsolated() {
            using(var container = new ServiceContainer()) {
                var core = (IContainerCore)container;
                core.Register(typeof(IGreeting), typeof(PlainGreetingService), ServiceLifetimes.Scoped, null);
                using(var nested = core.CreateNested()) {
                    Assert.Same(core, nested.Parent);

                    var first = nested.Resolve(typeof(IGreeting));
                    var second = nested.Resolve(typeof(IGreeting));

                    Assert.Same(first, second);
                }
            }
        }
    }
}
