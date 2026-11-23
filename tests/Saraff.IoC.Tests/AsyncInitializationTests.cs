using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Saraff.IoC.Tests {

    public interface IAsyncService {
        int InitCount { get; }
    }

    public sealed class AsyncService : IAsyncService, IAsyncServiceAlias, IAsyncInitializable {
        public int InitCount { get; private set; }

        public Task InitializeAsync(CancellationToken cancellationToken = default(CancellationToken)) {
            this.InitCount++;
            return Task.CompletedTask;
        }
    }

    public sealed class InitLog {
        public List<string> Items { get; } = new List<string>();
    }

    public sealed class FirstAsyncService : IAsyncInitializable {
        private readonly InitLog _log;

        [ServiceRequired]
        public FirstAsyncService(InitLog log) {
            this._log = log;
        }

        public Task InitializeAsync(CancellationToken cancellationToken = default(CancellationToken)) {
            this._log.Items.Add("first");
            return Task.CompletedTask;
        }
    }

    public sealed class SecondAsyncService : IAsyncInitializable {
        private readonly InitLog _log;

        [ServiceRequired]
        public SecondAsyncService(InitLog log, FirstAsyncService first) {
            this._log = log;
        }

        public Task InitializeAsync(CancellationToken cancellationToken = default(CancellationToken)) {
            this._log.Items.Add("second");
            return Task.CompletedTask;
        }
    }

    public interface IFlakyService {
        int Calls { get; }
    }

    public sealed class FlakyService : IFlakyService, IAsyncInitializable {
        public int Calls { get; private set; }

        public Task InitializeAsync(CancellationToken cancellationToken = default(CancellationToken)) {
            this.Calls++;
            if(this.Calls == 1) {
                throw new InvalidOperationException("first");
            }
            return Task.CompletedTask;
        }
    }

    public interface IAsyncDisposableService {
    }

    public sealed class AsyncDisposableService : IAsyncDisposableService, IAsyncDisposable {
        public bool DisposedFlag;
        public int DisposeCount;

        public ValueTask DisposeAsync() {
            this.DisposeCount++;
            this.DisposedFlag = true;
            return default(ValueTask);
        }
    }

    public sealed class AsyncComponentService : System.ComponentModel.Component, IAsyncComponentAlias, IAsyncDisposable {
        public bool DisposedAsync;
        public bool DisposedSync;

        public ValueTask DisposeAsync() {
            this.DisposedAsync = true;
            return default(ValueTask);
        }

        protected override void Dispose(bool disposing) {
            this.DisposedSync = true;
            base.Dispose(disposing);
        }
    }

    public sealed class GatedAsyncService : IAsyncService, IAsyncInitializable, IAsyncDisposable {
        public TaskCompletionSource<bool> Started { get; } = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource<bool> Gate { get; } = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        public int InitCount { get; private set; }

        public bool DisposedFlag;

        public async Task InitializeAsync(CancellationToken cancellationToken = default(CancellationToken)) {
            this.InitCount++;
            this.Started.TrySetResult(true);
            await this.Gate.Task;
        }

        public ValueTask DisposeAsync() {
            this.DisposedFlag = true;
            return default(ValueTask);
        }
    }

    public sealed class CancellableAsyncService : IAsyncService, IAsyncInitializable {
        public int InitCount { get; private set; }

        public Task InitializeAsync(CancellationToken cancellationToken = default(CancellationToken)) {
            cancellationToken.ThrowIfCancellationRequested();
            this.InitCount++;
            return Task.CompletedTask;
        }
    }

    public sealed class FailingGatedAsyncService : IAsyncService, IAsyncInitializable, IAsyncDisposable {
        public TaskCompletionSource<bool> Started { get; } = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource<bool> Gate { get; } = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        public int InitCount { get; private set; }

        public bool DisposedFlag;

        public async Task InitializeAsync(CancellationToken cancellationToken = default(CancellationToken)) {
            this.InitCount++;
            this.Started.TrySetResult(true);
            await this.Gate.Task;
            throw new InvalidOperationException("init failed");
        }

        public ValueTask DisposeAsync() {
            this.DisposedFlag = true;
            return default(ValueTask);
        }
    }

    public class AsyncInitializationTests {

        [Fact]
        public async Task ResolveAsyncInitializesServiceOnce() {
            using(var container = new ServiceContainer()) {
                container.Register<IAsyncService, AsyncService>();

                var first = await container.ResolveAsync<IAsyncService>();
                var second = await container.ResolveAsync<IAsyncService>();

                Assert.Same(first, second);
                Assert.Equal(1, first.InitCount);
            }
        }

        [Fact]
        public async Task InitializeAsyncInitializesAlreadyCreatedServices() {
            using(var container = new ServiceContainer()) {
                container.Register<IAsyncService, AsyncService>();
                var service = container.Resolve<IAsyncService>();

                Assert.Equal(0, service.InitCount);

                await container.InitializeAsync();

                Assert.Equal(1, service.InitCount);
            }
        }

        [Fact]
        public async Task DependenciesAreInitializedBeforeDependents() {
            using(var container = new ServiceContainer()) {
                var log = new InitLog();
                container.RegisterInstance(log);
                container.Register<FirstAsyncService, FirstAsyncService>(ServiceLifetime.Transient);
                container.Register<SecondAsyncService, SecondAsyncService>(ServiceLifetime.Transient);

                await container.ResolveAsync<SecondAsyncService>();

                Assert.Equal(new[] { "first", "second" }, log.Items);
            }
        }

        [Fact]
        public async Task FailedInitializationCanBeRetried() {
            using(var container = new ServiceContainer()) {
                container.Register<IFlakyService, FlakyService>();

                await Assert.ThrowsAsync<InvalidOperationException>(() => container.ResolveAsync<IFlakyService>());

                var service = await container.ResolveAsync<IFlakyService>();

                Assert.Equal(2, service.Calls);
            }
        }

        [Fact]
        public async Task ScopeResolveAsyncInitializesScopedService() {
            var container = new ServiceContainer();
            container.Register<IAsyncService, AsyncService>(ServiceLifetime.Scoped);
            var scope = container.CreateScope();

            var first = await scope.ResolveAsync<IAsyncService>();
            var second = await scope.ResolveAsync<IAsyncService>();
            var third = (IAsyncService)await scope.ResolveAsync(typeof(IAsyncService));

            Assert.Same(first, second);
            Assert.Same(first, third);
            Assert.Equal(1, first.InitCount);
            await scope.InitializeAsync();
            await scope.DisposeAsync();
            await container.DisposeAsync();
        }

        [Fact]
        public async Task DisposeAsyncDisposesScopedAsyncDisposable() {
            var container = new ServiceContainer();
            container.Register<IAsyncDisposableService, AsyncDisposableService>(ServiceLifetime.Scoped);
            var scope = container.CreateScope();
            var service = (AsyncDisposableService)scope.Resolve<IAsyncDisposableService>();

            await scope.DisposeAsync();

            Assert.True(service.DisposedFlag);
            await container.DisposeAsync();
        }

        [Fact]
        public async Task DisposeAsyncDisposesSingletonAndNestedScopes() {
            var container = new ServiceContainer();
            container.Register<IAsyncDisposableService, AsyncDisposableService>();
            container.Register<IAsyncDisposableService, AsyncDisposableService>(ServiceLifetime.Scoped, "scoped");
            var scope = container.CreateScope();
            var singleton = (AsyncDisposableService)container.Resolve<IAsyncDisposableService>();
            var scoped = (AsyncDisposableService)scope.ResolveNamed<IAsyncDisposableService>("scoped");

            await container.DisposeAsync();

            Assert.True(singleton.DisposedFlag);
            Assert.True(scoped.DisposedFlag);
        }

        [Fact]
        public void SyncDisposeThrowsForAsyncOnlyService() {
            var container = new ServiceContainer();
            container.Register<IAsyncDisposableService, AsyncDisposableService>();
            container.Resolve<IAsyncDisposableService>();

            Assert.Throws<InvalidOperationException>(() => container.Dispose());
        }

        [Fact]
        public void SyncDisposeStillDisposesSynchronousServices() {
            var container = new ServiceContainer();
            container.Register<IAsyncDisposableService, AsyncDisposableService>();
            container.Register<IGreeting, GreetingService>();
            container.Resolve<IAsyncDisposableService>();
            var greeting = (GreetingService)container.Resolve<IGreeting>();

            Assert.Throws<InvalidOperationException>(() => container.Dispose());

            Assert.True(greeting.DisposedFlag);
        }

        [Fact]
        public async Task ResolveAsyncReturnsNullForUnregisteredService() {
            using(var container = new ServiceContainer()) {
                Assert.Null(await container.ResolveAsync(typeof(IGreeting)));
            }
        }

        [Fact]
        public async Task ResolveAsyncHonorsCancellation() {
            using(var container = new ServiceContainer()) {
                container.Register<IAsyncService, CancellableAsyncService>();
                using(var cts = new CancellationTokenSource()) {
                    cts.Cancel();
                    await Assert.ThrowsAnyAsync<OperationCanceledException>(() => container.ResolveAsync<IAsyncService>(cts.Token));
                }

                var service = await container.ResolveAsync<IAsyncService>();

                Assert.Equal(1, service.InitCount);
            }
        }

        [Fact]
        public async Task ConcurrentResolveAsyncInitializesOnce() {
            var container = new ServiceContainer();
            container.Register<IAsyncService, GatedAsyncService>();
            var service = (GatedAsyncService)container.Resolve<IAsyncService>();
            var tasks = Enumerable.Range(0, 16).Select(_ => container.ResolveAsync<IAsyncService>()).ToArray();
            await service.Started.Task;
            service.Gate.TrySetResult(true);

            var results = await Task.WhenAll(tasks);

            Assert.All(results, item => Assert.Same(service, item));
            Assert.Equal(1, service.InitCount);
            await container.DisposeAsync();
        }

        [Fact]
        public async Task ConcurrentInitializeAsyncInitializesOnce() {
            var container = new ServiceContainer();
            container.Register<IAsyncService, GatedAsyncService>();
            var service = (GatedAsyncService)container.Resolve<IAsyncService>();
            var tasks = Enumerable.Range(0, 16).Select(_ => container.InitializeAsync()).ToArray();
            await service.Started.Task;
            service.Gate.TrySetResult(true);

            await Task.WhenAll(tasks);

            Assert.Equal(1, service.InitCount);
            await container.DisposeAsync();
        }

        [Fact]
        public async Task DisposeAsyncWaitsForInFlightInitialization() {
            var container = new ServiceContainer();
            container.Register<IAsyncService, GatedAsyncService>();
            var service = (GatedAsyncService)container.Resolve<IAsyncService>();
            var resolving = container.ResolveAsync<IAsyncService>();
            await service.Started.Task;

            var disposing = container.DisposeAsync().AsTask();
            Assert.False(disposing.IsCompleted);

            service.Gate.TrySetResult(true);
            await resolving;
            await disposing;

            Assert.Equal(1, service.InitCount);
            Assert.True(service.DisposedFlag);
        }

        [Fact]
        public async Task DisposeAsyncUsesAsyncDisposalForComponent() {
            var container = new ServiceContainer();
            container.Register<AsyncComponentService, AsyncComponentService>();
            var service = (AsyncComponentService)container.Resolve<AsyncComponentService>();

            await container.DisposeAsync();

            Assert.True(service.DisposedAsync);
            Assert.False(service.DisposedSync);
        }

        [Fact]
        public async Task DisposeAsyncDisposesSynchronousDisposables() {
            var container = new ServiceContainer();
            container.Register<ITransientService, DisposablePocoService>(ServiceLifetime.Transient);
            container.Register<IAsyncDisposableService, AsyncDisposableService>(ServiceLifetime.Transient);
            var synchronous = (DisposablePocoService)container.Resolve<ITransientService>();
            var asynchronous = (AsyncDisposableService)container.Resolve<IAsyncDisposableService>();

            await container.DisposeAsync();

            Assert.True(synchronous.DisposedFlag);
            Assert.True(asynchronous.DisposedFlag);
        }

        [Fact]
        public async Task DisposeAsyncIsIdempotentAndDisposesOnce() {
            var container = new ServiceContainer();
            container.Register<AsyncDisposableService, AsyncDisposableService>();
            var service = (AsyncDisposableService)container.Resolve<AsyncDisposableService>();

            await container.DisposeAsync();
            await container.DisposeAsync();

            Assert.Equal(1, service.DisposeCount);
        }

        [Fact]
        public async Task ConcurrentDisposeAsyncDisposesOnce() {
            var container = new ServiceContainer();
            container.Register<AsyncDisposableService, AsyncDisposableService>();
            var service = (AsyncDisposableService)container.Resolve<AsyncDisposableService>();

            await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => container.DisposeAsync().AsTask()));

            Assert.Equal(1, service.DisposeCount);
        }

        [Fact]
        public async Task DisposedScopeAsyncMethodsThrow() {
            using(var container = new ServiceContainer()) {
                var scope = container.CreateScope();
                Assert.NotNull(scope.ServiceProvider);
                scope.Dispose();

                Assert.Throws<ObjectDisposedException>(() => scope.ServiceProvider);
                await Assert.ThrowsAsync<ObjectDisposedException>(async () => await scope.ResolveAsync<IGreeting>());
                await Assert.ThrowsAsync<ObjectDisposedException>(async () => await scope.InitializeAsync());
            }
        }

        [Fact]
        public async Task SharedInstanceAcrossRegistrationsIsInitializedOnce() {
            using(var container = new ServiceContainer()) {
                var shared = new AsyncService();
                container.Register<IAsyncService>(provider => shared);
                container.Register<IAsyncServiceAlias>(provider => shared);
                container.Resolve<IAsyncService>();
                container.Resolve<IAsyncServiceAlias>();

                await container.InitializeAsync();

                Assert.Equal(1, shared.InitCount);
            }
        }

        [Fact]
        public async Task SharedAsyncComponentIsTrackedOnce() {
            var container = new ServiceContainer();
            var shared = new AsyncComponentService();
            container.Register<IAsyncComponentAlias>(provider => shared);
            container.Register<AsyncComponentService>(provider => shared);

            Assert.Same(shared, container.Resolve<IAsyncComponentAlias>());
            Assert.Same(shared, container.Resolve<AsyncComponentService>());

            await container.DisposeAsync();

            Assert.True(shared.DisposedAsync);
        }

        [Fact]
        public async Task DisposeAsyncSwallowsInitializationFailure() {
            var container = new ServiceContainer();
            container.Register<IAsyncService, FailingGatedAsyncService>();
            var service = (FailingGatedAsyncService)container.Resolve<IAsyncService>();
            var resolving = container.ResolveAsync<IAsyncService>();
            await service.Started.Task;

            var disposing = container.DisposeAsync().AsTask();
            Assert.False(disposing.IsCompleted);

            service.Gate.TrySetResult(true);
            await Assert.ThrowsAsync<InvalidOperationException>(() => resolving);
            await disposing;

            Assert.Equal(1, service.InitCount);
            Assert.True(service.DisposedFlag);
        }

        [Fact]
        public async Task CustomAsyncInitializationAdapterIsUsed() {
            var adapter = new CustomAsyncInitializationAdapter();
            var configuration = new ServiceContainerConfiguration { AsyncInitializationAdapter = adapter };
            using(var container = new ServiceContainer(configuration)) {
                container.Register<ICustomAsyncService, CustomAsyncService>(ServiceLifetime.Transient);
                var core = (IContainerCore)container;
                var service = (ICustomAsyncService)await core.ResolveAsync(typeof(ICustomAsyncService), CancellationToken.None);

                Assert.True(service.Initialized);
                Assert.Equal(1, adapter.Calls);
            }
        }
    }

    public interface ICustomAsyncService {
        bool Initialized { get; }
    }

    public sealed class CustomAsyncService : ICustomAsyncService {
        public bool Initialized { get; private set; }

        public void MarkInitialized() => this.Initialized = true;
    }

    public sealed class CustomAsyncInitializationAdapter : IAsyncInitializationAdapter {
        public int Calls { get; private set; }

        public bool CanInitialize(object instance) => instance is CustomAsyncService;

        public Task InitializeAsync(object instance, CancellationToken cancellationToken) {
            this.Calls++;
            ((CustomAsyncService)instance).MarkInitialized();
            return Task.CompletedTask;
        }
    }
}
