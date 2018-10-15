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
using System.Reflection;
using System.Text;

namespace Saraff.IoC {

    public interface IConfiguration {

        Type BindServiceAttributeType {
            get;
        }

        BindServiceCallback BindServiceCallback {
            get;
        }

        Type ServiceRequiredAttributeType {
            get;
        }

        Type ContextBinderType {
            get;
        }

        Type ProxyRequiredAttributeType {
            get;
        }

        Type ListenerType {
            get;
        }

        InvokingCallback InvokingCallback {
            get;
        }

        InvokedCallback InvokedCallback {
            get;
        }

        CatchCallback CatchCallback {
            get;
        }

        Type LazyCallbackType {
            get;
        }
    }

    public delegate void BindServiceCallback(Attribute attribute,BindServiceCallbackCore callback);

    public delegate void BindServiceCallbackCore(Type service,Type objectType);

    public delegate object InvokingCallback(object listener, MethodBase method, object instance, object[] parameters);

    public delegate object InvokedCallback(object listener, MethodBase method, object instance, object result);

    public delegate Exception CatchCallback(object listener, MethodBase method, object instance, Exception ex);

    public delegate T Lazy<T>() where T : class;
}
