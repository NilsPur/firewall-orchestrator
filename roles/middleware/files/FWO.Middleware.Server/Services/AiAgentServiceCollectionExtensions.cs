using Microsoft.Agents.AI.Hosting;

namespace FWO.Middleware.Server.Services
{
    internal static class AiAgentServiceCollectionExtensions
    {
        /// <summary>
        /// Registers the FWO assistant agent so every request resolves the currently configured provider and model.
        /// </summary>
        public static IServiceCollection AddFwoAssistantAgent(this IServiceCollection services)
        {
            services.AddAIAgent(AgentFactoryService.AgentName,
                    (serviceProvider, name) => serviceProvider.GetRequiredService<AgentFactoryService>().GetAgent().GetAwaiter().GetResult(),
                    ServiceLifetime.Transient)
                .WithSessionStore((serviceProvider, name) => new FwoAgentSessionStore(
                        serviceProvider.GetRequiredService<AiSessionService>(),
                        serviceProvider.GetRequiredService<IHttpContextAccessor>()),
                    ServiceLifetime.Singleton, withIsolation: false);
            return services;
        }
    }
}
