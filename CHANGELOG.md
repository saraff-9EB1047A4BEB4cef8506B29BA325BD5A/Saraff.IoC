# Changelog

## 2.0.0

### Added

- Registration API: `IServiceRegistry` (`Register`, `RegisterInstance`, factory overloads),
  `IServiceContainer` (`Resolve`, `ResolveAll`, `ResolveNamed`, `IsRegistered`, `CreateScope`).
- Lifetimes: `Singleton`, `Scoped`, `Transient`.
- Scopes (`IServiceScope`) and nested containers with proper disposal.
- Multiple registrations, named registrations, open generics, `IEnumerable<T>` resolution.
- POCO registrations without `System.ComponentModel.IComponent`.
- `ServiceContainerConfiguration` with explicit defaults and `UseConfiguration`.
- `CircularDependencyException` and `ServiceResolutionException`.
- Async initialization: `IAsyncInitializable`, `ResolveAsync`, `InitializeAsync`; services are
  initialized once, dependencies first, with retry after failure.
- `AsyncLazy<T>` helper for deferred asynchronous construction (also on `net40`).
- Async disposal: `IServiceContainer.DisposeAsync` and `IServiceScope.DisposeAsync`
  (`netstandard2.0`), with reverse-order `IAsyncDisposable` handling; `Component`-based
  `IAsyncDisposable` services are removed from the component collection and disposed asynchronously.
- `DisposeAsync()` waits for in-flight asynchronous initialization to complete.
- `Microsoft.Bcl.AsyncInterfaces` dependency for the `netstandard2.0` target.
- `IContainerCore` — нейтральный API ядра (`Resolve`, `Register`, `CreateNested`,
  `CreateInstance`), не зависящий от legacy-типов контейнера. Позволяет строить
  собственный публичный API (host-фасад) поверх контейнера.
- Функциональные группы конфигурации: `IContainerBindingConfiguration`,
  `IContainerInjectionConfiguration`, `IContainerInterceptionConfiguration`,
  `IContainerInvocationResultConfiguration`, `IContainerLifetimeConfiguration`,
  `IContainerRegistrationConfiguration`, `IContainerAsyncInitializationConfiguration`.
- `IServiceLifetime` / `IServiceLifetimeContext` и `IServiceLifetimeFactory` —
  подменяемые политики времени жизни; перегрузки `Register(..., IServiceLifetime)`.
- `IInvocationResult`, `InvokingResultCallback`, `InvokedResultCallback`,
  `CatchResultCallback` — собственный тип результата перехвата.
- `IServiceRegistration` / `IServiceRegistrationFactory` — подменяемое описание
  регистрации.
- `IAsyncInitializationAdapter` — подменяемая асинхронная инициализация
  (по умолчанию поддерживает `IAsyncInitializable`).

### Changed

- Single multi-target project `Saraff.IoC.csproj` (`net40;netstandard2.0`); `Saraff.IoCX.csproj`,
  `Saraff.IoC.nuspec` and `packages.config` removed. Package ID is `Saraff.IoC`.
- Versioning centralized in `Directory.Build.props`.
- `Dispose` is idempotent; resolution after disposal throws `ObjectDisposedException`.
- Resolution is thread-safe; cycle detection spans nested containers.
- Interception: fixed non-generic proxies, `out`/`ref` propagation, explicit interface
  implementations, generic methods and configurable `ServiceRequiredAttribute`.
- SourceLink 8.x and `System.Reflection.DispatchProxy` 4.8.2.
- `IConfiguration` стал агрегатом-маркером без собственных членов и наследует
  `IContainerBindingConfiguration`, `IContainerInjectionConfiguration`,
  `IContainerInterceptionConfiguration`. Существующие реализации с прежними
  публичными свойствами продолжают работать.
- Конфигурация читается через неизменяемый снимок `ContainerSettings`; изменения
  свойств конфигурации применяются после повторного `UseConfiguration`.
- `ServiceRegistration` больше не является публичным типом; ядро работает через
  `IServiceRegistration`.
- `ServiceContainer` реализует `IContainerCore`.

### Breaking

- Члены `IConfiguration` перемещены в базовые интерфейсы-группы; бинарная
  совместимость с предыдущими сборками нарушена.
- `ServiceRegistration.Lifetime` теперь `IServiceLifetime` (тип стал internal).

### Fixed

- `[ProxyRequired]` failed for any non-generic service (`GetGenericTypeDefinition` call).
- `DispatchProxy.Create` ambiguity on modern .NET runtimes.
- Proxied constructors no longer ignore the configured service-required attribute.
- Transient components are no longer silently retained against their owner's will.
- Synchronous `Dispose()` now reports async-only services instead of leaking them.
- `IAsyncDisposable` services are disposed asynchronously by `DisposeAsync()`.

## 1.0.15.745

- Thread-safety fixes, nested containers, packaging and Source Link updates.
