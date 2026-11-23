namespace Saraff.IoC {

    /// <summary>
    /// Настройки создания описаний регистраций.
    /// </summary>
    public interface IContainerRegistrationConfiguration {

        IServiceRegistrationFactory ServiceRegistrationFactory {
            get;
        }
    }
}
