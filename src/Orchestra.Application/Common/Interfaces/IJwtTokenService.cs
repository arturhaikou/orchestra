using Orchestra.Domain.Entities;

namespace Orchestra.Application.Common.Interfaces;

public interface IJwtTokenService
{
    string GenerateToken(User user);
}
