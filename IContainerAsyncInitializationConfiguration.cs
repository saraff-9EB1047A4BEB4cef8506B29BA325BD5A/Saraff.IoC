namespace Saraff.IoC {

    /// <summary>
    /// Настройки асинхронной инициализации созданных сервисов.
    /// </summary>
    public interface IContainerAsyncInitializationConfiguration {

        IAsyncInitializationAdapter AsyncInitializationAdapter {
            get;
        }
    }
}
