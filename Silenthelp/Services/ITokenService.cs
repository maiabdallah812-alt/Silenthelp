using SilentHelp.Models;

namespace SilentHelp.Services
{
    public interface ITokenService
    {
        string CreateToken(User user);
    }
}

