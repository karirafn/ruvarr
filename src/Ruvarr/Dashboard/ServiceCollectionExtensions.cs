using Ruvarr.Abstractions;
using Ruvarr.Dashboard.Queries.GetDashboard;

namespace Ruvarr.Dashboard;

internal static class ServiceCollectionExtensions
{
    internal static IServiceCollection AddDashboard(this IServiceCollection services)
    {
        services.AddTransient<IRequestHandler<GetDashboardQuery, DashboardData>, GetDashboardHandler>();

        return services;
    }
}
