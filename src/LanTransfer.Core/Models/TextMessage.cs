namespace LanTransfer.Core.Models;

public sealed record TextMessage(Guid Id, string Text, DateTimeOffset CreatedAt);
