using System;
using System.Collections.Generic;
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
    }

    public delegate void BindServiceCallback(Attribute attribute,BindServiceCallbackCore callback);

    public delegate void BindServiceCallbackCore(Type service,Type objectType);
}
