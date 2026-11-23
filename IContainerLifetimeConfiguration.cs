namespace Saraff.IoC {

    /// <summary>
    /// Настройки политик времени жизни сервисов.
    /// </summary>
    public interface IContainerLifetimeConfiguration {

        IServiceLifetimeFactory ServiceLifetimeFactory {
            get;
        }
    }
}
