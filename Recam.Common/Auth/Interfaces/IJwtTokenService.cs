using System.Security.Claims;

namespace Recam.Common.Auth.Implementation;

public interface IJwtTokenService
{
    string CreateToken(IEnumerable<Claim> claims);
}