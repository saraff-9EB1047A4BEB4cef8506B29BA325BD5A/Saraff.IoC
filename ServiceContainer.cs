/* Этот файл является частью библиотеки Saraff.IoC
 * © SARAFF SOFTWARE (Кирножицкий Андрей), 2016.
 * Saraff.IoC - свободная программа: вы можете перераспространять ее и/или
 * изменять ее на условиях Меньшей Стандартной общественной лицензии GNU в том виде,
 * в каком она была опубликована Фондом свободного программного обеспечения;
 * либо версии 3 лицензии, либо (по вашему выбору) любой более поздней
 * версии.
 * Saraff.IoC распространяется в надежде, что она будет полезной,
 * но БЕЗО ВСЯКИХ ГАРАНТИЙ; даже без неявной гарантии ТОВАРНОГО ВИДА
 * или ПРИГОДНОСТИ ДЛЯ ОПРЕДЕЛЕННЫХ ЦЕЛЕЙ. Подробнее см. в Меньшей Стандартной
 * общественной лицензии GNU.
 * Вы должны были получить копию Меньшей Стандартной общественной лицензии GNU
 * вместе с этой программой. Если это не так, см.
 * <http://www.gnu.org/licenses/>.)
 * 
 * This file is part of Saraff.IoC.
 * © SARAFF SOFTWARE (Kirnazhytski Andrei), 2016.
 * Saraff.IoC is free software: you can redistribute it and/or modify
 * it under the terms of the GNU Lesser General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 * Saraff.IoC is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
 * GNU Lesser General Public License for more details.
 * You should have received a copy of the GNU Lesser General Public License
 * along with Saraff.IoC. If not, see <http://www.gnu.org/licenses/>.
 */
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
#if !NETSTANDARD
using System.Runtime.Remoting.Messaging;
using System.Runtime.Remoting.Proxies;
using System.Security.Permissions;
#endif

namespace Saraff.IoC {

    /// <summary>
    /// IoC-контейнер.
    /// </summary>
    /// <remarks>
    /// Публичные операции потокобезопасны. Сервисы, связанные через <see cref="Bind(Type, Type)"/>,
    /// создаются один раз и кэшируются в контейнере (singleton в пределах контейнера и его потомков).
    /// Экземпляры, переданные через <see cref="Bind(Type, object)"/>, контейнером не освобождаются.
    /// </remarks>
    /// <seealso cref="System.ComponentModel.Container" />
    /// <seealso cref="System.IServiceProvider" />
    public class ServiceContainer : Container, IServiceProvider, IServiceContainer, IServiceRegistry, IContainerCore {
        private static readonly Dictionary<string, object> _emptyArguments = new Dictionary<string, object>();
#if NETSTANDARD
        private static readonly AsyncLocal<ResolutionFrame> _resolutionPath = new AsyncLocal<ResolutionFrame>();
#else
        [ThreadStatic]
        private static ResolutionFrame _threadResolutionPath;
#endif
        // Единая точка синхронизации контейнера. Порядок захвата: потомок -> родитель.
        // Dispose освобождает блокировку до освобождения вложенных контейнеров,
        // чтобы исключить инверсию порядка блокировок.
        private readonly object _sync = new object();
        private readonly ConcurrentDictionary<Type, object> _instances = new ConcurrentDictionary<Type, object>();
        private readonly ConcurrentDictionary<Type, ConcurrentDictionary<Type, TypePlan>> _plans = new ConcurrentDictionary<Type, ConcurrentDictionary<Type, TypePlan>>();
        private readonly ConcurrentDictionary<ServiceCacheKey, object> _singletons = new ConcurrentDictionary<ServiceCacheKey, object>();
        private readonly ConcurrentDictionary<ServiceCacheKey, object> _scoped = new ConcurrentDictionary<ServiceCacheKey, object>();
        private ComponentTracker _tracked = new ComponentTracker();
#if NETSTANDARD
        private readonly List<AsyncInitializationEntry> _pendingInitializations = new List<AsyncInitializationEntry>();
        private List<IComponent> _asyncComponents = new List<IComponent>();
        private Task[] _pendingInitializationsAtDispose;
        private List<IComponent> _asyncComponentsAtDispose;
#endif
        private volatile Dictionary<Type, Type> _binding = new Dictionary<Type, Type>();
        private volatile Dictionary<Type, IServiceRegistration[]> _registrations = new Dictionary<Type, IServiceRegistration[]>();
        private IConfiguration _config = null;
        private volatile ContainerSettings _settings;
        private List<ServiceContainer> _nested = new List<ServiceContainer>();
        private volatile bool _disposed;

        public ServiceContainer() {
        }

        public ServiceContainer(IConfiguration configuration) {
            this._config = configuration;
        }

        public ServiceContainer Parent { get; private set; }

        IContainerCore IContainerCore.Parent => this.Parent;

        IContainerCore IContainerCore.CreateNested() => this.CreateNestedContainer();

        object IContainerCore.CreateInstance(Type type, bool trackComponents, IDictionary<string, object> args) {
            this.ThrowIfDisposed();
            if(type == null) {
                throw new ArgumentNullException(nameof(type));
            }
            lock(this._sync) {
                this.ThrowIfDisposed();
                return this._CreateInstanceCore(type, args, trackComponents);
            }
        }

        private static ResolutionFrame ResolutionPath {
            get {
#if NETSTANDARD
                return _resolutionPath.Value;
#else
                return _threadResolutionPath;
#endif
            }
            set {
#if NETSTANDARD
                _resolutionPath.Value = value;
#else
                _threadResolutionPath = value;
#endif
            }
        }

        public ServiceContainer CreateNestedContainer() {
            this.ThrowIfDisposed();
            lock(this._sync) {
                this.ThrowIfDisposed();
                var _container = new ServiceContainer { Parent = this, _config = this._config };
                this._nested.Add(_container);
                return _container;
            }
        }

        /// <summary>
        /// Выполняет загрузку привязок из указанной сборки.
        /// </summary>
        /// <param name="assembly">The assembly.</param>
        public void Load(Assembly assembly) {
            this.ThrowIfDisposed();
            if(assembly == null) {
                throw new ArgumentNullException(nameof(assembly));
            }
            lock(this._sync) {
                this.ThrowIfDisposed();
                foreach(Attribute _attr in assembly.GetCustomAttributes(this.BindServiceAttribute, false)) {
                    this.BindServiceCallback(_attr, this.Bind);
                }
            }
        }

        /// <summary>
        /// Binds the specified service.
        /// </summary>
        /// <param name="service">The service.</param>
        /// <param name="obj">The object.</param>
        /// <exception cref="ArgumentNullException">Отсутствует информация о связываемых типах.</exception>
        /// <exception cref="InvalidOperationException">
        /// </exception>
        public void Bind(Type service, Type obj) {
            this.ThrowIfDisposed();
            lock(this._sync) {
                this.ThrowIfDisposed();
                if(service == null || obj == null) {
                    throw new ArgumentNullException("Отсутствует информация о связываемых типах.");
                }
                if(!service.IsInterface) {
                    throw new InvalidOperationException(string.Format("Тип \"{0}\" не является интерфейсом.", service.FullName));
                }
                if(obj.GetInterface(typeof(IComponent).FullName) == null) {
                    throw new InvalidOperationException(string.Format("Тип \"{0}\" не является производным от \"{1}\".", obj.FullName, typeof(IComponent).FullName));
                }
                var _binding = new Dictionary<Type, Type>(this._binding);
                _binding.Add(service, obj);
                this._binding = _binding;
                this.InvalidateSettingsFor(service);
            }
        }

        /// <summary>
        /// Binds the specified service.
        /// </summary>
        /// <typeparam name="TService">The type of the service.</typeparam>
        /// <typeparam name="T">Type of object.</typeparam>
        public void Bind<TService, T>() => this.Bind(typeof(TService), typeof(T));

        /// <summary>
        /// Binds the specified service.
        /// </summary>
        /// <param name="service">The service.</param>
        /// <param name="obj">The object.</param>
        public void Bind(Type service, object obj) {
            this.ThrowIfDisposed();
            if(obj == null) {
                throw new ArgumentNullException(nameof(obj));
            }
            lock(this._sync) {
                this.Bind(service, obj.GetType());
                if(this._instances.ContainsKey(service)) {
                    throw new ArgumentException(string.Format("Сервис \"{0}\" уже связан с экземпляром.", service.FullName));
                }
                this._AddInstance(service, obj);
                this.InvalidateSettingsFor(service);
            }
        }

        /// <summary>
        /// Binds the specified service.
        /// </summary>
        /// <typeparam name="TService">The type of the service.</typeparam>
        /// <param name="obj">The object.</param>
        public void Bind<TService>(object obj) => this.Bind(typeof(TService), obj);

        /// <summary>
        /// Регистрирует сервис с указанной реализацией и временем жизни.
        /// </summary>
        public void Register(Type serviceType, Type implementationType, ServiceLifetime lifetime = ServiceLifetime.Singleton, string name = null) {
            this.Register(serviceType, implementationType, this.CreateLifetime(lifetime), name);
        }

        /// <summary>
        /// Регистрирует сервис с указанной реализацией и политикой времени жизни.
        /// </summary>
        public void Register(Type serviceType, Type implementationType, IServiceLifetime lifetime, string name = null) {
            this.ThrowIfDisposed();
            if(serviceType == null) {
                throw new ArgumentNullException(nameof(serviceType));
            }
            if(implementationType == null) {
                throw new ArgumentNullException(nameof(implementationType));
            }
            if(lifetime == null) {
                throw new ArgumentNullException(nameof(lifetime));
            }
            ValidateRegistration(serviceType, implementationType);
            this.AddRegistration(this.CreateRegistration(serviceType, implementationType, null, null, lifetime, name));
        }

        /// <summary>
        /// Регистрирует сервис с указанной реализацией и временем жизни.
        /// </summary>
        public void Register<TService, TImplementation>(ServiceLifetime lifetime = ServiceLifetime.Singleton, string name = null) where TImplementation : TService {
            this.Register(typeof(TService), typeof(TImplementation), lifetime, name);
        }

        /// <summary>
        /// Регистрирует сервис с указанной реализацией и политикой времени жизни.
        /// </summary>
        public void Register<TService, TImplementation>(IServiceLifetime lifetime, string name = null) where TImplementation : TService {
            this.Register(typeof(TService), typeof(TImplementation), lifetime, name);
        }

        /// <summary>
        /// Регистрирует фабрику сервиса.
        /// </summary>
        public void Register(Type serviceType, Func<IServiceProvider, object> factory, ServiceLifetime lifetime = ServiceLifetime.Singleton, string name = null) {
            this.Register(serviceType, factory, this.CreateLifetime(lifetime), name);
        }

        /// <summary>
        /// Регистрирует фабрику сервиса.
        /// </summary>
        public void Register(Type serviceType, Func<IServiceProvider, object> factory, IServiceLifetime lifetime, string name = null) {
            this.ThrowIfDisposed();
            if(serviceType == null) {
                throw new ArgumentNullException(nameof(serviceType));
            }
            if(factory == null) {
                throw new ArgumentNullException(nameof(factory));
            }
            if(lifetime == null) {
                throw new ArgumentNullException(nameof(lifetime));
            }
            this.AddRegistration(this.CreateRegistration(serviceType, null, null, factory, lifetime, name));
        }

        /// <summary>
        /// Регистрирует фабрику сервиса.
        /// </summary>
        public void Register<TService>(Func<IServiceProvider, TService> factory, ServiceLifetime lifetime = ServiceLifetime.Singleton, string name = null) {
            this.Register(factory, this.CreateLifetime(lifetime), name);
        }

        /// <summary>
        /// Регистрирует фабрику сервиса.
        /// </summary>
        public void Register<TService>(Func<IServiceProvider, TService> factory, IServiceLifetime lifetime, string name = null) {
            if(factory == null) {
                throw new ArgumentNullException(nameof(factory));
            }
            this.Register(typeof(TService), provider => (object)factory(provider), lifetime, name);
        }

        /// <summary>
        /// Регистрирует готовый экземпляр сервиса. Экземпляр не освобождается контейнером.
        /// </summary>
        public void RegisterInstance(Type serviceType, object instance, string name = null) {
            this.ThrowIfDisposed();
            if(serviceType == null) {
                throw new ArgumentNullException(nameof(serviceType));
            }
            if(instance == null) {
                throw new ArgumentNullException(nameof(instance));
            }
            if(!serviceType.IsInstanceOfType(instance)) {
                throw new ArgumentException(string.Format("Экземпляр типа \"{0}\" не является экземпляром сервиса \"{1}\".", instance.GetType().FullName, serviceType.FullName));
            }
            this.AddRegistration(this.CreateRegistration(serviceType, instance.GetType(), instance, null, ServiceLifetimes.Singleton, name));
        }

        /// <summary>
        /// Регистрирует готовый экземпляр сервиса. Экземпляр не освобождается контейнером.
        /// </summary>
        public void RegisterInstance<TService>(TService instance, string name = null) => this.RegisterInstance(typeof(TService), instance, name);

        /// <summary>
        /// Создаёт область (scope) с собственными scoped-экземплярами.
        /// </summary>
        public IServiceScope CreateScope() => new ServiceScope(this.CreateNestedContainer());

        /// <summary>
        /// Разрешает сервис или возвращает <c>null</c>, если сервис не зарегистрирован.
        /// </summary>
        public object Resolve(Type serviceType) {
            this.ThrowIfDisposed();
            if(serviceType == null) {
                throw new ArgumentNullException(nameof(serviceType));
            }
            if(serviceType.IsGenericType && serviceType.GetGenericTypeDefinition() == typeof(IEnumerable<>)) {
                return this.ResolveAllArray(serviceType.GetGenericArguments()[0]);
            }
            IServiceRegistration _registration;
            ServiceContainer _owner;
            if(this.TryFindRegistration(serviceType, null, out _registration, out _owner)) {
                return this.ResolveRegistration(_registration, serviceType, _owner);
            }
            return this.GetService(serviceType);
        }

        /// <summary>
        /// Разрешает сервис или возвращает <c>null</c>, если сервис не зарегистрирован.
        /// </summary>
        public T Resolve<T>() => (T)this.Resolve(typeof(T));

        /// <summary>
        /// Разрешает все регистрации указанного сервиса.
        /// </summary>
        public IEnumerable<T> ResolveAll<T>() => (IEnumerable<T>)this.ResolveAllArray(typeof(T));

        /// <summary>
        /// Разрешает именованную регистрацию сервиса.
        /// </summary>
        public object ResolveNamed(Type serviceType, string name) {
            this.ThrowIfDisposed();
            if(serviceType == null) {
                throw new ArgumentNullException(nameof(serviceType));
            }
            if(name == null) {
                throw new ArgumentNullException(nameof(name));
            }
            IServiceRegistration _registration;
            ServiceContainer _owner;
            return this.TryFindRegistration(serviceType, name, out _registration, out _owner) ? this.ResolveRegistration(_registration, serviceType, _owner) : null;
        }

        /// <summary>
        /// Разрешает именованную регистрацию сервиса.
        /// </summary>
        public T ResolveNamed<T>(string name) => (T)this.ResolveNamed(typeof(T), name);

        /// <summary>
        /// Проверяет наличие регистрации сервиса.
        /// </summary>
        public bool IsRegistered(Type serviceType) {
            this.ThrowIfDisposed();
            if(serviceType == null) {
                throw new ArgumentNullException(nameof(serviceType));
            }
            IServiceRegistration _registration;
            ServiceContainer _owner;
            if(this.TryFindRegistration(serviceType, null, out _registration, out _owner)) {
                return true;
            }
            for(var _container = this; _container != null; _container = _container.Parent) {
                if(_container._binding.ContainsKey(serviceType)) {
                    return true;
                }
            }
            return false;
        }

#if NETSTANDARD

        /// <summary>
        /// Разрешает сервис и дожидается асинхронной инициализации созданных сервисов.
        /// </summary>
        public async Task<object> ResolveAsync(Type serviceType, CancellationToken cancellationToken = default(CancellationToken)) {
            this.ThrowIfDisposed();
            var _instance = this.Resolve(serviceType);
            if(_instance == null) {
                return null;
            }
            await this.InitializePendingAsync(cancellationToken).ConfigureAwait(false);
            return _instance;
        }

        /// <summary>
        /// Разрешает сервис и дожидается асинхронной инициализации созданных сервисов.
        /// </summary>
        public async Task<T> ResolveAsync<T>(CancellationToken cancellationToken = default(CancellationToken)) {
            return (T)await this.ResolveAsync(typeof(T), cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Инициализирует все созданные, но ещё не инициализированные сервисы.
        /// </summary>
        public Task InitializeAsync(CancellationToken cancellationToken = default(CancellationToken)) {
            this.ThrowIfDisposed();
            return this.InitializePendingAsync(cancellationToken);
        }

        private async Task InitializePendingAsync(CancellationToken cancellationToken) {
            var _chain = new List<ServiceContainer>();
            for(var _container = this; _container != null; _container = _container.Parent) {
                _chain.Insert(0, _container);
            }
            foreach(var _container in _chain) {
                await _container.InitializeOwnAsync(cancellationToken).ConfigureAwait(false);
            }
        }

        private async Task InitializeOwnAsync(CancellationToken cancellationToken) {
            while(true) {
                AsyncInitializationEntry _entry;
                lock(this._sync) {
                    if(this._pendingInitializations.Count == 0) {
                        return;
                    }
                    _entry = this._pendingInitializations[0];
                }
                var _task = _entry.InitializeAsync(cancellationToken);
                try {
                    await _task.ConfigureAwait(false);
                } catch {
                    _entry.Reset(_task);
                    throw;
                }
                lock(this._sync) {
                    this._pendingInitializations.Remove(_entry);
                }
            }
        }
#endif

        private void AddRegistration(IServiceRegistration registration) {
            lock(this._sync) {
                this.ThrowIfDisposed();
                var _copy = new Dictionary<Type, IServiceRegistration[]>(this._registrations);
                IServiceRegistration[] _existing;
                if(_copy.TryGetValue(registration.ServiceType, out _existing)) {
                    var _extended = new IServiceRegistration[_existing.Length + 1];
                    Array.Copy(_existing, _extended, _existing.Length);
                    _extended[_existing.Length] = registration;
                    _copy[registration.ServiceType] = _extended;
                } else {
                    _copy[registration.ServiceType] = new[] { registration };
                }
                this._registrations = _copy;
                if(registration.ServiceType == typeof(IConfiguration)) {
                    this._settings = null;
                }
            }
        }

        private IServiceRegistration CreateRegistration(Type serviceType, Type implementationType, object instance, Func<IServiceProvider, object> factory, IServiceLifetime lifetime, string name) {
            var _factory = this.Settings.ServiceRegistrationFactory;
            if(_factory != null) {
                var _registration = _factory.Create(serviceType, implementationType, instance, factory, lifetime, name);
                if(_registration != null) {
                    return _registration;
                }
            }
            return new ServiceRegistration(serviceType, implementationType, instance, factory, lifetime, name);
        }

        private IServiceLifetime CreateLifetime(ServiceLifetime lifetime) {
            var _factory = this.Settings.ServiceLifetimeFactory;
            return _factory?.Create(lifetime) ?? ServiceLifetimes.Singleton;
        }

        private void ValidateRegistration(Type serviceType, Type implementationType) {
            if(serviceType.IsGenericTypeDefinition || implementationType.IsGenericTypeDefinition) {
                if(!serviceType.IsGenericTypeDefinition || !implementationType.IsGenericTypeDefinition) {
                    throw new ArgumentException("Тип сервиса и тип реализации должны быть либо оба открытыми обобщёнными, либо оба закрытыми.");
                }
                if(serviceType.GetGenericArguments().Length != implementationType.GetGenericArguments().Length) {
                    throw new ArgumentException("Количество обобщённых параметров сервиса и реализации не совпадает.");
                }
                return;
            }
            if(serviceType.IsGenericType && serviceType.GetGenericTypeDefinition() == this.ContextBinder) {
                // IContextBinder<,> — маркерный ключ контекстной привязки, реализация не обязана его реализовывать.
                return;
            }
            if(!serviceType.IsAssignableFrom(implementationType)) {
                throw new ArgumentException(string.Format("Тип \"{0}\" не реализует \"{1}\".", implementationType.FullName, serviceType.FullName));
            }
        }

        private bool TryFindRegistration(Type serviceType, string name, out IServiceRegistration registration, out ServiceContainer owner) {
            for(var _container = this; _container != null; _container = _container.Parent) {
                var _registrations = _container._registrations;
                IServiceRegistration[] _candidates;
                if(_registrations.TryGetValue(serviceType, out _candidates)) {
                    var _candidate = SelectRegistration(_candidates, name);
                    if(_candidate != null) {
                        registration = _candidate;
                        owner = _container;
                        return true;
                    }
                }
                if(serviceType.IsGenericType) {
                    var _definition = serviceType.GetGenericTypeDefinition();
                    if(_definition != serviceType && _registrations.TryGetValue(_definition, out _candidates)) {
                        var _candidate = SelectRegistration(_candidates, name);
                        if(_candidate != null) {
                            registration = _candidate;
                            owner = _container;
                            return true;
                        }
                    }
                }
            }
            registration = null;
            owner = null;
            return false;
        }

        private static IServiceRegistration SelectRegistration(IServiceRegistration[] candidates, string name) {
            for(var i = candidates.Length - 1; i >= 0; i--) {
                if(string.Equals(candidates[i].Name, name, StringComparison.Ordinal)) {
                    return candidates[i];
                }
            }
            return null;
        }

        private object ResolveRegistration(IServiceRegistration registration, Type requestedServiceType, ServiceContainer owner) {
            var _key = new ServiceCacheKey(requestedServiceType, registration.Name);
            var _context = new ServiceLifetimeContext(owner, this, registration, requestedServiceType, _key);
            var _lifetime = registration.Lifetime ?? ServiceLifetimes.Singleton;
            return _lifetime.Resolve(_context);
        }

        private static object GetOrCreate(ServiceContainer owner, ConcurrentDictionary<ServiceCacheKey, object> cache, ServiceCacheKey key, Func<object> factory) {
            object _value;
            if(cache.TryGetValue(key, out _value)) {
                return _value;
            }
            lock(owner._sync) {
                if(cache.TryGetValue(key, out _value)) {
                    return _value;
                }
                _value = factory();
                if(_value != null) {
                    cache[key] = _value;
                }
                return _value;
            }
        }

        private object CreateFromRegistration(IServiceRegistration registration, Type requestedServiceType, ServiceContainer context) {
            if(registration.Instance != null) {
                return context.CreateProxyIfRequired(requestedServiceType, registration.Instance);
            }
            if(registration.Factory != null) {
                var _value = registration.Factory(context);
                if(_value == null) {
                    return null;
                }
                context.TrackInstance(_value);
                return context.CreateProxyIfRequired(requestedServiceType, _value);
            }
            var _instance = context._CreateInstanceCore(registration.GetImplementationType(requestedServiceType), _emptyArguments, true);
            return context.CreateProxyIfRequired(requestedServiceType, _instance);
        }

        private object ResolveAllArray(Type elementType) {
            var _values = new List<object>();
            for(var _container = this; _container != null; _container = _container.Parent) {
                var _registrations = _container._registrations;
                IServiceRegistration[] _candidates;
                if(_registrations.TryGetValue(elementType, out _candidates)) {
                    foreach(var _registration in _candidates) {
                        _values.Add(this.ResolveRegistration(_registration, elementType, _container));
                    }
                }
                if(elementType.IsGenericType) {
                    var _definition = elementType.GetGenericTypeDefinition();
                    if(_definition != elementType && _registrations.TryGetValue(_definition, out _candidates)) {
                        foreach(var _registration in _candidates) {
                            _values.Add(this.ResolveRegistration(_registration, elementType, _container));
                        }
                    }
                }
            }
            var _array = Array.CreateInstance(elementType, _values.Count);
            for(var i = 0; i < _values.Count; i++) {
                _array.SetValue(_values[i], i);
            }
            return _array;
        }

        private void TrackInstance(object instance) {
            lock(this._sync) {
                this.ThrowIfDisposed();
                if(instance is IComponent _component) {
                    var _alreadyAdded = false;
                    foreach(IComponent _existingComponent in this.Components) {
                        if(ReferenceEquals(_existingComponent, _component)) {
                            _alreadyAdded = true;
                            break;
                        }
                    }
                    if(!_alreadyAdded) {
                        this.Add(_component);
                    }
#if NETSTANDARD
                    if(instance is IAsyncDisposable) {
                        var _trackedComponent = false;
                        foreach(var _existing in this._asyncComponents) {
                            if(ReferenceEquals(_existing, instance)) {
                                _trackedComponent = true;
                                break;
                            }
                        }
                        if(!_trackedComponent) {
                            this._asyncComponents.Add(_component);
                        }
                    }
#endif
                } else if(instance is IDisposable _disposable) {
                    this._tracked.Track(_disposable);
                }
#if NETSTANDARD
                else if(instance is IAsyncDisposable _asyncDisposable) {
                    this._tracked.Track(_asyncDisposable);
                }
                var _asyncInitialization = this.Settings.AsyncInitializationAdapter;
                if(_asyncInitialization != null && _asyncInitialization.CanInitialize(instance)) {
                    var _known = false;
                    foreach(var _pending in this._pendingInitializations) {
                        if(ReferenceEquals(_pending.Instance, instance)) {
                            _known = true;
                            break;
                        }
                    }
                    if(!_known) {
                        this._pendingInitializations.Add(new AsyncInitializationEntry(instance, _asyncInitialization));
                    }
                }
#endif
            }
        }

        /// <summary>
        /// Создает экземпляр указанного типа и осуществляет внедрение зависимостей.
        /// </summary>
        /// <typeparam name="T">Тип.</typeparam>
        /// <returns>Экземпляр указанного типа.</returns>
        public T CreateInstance<T>(params CtorCallback[] args) where T : class => this.CreateInstance(typeof(T), true, args) as T;

        /// <summary>
        /// Создает экземпляр указанного типа и осуществляет внедрение зависимостей.
        /// </summary>
        /// <typeparam name="T">Тип.</typeparam>
        /// <param name="trackComponents">Определяет, должен ли созданный <see cref="IComponent"/> быть зарегистрирован в контейнере.</param>
        /// <param name="args">Аргументы конструктора.</param>
        /// <returns>Экземпляр указанного типа.</returns>
        public T CreateInstance<T>(bool trackComponents, params CtorCallback[] args) where T : class => this.CreateInstance(typeof(T), trackComponents, args) as T;

        /// <summary>
        /// Создает экземпляр указанного типа и осуществляет внедрение зависимостей.
        /// </summary>
        /// <param name="type">Тип.</param>
        /// <param name="args">Аргументы конструктора.</param>
        /// <returns>Экземпляр указанного типа.</returns>
        public object CreateInstance(Type type, params CtorCallback[] args) => this.CreateInstance(type, true, args);

        /// <summary>
        /// Создает экземпляр указанного типа и осуществляет внедрение зависимостей.
        /// </summary>
        /// <param name="type">Тип.</param>
        /// <param name="trackComponents">Определяет, должен ли созданный <see cref="IComponent"/> быть зарегистрирован в контейнере.</param>
        /// <param name="args">Аргументы конструктора.</param>
        /// <returns>Экземпляр указанного типа.</returns>
        public object CreateInstance(Type type, bool trackComponents, params CtorCallback[] args) {
            this.ThrowIfDisposed();
            if(type == null) {
                throw new ArgumentNullException(nameof(type));
            }
            lock(this._sync) {
                this.ThrowIfDisposed();
                var _args = new Dictionary<string, object>();
                if(args != null) {
                    foreach(var _arg in args) {
                        if(_arg != null) {
                            _arg((name, val) => _args.Add(name, val));
                        }
                    }
                }
                return this._CreateInstanceCore(type, _args, trackComponents);
            }
        }

        private object _CreateInstanceCore(Type type, IDictionary<string, object> ctorArgs, bool trackComponents) {
            this.ThrowIfDisposed();
            this.EnsureNoCycle(type);
            var _frame = ResolutionPath;
            ResolutionPath = new ResolutionFrame(type, _frame);
            try {
                var _plan = this.GetPlan(type);
                if(_plan.Ctor == null) {
                    throw new ServiceResolutionException(string.Format("IoC. Не удалось найти подходящий конструктор для создания экземпляра \"{0}\".", type.FullName));
                }
                var _args = new List<object>();
                foreach(var _param in _plan.Ctor.GetParameters()) {
                    object _value;
                    _args.Add(ctorArgs != null && ctorArgs.TryGetValue(_param.Name, out _value) ? _value : this._GetServiceCore(_param.ParameterType, type));
                }
                var _argsArray = _args.ToArray();
                var _instance = _plan.Factory != null ? _plan.Factory(_argsArray) : _plan.Ctor.Invoke(_argsArray);
                foreach(var _prop in _plan.Properties) {
                    _prop.SetValue(_instance, this._GetServiceCore(_prop.PropertyType, type), null);
                }
                if(trackComponents) {
                    this.TrackInstance(_instance);
                }
                return _instance;
            } finally {
                ResolutionPath = _frame;
            }
        }

        private void _AddInstance(Type service, object instance) {
            this._instances[service] = this.CreateProxyIfRequired(service, instance);
        }

        private object CreateProxyIfRequired(Type service, object instance) {
            if(!instance.GetType().IsDefined(this.ProxyRequiredAttribute, false)) {
                return instance;
            }
#if NETSTANDARD
            var _type = service;
            if(service.IsGenericType && service.GetGenericTypeDefinition() == this.ContextBinder) {
                _type = service.GetGenericArguments()[0];
            }
            return new _InstanceProxy { Container = this, Instance = instance }.GetTransparentProxy(_type);
#else
            return new _InstanceProxy(this, instance).GetTransparentProxy();
#endif
        }

        /// <summary>
        /// Returns an object that represents a service provided by the System.ComponentModel.Component
        /// or by its System.ComponentModel.Container.
        /// </summary>
        /// <param name="service">A service provided by the System.ComponentModel.Component.</param>
        /// <returns>
        /// An System.Object that represents a service provided by the System.ComponentModel.Component,
        /// or null if the System.ComponentModel.Component does not provide the specified
        /// service.
        /// </returns>
        protected override object GetService(Type service) {
            this.ThrowIfDisposed();
            if(service == null) {
                throw new ArgumentNullException(nameof(service));
            }
            Type _implementation;
            if(this._binding.TryGetValue(service, out _implementation)) {
                object _instance;
                if(this._instances.TryGetValue(service, out _instance)) {
                    return _instance;
                }
                lock(this._sync) {
                    this.ThrowIfDisposed();
                    if(this._instances.TryGetValue(service, out _instance)) {
                        return _instance;
                    }
                    _instance = this._CreateInstanceCore(_implementation, _emptyArguments, true);
                    this._AddInstance(service, _instance);
                    return this._instances[service];
                }
            }
            return this.Parent?.GetService(service) ?? base.GetService(service);
        }

        /// <summary>
        /// Returns an object that represents a service provided by the System.ComponentModel.Component
        /// or by its System.ComponentModel.Container.
        /// </summary>
        /// <param name="serviceType">A service provided by the System.ComponentModel.Component.</param>
        /// <returns>
        /// An System.Object that represents a service provided by the System.ComponentModel.Component,
        /// or null if the System.ComponentModel.Component does not provide the specified
        /// service.
        /// </returns>
        object IServiceProvider.GetService(Type serviceType) {
            this.ThrowIfDisposed();
            return this.Resolve(serviceType);
        }

        private object _GetServiceCore(Type type, Type context) {
            this.ThrowIfDisposed();
            if(type.IsGenericType && type.GetGenericTypeDefinition() == this.ContextBinder) {
                // IContextBinder<,> используется как ключ контекстной привязки и не является внедряемым сервисом.
                return null;
            }
            if(type.IsGenericType && type.GetGenericTypeDefinition() == this.LazyCallbackType) {
                return (Activator.CreateInstance(typeof(_Lazy<,>).MakeGenericType(context, type.GetGenericArguments()[0]), this) as _ILazy).CreateLazyService();
            }
            if(type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IEnumerable<>)) {
                return this.Resolve(type);
            }
            if(type.IsInterface) {
                return this.Resolve(this.ContextBinder.MakeGenericType(type, context)) ?? this.Resolve(type);
            }
            if(type.IsAbstract) {
                return this.Resolve(type);
            }
            IServiceRegistration _registration;
            ServiceContainer _owner;
            if(this.TryFindRegistration(type, null, out _registration, out _owner)) {
                return this.Resolve(type);
            }
            return this._CreateInstanceCore(type, _emptyArguments, true);
        }

        private bool BeginDispose(bool captureAsync, out List<ServiceContainer> nested, out ComponentTracker tracked) {
            lock(this._sync) {
                if(this._disposed) {
                    nested = null;
                    tracked = null;
                    return false;
                }
                this._disposed = true;
                nested = this._nested;
                this._nested = null;
                this._binding = new Dictionary<Type, Type>();
                this._registrations = new Dictionary<Type, IServiceRegistration[]>();
                this._instances.Clear();
                this._singletons.Clear();
                this._scoped.Clear();
                tracked = this._tracked;
                this._tracked = new ComponentTracker();
#if NETSTANDARD
                this._pendingInitializationsAtDispose = captureAsync ? this._pendingInitializations.Select(x => x.StartedTask).Where(x => x != null).ToArray() : null;
                this._asyncComponentsAtDispose = captureAsync ? this._asyncComponents : null;
                this._asyncComponents = new List<IComponent>();
                this._pendingInitializations.Clear();
#endif
                this._config = null;
                this._settings = null;
                return true;
            }
        }

        protected override void Dispose(bool disposing) {
            if(!disposing) {
                return;
            }
            List<ServiceContainer> _nested;
            ComponentTracker _tracked;
            if(!this.BeginDispose(false, out _nested, out _tracked)) {
                return;
            }
            var _parent = this.Parent;
            if(_parent != null) {
                _parent.RemoveNested(this);
            }
            if(_nested != null) {
                foreach(var _item in _nested) {
                    _item.Dispose();
                }
            }
#if NETSTANDARD
            var _asyncOnly = _tracked.DisposeAll();
#else
            _tracked.DisposeAll();
#endif
            base.Dispose(true);
#if NETSTANDARD
            if(_asyncOnly) {
                throw new InvalidOperationException("Среди сервисов есть IAsyncDisposable. Используйте DisposeAsync().");
            }
#endif
        }

#if NETSTANDARD

        /// <summary>
        /// Асинхронно освобождает контейнер, вложенные области и сервисы, реализующие <see cref="IAsyncDisposable"/>.
        /// </summary>
        public ValueTask DisposeAsync() {
            List<ServiceContainer> _nested;
            ComponentTracker _tracked;
            if(!this.BeginDispose(true, out _nested, out _tracked)) {
                return default(ValueTask);
            }
            return new ValueTask(this.DisposeAsyncCore(_nested, _tracked));
        }

        private async Task DisposeAsyncCore(List<ServiceContainer> nested, ComponentTracker tracked) {
            var _pending = this._pendingInitializationsAtDispose;
            this._pendingInitializationsAtDispose = null;
            if(_pending != null) {
                foreach(var _task in _pending) {
                    try {
                        await _task.ConfigureAwait(false);
                    } catch {
                        // Ошибка инициализации уже передана вызывающему; освобождение продолжается.
                    }
                }
            }
            var _parent = this.Parent;
            if(_parent != null) {
                _parent.RemoveNested(this);
            }
            if(nested != null) {
                foreach(var _item in nested) {
                    await _item.DisposeAsync().ConfigureAwait(false);
                }
            }
            var _asyncComponents = this._asyncComponentsAtDispose;
            this._asyncComponentsAtDispose = null;
            if(_asyncComponents != null) {
                for(var i = _asyncComponents.Count - 1; i >= 0; i--) {
                    var _component = _asyncComponents[i];
                    if(_component == null) {
                        continue;
                    }
                    this.Remove(_component);
                    await ((IAsyncDisposable)_component).DisposeAsync().ConfigureAwait(false);
                }
            }
            await tracked.DisposeAllAsync().ConfigureAwait(false);
            base.Dispose(true);
        }

#endif

        private void ThrowIfDisposed() {
            if(this._disposed) {
                throw new ObjectDisposedException(nameof(ServiceContainer));
            }
        }

        internal void RemoveNested(ServiceContainer container) {
            lock(this._sync) {
                this._nested?.Remove(container);
            }
        }

        /// <summary>
        /// Устанавливает конфигурацию контейнера. Вложенные контейнеры, созданные ранее, не обновляются.
        /// </summary>
        public void UseConfiguration(IConfiguration configuration) {
            this.ThrowIfDisposed();
            lock(this._sync) {
                this.ThrowIfDisposed();
                this._config = configuration;
                this._settings = null;
            }
        }

        private TypePlan GetPlan(Type type) {
            var _required = this.ServiceRequiredAttribute;
            var _plans = this._plans.GetOrAdd(_required, key => new ConcurrentDictionary<Type, TypePlan>());
            return _plans.GetOrAdd(type, key => TypePlan.Create(key, _required));
        }

        private void EnsureNoCycle(Type type) {
            for(var _frame = ResolutionPath; _frame != null; _frame = _frame.Parent) {
                if(_frame.Type == type) {
                    throw new CircularDependencyException(string.Format("IoC. Обнаружена циклическая зависимость в \"{1}\".{0}Трассировка:{0}{2}", Environment.NewLine, type.FullName, this.BuildTrace(type)));
                }
            }
        }

        private string BuildTrace(Type type) {
            var _trace = new StringBuilder();
            _trace.AppendFormat("  в {0}{1}", type.FullName, Environment.NewLine);
            for(var _frame = ResolutionPath; _frame != null; _frame = _frame.Parent) {
                _trace.AppendFormat("  в {0}{1}", _frame.Type.FullName, Environment.NewLine);
            }
            return _trace.ToString();
        }

        private Type ServiceRequiredAttribute => this.Settings.ServiceRequiredAttributeType;

        private Type BindServiceAttribute => this.Settings.BindServiceAttributeType;

        private Type ProxyRequiredAttribute => this.Settings.ProxyRequiredAttributeType;

        private BindServiceCallback BindServiceCallback => this.Settings.BindServiceCallback;

        private Type ContextBinder => this.Settings.ContextBinderType.GetGenericTypeDefinition();

        private Type Listener => this.Settings.ListenerType;

        private InvokingResultCallback InvokingResultCallback => this.Settings.InvokingResultCallback ?? this.AdaptInvokingCallback();

        private InvokedResultCallback InvokedResultCallback => this.Settings.InvokedResultCallback ?? this.AdaptInvokedCallback();

        private CatchResultCallback CatchResultCallback => this.Settings.CatchResultCallback ?? this.AdaptCatchCallback();

        private InvokingResultCallback AdaptInvokingCallback() {
            var _legacy = this.Settings.InvokingCallback;
            if(_legacy == null) {
                return DefaultContainerInvocationResultConfiguration.Instance.InvokingResultCallback;
            }
            return (listener, method, instance, parameters) => _legacy(listener, method, instance, parameters);
        }

        private InvokedResultCallback AdaptInvokedCallback() {
            var _legacy = this.Settings.InvokedCallback;
            if(_legacy == null) {
                return DefaultContainerInvocationResultConfiguration.Instance.InvokedResultCallback;
            }
            return (listener, method, instance, result) => _legacy(listener, method, instance, result);
        }

        private CatchResultCallback AdaptCatchCallback() {
            var _legacy = this.Settings.CatchCallback;
            if(_legacy == null) {
                return DefaultContainerInvocationResultConfiguration.Instance.CatchResultCallback;
            }
            return (listener, method, instance, ex) => _legacy(listener, method, instance, ex);
        }

        private Type LazyCallbackType => this.Settings.LazyCallbackType.GetGenericTypeDefinition();

        private void InvalidateSettingsFor(Type serviceType) {
            if(serviceType == typeof(IConfiguration)) {
                this._settings = null;
            }
        }

        private ContainerSettings Settings {
            get {
                var _settings = this._settings;
                if(_settings != null) {
                    return _settings;
                }
                lock(this._sync) {
                    if(this._settings == null) {
                        this._settings = ContainerSettings.Create(this.ConfigurationService);
                    }
                    return this._settings;
                }
            }
        }

        private IConfiguration ConfigurationService {
            get {
                lock(this._sync) {
                    if(this._config == null) {
                        object _instance;
                        if(this._instances.TryGetValue(typeof(IConfiguration), out _instance)) {
                            this._config = _instance as IConfiguration;
                        } else {
                            IServiceRegistration _registration;
                            ServiceContainer _owner;
                            if(this.TryFindRegistration(typeof(IConfiguration), null, out _registration, out _owner)) {
                                if(_registration.Instance is IConfiguration _registered) {
                                    this._config = _registered;
                                } else if(_registration.ImplementationType != null) {
                                    var _created = Activator.CreateInstance(_registration.GetImplementationType(typeof(IConfiguration)));
                                    if(_created is IComponent _component) {
                                        this.Add(_component);
                                    }
                                    this._config = _created as IConfiguration;
                                }
                            } else {
                                Type _type;
                                if(this._binding.TryGetValue(typeof(IConfiguration), out _type)) {
                                    var _created = Activator.CreateInstance(_type);
                                    if(_created is IComponent _component) {
                                        this.Add(_component);
                                    }
                                    this._config = _created as IConfiguration;
                                }
                            }
                        }
                    }
                    return this._config;
                }
            }
        }

        private static MethodInfo FindImplementation(Type type, MethodInfo targetMethod) {
            if(targetMethod == null) {
                return null;
            }
            var _targetDefinition = targetMethod.IsGenericMethod ? targetMethod.GetGenericMethodDefinition() : targetMethod;
            var _declaring = targetMethod.DeclaringType;
            if(_declaring != null && _declaring.IsInterface && _declaring.IsAssignableFrom(type)) {
                InterfaceMapping _map;
                try {
                    _map = type.GetInterfaceMap(_declaring);
                } catch(ArgumentException) {
                    _map = default(InterfaceMapping);
                }
                if(_map.InterfaceMethods != null) {
                    var _index = -1;
                    for(var i = 0; i < _map.InterfaceMethods.Length; i++) {
                        if(_map.InterfaceMethods[i] == _targetDefinition) {
                            _index = i;
                            break;
                        }
                    }
                    if(_index < 0) {
                        for(var i = 0; i < _map.InterfaceMethods.Length; i++) {
                            if(_map.InterfaceMethods[i].Name == _targetDefinition.Name && ParametersMatch(_map.InterfaceMethods[i], _targetDefinition)) {
                                _index = i;
                                break;
                            }
                        }
                    }
                    if(_index >= 0) {
                        var _implementation = _map.TargetMethods[_index];
                        if(_implementation == null) {
                            return null;
                        }
                        if(targetMethod.IsGenericMethod && _implementation.IsGenericMethodDefinition) {
                            return _implementation.MakeGenericMethod(targetMethod.GetGenericArguments());
                        }
                        return _implementation;
                    }
                }
            }
            if(targetMethod.IsGenericMethod) {
                var _genericArguments = targetMethod.GetGenericArguments();
                foreach(var _method in type.GetMethods()) {
                    if(_method.IsGenericMethod && _method.Name == targetMethod.Name && _method.GetGenericArguments().Length == _genericArguments.Length) {
                        var _constructed = _method.MakeGenericMethod(_genericArguments);
                        if(ParametersMatch(_constructed, targetMethod)) {
                            return _constructed;
                        }
                    }
                }
                return null;
            }
            try {
                return type.GetMethod(targetMethod.Name, targetMethod.GetParameters().Select(x => x.ParameterType).ToArray());
            } catch(AmbiguousMatchException) {
                return null;
            }
        }

        private static bool ParametersMatch(MethodInfo candidate, MethodInfo target) {
            var _candidate = candidate.GetParameters();
            var _target = target.GetParameters();
            if(_candidate.Length != _target.Length) {
                return false;
            }
            for(var i = 0; i < _candidate.Length; i++) {
                var _left = _candidate[i].ParameterType;
                var _right = _target[i].ParameterType;
                if(_left != _right && !(_left.IsGenericParameter && _right.IsGenericParameter)) {
                    return false;
                }
            }
            return true;
        }

#if NETSTANDARD

        private sealed class AsyncInitializationEntry {
            private readonly object _sync = new object();
            private Task _initialization;

            public AsyncInitializationEntry(object instance, IAsyncInitializationAdapter initializable) {
                this.Instance = instance;
                this.Initializable = initializable;
            }

            public object Instance { get; private set; }

            public IAsyncInitializationAdapter Initializable { get; private set; }

            public Task StartedTask {
                get {
                    lock(this._sync) {
                        return this._initialization;
                    }
                }
            }

            public Task InitializeAsync(CancellationToken cancellationToken) {
                lock(this._sync) {
                    if(this._initialization == null) {
                        this._initialization = this.RunAsync(cancellationToken);
                    }
                    return this._initialization;
                }
            }

            public void Reset(Task initialization) {
                lock(this._sync) {
                    if(ReferenceEquals(this._initialization, initialization)) {
                        this._initialization = null;
                    }
                }
            }

            private async Task RunAsync(CancellationToken cancellationToken) {
                await this.Initializable.InitializeAsync(this.Instance, cancellationToken).ConfigureAwait(false);
            }
        }

#endif

        private sealed class ServiceLifetimeContext : IServiceLifetimeContext {
            private readonly ServiceContainer _owner;
            private readonly ServiceContainer _scope;
            private readonly IServiceRegistration _registration;
            private readonly Type _requestedServiceType;
            private readonly ServiceCacheKey _key;

            public ServiceLifetimeContext(ServiceContainer owner, ServiceContainer scope, IServiceRegistration registration, Type requestedServiceType, ServiceCacheKey key) {
                this._owner = owner;
                this._scope = scope;
                this._registration = registration;
                this._requestedServiceType = requestedServiceType;
                this._key = key;
            }

            public Type ServiceType => this._requestedServiceType;

            public string Name => this._registration.Name;

            public IContainerCore Owner => this._owner;

            public IContainerCore Scope => this._scope;

            public object GetOrAddSingleton() => GetOrCreate(this._owner, this._owner._singletons, this._key, () => this._owner.CreateFromRegistration(this._registration, this._requestedServiceType, this._owner));

            public object GetOrAddScoped() => GetOrCreate(this._scope, this._scope._scoped, this._key, () => this._scope.CreateFromRegistration(this._registration, this._requestedServiceType, this._scope));

            public object Create() => this._scope.CreateFromRegistration(this._registration, this._requestedServiceType, this._scope);
        }

        private struct ServiceCacheKey : IEquatable<ServiceCacheKey> {
            public ServiceCacheKey(Type type, string name) {
                this.Type = type;
                this.Name = name;
            }

            public Type Type { get; private set; }

            public string Name { get; private set; }

            public bool Equals(ServiceCacheKey other) => this.Type == other.Type && string.Equals(this.Name, other.Name, StringComparison.Ordinal);

            public override bool Equals(object obj) => obj is ServiceCacheKey _other && this.Equals(_other);

            public override int GetHashCode() => (this.Type?.GetHashCode() ?? 0) ^ (this.Name?.GetHashCode() ?? 0);
        }

        private sealed class ResolutionFrame {
            public ResolutionFrame(Type type, ResolutionFrame parent) {
                this.Type = type;
                this.Parent = parent;
            }

            public Type Type { get; private set; }

            public ResolutionFrame Parent { get; private set; }
        }

        private sealed class TypePlan {
            private TypePlan(ConstructorInfo ctor, Func<object[], object> factory, PropertyInfo[] properties) {
                this.Ctor = ctor;
                this.Factory = factory;
                this.Properties = properties;
            }

            public ConstructorInfo Ctor { get; private set; }

            public Func<object[], object> Factory { get; private set; }

            public PropertyInfo[] Properties { get; private set; }

            public static TypePlan Create(Type type, Type requiredAttribute) {
                ConstructorInfo _ctor = null;
                foreach(var _item in type.GetConstructors()) {
                    if(_item.IsDefined(requiredAttribute, false)) {
                        _ctor = _item;
                        break;
                    }
                }
                if(_ctor == null) {
                    _ctor = type.GetConstructor(Type.EmptyTypes);
                }
                var _properties = new List<PropertyInfo>();
                foreach(var _prop in type.GetProperties()) {
                    if(_prop.IsDefined(requiredAttribute, false)) {
                        _properties.Add(_prop);
                    }
                }
                return new TypePlan(_ctor, CompileFactory(_ctor), _properties.ToArray());
            }

            private static Func<object[], object> CompileFactory(ConstructorInfo ctor) {
                if(ctor == null) {
                    return null;
                }
                try {
                    var _parameters = ctor.GetParameters();
                    var _args = Expression.Parameter(typeof(object[]), "args");
                    var _arguments = new Expression[_parameters.Length];
                    for(var i = 0; i < _parameters.Length; i++) {
                        _arguments[i] = Expression.Convert(Expression.ArrayIndex(_args, Expression.Constant(i)), _parameters[i].ParameterType);
                    }
                    var _new = Expression.New(ctor, _arguments);
                    return Expression.Lambda<Func<object[], object>>(Expression.Convert(_new, typeof(object)), _args).Compile();
                } catch(Exception) {
                    return null;
                }
            }
        }

        public delegate void CtorCallback(CtorCallbackCore callback);

        public delegate void CtorCallbackCore(string name, object val);

        private interface _ILazy {

            Delegate CreateLazyService();
        }

        private sealed class _Lazy<T, TResult> : _ILazy where TResult : class {

            public _Lazy(ServiceContainer container) {
                this.Container = container;
            }

            public Delegate CreateLazyService() {
                if(typeof(TResult).IsGenericType && typeof(TResult).GetGenericTypeDefinition() == this.Container.ContextBinder) {
                    return null;
                }
                return Delegate.CreateDelegate(this.Container.LazyCallbackType.MakeGenericType(typeof(TResult)), this, new Lazy<TResult>(this.GetService).Method);
            }

            private TResult GetService() {
                lock(this.Container._sync) {
                    return this.Container._GetServiceCore(typeof(TResult), typeof(T)) as TResult;
                }
            }

            private ServiceContainer Container { get; set; }
        }

#if NETSTANDARD

        public class _InstanceProxy : DispatchProxy {
            private static readonly ConcurrentDictionary<Type, ConcurrentDictionary<MethodInfo, MethodInfo>> _implementations = new ConcurrentDictionary<Type, ConcurrentDictionary<MethodInfo, MethodInfo>>();

            public _InstanceProxy() {
            }

            protected override object Invoke(MethodInfo targetMethod, object[] args) {
                var _listener = (this.Container as IServiceProvider).GetService(this.Container.ContextBinder.MakeGenericType(this.Container.Listener, this.Instance.GetType())) ?? (this.Container as IServiceProvider).GetService(this.Container.Listener);
                try {
                    var _target = this.GetImplementation(targetMethod);
                    if(_target == null) {
                        throw new MissingMethodException(string.Format("IoC. Не удалось найти реализацию метода \"{0}.{1}\".", targetMethod.DeclaringType?.FullName, targetMethod.Name));
                    }
                    var _args = args ?? new object[0];
                    var _parameters = _target.GetParameters();
                    var _callArgs = new object[_parameters.Length];
                    for(var i = 0; i < _parameters.Length; i++) {
                        var _param = _parameters[i];
                        _callArgs[i] =
                            !_param.IsOut && _args[i] == null && _param.IsDefined(this.Container.ServiceRequiredAttribute, false) ?
                                this.Container._GetServiceCore(_param.ParameterType, this.Instance.GetType()) :
                                _args[i];
                    }

                    var _invoking = this.Container.InvokingResultCallback(_listener, targetMethod, this.Instance, _callArgs);
                    var _result = _invoking.Handled ? _invoking.Value : _target.Invoke(this.Instance, _callArgs);

                    for(var i = 0; i < _parameters.Length; i++) {
                        if(_parameters[i].ParameterType.IsByRef) {
                            _args[i] = _callArgs[i];
                        }
                    }

                    var _invoked = this.Container.InvokedResultCallback(_listener, targetMethod, this.Instance, _result);
                    return _invoked.Handled ? _invoked.Value : _result;
                } catch(Exception ex) {
                    var _caught = this.Container.CatchResultCallback(_listener, targetMethod, this.Instance, ex);
                    if(!_caught.Handled) {
                        throw;
                    }
                    if(_caught.Value == null) {
                        return GetDefaultResult(targetMethod);
                    }
                    if(_caught.Value is Exception _mapped) {
                        throw _mapped;
                    }
                    throw new InvalidOperationException("IoC. CatchCallback должен вернуть InvocationResult с Exception или null.");
                }
            }

            private static object GetDefaultResult(MethodInfo method) {
                var _returnType = method.ReturnType;
                return _returnType == typeof(void) || !_returnType.IsValueType ? null : Activator.CreateInstance(_returnType);
            }

            private MethodInfo GetImplementation(MethodInfo targetMethod) {
                var _type = this.Instance.GetType();
                var _implementations = _InstanceProxy._implementations.GetOrAdd(_type, key => new ConcurrentDictionary<MethodInfo, MethodInfo>());
                return _implementations.GetOrAdd(targetMethod, key => FindImplementation(_type, key));
            }

            internal object GetTransparentProxy(Type type) {
                var _create = typeof(DispatchProxy).GetMethods(BindingFlags.Static | BindingFlags.Public)
                    .First(x => x.Name == nameof(DispatchProxy.Create) && x.IsGenericMethodDefinition && x.GetGenericArguments().Length == 2);
                var _proxy = _create.MakeGenericMethod(type, this.GetType()).Invoke(null, null) as _InstanceProxy;
                if(_proxy != null) {
                    _proxy.Instance = this.Instance;
                    _proxy.Container = this.Container;
                }
                return _proxy;
            }

            internal object Instance { get; set; }

            internal ServiceContainer Container { get; set; }
        }
#else

        private sealed class _InstanceProxy : RealProxy {
            private object _instance;
            private ServiceContainer _container;

            [PermissionSet(SecurityAction.LinkDemand)]
            public _InstanceProxy(ServiceContainer container, object instance) : base(instance?.GetType()) {
                this._container = container;
                this._instance = instance;
            }

            [SecurityPermissionAttribute(SecurityAction.LinkDemand, Flags = SecurityPermissionFlag.Infrastructure)]
            public override IMessage Invoke(IMessage msg) {
                var _msg = msg as IMethodCallMessage;
                var _listener = (this._container as IServiceProvider).GetService(this._container.ContextBinder.MakeGenericType(this._container.Listener, this._instance.GetType())) ?? (this._container as IServiceProvider).GetService(this._container.Listener);
                object[] _argsArray = null;
                try {
                    var _target = FindImplementation(this._instance.GetType(), _msg.MethodBase as MethodInfo);
                    if(_target == null) {
                        throw new MissingMethodException(string.Format("IoC. Не удалось найти реализацию метода \"{0}.{1}\".", _msg.MethodBase.DeclaringType?.FullName, _msg.MethodName));
                    }
                    var _args = new List<object>();
                    foreach(var _param in _target.GetParameters()) {
                        _args.Add(
                            !_param.IsOut && _msg.GetArg(_param.Position) == null && _param.IsDefined(this._container.ServiceRequiredAttribute, false) ?
                                this._container._GetServiceCore(_param.ParameterType, this._instance.GetType()) :
                                _msg.GetArg(_param.Position));
                    }
                    _argsArray = _args.ToArray();

                    var _invoking = this._container.InvokingResultCallback(_listener, _msg.MethodBase, this._instance, _argsArray);
                    var _result = _invoking.Handled ? _invoking.Value : _target.Invoke(this._instance, _argsArray);

                    var _invoked = this._container.InvokedResultCallback(_listener, _msg.MethodBase, this._instance, _result);
                    return new ReturnMessage(_invoked.Handled ? _invoked.Value : _result, _argsArray, 0, _msg.LogicalCallContext, _msg);
                } catch(Exception ex) {
                    var _caught = this._container.CatchResultCallback(_listener, _msg.MethodBase, this._instance, ex);
                    if(!_caught.Handled) {
                        return new ReturnMessage(ex, _msg);
                    }
                    if(_caught.Value == null) {
                        return new ReturnMessage(GetDefaultResult(_msg.MethodBase), _argsArray ?? _msg.Args, 0, _msg.LogicalCallContext, _msg);
                    }
                    if(_caught.Value is Exception _mapped) {
                        return new ReturnMessage(_mapped, _msg);
                    }
                    throw new InvalidOperationException("IoC. CatchCallback должен вернуть InvocationResult с Exception или null.");
                }
            }

            private static object GetDefaultResult(MethodBase method) {
                var _returnType = (method as MethodInfo)?.ReturnType;
                return _returnType == null || _returnType == typeof(void) || !_returnType.IsValueType ? null : Activator.CreateInstance(_returnType);
            }
        }

#endif

    }
}
