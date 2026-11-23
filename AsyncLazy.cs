using System;
using System.Threading;
using System.Threading.Tasks;

namespace Saraff.IoC {

    /// <summary>
    /// Ленивое асинхронное значение: фабрика выполняется один раз, результат (или исключение)
    /// кэшируется. Удобно для сервисов, требующих асинхронного создания.
    /// </summary>
    public sealed class AsyncLazy<T> {
        private readonly System.Lazy<Task<T>> _value;

        /// <summary>
        /// Создаёт экземпляр с фабрикой без токена отмены.
        /// </summary>
        public AsyncLazy(Func<Task<T>> factory) : this(EnsureFactory(factory), CancellationToken.None) {
        }

        /// <summary>
        /// Создаёт экземпляр с фабрикой, принимающей токен отмены.
        /// </summary>
        public AsyncLazy(Func<CancellationToken, Task<T>> factory) : this(factory, CancellationToken.None) {
        }

        /// <summary>
        /// Создаёт экземпляр с фабрикой и токеном отмены.
        /// </summary>
        public AsyncLazy(Func<CancellationToken, Task<T>> factory, CancellationToken cancellationToken) {
            if(factory == null) {
                throw new ArgumentNullException(nameof(factory));
            }
            this._value = new System.Lazy<Task<T>>(() => factory(cancellationToken), LazyThreadSafetyMode.ExecutionAndPublication);
        }

        /// <summary>
        /// Задача, возвращающая значение. Фабрика запускается при первом обращении.
        /// </summary>
        public Task<T> Value => this._value.Value;

        /// <summary>
        /// Признак того, что фабрика уже запускалась.
        /// </summary>
        public bool IsValueCreated => this._value.IsValueCreated;

        private static Func<CancellationToken, Task<T>> EnsureFactory(Func<Task<T>> factory) {
            if(factory == null) {
                throw new ArgumentNullException(nameof(factory));
            }
            return _ => factory();
        }
    }
}
