using GrapeCity.Enterprise.Identity.ExternalIdentityProvider.Configuration;

using System;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace OAuthAPISecurityProvider
{
    /*
    In the spirit of completeness, let me lead out by stating the obvious: yes, this is a hack.

    However:

    1. The preferred/framework-friendly way of doing this operation (see OIDCTokenValidate.cs) does
    not work when dynamically loaded into Wyn today as a security provider, and

    2. This is _technically_ not incorrect, insofar as it uses the provided access token against the
    OIDC userinfo endpoint, which will kick back a 401 if the token signatures don't match, or if it's 
    expired, etc.  So, while it introduces an extra http call, in terms of validating whether the token
    is baseline valid for the B2C OAuth endpoint in question, it's covering most of the invalid token cases.
    
    Until such time as the token validation becomes viable in the CSP flow, this will have to do.
    */
    public static class OIDCUserInfoValidate 
    {
        private static HttpClient _client = new HttpClient();

        private static OIDCWellKnownConfig _cfg;
        private static string[] _validAudiences;

        public static async Task Configure(ConfigurationCollection settings) 
        {
            // retrieve the configuration document and load it into our class
            string wellKnownUri = settings.Text(Consts.ConfigurationItemOAuthWellKnownUri);
            _cfg = await OIDCWellKnownConfig.Load(wellKnownUri);
            string validAudiences = settings.Text(Consts.ConfigurationItemValidAudiences, "");
            if(!String.IsNullOrWhiteSpace(validAudiences))
                _validAudiences = validAudiences.Split(',')
                    .Where(r=> !string.IsNullOrWhiteSpace(r))
                    .Select(r=> r.Trim())
                    .ToArray();
        }

        private static async Task<JwtSecurityToken> ValidateCore(string rawtoken)
        {
            using(var msg = new HttpRequestMessage(HttpMethod.Get, _cfg.userinfo_endpoint))
            {
                msg.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", rawtoken);
                try
                {
                    using(var resp = await _client.SendAsync(msg))
                    {
                        resp.EnsureSuccessStatusCode();

                        // if we're this far, decode the token and return it
                        var handler = new JwtSecurityTokenHandler();
			            return (JwtSecurityToken)handler.ReadToken(rawtoken);
                    }
                }
                catch(System.Exception ex)
                {
                    Logger.Exception(ex);
                    throw;
                }
            }
        }

        public static async Task<JwtSecurityToken> Validate(string rawtoken) 
		{
            Logger.Debug("Validating token (OAuth UserInfo endpoint)...");
            JwtSecurityToken parsedToken = await ValidateCore(rawtoken);
            // if no exception has been thrown at this point, this token is still valid and unexpired.

            // check the issuer against the configuration document
            if(!parsedToken.Issuer.Equals(_cfg.issuer, StringComparison.CurrentCultureIgnoreCase))
                throw new Exception("Jwt issuer does not match expected value.");

            if(_validAudiences != null 
                && parsedToken.Audiences != null 
                && !_validAudiences.Any(r=> parsedToken.Audiences.Any(t=> t.Equals(r, StringComparison.CurrentCultureIgnoreCase))))
                throw new Exception("Jwt audience does not match any configured audience.");

            return parsedToken;
        }
    }
}