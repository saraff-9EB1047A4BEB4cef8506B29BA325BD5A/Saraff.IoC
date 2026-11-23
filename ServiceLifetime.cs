using System;

namespace Saraff.IoC {

    /// <summary>
    /// Определяет время жизни зарегистрированного сервиса.
    /// </summary>
    public enum ServiceLifetime {

        /// <summary>
        /// Один экземпляр на контейнер, в котором выполнена регистрация.
        /// </summary>
        Singleton,

        /// <summary>
        /// Один экземпляр на область (scope).
        /// </summary>
        Scoped,

        /// <summary>
        /// Новый экземпляр при каждом разрешении.
        /// </summary>
        Transient
    }
}
