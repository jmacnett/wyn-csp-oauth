using GrapeCity.Enterprise.Identity.ExternalIdentityProvider.Configuration;

using Microsoft.IdentityModel.Tokens;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

using System;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;

namespace OAuthAPISecurityProvider
{
    /* 
    Let me start out by unequivocably stating this: yes, this is normally the correct way to do this.  
    
    Now, with that said:

    At the time of this writing, this class WILL work independantly of Wyn (e.g., in a console test harness);
    however, something to do with the dynamic assembly loading occuring for the custom security providers
    and incorrect libraries being inadvertently loaded downstream of Microsoft.IdentityModel.Protocols.OpenIdConnect 
    (more specifically, the completely-internal JsonConvert serializer class in Microsoft.IdentityModel.Json) cause it to
    be unable to deserialize the output of the well-known oidc endpoint correctly.  Thus, while the code in this class is
    _correct_ as far as it goes, it will not work today in the context of the Wyn CSP setup.

    In the future, I hope to track down exactly why this is a problem; hopefully, by the time Wyn upgrades to the next LTS
    dotnet version, this will be operable again (currently, they're on 6.0.2 in the docker images).

    So, in summation: don't use this code for the CSP OIDC validation; I'm including it here for posterity, in the hopes that 
    eventually it will be usable for that purpose.
    */
    public static class OIDCTokenValidate
    {
        private static OpenIdConnectConfiguration _cfg;
        private static async Task<OpenIdConnectConfiguration> Load(string _wellknown)
		{
            try 
            {
                var configurationManager = new ConfigurationManager<OpenIdConnectConfiguration>(
                    _wellknown,
                    new OpenIdConnectConfigurationRetriever(), new HttpDocumentRetriever());
                return await configurationManager.GetConfigurationAsync(CancellationToken.None);
            } catch(Exception ex) {
                Logger.Exception(ex);
                throw;
            }
		}

        public static async Task Configure(ConfigurationCollection settings) 
        {
            // retrieve the configuration document and load it into our class
            string wellKnownUri = settings.Text(Consts.ConfigurationItemOAuthWellKnownUri);
            _cfg = await Load(wellKnownUri);
        }

        public static ClaimsPrincipal Validate(string rawtoken)
        {
            var validatorParams = new TokenValidationParameters
                {
                    RequireExpirationTime = true,
                    RequireSignedTokens = true,
                    ValidateIssuer = true,
                    ValidIssuer = _cfg.Issuer,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKeys = _cfg.SigningKeys,
                    ValidateLifetime = true,
                    // ValidAudiences = new List<string>() { }
                    ValidateAudience = false
                };

            return new JwtSecurityTokenHandler()
                .ValidateToken(rawtoken, validatorParams, out var rawValidatedToken);
        }
    }
}