using System;
using System.ComponentModel;
using Saraff.IoC;

namespace Saraff.IoC.Tests {

    public interface IAsyncServiceAlias {
    }

    public sealed class NoCtorService {
        public NoCtorService(int value) {
            this.Value = value;
        }

        public int Value { get; private set; }
    }

    public abstract class AbstractDependency {
        public abstract string Name { get; }
    }

    public sealed class ConcreteDependency : AbstractDependency {
        public override string Name => "concrete";
    }

    public sealed class AbstractConsumer {
        [ServiceRequired]
        public AbstractConsumer(AbstractDependency dependency) {
            this.Dependency = dependency;
        }

        public AbstractDependency Dependency { get; private set; }
    }

    public sealed class ConcreteConsumer {
        [ServiceRequired]
        public ConcreteConsumer(ConcreteDependency dependency) {
            this.Dependency = dependency;
        }

        public ConcreteDependency Dependency { get; private set; }
    }

    public sealed class BoundConfigConsumer {
        [CustomServiceRequired]
        public BoundConfigConsumer(IGreeting greeting) {
            this.Greeting = greeting;
        }

        public IGreeting Greeting { get; private set; }
    }

    public interface IAsyncComponentAlias {
    }

    public sealed class ContextBinderConsumer {
        [ServiceRequired]
        public ContextBinderConsumer(IContextBinder<IContextService, ContextBinderConsumer> binder) {
            this.Binder = binder;
        }

        public IContextBinder<IContextService, ContextBinderConsumer> Binder { get; private set; }
    }

    public sealed class LazyContextConsumer {
        [ServiceRequired]
        public LazyContextConsumer(Saraff.IoC.Lazy<IContextBinder<IContextService, ContextConsumer>> lazy) {
            this.Lazy = lazy;
        }

        public Saraff.IoC.Lazy<IContextBinder<IContextService, ContextConsumer>> Lazy { get; private set; }
    }

    [ProxyRequired]
    public sealed class ContextProxyService : Component, IContextService {
        public string Name => "special";
    }

    public sealed class TwoArgumentRepository<T1, T2> : IRepository<T1> {
        public Type ItemType => typeof(T1);
    }
}
