using AetherCore.DTO;

namespace AetherCore.Module.Interface
{
    public interface IModerationClient
    {
        Task<List<ModerationResponse>> CheckBatchAsync(List<ModerationInput> inputs);
    }
}
