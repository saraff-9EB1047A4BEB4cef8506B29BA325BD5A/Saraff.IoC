using System;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace Saraff.IoC.Tests {

    public class ConcurrencyTests {

        private static T Resolve<T>(ServiceContainer container) => (T)((IServiceProvider)container).GetService(typeof(T));

        [Fact]
        public void ParallelResolutionReturnsSingleInstance() {
            CountingGreetingService.CreatedCount = 0;
            using(var container = new ServiceContainer()) {
                container.Bind<IGreeting, CountingGreetingService>();

                var results = new IGreeting[64];
                Parallel.For(0, results.Length, i => results[i] = Resolve<IGreeting>(container));

                Assert.All(results, item => Assert.Same(results[0], item));
                Assert.Equal(1, CountingGreetingService.CreatedCount);
            }
        }

        [Fact]
        public void ParallelNestedContainerLifecycle() {
            using(var container = new ServiceContainer()) {
                container.Bind<IGreeting, GreetingService>();

                Parallel.For(0, 32, i => {
                    using(var nested = container.CreateNestedContainer()) {
                        nested.Bind<IDependency, DependencyService>();

                        var dependency = Resolve<IDependency>(nested);
                        Assert.NotNull(dependency);
                        Assert.NotNull(Resolve<IGreeting>(nested));
                    }
                });
            }
        }

        [Fact]
        public void RegistrySingletonIsCreatedOnce() {
            CountingGreetingService.CreatedCount = 0;
            using(var container = new ServiceContainer()) {
                container.Register<IGreeting, CountingGreetingService>();

                var results = new IGreeting[64];
                Parallel.For(0, results.Length, i => results[i] = container.Resolve<IGreeting>());

                Assert.All(results, item => Assert.Same(results[0], item));
                Assert.Equal(1, CountingGreetingService.CreatedCount);
            }
        }

        [Fact]
        public void ConcurrentRegistrationsAreAllRetained() {
            using(var container = new ServiceContainer()) {
                Parallel.For(0, 64, i => container.Register<IPlugin, PluginA>(ServiceLifetime.Transient));

                Assert.Equal(64, container.ResolveAll<IPlugin>().Count());
            }
        }

        [Fact]
        public async Task ResolveAndDisposeDoNotDeadlock() {
            var container = new ServiceContainer();
            container.Register<ITransientService, TransientService>(ServiceLifetime.Transient);
            var workers = Enumerable.Range(0, 8).Select(_ => Task.Run(() => {
                for(var i = 0; i < 200; i++) {
                    try {
                        container.Resolve<ITransientService>();
                    } catch(ObjectDisposedException) {
                    }
                }
            })).ToArray();
            var dispose = Task.Run(() => container.Dispose());
            var all = Task.WhenAll(workers.Concat(new[] { dispose }));

            Assert.Same(all, await Task.WhenAny(all, Task.Delay(TimeSpan.FromSeconds(20))));
            await all;
        }

        [Fact]
        public async Task NestedScopesAndParentDisposeDoNotDeadlock() {
            var root = new ServiceContainer();
            root.Register<ISingletonService, SingletonService>();
            root.Register<IScopedService, ScopedService>(ServiceLifetime.Scoped);
            var workers = Enumerable.Range(0, 8).Select(_ => Task.Run(() => {
                for(var i = 0; i < 100; i++) {
                    try {
                        using(var scope = root.CreateScope()) {
                            Assert.NotNull(scope.Resolve<IScopedService>());
                            Assert.NotNull(scope.Resolve<ISingletonService>());
                        }
                    } catch(ObjectDisposedException) {
                    }
                }
            })).ToArray();
            var dispose = Task.Run(() => root.Dispose());
            var all = Task.WhenAll(workers.Concat(new[] { dispose }));

            Assert.Same(all, await Task.WhenAny(all, Task.Delay(TimeSpan.FromSeconds(20))));
            await all;
        }
    }
}
