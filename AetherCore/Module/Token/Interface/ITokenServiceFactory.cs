namespace AetherCore.Module.Token.Interface
{
    public interface ITokenServiceFactory
    {
        ITokenService Create(string tokenType);
    }
}
