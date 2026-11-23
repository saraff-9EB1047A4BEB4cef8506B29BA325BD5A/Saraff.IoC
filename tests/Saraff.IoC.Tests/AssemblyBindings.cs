using Saraff.IoC;
using Saraff.IoC.Tests;

[assembly: BindService(typeof(ILoadedService), typeof(LoadedService))]

namespace Saraff.IoC.Tests {

    public interface ILoadedService {
        string Name { get; }
    }

    public sealed class LoadedService : System.ComponentModel.Component, ILoadedService {
        public string Name => "loaded";
    }
}
