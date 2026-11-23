using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Saraff.IoC.Tests {

    public class AsyncLazyTests {

        [Fact]
        public async Task AsyncLazyExecutesFactoryOnce() {
            var calls = 0;
            var lazy = new AsyncLazy<int>(async cancellationToken => {
                Interlocked.Increment(ref calls);
                await Task.Yield();
                return 42;
            });

            Assert.False(lazy.IsValueCreated);
            Assert.Equal(42, await lazy.Value);
            Assert.Equal(42, await lazy.Value);
            Assert.Equal(1, calls);
            Assert.True(lazy.IsValueCreated);
        }

        [Fact]
        public async Task AsyncLazyPropagatesFactoryExceptionOnce() {
            var calls = 0;
            var lazy = new AsyncLazy<int>(cancellationToken => {
                Interlocked.Increment(ref calls);
                throw new InvalidOperationException("boom");
            });

            await Assert.ThrowsAsync<InvalidOperationException>(async () => await lazy.Value);
            await Assert.ThrowsAsync<InvalidOperationException>(async () => await lazy.Value);

            Assert.Equal(1, calls);
        }

        [Fact]
        public async Task AsyncLazySupportsTokenlessFactoryAndValidatesNull() {
            var lazy = new AsyncLazy<int>(() => Task.FromResult(7));

            Assert.Equal(7, await lazy.Value);
            Assert.Throws<ArgumentNullException>(() => new AsyncLazy<int>((Func<Task<int>>)null));
            Assert.Throws<ArgumentNullException>(() => new AsyncLazy<int>((Func<CancellationToken, Task<int>>)null));
        }
    }
}
