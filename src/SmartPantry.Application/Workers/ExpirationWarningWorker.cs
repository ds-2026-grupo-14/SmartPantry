using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SmartPantry.Pantry;
using Volo.Abp.BackgroundWorkers;
using Volo.Abp.Threading;
using Volo.Abp.Uow;

namespace SmartPantry.Workers;

public class ExpirationWarningWorker : AsyncPeriodicBackgroundWorkerBase
{
    public ExpirationWarningWorker(
        AbpAsyncTimer timer,
        IServiceScopeFactory serviceScopeFactory) 
        : base(timer, serviceScopeFactory)
    {
        // 12 hours in milliseconds
        timer.Period = 12 * 60 * 60 * 1000;
    }

    protected override async Task DoWorkAsync(PeriodicBackgroundWorkerContext workerContext)
    {
        Logger.LogInformation("Starting ExpirationWarningWorker to process pantry expirations...");

        try
        {
            using var scope = workerContext.ServiceProvider.CreateScope();
            
            var uowManager = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();
            var expirationManager = scope.ServiceProvider.GetRequiredService<PantryExpirationManager>();

            using (var uow = uowManager.Begin(new AbpUnitOfWorkOptions { IsTransactional = true }))
            {
                var processedCount = await expirationManager.ProcessExpirationsAsync(
                    referenceLocalDate: null, 
                    cancellationToken: workerContext.CancellationToken);
                    
                await uow.CompleteAsync();
                
                Logger.LogInformation("ExpirationWarningWorker completed successfully. Processed warnings: {Count}", processedCount);
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "An error occurred while executing ExpirationWarningWorker. The timer will continue.");
        }
    }
}
