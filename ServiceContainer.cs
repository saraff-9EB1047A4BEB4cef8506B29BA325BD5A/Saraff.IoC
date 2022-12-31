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
using System.Collections.Generic;
using System.ComponentModel;
using System.Reflection;
#if NETSTANDARD2_0
using System.Linq;
#else
using System.Runtime.Remoting.Messaging;
using System.Runtime.Remoting.Proxies;
using System.Security.Permissions;
#endif

namespace Saraff.IoC {

    /// <summary>
    /// IoC-контейнер.
    /// </summary>
    /// <seealso cref="System.ComponentModel.Container" />
    /// <seealso cref="System.IServiceProvider" />
    public sealed class ServiceContainer : Container, IServiceProvider {
        private Dictionary<Type, Type> _binding = new Dictionary<Type, Type>();
        private Dictionary<Type, object> _instances = new Dictionary<Type, object>();
        private Stack<Type> _stack = new Stack<Type>();
        private Stack<Stack<Type>> _frames = new Stack<Stack<Type>>();
        private IConfiguration _config = null;
        private List<ServiceContainer> _nested = new List<ServiceContainer>();

        public ServiceContainer Parent { get; private set; }

        public ServiceContainer CreateNestedContainer() {
            lock(this) {
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
            lock(this) {
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
            lock(this) {
                if(service == null || obj == null) {
                    throw new ArgumentNullException("Отсутствует информация о связываемых типах.");
                }
                if(!service.IsInterface) {
                    throw new InvalidOperationException(string.Format("Тип \"{0}\" не является интерфейсом.", service.FullName));
                }
                if(obj.GetInterface((typeof(IComponent).FullName)) == null) {
                    throw new InvalidOperationException(string.Format("Тип \"{0}\" не является производным от \"{1}\".", obj.FullName, typeof(IComponent).FullName));
                }
                this._binding.Add(service, obj);
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
            lock(this) {
                this.Bind(service, obj.GetType());
                this._AddInstance(service, obj);
            }
        }

        /// <summary>
        /// Binds the specified service.
        /// </summary>
        /// <typeparam name="TService">The type of the service.</typeparam>
        /// <param name="obj">The object.</param>
        public void Bind<TService>(object obj) => this.Bind(typeof(TService), obj);

        /// <summary>
        /// Создает экземпляр указанного типа и осуществляет внедрение зависимостей.
        /// </summary>
        /// <typeparam name="T">Тип.</typeparam>
        /// <returns>Экземпляр указанного типа.</returns>
        public T CreateInstance<T>(params CtorCallback[] args) where T : class => this.CreateInstance(typeof(T), args) as T;

        /// <summary>
        /// Создает экземпляр указанного типа и осуществляет внедрение зависимостей.
        /// </summary>
        /// <param name="type">Тип.</param>
        /// <returns>Экземпляр указанного типа.</returns>
        public object CreateInstance(Type type, params CtorCallback[] args) {
            lock(this) {
                this._frames.Push(_stack);
                this._stack = new Stack<Type>();
                try {
                    var _args = new Dictionary<string, object>();
                    foreach(var _arg in args) {
                        _arg((name, val) => _args.Add(name, val));
                    }
                    return this._CreateInstanceCore(type, _args);
                } finally {
                    this._stack = this._frames.Pop();
                }
            }
        }

        private object _CreateInstanceCore(Type type, IDictionary<string, object> ctorArgs) {
            if(this._stack.Contains(type)) {
                var _trace = string.Empty;
                this._stack.Push(type);
                foreach(var _item in this._stack) {
                    _trace += string.Format("  в {1}{0}", Environment.NewLine, _item.FullName);
                }
                throw new InvalidOperationException(string.Format("IoC. Обнаружена циклическая зависимость в \"{1}\".{0}Трасcировка:{0}{2}", Environment.NewLine, type.FullName, _trace));
            }
            this._stack.Push(type);
            try {
                object _Factory() {
                    foreach(var _ctor in type.GetConstructors()) {
                        if(_ctor.IsDefined(this.ServiceRequiredAttribute, false)) {
                            var _args = new List<object>();
                            foreach(var _param in _ctor.GetParameters()) {
                                _args.Add(ctorArgs.ContainsKey(_param.Name) ? ctorArgs[_param.Name] : this._GetServiceCore(_param.ParameterType, type));
                            }
                            return Activator.CreateInstance(type, _args.ToArray());
                        }
                    }
                    if(type.GetConstructor(Type.EmptyTypes) != null) {
                        return Activator.CreateInstance(type);
                    }
                    throw new InvalidOperationException(string.Format("IoC. Не удалось найти подходящий конструктор для создания экземпляра \"{0}\".", type.FullName));
                };
                var _instance = _Factory();
                foreach(var _prop in type.GetProperties()) {
                    if(_prop.IsDefined(this.ServiceRequiredAttribute, false)) {
                        _prop.SetValue(_instance, this._GetServiceCore(_prop.PropertyType, type), null);
                    }
                }
                if(_instance is IComponent _component) {
                    this.Add(_component);
                }
                return _instance;
            } finally {
                this._stack.Pop();
            }
        }

        private void _AddInstance(Type service, object instance) {
            this._instances.Add(
                service,
                instance.GetType().IsDefined(this.ProxyRequiredAttribute, false) ?
#if NETSTANDARD2_0
                    new _InstanceProxy { Container = this, Instance = instance }.GetTransparentProxy(service.GetGenericTypeDefinition() == this.ContextBinder ? service.GetGenericArguments()[0] : service)
#else
                    new _InstanceProxy(this, instance).GetTransparentProxy()
#endif
                    : instance);
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
            lock(this) {
                if(this._binding.ContainsKey(service)) {
                    if(!this._instances.ContainsKey(service)) {
                        this._AddInstance(service, this._CreateInstanceCore(this._binding[service], new Dictionary<string, object>()));
                    }
                    return this._instances[service];
                }
                return this.Parent?.GetService(service) ?? base.GetService(service);
            }
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
        object IServiceProvider.GetService(Type serviceType) {
            lock(this) {
                this._frames.Push(_stack);
                this._stack = new Stack<Type>();
                try {
                    return this.GetService(serviceType);
                } finally {
                    this._stack = this._frames.Pop();
                }
            }
        }

        private object _GetServiceCore(Type type, Type context) {
            if(type.IsGenericType && type.GetGenericTypeDefinition() == this.ContextBinder) {
                return null;
            }
            return type.IsGenericType && type.GetGenericTypeDefinition() == this.LazyCallbackType ?
                (Activator.CreateInstance(typeof(_Lazy<,>).MakeGenericType(context, type.GetGenericArguments()[0]), this) as _ILazy).CreateLazyService() :
                (type.IsInterface ?
                    (this.GetService(this.ContextBinder.MakeGenericType(type, context)) ?? this.GetService(type)) :
                    this._CreateInstanceCore(type, new Dictionary<string, object>()));
        }

        protected override void Dispose(bool disposing) {
            if(this._nested != null) {
                foreach(var _item in this._nested) {
                    _item.Dispose();
                }
            }
            base.Dispose(disposing);
            this._nested = null;
            this._binding = null;
            this._instances = null;
        }

        private Type ServiceRequiredAttribute => this.ConfigurationService?.ServiceRequiredAttributeType ?? typeof(ServiceRequiredAttribute);

        private Type BindServiceAttribute => this.ConfigurationService?.BindServiceAttributeType ?? typeof(BindServiceAttribute);

        private Type ProxyRequiredAttribute => this.ConfigurationService?.ProxyRequiredAttributeType ?? typeof(ProxyRequiredAttribute);

        private BindServiceCallback BindServiceCallback => this.ConfigurationService?.BindServiceCallback ?? new BindServiceCallback((x, callback) => {
            if(x is BindServiceAttribute _attr) {
                callback(_attr.Service, _attr.ObjectType);
            }
        });

        private Type ContextBinder => this.ConfigurationService?.ContextBinderType?.GetGenericTypeDefinition() ?? typeof(IContextBinder<,>);

        private Type Listener => this.ConfigurationService?.ListenerType ?? typeof(IListener);

        private InvokingCallback InvokingCallback => this.ConfigurationService?.InvokingCallback ?? new InvokingCallback((listener, method, instance, parameters) => (listener as IListener)?.OnInvoking(method, instance, parameters));

        private InvokedCallback InvokedCallback => this.ConfigurationService?.InvokedCallback ?? new InvokedCallback((listener, method, instance, result) => (listener as IListener)?.OnInvoked(method, instance, result));

        private CatchCallback CatchCallback => this.ConfigurationService?.CatchCallback ?? new CatchCallback((listener, method, instance, ex) => (listener as IListener)?.OnCatch(method, instance, ex));

        private Type LazyCallbackType => this.ConfigurationService?.LazyCallbackType?.GetGenericTypeDefinition() ?? typeof(Lazy<>);

        private IConfiguration ConfigurationService {
            get {
                if(this._config == null && this._binding.ContainsKey(typeof(IConfiguration))) {
                    if(Activator.CreateInstance(this._binding[typeof(IConfiguration)]) is IComponent _component) {
                        this.Add(_component);
                        this._config = _component as IConfiguration;
                    }
                }
                return this._config;
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
                lock(this.Container) {
                    return this.Container._GetServiceCore(typeof(TResult), typeof(T)) as TResult;
                }
            }

            private ServiceContainer Container { get; set; }
        }

#if NETSTANDARD2_0

        public class _InstanceProxy : DispatchProxy {

            public _InstanceProxy() {
            }

            protected override object Invoke(MethodInfo targetMethod, object[] args) {
                var _listener = (this.Container as IServiceProvider).GetService(this.Container.ContextBinder.MakeGenericType(this.Container.Listener, this.Instance.GetType())) ?? (this.Container as IServiceProvider).GetService(this.Container.Listener);
                try {
                    var _args = new List<object>();

                    IEnumerable<ParameterInfo> _params = null;
                    if(targetMethod.IsGenericMethod) {
                        _params = 
                            this.Instance.GetType().GetMethods()
                            .Where(x => x.IsGenericMethod && x.Name == targetMethod.Name)
                            .Where(x => x.GetGenericArguments().Length == targetMethod.GetGenericArguments().Length)
                            .Single(x => x.MakeGenericMethod(targetMethod.GetGenericArguments()).GetParameters().SequenceEqual(targetMethod.GetParameters(), new _Comparer()))
                            .MakeGenericMethod(targetMethod.GetGenericArguments()).GetParameters();
                    } else {
                        _params = this.Instance.GetType().GetMethod(targetMethod.Name, targetMethod.GetParameters().Select(x => x.ParameterType).ToArray()).GetParameters();
                    }

                    foreach(var _param in _params) {
                        _args.Add(
                            !_param.IsOut && args[_param.Position] == null && _param.IsDefined(typeof(ServiceRequiredAttribute), false) ? 
                                this.Container._GetServiceCore(_param.ParameterType, this.Instance.GetType()) : 
                                args[_param.Position]);
                    }
                    var _argsArray = _args.ToArray();

                    var _result = this.Container.InvokingCallback(_listener, targetMethod, this.Instance, _argsArray) ?? targetMethod.Invoke(this.Instance, _argsArray);

                    return this.Container.InvokedCallback(_listener, targetMethod, this.Instance, _result) ?? _result;
                } catch(Exception ex) {
                    for(var _ex = this.Container.CatchCallback(_listener, targetMethod, this.Instance, ex); _ex != null;) {
                        throw _ex;
                    }
                    throw;
                }
            }

            internal object GetTransparentProxy(Type type) {
                var _proxy = (typeof(DispatchProxy).GetMethod(nameof(DispatchProxy.Create), BindingFlags.Static | BindingFlags.Public).MakeGenericMethod(type, this.GetType()).Invoke(null, null)) as _InstanceProxy;
                if(_proxy != null) {
                    _proxy.Instance = this.Instance;
                    _proxy.Container = this.Container;
                }
                return _proxy;
            }

            internal object Instance { get; set; }

            internal ServiceContainer Container { get; set; }

            private sealed class _Comparer : IEqualityComparer<ParameterInfo> {

                public bool Equals(ParameterInfo x, ParameterInfo y) => x.ParameterType == y.ParameterType;

                public int GetHashCode(ParameterInfo obj) => obj.ParameterType.GetHashCode();
            }
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
                try {
                    var _args = new List<object>();

                    IEnumerable<ParameterInfo> _params = null;
                    if(_msg.MethodBase.IsGenericMethod) {
                        var _methods = new List<MethodInfo>();
                        foreach(var _method in this._instance.GetType().GetMethods()) {
                            if(_method.IsGenericMethod && _method.Name == _msg.MethodName && _method.GetGenericArguments().Length == _msg.MethodBase.GetGenericArguments().Length) {
                                bool _SequenceEqual(ParameterInfo[] _seq1, ParameterInfo[] _seq2) {
                                    if(_seq1.Length != _seq2.Length) {
                                        return false;
                                    }
                                    for(int i = 0; i < _seq1.Length; i++) {
                                        if(_seq1[i].ParameterType != _seq2[i].ParameterType) {
                                            return false;
                                        }
                                    }
                                    return true;
                                }

                                if(_SequenceEqual(_method.MakeGenericMethod(_msg.MethodBase.GetGenericArguments()).GetParameters(), _msg.MethodBase.GetParameters())) {
                                    _methods.Add(_method);
                                }
                            }
                        }
                        if(_methods.Count != 1) {
                            throw new InvalidOperationException();
                        }
                        _params = _methods[0].MakeGenericMethod(_msg.MethodBase.GetGenericArguments()).GetParameters();
                    } else {
                        _params = this._instance.GetType().GetMethod(_msg.MethodName, _msg.MethodSignature as Type[]).GetParameters();
                    }

                    foreach(var _param in _params) {
                        _args.Add(
                            !_param.IsOut && _msg.GetArg(_param.Position) == null && _param.IsDefined(this._container.ServiceRequiredAttribute, false) ? 
                                this._container._GetServiceCore(_param.ParameterType, this._instance.GetType()) : 
                                _msg.GetArg(_param.Position));
                    }
                    var _argsArray = _args.ToArray();

                    var _result = this._container.InvokingCallback(_listener, _msg.MethodBase, this._instance, _argsArray) ?? _msg.MethodBase.Invoke(this._instance, _argsArray);

                    return new ReturnMessage(this._container.InvokedCallback(_listener, _msg.MethodBase, this._instance, _result) ?? _result, _argsArray, 0, _msg.LogicalCallContext, _msg);
                } catch(Exception ex) {
                    return new ReturnMessage(this._container.CatchCallback(_listener, _msg.MethodBase, this._instance, ex) ?? ex, _msg);
                }
            }
        }

#endif

    }
}