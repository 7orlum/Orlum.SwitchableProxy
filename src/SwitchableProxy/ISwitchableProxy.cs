using System;
using System.Threading;
using System.Threading.Tasks;


namespace Orlum.SwitchableProxy
{
    public interface ISwitchableProxy
    {
        bool Disabled { get; }
        string Address { get; }
        int Port { get; }
        long ProxiesUsed { get; }


        Task<string> GetCurrentExitNodeAsync(CancellationToken cancellationToken = default);
        Task ChangeExitNodeAsync(CancellationToken cancellationToken = default);
    }
}