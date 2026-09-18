using KineMotion.Core.Services;
using KineMotion.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace KineMotion.Infrastructure;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddKineMotionInfrastructure(this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<KineMotionDbContext>(options =>
            options.UseNpgsql(connectionString, npgsql => npgsql.EnableRetryOnFailure(maxRetryCount: 3)));

        services.AddScoped<ISessionRepository, SessionRepository>();
        services.AddScoped<IPrescriptionRepository, PrescriptionRepository>();
        services.AddSingleton<RomCalculator>();
        services.AddSingleton<TremorAnalyzer>();

        return services;
    }
}
