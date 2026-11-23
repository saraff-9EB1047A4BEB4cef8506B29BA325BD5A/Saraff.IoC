using System;
using System.Threading;
using System.Threading.Tasks;

namespace Saraff.IoC {

    internal sealed class ServiceScope : IServiceScope {
        private ServiceContainer _container;

        public ServiceScope(ServiceContainer container) {
            _container = container;
        }

        public IServiceProvider ServiceProvider => this.RequireContainer();

        public object Resolve(Type serviceType) => this.RequireContainer().Resolve(serviceType);

        public T Resolve<T>() => (T)this.Resolve(typeof(T));

        public object ResolveNamed(Type serviceType, string name) => this.RequireContainer().ResolveNamed(serviceType, name);

        public T ResolveNamed<T>(string name) => (T)this.ResolveNamed(typeof(T), name);

#if NETSTANDARD

        public Task<object> ResolveAsync(Type serviceType, CancellationToken cancellationToken = default(CancellationToken)) => this.RequireContainer().ResolveAsync(serviceType, cancellationToken);

        public Task<T> ResolveAsync<T>(CancellationToken cancellationToken = default(CancellationToken)) => this.RequireContainer().ResolveAsync<T>(cancellationToken);

        public Task InitializeAsync(CancellationToken cancellationToken = default(CancellationToken)) => this.RequireContainer().InitializeAsync(cancellationToken);

        public ValueTask DisposeAsync() {
            var _container = this._container;
            this._container = null;
            return _container == null ? default(ValueTask) : _container.DisposeAsync();
        }

#endif

        public void Dispose() {
            var _container = this._container;
            this._container = null;
            if(_container != null) {
                _container.Dispose();
            }
        }

        private ServiceContainer RequireContainer() {
            var _container = this._container;
            if(_container == null) {
                throw new ObjectDisposedException(nameof(ServiceScope));
            }
            return _container;
        }
    }
}
