using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using Xunit;


namespace Orlum.SwitchableProxy.Tests.Unit
{
    public class TorProxyTest
    {
        private readonly IConfiguration _configurationTorProxyEnabled;
        private readonly IConfiguration _configurationTorProxyDisabled;
        private readonly IConfiguration _configurationNotDefaultSettings;
        private readonly IConfiguration _configurationEmpty;
        private readonly ILogger _logger;


        public TorProxyTest()
        {
            var assemblyFolder = Path.GetDirectoryName(Assembly.GetAssembly(GetType()).Location);

            _configurationTorProxyEnabled = new ConfigurationBuilder()
                   .SetBasePath(assemblyFolder)
                   .AddYamlFile("TorProxyEnabled.yaml", optional: false, reloadOnChange: false)
                   .Build()
                   .GetSection("TorProxy");

            _configurationTorProxyDisabled = new ConfigurationBuilder()
                   .SetBasePath(assemblyFolder)
                   .AddYamlFile("TorProxyDisabled.yaml", optional: false, reloadOnChange: false)
                   .Build()
                   .GetSection("TorProxy");

            _configurationNotDefaultSettings = new ConfigurationBuilder()
                   .SetBasePath(assemblyFolder)
                   .AddYamlFile("TorProxyNotDefaultSettings.yaml", optional: false, reloadOnChange: false)
                   .Build()
                   .GetSection("TorProxy");

            _configurationEmpty = new ConfigurationBuilder()
                   .SetBasePath(assemblyFolder)
                   .AddYamlFile("Empty.yaml", optional: false, reloadOnChange: false)
                   .Build()
                   .GetSection("TorProxy");

            _logger = NullLogger.Instance;
        }


        [Fact]
        public async Task ExitNodeAreBeingChanged()
        {
            using var proxy = new TorProxy(_configurationTorProxyEnabled, _logger);
            var address = await proxy.GetCurrentExitNodeAsync().ConfigureAwait(false);

            proxy.ChangeExitNodeAsync().GetAwaiter().GetResult();
            var newAddress = await proxy.GetCurrentExitNodeAsync().ConfigureAwait(false);

            Assert.NotEqual(address, newAddress);
        }


        [Fact]
        public async Task ExitNodeAreBeingChangedForAllConnectionsSimultaneously()
        {
            using var proxy1 = new TorProxy(_configurationTorProxyEnabled, _logger);
            var address1 = await proxy1.GetCurrentExitNodeAsync().ConfigureAwait(false);

            using var proxy2 = new TorProxy(_configurationTorProxyEnabled, _logger);
            var address2 = await proxy2.GetCurrentExitNodeAsync().ConfigureAwait(false);

            proxy1.ChangeExitNodeAsync().GetAwaiter().GetResult();
            var newAddress1 = await proxy1.GetCurrentExitNodeAsync().ConfigureAwait(false);
            var newAddress2 = await proxy2.GetCurrentExitNodeAsync().ConfigureAwait(false);

            Assert.NotEqual(address1, newAddress1);
            Assert.NotEqual(address2, newAddress2);
        }


        [Fact]
        public async Task ExitNodeDoesNotBeingChangedIfTorProxyIsDisabled()
        {
            using var proxy = new TorProxy(_configurationTorProxyDisabled, _logger);
            var address = await proxy.GetCurrentExitNodeAsync().ConfigureAwait(false);

            proxy.ChangeExitNodeAsync().GetAwaiter().GetResult();
            var newAddress = await proxy.GetCurrentExitNodeAsync().ConfigureAwait(false);

            Assert.Equal(address, newAddress);
        }


        [Fact]
        public void ProxiesUsedPropertyCountsUsedProxies()
        {
            using var proxy = new TorProxy(_configurationTorProxyEnabled, _logger);
            Assert.Equal(1, proxy.ProxiesUsed);

            proxy.ChangeExitNodeAsync().GetAwaiter().GetResult();
            Assert.Equal(2, proxy.ProxiesUsed);

            proxy.ChangeExitNodeAsync().GetAwaiter().GetResult();
            Assert.Equal(3, proxy.ProxiesUsed);
        }


        [Fact]
        public void ProxiesUsedPropertyDoesNotCountingIfTorProxyIsDisabled()
        {
            using var proxy = new TorProxy(_configurationTorProxyDisabled, _logger);
            Assert.Equal(0, proxy.ProxiesUsed);

            proxy.ChangeExitNodeAsync().GetAwaiter().GetResult();
            Assert.Equal(0, proxy.ProxiesUsed);
        }


        [Fact]
        public void DefaultSettingsAreBeingInitializedCorrectly()
        {
            using var proxy = new TorProxy(_configurationEmpty, _logger);
            Assert.False(proxy.Disabled);
            Assert.Equal("127.0.0.1", proxy.Address);
            Assert.Equal(9050, proxy.Port);
            Assert.Equal(9051, proxy.ControlPort);
            Assert.Equal(string.Empty, proxy.ControlPassword);
            Assert.Equal(TimeSpan.FromSeconds(60), proxy.CircuitBiuldTimeoutSeconds);
        }


        [Fact]
        public void ConfigurationFileAreBeingReadCorrectly()
        {
            using var proxy = new TorProxy(_configurationNotDefaultSettings, _logger);
            Assert.True(proxy.Disabled);
            Assert.Equal("127.0.0.100", proxy.Address);
            Assert.Equal(19050, proxy.Port);
            Assert.Equal(19051, proxy.ControlPort);
            Assert.Equal("111", proxy.ControlPassword);
            Assert.Equal(TimeSpan.FromSeconds(20.5), proxy.CircuitBiuldTimeoutSeconds);
        }
    }
}
