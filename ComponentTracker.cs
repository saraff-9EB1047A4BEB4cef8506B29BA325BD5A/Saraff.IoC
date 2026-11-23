using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Saraff.IoC {

    internal sealed class ComponentTracker {
        private readonly List<object> _items = new List<object>();

        public void Track(object instance) {
            lock(this._items) {
                this._items.Add(instance);
            }
        }

        public bool DisposeAll() {
            object[] _items;
            lock(this._items) {
                _items = this._items.ToArray();
                this._items.Clear();
            }
#if NETSTANDARD
            var _asyncOnly = false;
#endif
            for(var i = _items.Length - 1; i >= 0; i--) {
                var _disposable = _items[i] as IDisposable;
                if(_disposable != null) {
                    _disposable.Dispose();
                    continue;
                }
#if NETSTANDARD
                if(_items[i] is IAsyncDisposable) {
                    _asyncOnly = true;
                }
#endif
            }
#if NETSTANDARD
            return _asyncOnly;
#else
            return false;
#endif
        }

#if NETSTANDARD

        public async Task DisposeAllAsync() {
            object[] _items;
            lock(this._items) {
                _items = this._items.ToArray();
                this._items.Clear();
            }
            for(var i = _items.Length - 1; i >= 0; i--) {
                if(_items[i] is IAsyncDisposable _asyncDisposable) {
                    await _asyncDisposable.DisposeAsync().ConfigureAwait(false);
                    continue;
                }
                if(_items[i] is IDisposable _disposable) {
                    _disposable.Dispose();
                }
            }
        }

#endif
    }
}
