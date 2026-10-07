using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace AtraccionesService.API.Tests.Infrastructure;

/// <summary>Emite access tokens firmados con una clave local de pruebas.</summary>
public static class TestTokens
{
    public const string Issuer = "https://issuer.test";
    public const string Audience = "atracciones-api";

    public static readonly SymmetricSecurityKey Key = new(Encoding.UTF8.GetBytes("clave-de-pruebas-solo-para-tests-0123456789abcdef"));

    private static readonly SymmetricSecurityKey OtherKey = new(Encoding.UTF8.GetBytes("otra-clave-que-el-api-no-reconoce-0123456789abcdef"));

    public static string Create(
        string subject = "user-1",
        string scopes = "attractions:read attractions:book attractions:cancel",
        string? email = "user1@ejemplo.com",
        bool? emailVerified = true,
        DateTime? expires = null,
        bool wrongKey = false,
        string issuer = Issuer)
    {
        var claims = new List<Claim> { new("sub", subject), new("scope", scopes) };
        if (email is not null)
        {
            claims.Add(new Claim("email", email));
        }

        if (emailVerified is not null)
        {
            claims.Add(new Claim("email_verified", emailVerified.Value ? "true" : "false", ClaimValueTypes.Boolean));
        }

        var expiresAt = expires ?? DateTime.UtcNow.AddMinutes(10);
        return new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = issuer,
            Audience = Audience,
            Subject = new ClaimsIdentity(claims),
            NotBefore = expiresAt.AddMinutes(-30),
            IssuedAt = expiresAt.AddMinutes(-30),
            Expires = expiresAt,
            SigningCredentials = new SigningCredentials(wrongKey ? OtherKey : Key, SecurityAlgorithms.HmacSha256),
        });
    }
}
