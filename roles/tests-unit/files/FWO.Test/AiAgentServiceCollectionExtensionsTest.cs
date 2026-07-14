using FWO.Middleware.Server.Services;
using Microsoft.Agents.AI;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace FWO.Test
{
    [TestFixture]
    public class AiAgentServiceCollectionExtensionsTest
    {
        [Test]
        public void AddFwoAssistantAgent_RegistersAgentAsTransient()
        {
            ServiceCollection services = new();

            services.AddFwoAssistantAgent();

            ServiceDescriptor agentRegistration = services.Single(descriptor =>
                descriptor.ServiceType == typeof(AIAgent)
                && descriptor.IsKeyedService
                && Equals(descriptor.ServiceKey, AgentFactoryService.AgentName));
            Assert.That(agentRegistration.Lifetime, Is.EqualTo(ServiceLifetime.Transient));
        }
    }
}
