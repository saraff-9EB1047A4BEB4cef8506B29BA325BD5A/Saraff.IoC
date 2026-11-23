using System;
using System.Threading;
using System.Threading.Tasks;

namespace Saraff.IoC {

#if NETSTANDARD

    /// <summary>
    /// Сервис, требующий асинхронной инициализации после создания.
    /// </summary>
    public interface IAsyncInitializable {

        /// <summary>
        /// Выполняет асинхронную инициализацию. Вызывается контейнером один раз.
        /// </summary>
        Task InitializeAsync(CancellationToken cancellationToken = default(CancellationToken));
    }

#endif
}
