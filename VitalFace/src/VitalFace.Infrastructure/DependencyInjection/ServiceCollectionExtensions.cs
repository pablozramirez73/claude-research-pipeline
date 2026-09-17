using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using QuestPDF.Infrastructure;
using VitalFace.Core.Abstractions;
using VitalFace.Infrastructure.Persistence;
using VitalFace.Infrastructure.Pdf;

namespace VitalFace.Infrastructure.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddVitalFaceInfrastructure(this IServiceCollection services, string postgresConnectionString)
    {
        QuestPDF.Settings.License = LicenseType.Community;

        services.AddDbContext<VitalFaceDbContext>(options =>
            options.UseNpgsql(postgresConnectionString, npgsql =>
                npgsql.EnableRetryOnFailure(maxRetryCount: 3)));

        services.AddScoped<IScreeningRepository, ScreeningRepository>();
        services.AddSingleton<IScreeningTicketService, ScreeningTicketService>();

        return services;
    }
}
