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
    public sealed class ServiceContainer:Container, IServiceProvider {
        private Dictionary<Type,Type> _binding = new Dictionary<Type,Type>();
        private Dictionary<Type,object> _instances = new Dictionary<Type,object>();
        private Stack<Type> _stack = new Stack<Type>();
        private IConfiguration _config = null;

        /// <summary>
        /// Выполняет загрузку привязок из указанной сборки.
        /// </summary>
        /// <param name="assembly">The assembly.</param>
        public void Load(Assembly assembly) {
            foreach(Attribute _attr in assembly.GetCustomAttributes(this.BindServiceAttribute,false)) {
                this.BindServiceCallback(_attr,this.Bind);
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
        public void Bind(Type service,Type obj) {
            if(service == null || obj == null) {
                throw new ArgumentNullException("Отсутствует информация о связываемых типах.");
            }
            if(!service.IsInterface) {
                throw new InvalidOperationException(string.Format("Тип \"{0}\" не является интерфейсом.",service.FullName));
            }
            if(obj.GetInterface((typeof(IComponent).FullName)) == null) {
                throw new InvalidOperationException(string.Format("Тип \"{0}\" не является производным от \"{1}\".",obj.FullName,typeof(IComponent).FullName));
            }
            this._binding.Add(service,obj);
        }

        /// <summary>
        /// Binds the specified service.
        /// </summary>
        /// <typeparam name="TService">The type of the service.</typeparam>
        /// <typeparam name="T">Type of object.</typeparam>
        public void Bind<TService, T>() {
            this.Bind(typeof(TService),typeof(T));
        }

        /// <summary>
        /// Binds the specified service.
        /// </summary>
        /// <param name="service">The service.</param>
        /// <param name="obj">The object.</param>
        public void Bind(Type service,object obj) {
            this.Bind(service,obj.GetType());
            this._AddInstance(service,obj);
        }

        /// <summary>
        /// Binds the specified service.
        /// </summary>
        /// <typeparam name="TService">The type of the service.</typeparam>
        /// <param name="obj">The object.</param>
        public void Bind<TService>(object obj) {
            this.Bind(typeof(TService),obj);
        }

        /// <summary>
        /// Создает экземпляр указанного типа и осуществляет внедрение зависимостей.
        /// </summary>
        /// <typeparam name="T">Тип.</typeparam>
        /// <returns>Экземпляр указанного типа.</returns>
        public T CreateInstance<T>(params CtorCallback[] args) where T : class {
            return this.CreateInstance(typeof(T),args) as T;
        }

        /// <summary>
        /// Создает экземпляр указанного типа и осуществляет внедрение зависимостей.
        /// </summary>
        /// <param name="type">Тип.</param>
        /// <returns>Экземпляр указанного типа.</returns>
        public object CreateInstance(Type type,params CtorCallback[] args) {
            try {
                var _args = new Dictionary<string,object>();
                foreach(var _arg in args) {
                    _arg((name,val) => _args.Add(name,val));
                }
                return this._CreateInstanceCore(type,_args);
            } finally {
                this._stack.Clear();
            }
        }

        private object _CreateInstanceCore(Type type,IDictionary<string,object> ctorArgs) {
            if(this._stack.Contains(type)) {
                var _trace = string.Empty;
                this._stack.Push(type);
                foreach(var _item in this._stack) {
                    _trace+=string.Format("  в {1}{0}",Environment.NewLine,_item.FullName);
                }
                throw new InvalidOperationException(string.Format("IoC. Обнаружена циклическая зависимость в \"{1}\".{0}Трасcировка:{0}{2}",Environment.NewLine,type.FullName,_trace));
            }
            this._stack.Push(type);
            try {
                var _inst = new _Func(()=> {
                    foreach(var _ctor in type.GetConstructors()) {
                        if(_ctor.IsDefined(this.ServiceRequiredAttribute,false)) {
                            var _args = new List<object>();
                            foreach(var _param in _ctor.GetParameters()) {
                                if(_param.ParameterType.IsGenericType&&_param.ParameterType.GetGenericTypeDefinition()==this.ContextBinder) {
                                    _args.Add(null);
                                } else {
                                    _args.Add(ctorArgs.ContainsKey(_param.Name) ? ctorArgs[_param.Name] : (_param.ParameterType.IsInterface ? (this.GetService(this.ContextBinder.MakeGenericType(_param.ParameterType,type))??this.GetService(_param.ParameterType)) : this._CreateInstanceCore(_param.ParameterType,new Dictionary<string,object>())));
                                }
                            }
                            return Activator.CreateInstance(type,_args.ToArray());
                        }
                    }
                    if(type.GetConstructor(Type.EmptyTypes)!=null) {
                        return Activator.CreateInstance(type);
                    }
                    throw new InvalidOperationException(string.Format("IoC. Не удалось найти подходящий конструктор для создания экземпляра \"{0}\".",type.FullName));
                })();
                foreach(var _prop in type.GetProperties()) {
                    if(_prop.IsDefined(this.ServiceRequiredAttribute,false)) {
                        if(_prop.PropertyType.IsGenericType&&_prop.PropertyType.GetGenericTypeDefinition()==this.ContextBinder) {
                            _prop.SetValue(_inst,null,null);
                        } else {
                            _prop.SetValue(_inst,_prop.PropertyType.IsInterface ? (this.GetService(this.ContextBinder.MakeGenericType(_prop.PropertyType,type))??this.GetService(_prop.PropertyType)) : this._CreateInstanceCore(_prop.PropertyType,new Dictionary<string,object>()),null);
                        }
                    }
                }
                var _component = _inst as IComponent;
                if(_component != null) {
                    this.Add(_component);
                }
                return _inst;
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
            if(this._binding.ContainsKey(service)) {
                if(!this._instances.ContainsKey(service)) {
                    this._AddInstance(service,this._CreateInstanceCore(this._binding[service],new Dictionary<string,object>()));
                }
                return this._instances[service];
            }
            return base.GetService(service);
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
            try {
                return this.GetService(serviceType);
            } finally {
                this._stack.Clear();
            }
        }

        private Type ServiceRequiredAttribute {
            get {
                return this.ConfigurationService?.ServiceRequiredAttributeType ?? typeof(ServiceRequiredAttribute);
            }
        }

        private Type BindServiceAttribute {
            get {
                return this.ConfigurationService?.BindServiceAttributeType ?? typeof(BindServiceAttribute);
            }
        }

        private Type ProxyRequiredAttribute {
            get {
                return this.ConfigurationService?.ProxyRequiredAttributeType ?? typeof(ProxyRequiredAttribute);
            }
        }

        private BindServiceCallback BindServiceCallback {
            get {
                return this.ConfigurationService?.BindServiceCallback ?? new BindServiceCallback((x, callback) => {
                    var _attr = x as BindServiceAttribute;
                    if(_attr != null) {
                        callback(_attr.Service, _attr.ObjectType);
                    }
                });
            }
        }

        private Type ContextBinder {
            get {
                return this.ConfigurationService?.ContextBinderType?.GetGenericTypeDefinition() ?? typeof(IContextBinder<,>);
            }
        }

        private IConfiguration ConfigurationService {
            get {
                if(this._config==null&&this._binding.ContainsKey(typeof(IConfiguration))) {
                    var _component = Activator.CreateInstance(this._binding[typeof(IConfiguration)]) as IComponent;
                    if(_component!=null) {
                        this.Add(_component);
                        this._config=_component as IConfiguration;
                    }
                }
                return this._config;
            }
        }

        private delegate object _Func();

        public delegate void CtorCallback(CtorCallbackCore callback);

        public delegate void CtorCallbackCore(string name,object val);

#if NETSTANDARD2_0

        public class _InstanceProxy : DispatchProxy {

            public _InstanceProxy() {
            }

            protected override object Invoke(MethodInfo targetMethod, object[] args) {
                var _args = new List<object>();
                foreach(var _param in this.Instance.GetType().GetMethod(targetMethod.Name, targetMethod.GetParameters().Select(x => x.ParameterType).ToArray()).GetParameters()) {
                    _args.Add(!_param.IsOut && args[_param.Position] == null && _param.IsDefined(typeof(ServiceRequiredAttribute), false) ? (_param.ParameterType.IsInterface ? (this.Container.GetService(this.Container.ContextBinder.MakeGenericType(_param.ParameterType, this.Instance.GetType())) ?? this.Container.GetService(_param.ParameterType)) : this.Container._CreateInstanceCore(_param.ParameterType, new Dictionary<string, object>())) : args[_param.Position]);
                }
                var _argsArray = _args.ToArray();
                return targetMethod.Invoke(this.Instance, _argsArray);
            }

            internal object GetTransparentProxy(Type type) {
                var _proxy = (typeof(DispatchProxy).GetMethod(nameof(DispatchProxy.Create), BindingFlags.Static | BindingFlags.Public).MakeGenericMethod(type, this.GetType()).Invoke(null, null)) as _InstanceProxy;
                if(_proxy != null) {
                    _proxy.Instance = this.Instance;
                    _proxy.Container = this.Container;
                }
                return _proxy;
            }

            internal object Instance {
                get; set;
            }

            internal ServiceContainer Container {
                get; set;
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
                try {
                    var _args = new List<object>();
                    foreach(var _param in this._instance.GetType().GetMethod(_msg.MethodName, _msg.MethodSignature as Type[]).GetParameters()) {
                        _args.Add(!_param.IsOut && _msg.GetArg(_param.Position) == null && _param.IsDefined(this._container.ServiceRequiredAttribute, false) ? (_param.ParameterType.IsInterface ? (this._container.GetService(this._container.ContextBinder.MakeGenericType(_param.ParameterType, this._instance.GetType())) ?? this._container.GetService(_param.ParameterType)) : this._container._CreateInstanceCore(_param.ParameterType, new Dictionary<string, object>())) : _msg.GetArg(_param.Position));
                    }
                    var _argsArray = _args.ToArray();
                    return new ReturnMessage(_msg.MethodBase.Invoke(this._instance, _argsArray), _argsArray, 0, _msg.LogicalCallContext, _msg);
                } catch(Exception ex) {
                    return new ReturnMessage(ex, _msg);
                }
            }
        }

#endif

    }
}