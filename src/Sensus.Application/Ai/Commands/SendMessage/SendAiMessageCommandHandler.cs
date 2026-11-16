using System.Diagnostics;
using System.Text.Json;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sensus.Application.Ai.DTOs;
using Sensus.Application.Ai.Services;
using Sensus.Application.Common.Interfaces;
using Sensus.Application.Common.Settings;
using Sensus.Domain.Common;
using Sensus.Domain.Entities;
using Sensus.Domain.Enums;
using Sensus.Domain.Repositories;
using Sensus.Domain.ValueObjects;

namespace Sensus.Application.Ai.Commands.SendMessage;

/// <summary>
/// Orquestador del bucle agéntico de 7 capas para Sensi IA (FR-046-06, FR-046-07, NFR-046-01, NFR-046-04).
/// </summary>
public sealed class SendAiMessageCommandHandler : IRequestHandler<SendAiMessageCommand, Result<AiTurnResponseDto>>
{
    /// <summary>Límite de ejecución de la extracción de presupuesto (mitigación de ReDoS, regla S6444).</summary>
    private static readonly TimeSpan BudgetRegexTimeout = TimeSpan.FromSeconds(1);

    private readonly ICurrentUserService _currentUserService;
    private readonly IUserRepository _userRepository;
    private readonly IAiConversationRepository _conversationRepository;
    private readonly IAiMessageRepository _messageRepository;
    private readonly IAiTraceLogRepository _traceLogRepository;
    private readonly IScopeAuthorizationMapper _scopeMapper;
    private readonly IMcpToolRegistry _toolRegistry;
    private readonly IMcpToolExecutor _toolExecutor;
    private readonly IPromptInjectionDetector _injectionDetector;
    private readonly ITextModerationService _textModerationService;
    private readonly IHardRuleLeakDetector _hardRuleLeakDetector;
    private readonly IPiiRedactor _piiRedactor;
    private readonly IAiModelRouter _modelRouter;
    private readonly IAiBudgetGuard _budgetGuard;
    private readonly ITwoBlockPromptBuilder _promptBuilder;
    private readonly IKnowledgeBaseProvider _knowledgeBaseProvider;
    private readonly IAiChatGateway _chatGateway;
    private readonly IDeterministicAiFallbackService _fallbackService;
    private readonly ICorrelationIdAccessor _correlationIdAccessor;
    private readonly SensiSettings _sensiSettings;
    private readonly ILogger<SendAiMessageCommandHandler> _logger;

    public SendAiMessageCommandHandler(
        ICurrentUserService currentUserService,
        IUserRepository userRepository,
        IAiConversationRepository conversationRepository,
        IAiMessageRepository messageRepository,
        IAiTraceLogRepository traceLogRepository,
        IScopeAuthorizationMapper scopeMapper,
        IMcpToolRegistry toolRegistry,
        IMcpToolExecutor toolExecutor,
        IPromptInjectionDetector injectionDetector,
        ITextModerationService textModerationService,
        IHardRuleLeakDetector hardRuleLeakDetector,
        IPiiRedactor piiRedactor,
        IAiModelRouter modelRouter,
        IAiBudgetGuard budgetGuard,
        ITwoBlockPromptBuilder promptBuilder,
        IKnowledgeBaseProvider knowledgeBaseProvider,
        IAiChatGateway chatGateway,
        IDeterministicAiFallbackService fallbackService,
        ICorrelationIdAccessor correlationIdAccessor,
        IOptions<SensiSettings> sensiOptions,
        ILogger<SendAiMessageCommandHandler> logger)
    {
        _currentUserService = currentUserService;
        _userRepository = userRepository;
        _conversationRepository = conversationRepository;
        _messageRepository = messageRepository;
        _traceLogRepository = traceLogRepository;
        _scopeMapper = scopeMapper;
        _toolRegistry = toolRegistry;
        _toolExecutor = toolExecutor;
        _injectionDetector = injectionDetector;
        _textModerationService = textModerationService;
        _hardRuleLeakDetector = hardRuleLeakDetector;
        _piiRedactor = piiRedactor;
        _modelRouter = modelRouter;
        _budgetGuard = budgetGuard;
        _promptBuilder = promptBuilder;
        _knowledgeBaseProvider = knowledgeBaseProvider;
        _chatGateway = chatGateway;
        _fallbackService = fallbackService;
        _correlationIdAccessor = correlationIdAccessor;
        _sensiSettings = sensiOptions.Value;
        _logger = logger;
    }

    public async Task<Result<AiTurnResponseDto>> Handle(
        SendAiMessageCommand request,
        CancellationToken cancellationToken)
    {
        var correlationId = !string.IsNullOrWhiteSpace(_correlationIdAccessor.CorrelationId)
            ? _correlationIdAccessor.CorrelationId
            : Guid.NewGuid().ToString();

        // 1. Capa 4: Autenticación y Autorización
        if (string.IsNullOrWhiteSpace(_currentUserService.UserId) || !Guid.TryParse(_currentUserService.UserId, out var userId))
        {
            return Result.Failure<AiTurnResponseDto>(
                Error.Unauthorized("Ai.Unauthorized", "Se requiere autenticación para interactuar con Sensi IA."));
        }

        var user = await _userRepository.GetByIdAsync(userId, cancellationToken);
        if (user is null || !user.Estatus)
        {
            return Result.Failure<AiTurnResponseDto>(
                Error.Unauthorized("Ai.UserNotFound", "El usuario no existe o se encuentra inactivo."));
        }

        if (user.HasRole(RoleType.Invitado))
        {
            return Result.Failure<AiTurnResponseDto>(
                Error.Forbidden("Ai.GuestForbidden", "Las sesiones de invitado no tienen acceso a Sensi IA."));
        }

        var activeRole = user.HasRole(RoleType.Comerciante) ? RoleType.Comerciante : RoleType.Consumidor;

        // 2. Capa 5: Guardrails de Entrada (Longitud, Inyección, Moderación)
        if (string.IsNullOrWhiteSpace(request.Message))
        {
            return Result.Failure<AiTurnResponseDto>(
                Error.Validation("Ai.EmptyMessage", "El mensaje no puede estar vacío."));
        }

        if (request.Message.Length > _sensiSettings.MaxInputChars)
        {
            return Result.Failure<AiTurnResponseDto>(
                Error.Validation("Ai.MessageTooLong", $"El mensaje excede el límite máximo de {_sensiSettings.MaxInputChars} caracteres."));
        }

        var injectionCheck = _injectionDetector.Check(request.Message);
        if (injectionCheck.IsSuspicious)
        {
            _logger.LogWarning("Inyección de prompt detectada en mensaje de usuario {UserId}: {Reason}", userId, injectionCheck.Reason);
            return Result.Failure<AiTurnResponseDto>(
                Error.Validation("Ai.PromptInjectionDetected", "Tu mensaje contiene patrones no permitidos por nuestras normas de seguridad."));
        }

        var inputModeration = await _textModerationService.ModerateTextAsync(request.Message, cancellationToken);
        if (!inputModeration.IsApproved)
        {
            _logger.LogWarning("Mensaje de usuario {UserId} bloqueado por moderación de texto. Decisión: {Decision}", userId, inputModeration.Decision);
            return Result.Failure<AiTurnResponseDto>(
                Error.Validation("Ai.MessageBlockedByModeration", inputModeration.Message ?? "Tu mensaje no cumple con nuestras normas de contenido."));
        }

        // 3. Obtener o crear conversación con afinidad de sesión (Capa 6)
        var knowledgeDoc = _knowledgeBaseProvider.GetDocument();
        var now = DateTime.UtcNow;
        Guid? convId = null;
        if (!string.IsNullOrWhiteSpace(request.ConversationId) && Guid.TryParse(request.ConversationId, out var parsedConvId))
        {
            convId = parsedConvId;
        }

        AiConversation conversation;
        var validatedScreen = KnownScreenContexts.Normalize(request.ScreenContext);

        if (convId.HasValue)
        {
            var existing = await _conversationRepository.GetByIdAsync(convId.Value, cancellationToken);
            if (existing is null || existing.OwnerUserId != userId)
            {
                return Result.Failure<AiTurnResponseDto>(
                    Error.NotFound("Ai.ConversationNotFound", "La conversación no existe o no pertenece al usuario."));
            }

            conversation = existing;
            if (validatedScreen != null)
            {
                conversation.UpdateScreenContext(validatedScreen, now);
            }
        }
        else
        {
            var active = await _conversationRepository.GetActiveByUserIdAsync(userId, cancellationToken);
            if (active is not null)
            {
                conversation = active;
                if (validatedScreen != null)
                {
                    conversation.UpdateScreenContext(validatedScreen, now);
                }
            }
            else
            {
                var isReasoningAvailable = await _budgetGuard.IsReasoningBudgetAvailableAsync(cancellationToken);
                var routing = _modelRouter.RouteInitialTurn(request.Message, isReasoningAvailable);
                var created = AiConversation.Create(
                    userId,
                    activeRole,
                    routing.SelectedModel,
                    routing.Tier,
                    knowledgeDoc.Version,
                    validatedScreen,
                    now);

                if (created.IsFailure)
                {
                    return Result.Failure<AiTurnResponseDto>(created.Error);
                }

                conversation = created.Value;
                await _conversationRepository.AddAsync(conversation, cancellationToken);
            }
        }

        // 4. Mapeo de Scopes y Herramientas Permitidas (Capa 3 & 4)
        var callerScopes = _scopeMapper.MapScopesByRole(activeRole);
        var allowedTools = _toolRegistry.GetAllowedTools(callerScopes);

        // Guardar mensaje de usuario en repositorio
        var userMsgResult = AiMessage.Create(
            conversation.Id,
            userId,
            AiMessageRole.User,
            _piiRedactor.Redact(request.Message),
            false,
            null,
            null,
            null,
            null,
            now);

        if (userMsgResult.IsSuccess)
        {
            await _messageRepository.AddAsync(userMsgResult.Value, cancellationToken);
            conversation.IncrementMessageCount(now);
            await _conversationRepository.UpdateAsync(conversation, cancellationToken);
        }

        // 5. Verificación de Presupuesto (Capa 6 - EX-001)
        var estimatedCost = conversation.RoutingTier == AiRoutingTier.Reasoning
            ? _sensiSettings.ReasoningEstimatedCostUsd
            : _sensiSettings.FastEstimatedCostUsd;

        var canExecute = await _budgetGuard.CanExecuteTurnAsync(userId, estimatedCost, cancellationToken);

        if (!canExecute)
        {
            _logger.LogWarning("Presupuesto o cuota diaria agotada para usuario {UserId}. Activando degradación determinista.", userId);
            return await ExecuteFallbackTurnAsync(
                request.Message,
                conversation,
                userId,
                correlationId,
                knowledgeDoc.Version,
                knowledgeDoc.HashSha256,
                "Límite de presupuesto mensual o cuota diaria de IA alcanzado.",
                cancellationToken);
        }

        // 6. Preparar Prompt en 2 Bloques y Ejecutar Bucle Agéntico
        var sessionContext = new UserSessionContext(
            activeRole,
            validatedScreen ?? conversation.LastScreenContext,
            request.Latitude,
            request.Longitude,
            request.RadiusKm);

        var recentMessages = await _messageRepository.GetRecentByConversationIdAsync(
            conversation.Id,
            _sensiSettings.MaxTurnsHistory,
            cancellationToken);

        var historyItems = recentMessages
            .Where(m => m.Id != (userMsgResult.IsSuccess ? userMsgResult.Value.Id : Guid.Empty))
            .Select(m => new AiMessageHistoryItem(
                m.Role == AiMessageRole.Assistant ? "assistant" : "user",
                m.RedactedContent))
            .ToList();

        var stopwatch = Stopwatch.StartNew();
        var toolsInvoked = new List<string>();
        var citedOfferIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var citedOffersList = new List<CitedOfferDto>();
        var packageCandidates = new List<RecommendedPackageItem>();
        string replyText = string.Empty;
        var rounds = 0;
        var maxRounds = _sensiSettings.MaxAgenticRounds;
        var promptTokensTotal = 0;
        var cachedTokensTotal = 0;
        var completionTokensTotal = 0;
        var cacheHitAny = false;

        try
        {
            var activeHistory = new List<AiMessageHistoryItem>(historyItems);

            while (rounds < maxRounds)
            {
                rounds++;
                var promptPayload = _promptBuilder.Build(
                    allowedTools,
                    knowledgeDoc,
                    sessionContext,
                    activeHistory,
                    request.Message);

                var chatRequest = new AiChatRequest(
                    conversation.AssignedModel,
                    promptPayload.OrderedTools,
                    promptPayload.StaticDeveloperPrompt,
                    promptPayload.DynamicSessionContext,
                    promptPayload.History,
                    promptPayload.CurrentUserPrompt);

                var completion = await _chatGateway.CompleteTurnAsync(chatRequest, cancellationToken);

                promptTokensTotal += completion.PromptTokens;
                cachedTokensTotal += completion.CachedTokens;
                completionTokensTotal += completion.CompletionTokens;
                if (completion.CacheHit)
                {
                    cacheHitAny = true;
                }

                if (completion.ToolCalls.Count == 0)
                {
                    replyText = completion.ReplyText;
                    break;
                }

                // Ejecución de herramientas MCP solicitadas por el modelo
                foreach (var toolCall in completion.ToolCalls)
                {
                    toolsInvoked.Add(toolCall.ToolName);
                    var toolResult = await _toolExecutor.ExecuteAsync(toolCall, userId, callerScopes, cancellationToken);

                    // Punto 9: Llenar citedOfferIds y ofertas candidatas para recomendación de paquete
                    if (toolResult.Success && !string.IsNullOrWhiteSpace(toolResult.ResultJson))
                    {
                        ExtractOffersFromToolResult(toolCall.ToolName, toolResult.ResultJson, citedOfferIds, citedOffersList, packageCandidates);
                    }

                    activeHistory.Add(new AiMessageHistoryItem("assistant", $"[Tool {toolCall.ToolName}]"));
                    activeHistory.Add(new AiMessageHistoryItem("developer", $"[Tool Result {toolCall.ToolName}]: {toolResult.ResultJson}"));
                }

                if (!string.IsNullOrWhiteSpace(completion.ReplyText))
                {
                    replyText = completion.ReplyText;
                }
            }

            stopwatch.Stop();

            // Punto 13: si se agotaron las rondas agénticas y el modelo no devolvió texto final, activar modo de respaldo
            if (string.IsNullOrWhiteSpace(replyText))
            {
                _logger.LogWarning("Rondas agénticas agotadas ({MaxRounds}) sin texto final de respuesta para usuario {UserId}. Activando degradación determinista.", maxRounds, userId);
                return await ExecuteFallbackTurnAsync(
                    request.Message,
                    conversation,
                    userId,
                    correlationId,
                    knowledgeDoc.Version,
                    knowledgeDoc.HashSha256,
                    "Rondas agénticas agotadas sin respuesta final.",
                    cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Punto 14: solo la cancelación del cliente se re-lanza; el timeout de HttpClient
            // (TaskCanceledException sin cancelación solicitada) cae al fallback determinista.
            throw;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Fallo durante la invocación del modelo OpenAI para usuario {UserId}. Activando degradación determinista.", userId);
            return await ExecuteFallbackTurnAsync(
                request.Message,
                conversation,
                userId,
                correlationId,
                knowledgeDoc.Version,
                knowledgeDoc.HashSha256,
                "Fallo o timeout en la llamada al modelo de IA.",
                cancellationToken);
        }

        // 7. Capa 5 & 1: Guardrails de Salida (Hard Rules, Moderación y Redacción PII)
        if (_hardRuleLeakDetector.ContainsForbiddenLeaks(replyText))
        {
            _logger.LogWarning("Fuga de Hard Rule detectada en respuesta del modelo. Sustituyendo por respuesta oficial segura.");
            replyText = "Soy Sensi, el asistente virtual oficial de Sensus. Estoy aquí para ayudarte a descubrir y aprovechar las mejores ofertas locales.";
        }

        var outputModeration = await _textModerationService.ModerateTextAsync(replyText, cancellationToken);
        if (!outputModeration.IsApproved)
        {
            _logger.LogWarning("Respuesta del modelo bloqueada por moderación de salida. Activando respuesta segura.");
            replyText = "No puedo generar esa respuesta en este momento debido a nuestras políticas de seguridad.";
        }

        var sanitizedReply = _piiRedactor.Redact(replyText);

        // 8. Costo, Registro de Gasto y Observabilidad (Capa 6 & 7)
        var actualCostUsd = BudgetCalculator.EstimateTurnCostUsd(
            conversation.RoutingTier,
            promptTokensTotal,
            cachedTokensTotal,
            completionTokensTotal);

        await _budgetGuard.RecordSpendAsync(userId, actualCostUsd, conversation.RoutingTier, cancellationToken);

        // Punto 9: Validar y construir paquete de ofertas si el usuario especificó presupuesto
        RecommendedPackageDto? recommendedPackageDto = null;
        var userBudget = ExtractBudgetFromPrompt(request.Message);
        if (userBudget.HasValue && userBudget.Value > 0 && packageCandidates.Count > 0)
        {
            var packageResult = BudgetCalculator.BuildPackage(userBudget.Value, packageCandidates);
            if (packageResult.IsSuccess)
            {
                var pkg = packageResult.Value;
                recommendedPackageDto = new RecommendedPackageDto(
                    pkg.BudgetMxn,
                    pkg.TotalPriceMxn,
                    pkg.SavingsMxn,
                    pkg.Items.Select(i => new CitedOfferDto(
                        i.OfferId,
                        i.Title,
                        i.MerchantId,
                        i.MerchantName,
                        i.OfferPriceMxn,
                        i.RegularPriceMxn,
                        i.AvailableStock,
                        i.IsOpenNow,
                        i.DistanceKm)).ToList());
            }
        }

        var assistantMsgResult = AiMessage.Create(
            conversation.Id,
            userId,
            AiMessageRole.Assistant,
            sanitizedReply,
            false,
            null,
            null,
            citedOfferIds,
            toolsInvoked,
            DateTime.UtcNow);

        var assistantMessage = assistantMsgResult.Value;
        await _messageRepository.AddAsync(assistantMessage, cancellationToken);
        conversation.IncrementMessageCount(DateTime.UtcNow);
        await _conversationRepository.UpdateAsync(conversation, cancellationToken);

        var traceLogResult = AiTraceLog.Create(
            conversation.Id,
            userId,
            correlationId,
            knowledgeDoc.Version,
            knowledgeDoc.HashSha256,
            conversation.RoutingTier,
            conversation.AssignedModel,
            _piiRedactor.Redact(request.Message),
            sanitizedReply,
            toolsInvoked,
            promptTokensTotal,
            cachedTokensTotal,
            completionTokensTotal,
            stopwatch.ElapsedMilliseconds,
            actualCostUsd,
            DateTime.UtcNow,
            TimeSpan.FromDays(_sensiSettings.TraceRetentionDays));

        if (traceLogResult.IsSuccess)
        {
            await _traceLogRepository.AddAsync(traceLogResult.Value, cancellationToken);
        }

        var usage = new AiUsageSummaryDto(
            promptTokensTotal,
            cachedTokensTotal,
            completionTokensTotal,
            cacheHitAny);

        return Result.Success(new AiTurnResponseDto(
            conversation.Id.ToString(),
            assistantMessage.Id.ToString(),
            sanitizedReply,
            recommendedPackageDto,
            citedOffersList.Count > 0 ? citedOffersList : null,
            toolsInvoked,
            false,
            null,
            conversation.RoutingTier.ToString(),
            knowledgeDoc.Version,
            correlationId,
            usage));
    }

    private static void ExtractOffersFromToolResult(
        string toolName,
        string resultJson,
        HashSet<string> citedOfferIds,
        List<CitedOfferDto> citedOffersList,
        List<RecommendedPackageItem> packageCandidates)
    {
        try
        {
            using var doc = JsonDocument.Parse(resultJson);
            var root = doc.RootElement;

            if (string.Equals(toolName, SensiToolNames.SearchOffers, StringComparison.OrdinalIgnoreCase))
            {
                if (root.TryGetProperty("offers", out var offersArray) && offersArray.ValueKind == JsonValueKind.Array)
                {
                    foreach (var elem in offersArray.EnumerateArray())
                    {
                        var id = elem.TryGetProperty("id", out var idProp) ? idProp.GetString() : null;
                        var title = elem.TryGetProperty("title", out var tProp) ? tProp.GetString() ?? string.Empty : string.Empty;
                        var discountedPrice = elem.TryGetProperty("discountedPrice", out var dpProp) ? dpProp.GetDecimal() : 0m;
                        var originalPrice = elem.TryGetProperty("originalPrice", out var opProp) ? opProp.GetDecimal() : discountedPrice;
                        var stock = elem.TryGetProperty("stock", out var stProp) ? stProp.GetInt32() : 1;
                        var merchantId = elem.TryGetProperty("merchantId", out var mProp) ? mProp.GetString() ?? string.Empty : string.Empty;

                        if (!string.IsNullOrWhiteSpace(id) && citedOfferIds.Add(id))
                        {
                            citedOffersList.Add(new CitedOfferDto(id, title, merchantId, string.Empty, discountedPrice, originalPrice, stock, true, null));
                            packageCandidates.Add(new RecommendedPackageItem(
                                id, title, merchantId, string.Empty, discountedPrice, originalPrice, stock, true));
                        }
                    }
                }
            }
            else if (string.Equals(toolName, SensiToolNames.GetOfferDetail, StringComparison.OrdinalIgnoreCase))
            {
                if (root.TryGetProperty("found", out var foundProp) && foundProp.GetBoolean())
                {
                    var id = root.TryGetProperty("id", out var idProp) ? idProp.GetString() : null;
                    var title = root.TryGetProperty("title", out var tProp) ? tProp.GetString() ?? string.Empty : string.Empty;
                    var discountedPrice = root.TryGetProperty("discountedPrice", out var dpProp) ? dpProp.GetDecimal() : 0m;
                    var originalPrice = root.TryGetProperty("originalPrice", out var opProp) ? opProp.GetDecimal() : discountedPrice;
                    var stock = root.TryGetProperty("stock", out var stProp) ? stProp.GetInt32() : 1;
                    string? merchantName = null;
                    string merchantId = string.Empty;

                    if (root.TryGetProperty("merchant", out var mObj) && mObj.ValueKind == JsonValueKind.Object)
                    {
                        if (mObj.TryGetProperty("nombre", out var nameProp)) merchantName = nameProp.GetString();
                        if (mObj.TryGetProperty("id", out var midProp)) merchantId = midProp.GetString() ?? string.Empty;
                    }

                    if (!string.IsNullOrWhiteSpace(id) && citedOfferIds.Add(id))
                    {
                        citedOffersList.Add(new CitedOfferDto(id, title, merchantId, merchantName ?? string.Empty, discountedPrice, originalPrice, stock, true, null));
                        packageCandidates.Add(new RecommendedPackageItem(
                            id, title, merchantId, merchantName ?? string.Empty, discountedPrice, originalPrice, stock, true));
                    }
                }
            }
        }
        catch
        {
            // Resilient parsing: si la respuesta JSON no tiene estructura de ofertas, continuar sin romper el turno
        }
    }

    private static decimal? ExtractBudgetFromPrompt(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return null;
        }

        System.Text.RegularExpressions.Match match;
        try
        {
            match = System.Text.RegularExpressions.Regex.Match(
                message,
                @"(?:\$|presupuesto\s+(?:de\s+)?|con\s+)(?<amount>\d+(?:\.\d{1,2})?)\s*(?:pesos|mxn|\$)?",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase,
                BudgetRegexTimeout);
        }
        catch (System.Text.RegularExpressions.RegexMatchTimeoutException)
        {
            // Sin presupuesto confiable no se arma paquete; el backend nunca inventa un monto.
            return null;
        }

        if (match.Success &&
            decimal.TryParse(
                match.Groups["amount"].Value,
                System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture,
                out var parsed) &&
            parsed > 0)
        {
            return parsed;
        }

        return null;
    }

    private async Task<Result<AiTurnResponseDto>> ExecuteFallbackTurnAsync(
        string userPrompt,
        AiConversation conversation,
        Guid userId,
        string correlationId,
        string markdownVersion,
        string markdownHash,
        string degradationReason,
        CancellationToken cancellationToken)
    {
        var userBudget = ExtractBudgetFromPrompt(userPrompt);
        var fallback = await _fallbackService.BuildFallbackAsync(userPrompt, userId, userBudget, cancellationToken);
        var now = DateTime.UtcNow;

        var assistantMsgResult = AiMessage.Create(
            conversation.Id,
            userId,
            AiMessageRole.Assistant,
            _piiRedactor.Redact(fallback.Reply),
            true,
            degradationReason,
            fallback.RecommendedPackage,
            fallback.OffersCited,
            Array.Empty<string>(),
            now);

        var assistantMessage = assistantMsgResult.Value;
        await _messageRepository.AddAsync(assistantMessage, cancellationToken);
        conversation.IncrementMessageCount(now);
        await _conversationRepository.UpdateAsync(conversation, cancellationToken);

        var traceLogResult = AiTraceLog.Create(
            conversation.Id,
            userId,
            correlationId,
            markdownVersion,
            markdownHash,
            conversation.RoutingTier,
            "deterministic-fallback",
            _piiRedactor.Redact(userPrompt),
            _piiRedactor.Redact(fallback.Reply),
            Array.Empty<string>(),
            0,
            0,
            0,
            5,
            0m,
            now,
            TimeSpan.FromDays(_sensiSettings.TraceRetentionDays));

        if (traceLogResult.IsSuccess)
        {
            await _traceLogRepository.AddAsync(traceLogResult.Value, cancellationToken);
        }

        RecommendedPackageDto? packageDto = null;
        if (fallback.RecommendedPackage is not null)
        {
            packageDto = new RecommendedPackageDto(
                fallback.RecommendedPackage.BudgetMxn,
                fallback.RecommendedPackage.TotalPriceMxn,
                fallback.RecommendedPackage.SavingsMxn,
                fallback.RecommendedPackage.Items.Select(i => new CitedOfferDto(
                    i.OfferId,
                    i.Title,
                    i.MerchantId,
                    i.MerchantName,
                    i.OfferPriceMxn,
                    i.RegularPriceMxn,
                    i.AvailableStock,
                    i.IsOpenNow,
                    i.DistanceKm)).ToList());
        }

        var usage = new AiUsageSummaryDto(0, 0, 0, false);

        return Result.Success(new AiTurnResponseDto(
            conversation.Id.ToString(),
            assistantMessage.Id.ToString(),
            assistantMessage.RedactedContent,
            packageDto,
            null,
            Array.Empty<string>(),
            true,
            degradationReason,
            conversation.RoutingTier.ToString(),
            markdownVersion,
            correlationId,
            usage));
    }
}
