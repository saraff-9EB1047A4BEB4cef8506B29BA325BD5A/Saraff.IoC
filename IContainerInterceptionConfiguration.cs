using System;

namespace Saraff.IoC {

    /// <summary>
    /// Настройки проксирования и прослушивания вызовов.
    /// </summary>
    public interface IContainerInterceptionConfiguration {

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
    }
}
