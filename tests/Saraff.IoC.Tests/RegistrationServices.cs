using System;
using System.Collections.Generic;

namespace Saraff.IoC.Tests {

    public interface ISingletonService {
        Guid Id { get; }
    }

    public sealed class SingletonService : ISingletonService {
        public Guid Id { get; } = Guid.NewGuid();
    }

    public interface ITransientService {
        Guid Id { get; }
    }

    public sealed class TransientService : ITransientService {
        public Guid Id { get; } = Guid.NewGuid();
    }

    public interface IScopedService {
        Guid Id { get; }
    }

    public sealed class ScopedService : IScopedService {
        public Guid Id { get; } = Guid.NewGuid();
    }

    public sealed class DisposablePocoService : ITransientService, IDisposable {
        public Guid Id { get; } = Guid.NewGuid();
        public bool DisposedFlag;

        public void Dispose() {
            this.DisposedFlag = true;
        }
    }

    public interface IPlugin {
        string Name { get; }
    }

    public sealed class PluginA : IPlugin {
        public string Name => "a";
    }

    public sealed class PluginB : IPlugin {
        public string Name => "b";
    }

    public interface IRepository<T> {
        Type ItemType { get; }
    }

    public sealed class Repository<T> : IRepository<T> {
        public Type ItemType => typeof(T);
    }

    public sealed class Customer {
    }

    public sealed class PluginConsumer {
        [ServiceRequired]
        public PluginConsumer(IEnumerable<IPlugin> plugins) {
            this.Plugins = plugins;
        }

        public IEnumerable<IPlugin> Plugins { get; private set; }
    }

    public sealed class RegisteredConsumer {
        [ServiceRequired]
        public RegisteredConsumer(ITransientService service) {
            this.Service = service;
        }

        public ITransientService Service { get; private set; }
    }
}
