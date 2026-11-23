using System;

namespace Saraff.IoC {

    /// <summary>
    /// Настройки внедрения зависимостей и специальных generic-типов.
    /// </summary>
    public interface IContainerInjectionConfiguration {

        Type ServiceRequiredAttributeType {
            get;
        }

        Type ContextBinderType {
            get;
        }

        Type LazyCallbackType {
            get;
        }
    }
}
