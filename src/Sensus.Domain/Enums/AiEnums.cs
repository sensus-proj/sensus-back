namespace Sensus.Domain.Enums;

public enum AiRoutingTier
{
    Fast = 1,
    Reasoning = 2
}

public enum AiConversationStatus
{
    Active = 1,
    Archived = 2
}

public enum AiMessageRole
{
    User = 1,
    Assistant = 2,
    SystemNotice = 3
}

public enum AiScope
{
    AiChat = 1,
    OffersRead = 2,
    AccountRead = 3,
    ClaimsRead = 4,
    MerchantRead = 5
}
