using System;
using System.Threading.Tasks;

namespace Hourstone.Companion.App;

internal interface IStoreStartupController
{
    Task<AppStartupState> GetStateAsync();
    Task<AppStartupState> SetEnabledAsync(bool enabled);
}

internal sealed class StoreStartupController : IStoreStartupController
{
    static void RequireStore()
    {
        if (!AppDistribution.Current.IsStore) throw new InvalidOperationException("Store startup requires package identity.");
    }
    public Task<AppStartupState> GetStateAsync() { RequireStore(); return StartupService.GetStateAsync(); }
    public Task<AppStartupState> SetEnabledAsync(bool enabled) { RequireStore(); return StartupService.SetEnabledAsync(enabled); }
}
