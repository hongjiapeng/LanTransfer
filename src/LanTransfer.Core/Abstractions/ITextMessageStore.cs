using LanTransfer.Core.Models;

namespace LanTransfer.Core.Abstractions;

public interface ITextMessageStore
{
    Task<TextMessage> AddAsync(string text, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TextMessage>> ListAsync(CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
