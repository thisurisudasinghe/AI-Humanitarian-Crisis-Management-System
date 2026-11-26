using Cms.Application.Cases.Interfaces;
using Cms.Application.Contracts;
using Cms.Application.Interfaces.Announcements;
using Cms.Application.Interfaces.AI;
using Cms.Application.Interfaces.Governance;
using Cms.Infrastructure.Notifications;
using Cms.Infrastructure.Services.AI;
using Cms.Infrastructure.Services.Announcements;
using Cms.Infrastructure.Services.Governance;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Cms.Api.Modules;

/// <summary>
/// Member 3 - AI decision support, human governance and notifications (M3/M4/M9).
/// Registers real implementations after the foundation so fail-closed stubs are
/// replaced. Every critical action remains behind a recorded human decision.
/// </summary>
public static class AiModule
{
    public static IServiceCollection AddAiModule(this IServiceCollection services)
    {
        // AI decision support: offline rule-based provider with optional LLM fallback.
        services.AddScoped<RuleBasedProvider>();
        services.AddScoped<LlmProvider>();
        services.AddScoped<IAiProvider, AiProviderSelector>();
        services.AddScoped<IAiChatService, AiChatService>();
        services.AddScoped<IRecommendationService, RecommendationService>();
        services.AddScoped<IMatchRecommendationService, MatchRecommendationService>();

        // React to request submission without ever blocking the citizen flow.
        services.TryAddEnumerable(
            ServiceDescriptor.Scoped<IOutboxHandler, RequestSubmittedHandler>());
        services.TryAddEnumerable(
            ServiceDescriptor.Scoped<IOutboxHandler, EmailNotificationHandler>());

        // Human governance: real gates replace both fail-closed placeholders.
        services.AddScoped<Cms.Application.Contracts.IDecisionGate, DecisionGate>();
        services.AddScoped<Cms.Application.Interfaces.Governance.IDecisionGate, DecisionGate>();
        services.AddScoped<ICloseCaseDecisionGate, CloseCaseDecisionGateAdapter>();
        services.AddScoped<Cms.Application.Interfaces.Governance.IDecisionService,
            Cms.Infrastructure.Services.Governance.DecisionService>();
        services.AddScoped<IPostIncidentReviewService, PostIncidentReviewService>();

        // Emergency broadcasts are created pending approval and fanned out via outbox.
        services.AddScoped<IAnnouncementService, AnnouncementService>();

        return services;
    }
}
