# Saraff.IoC

[![license: LGPL 3.0 or later](https://img.shields.io/badge/license-LGPL%203.0%20or%20later-blue?style=flat&logo=git)](LICENSE.md)
[![NuGet](https://img.shields.io/nuget/v/Saraff.IoC.svg?style=flat&logo=nuget)](https://www.nuget.org/packages/Saraff.IoC/)

Saraff.IoC is a lightweight dependency injection container for .NET. It helps you split an
application into loosely-coupled, highly-cohesive pieces and glue them back together in a
flexible manner.

Targets: `net40`, `netstandard2.0`.

Async initialization and `DisposeAsync` are available in the `netstandard2.0` assembly.

## Features

- Constructor and property injection via attributes (`[ServiceRequired]`).
- Registration API with `Singleton`, `Scoped` and `Transient` lifetimes.
- Scopes (`CreateScope`) and nested containers.
- Multiple registrations, named registrations, open generics and `IEnumerable<T>` resolution.
- Instance and factory registrations.
- Optional interception of `[ProxyRequired]` services through `IListener`
  (`DispatchProxy` on .NET Standard, `RealProxy` on .NET Framework).
- Context-aware bindings through `IContextBinder<TService, TContext>`.
- Async initialization (`IAsyncInitializable`, `ResolveAsync`, `InitializeAsync`) and
  async disposal (`DisposeAsync`) for the `netstandard2.0` target.
- `AsyncLazy<T>` for deferred asynchronous construction.
- Pluggable configuration (`IConfiguration`, `ServiceContainerConfiguration`) split into
  functional groups.
- `IContainerCore` for building a custom public API without exposing the legacy container types.
- Thread-safe resolution and cycle detection with a readable trace.

## Installation

```powershell
dotnet add package Saraff.IoC
```

## Quick start

```csharp
using Saraff.IoC;

public interface IGreeting {
    string Greet();
}

public sealed class Greeting : IGreeting {
    public string Greet() => "hello";
}

using (var container = new ServiceContainer()) {
    container.Register<IGreeting, Greeting>();
    var greeting = container.Resolve<IGreeting>();
    Console.WriteLine(greeting.Greet()); // hello
}
```

## Lifetimes and scopes

```csharp
using (var container = new ServiceContainer()) {
    container.Register<ISingleton, Singleton>(ServiceLifetime.Singleton);
    container.Register<IScoped, Scoped>(ServiceLifetime.Scoped);
    container.Register<ITransient, Transient>(ServiceLifetime.Transient);

    using (var scope = container.CreateScope()) {
        var scoped1 = scope.Resolve<IScoped>();
        var scoped2 = scope.Resolve<IScoped>(); // same instance
        var transient1 = scope.Resolve<ITransient>();
        var transient2 = scope.Resolve<ITransient>(); // different instances
    }
}
```

`Singleton` instances live in the container where the registration was made, `Scoped`
instances live in the scope, and `Transient` instances are created per resolution.
`IDisposable` services (including `IComponent` implementations) are disposed with their owner.

## Registration

```csharp
container.Register<IFoo, Foo>();                                  // singleton
container.Register<IFoo, Foo>(ServiceLifetime.Transient);
container.Register(typeof(IRepository<>), typeof(Repository<>));    // open generic
container.RegisterInstance<IConfiguration>(configuration);         // external instance
container.Register<IPlugin>(sp => new Plugin(sp.Resolve<ILogger>()));

var plugins = container.ResolveAll<IPlugin>();
var named = container.ResolveNamed<IPlugin>("primary");
var all = container.Resolve<IEnumerable<IPlugin>>();
```

Registration does not require `System.ComponentModel.IComponent`; plain POCOs are supported.

## Async initialization and disposal

Services that require asynchronous start implement `IAsyncInitializable`; services that hold
asynchronous resources implement `IAsyncDisposable`.

```csharp
public sealed class Database : IDatabase, IAsyncInitializable, IAsyncDisposable {
    public Task InitializeAsync(CancellationToken cancellationToken) => this.ConnectAsync(cancellationToken);
    public ValueTask DisposeAsync() => this.DisconnectAsync();
}

using (var container = new ServiceContainer()) {
    container.Register<IDatabase, Database>(ServiceLifetime.Scoped);

    using (var scope = container.CreateScope()) {
        var database = await scope.ResolveAsync<IDatabase>(); // create + initialize
        // ...
        await scope.DisposeAsync();                          // awaits IAsyncDisposable
    }
}
```

- `ResolveAsync<T>` resolves synchronously, then initializes every created-but-uninitialized
  service (dependencies first, in creation order). `InitializeAsync()` does the same for
  services created earlier with synchronous `Resolve`.
- Initialization runs once per instance; a failed initialization can be retried.
- `DisposeAsync()` waits for in-flight initialization before disposing resources.
- `AsyncLazy<T>` defers asynchronous construction and runs its factory once:

```csharp
container.RegisterInstance(new AsyncLazy<IConnection>(ct => ConnectAsync(ct)));
var lazy = container.Resolve<AsyncLazy<IConnection>>();
var connection = await lazy.Value;
```

Compatibility notes:

- Async initialization/disposal APIs are available in the `netstandard2.0` assembly;
  the `net40` assembly is synchronous (`AsyncLazy<T>` is still available there).
- `DisposeAsync()` disposes `IAsyncDisposable` services, including `Component`-based ones
  (they are removed from the component collection before asynchronous disposal).
  Synchronous `Dispose()` keeps using synchronous disposal.
- Services registered through `RegisterInstance` are not initialized automatically.
- Synchronous `Dispose()` throws `InvalidOperationException` if an async-only service is tracked.

## Injection attributes

```csharp
public sealed class Consumer {
    [ServiceRequired]
    public Consumer(IGreeting greeting) { ... }

    [ServiceRequired]
    public ILogger Logger { get; set; }
}
```

The constructor marked with `[ServiceRequired]` is selected; parameters are resolved by type.
Otherwise the parameterless constructor is used.

## Legacy API

The original API is preserved:

```csharp
container.Bind<IGreeting, Greeting>();           // singleton, requires IComponent
container.Bind<IGreeting>(greetingInstance);
container.Load(typeof(Program).Assembly);        // [assembly: BindService(...)]

var consumer = container.CreateInstance<Consumer>();
using (var child = container.CreateNestedContainer()) { ... }
```

## Interception

```csharp
[ProxyRequired]
public sealed class Greeting : Component, IGreeting {
    public string Greet() => "hello";
}

public sealed class LoggingListener : Component, IListener {
    public InvocationResult OnInvoking(MethodBase method, object instance, object[] parameters) => InvocationResult.NotHandled;
    public InvocationResult OnInvoked(MethodBase method, object instance, object result) => InvocationResult.NotHandled;
    public InvocationResult OnCatch(MethodBase method, object instance, Exception ex) => InvocationResult.NotHandled;
}

container.Bind<IListener>(listener);
container.Register<IListener, LoggingListener>();
container.Register<IGreeting, Greeting>();
```

`IListener` callbacks return `InvocationResult`: `NotHandled` continues the normal pipeline,
`FromResult(value)` takes control of the call. `OnInvoking` can short-circuit a call
(including `void` methods and `null` results), `OnInvoked` can replace a result with `null`,
and `OnCatch` can map an exception (`FromResult(exception)`) or suppress it (`FromResult(null)`).
Methods may also receive dependencies through `[ServiceRequired]` parameters when a `null`
argument is passed.

## Configuration

```csharp
container.UseConfiguration(new ServiceContainerConfiguration {
    ServiceRequiredAttributeType = typeof(MyServiceRequiredAttribute),
    ListenerType = typeof(IMyListener)
});
```

`IConfiguration` is an aggregate marker without members of its own. Settings live in
functional groups: `IContainerBindingConfiguration`, `IContainerInjectionConfiguration`,
`IContainerInterceptionConfiguration`, `IContainerInvocationResultConfiguration`,
`IContainerLifetimeConfiguration`, `IContainerRegistrationConfiguration` and
`IContainerAsyncInitializationConfiguration`. An object may implement only the groups it
needs; missing groups fall back to defaults. `ServiceContainerConfiguration` implements all
of them.

Configuration is captured as an immutable snapshot on first use. Change the configuration
object and call `UseConfiguration` again to apply the changes; mutations after a snapshot
are not observed.

`IContainerInvocationResultConfiguration` callbacks take precedence over the legacy
`InvokingCallback`/`InvokedCallback`/`CatchCallback`; when result callbacks are not set,
the legacy callbacks are adapted automatically.

## Custom public API

`IContainerCore` exposes the neutral core API (`Type`/`object`, `IServiceLifetime`,
`IServiceRegistration`, `IInvocationResult`, `IAsyncInitializationAdapter`) and does not
require `IServiceContainer`, `IServiceRegistry`, `ServiceLifetime`, `ServiceRegistration`,
`InvocationResult` or `IAsyncInitializable`. A framework can wrap it with its own types:

```csharp
public interface IMyContainer {
    T Resolve<T>();
}

public sealed class MyContainer : IMyContainer {
    private readonly IContainerCore _core;

    public MyContainer(IContainerCore core) => this._core = core;

    public T Resolve<T>() => (T)this._core.Resolve(typeof(T));
}

var container = new ServiceContainer();
var api = new MyContainer(container);
```

Custom lifetimes, registration descriptors, async-initialization adapters and invocation
results are supplied through the corresponding configuration groups.

## Migration from Saraff.IoCX

`Saraff.IoCX` (netstandard2.0) is superseded by `Saraff.IoC` 2.x, which ships both
`net40` and `netstandard2.0` assemblies. Replace the package reference:

```powershell
dotnet remove package Saraff.IoCX
dotnet add package Saraff.IoC
```

The public namespaces and the `ServiceContainer` type are unchanged.

## License

LGPL-3.0-or-later. See [LICENSE.md](LICENSE.md).
