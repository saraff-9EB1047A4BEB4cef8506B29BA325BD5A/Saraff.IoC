using System;

namespace Saraff.IoC {

    /// <summary>
    /// Настройки привязки сервисов из атрибутов сборки.
    /// </summary>
    public interface IContainerBindingConfiguration {

        Type BindServiceAttributeType {
            get;
        }

        BindServiceCallback BindServiceCallback {
            get;
        }
    }
}
