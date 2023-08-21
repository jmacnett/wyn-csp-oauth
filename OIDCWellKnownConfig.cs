using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.IdentityModel.Tokens;
using Microsoft.IdentityModel.Protocols;
using System.Threading;

namespace OAuthAPISecurityProvider
{
    public class OIDCWellKnownConfig 
    {
        public OIDCWellKnownConfig()
        {
            this.SigningKeys = new List<SecurityKey>();
        }

        public string issuer { get; set; }
        public string authorization_endpoint { get; set; }
        public string token_endpoint { get; set; }
        public string end_session_endpoint { get; set; }
        public string jwks_uri { get; set; }
        public string userinfo_endpoint { get; set; }
        public string[] response_modes_supported { get; set; }
        public string[] response_types_supported { get; set; }
        public string[] scopes_supported { get; set; }
        public string[] subject_types_supported { get; set; }
        public string[] id_token_signing_alg_values_supported { get; set; }
        public string[] token_endpoint_auth_methods_supported { get; set; }
        public string[] claims_supported { get; set; }

        public JsonWebKeySet JsonWebKeySet {get; set;}

        public List<SecurityKey> SigningKeys { get; set;}

        public static async Task<OIDCWellKnownConfig> Load(string wellKnownUri, HttpDocumentRetriever retriever = null)
        {
            if(retriever == null)
                retriever = new HttpDocumentRetriever();

            // get well-known endpoint oidc configuration
            string rawdoc = await retriever.GetDocumentAsync(wellKnownUri, CancellationToken.None).ConfigureAwait(false);

            // hand-rolled deserialize into one-of class to avoid Microsoft.Identity.Json internal-only deserializer breakage
            var cfg = System.Text.Json.JsonSerializer.Deserialize<OIDCWellKnownConfig>(rawdoc);

            // if we have signing keys configured, we need to fill em up
            if (!string.IsNullOrEmpty(cfg.jwks_uri))
            {
                Logger.Debug("jwks_uri exists, retrieving keys...");
                string keys = await retriever.GetDocumentAsync(cfg.jwks_uri, CancellationToken.None).ConfigureAwait(false);
                Logger.Debug($"keys: {keys}");
                cfg.JsonWebKeySet = new JsonWebKeySet(keys);
                foreach (SecurityKey key in cfg.JsonWebKeySet.GetSigningKeys())
                {
                    Logger.Debug($"Adding key {key.KeyId}");
                    cfg.SigningKeys.Add(key);
                }

                Logger.Debug("exiting jwks_uri handling");
            }  

            return cfg;          
        }
    }
}