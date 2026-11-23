using System;

namespace Saraff.IoC {

    public struct InvocationResult : IInvocationResult {

        public InvocationResult(bool handled, object value) {
            this.Handled = handled;
            this.Value = value;
        }

        public bool Handled { get; }

        public object Value { get; }

        public static InvocationResult NotHandled => default(InvocationResult);

        public static InvocationResult FromResult(object value) => new InvocationResult(true, value);
    }
}
