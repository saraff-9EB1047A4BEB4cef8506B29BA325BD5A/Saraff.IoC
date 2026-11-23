using System;
using System.ComponentModel;
using System.Reflection;
using System.Threading;
using Saraff.IoC;

namespace Saraff.IoC.Tests {

    public interface IGreeting {
        string Greet();
    }

    public class GreetingService : Component, IGreeting {
        public bool DisposedFlag;

        public string Greet() => "hello";

        protected override void Dispose(bool disposing) {
            this.DisposedFlag = true;
            base.Dispose(disposing);
        }
    }

    public sealed class CountingGreetingService : Component, IGreeting {
        public static int CreatedCount;

        public CountingGreetingService() {
            Interlocked.Increment(ref CreatedCount);
        }

        public string Greet() => "hello";
    }

    public sealed class PlainGreetingService : IGreeting {
        public string Greet() => "hello";
    }

    public sealed class CtorConsumer {
        [ServiceRequired]
        public CtorConsumer(IGreeting greeting) {
            this.Greeting = greeting;
        }

        public IGreeting Greeting { get; private set; }
    }

    public sealed class PropertyConsumer {
        [ServiceRequired]
        public IGreeting Greeting { get; set; }
    }

    public sealed class LazyConsumer {
        [ServiceRequired]
        public LazyConsumer(Saraff.IoC.Lazy<IGreeting> greeting) {
            this.Greeting = greeting;
        }

        public Saraff.IoC.Lazy<IGreeting> Greeting { get; private set; }
    }

    public interface IContextService {
        string Name { get; }
    }

    public sealed class DefaultContextService : Component, IContextService {
        public string Name => "default";
    }

    public sealed class SpecialContextService : Component, IContextService {
        public string Name => "special";
    }

    public sealed class ContextConsumer {
        [ServiceRequired]
        public ContextConsumer(IContextService service) {
            this.Service = service;
        }

        public IContextService Service { get; private set; }
    }

    public sealed class OtherContextConsumer {
        [ServiceRequired]
        public OtherContextConsumer(IContextService service) {
            this.Service = service;
        }

        public IContextService Service { get; private set; }
    }

    public interface ICycleA {
    }

    public interface ICycleB {
    }

    public sealed class CycleA : Component, ICycleA {
        [ServiceRequired]
        public CycleA(ICycleB b) {
            this.B = b;
        }

        public ICycleB B { get; private set; }
    }

    public sealed class CycleB : Component, ICycleB {
        [ServiceRequired]
        public CycleB(ICycleA a) {
            this.A = a;
        }

        public ICycleA A { get; private set; }
    }

    public interface IDependency {
    }

    public sealed class DependencyService : Component, IDependency {
        public bool DisposedFlag;

        protected override void Dispose(bool disposing) {
            this.DisposedFlag = true;
            base.Dispose(disposing);
        }
    }

    public interface IProxyService {
        IDependency Injected { get; }

        string Execute(string input);

        string Fail();

        int FailValue();

        void Assign(out int value, ref string text);

        T Echo<T>(T value);

        void InjectDependency(IDependency dependency);

        void Touch();
    }

    [ProxyRequired]
    public sealed class ProxyService : Component, IProxyService {
        public int ExecuteCount;

        public bool Touched;

        public IDependency Injected { get; private set; }

        public string Execute(string input) {
            this.ExecuteCount++;
            return "impl:" + input;
        }

        public string Fail() {
            throw new InvalidOperationException("original");
        }

        public int FailValue() {
            throw new InvalidOperationException("original");
        }

        public void Assign(out int value, ref string text) {
            value = 42;
            text = text + "!";
        }

        public T Echo<T>(T value) => value;

        public void InjectDependency([CustomServiceRequired] IDependency dependency) {
            this.Injected = dependency;
        }

        public void Touch() {
            this.Touched = true;
        }
    }

    public interface IExplicitService {
        int GetValue();
    }

    [ProxyRequired]
    public sealed class ExplicitService : Component, IExplicitService {
        int IExplicitService.GetValue() => 7;
    }

    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Constructor | AttributeTargets.Parameter, AllowMultiple = false, Inherited = false)]
    public sealed class CustomServiceRequiredAttribute : Attribute {
    }

    public sealed class CustomConfiguration : Component, IConfiguration {
        public Type BindServiceAttributeType => null;

        public BindServiceCallback BindServiceCallback => null;

        public Type ServiceRequiredAttributeType => typeof(CustomServiceRequiredAttribute);

        public Type ContextBinderType => null;

        public Type ProxyRequiredAttributeType => null;

        public Type ListenerType => null;

        public InvokingCallback InvokingCallback => null;

        public InvokedCallback InvokedCallback => null;

        public CatchCallback CatchCallback => null;

        public Type LazyCallbackType => null;
    }

    public sealed class CustomConsumer {
        [CustomServiceRequired]
        public CustomConsumer(IGreeting greeting) {
            this.Greeting = greeting;
        }

        public IGreeting Greeting { get; private set; }
    }

    public sealed class RecordingListener : Component, IListener {
        public int InvokingCount;
        public int InvokedCount;
        public int CatchCount;

        public Func<MethodBase, object, object[], InvocationResult> InvokingHandler;
        public Func<MethodBase, object, object, InvocationResult> InvokedHandler;
        public Func<MethodBase, object, Exception, InvocationResult> CatchHandler;

        public InvocationResult OnInvoking(MethodBase method, object instance, object[] parameters) {
            this.InvokingCount++;
            return this.InvokingHandler?.Invoke(method, instance, parameters) ?? InvocationResult.NotHandled;
        }

        public InvocationResult OnInvoked(MethodBase method, object instance, object result) {
            this.InvokedCount++;
            return this.InvokedHandler?.Invoke(method, instance, result) ?? InvocationResult.NotHandled;
        }

        public InvocationResult OnCatch(MethodBase method, object instance, Exception ex) {
            this.CatchCount++;
            return this.CatchHandler?.Invoke(method, instance, ex) ?? InvocationResult.NotHandled;
        }
    }
}
