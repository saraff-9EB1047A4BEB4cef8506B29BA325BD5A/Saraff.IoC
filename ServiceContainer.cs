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
 * 
 */
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Reflection;
using System.Web;

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

        /// <summary>
        /// Выполняет загрузку привязок из указанной сборки.
        /// </summary>
        /// <param name="assembly">The assembly.</param>
        public void Load(Assembly assembly) {
            foreach(BindServiceAttribute _attr in assembly.GetCustomAttributes(typeof(BindServiceAttribute),false)) {
                this.Bind(_attr.Service,_attr.ObjectType);
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
            this._instances.Add(service,obj);
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
        public T CreateInstance<T>() where T : class {
            return this.CreateInstance(typeof(T)) as T;
        }

        /// <summary>
        /// Создает экземпляр указанного типа и осуществляет внедрение зависимостей.
        /// </summary>
        /// <param name="type">Тип.</param>
        /// <returns>Экземпляр указанного типа.</returns>
        public object CreateInstance(Type type) {
            try {
                return this._CreateInstanceCore(type);
            } finally {
                this._stack.Clear();
            }
        }

        private object _CreateInstanceCore(Type type) {
            if(this._stack.Contains(type)) {
                throw new InvalidOperationException(string.Format("IoC. Обнаружена циклическая зависимость в \"{0}\".",type.FullName));
            }
            this._stack.Push(type);
            try {
                var _inst = new _Func(()=> {
                    foreach(var _ctor in type.GetConstructors()) {
                        if(_ctor.GetCustomAttributes(typeof(ServiceRequiredAttribute),false).Length > 0) {
                            var _args = new List<object>();
                            foreach(var _param in _ctor.GetParameters()) {
                                _args.Add(_param.ParameterType.IsInterface ? this.GetService(_param.ParameterType) : this._CreateInstanceCore(_param.ParameterType));
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
                    if(_prop.PropertyType.IsInterface) {
                        foreach(var _attr in _prop.GetCustomAttributes(typeof(ServiceRequiredAttribute),false)) {
                            _prop.SetValue(_inst,this.GetService(_prop.PropertyType),null);
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
                    this._instances.Add(service,this._CreateInstanceCore(this._binding[service]));
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

        private delegate object _Func();
    }
}