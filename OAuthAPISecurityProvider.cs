using GrapeCity.Enterprise.Identity.ExternalIdentityProvider;
using GrapeCity.Enterprise.Identity.ExternalIdentityProvider.Configuration;
using GrapeCity.Enterprise.Identity.SecurityProvider;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Microsoft.IdentityModel.Tokens;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Threading;

namespace OAuthAPISecurityProvider
{
	public class OAuthAPISecurityProvider : ISecurityProvider
	{
		public string ProviderName => Consts.ProviderName;

		private TokenValidationParameters _validatorParams;
		private TimeSpan _defaultTTL { get; set; }

		private async Task<TokenValidationParameters> GetValidatorParams(ConfigurationCollection settings)
		{
			string wellknown = settings.Text(Consts.ConfigurationItemOAuthWellKnownUri);

			var configurationManager = new ConfigurationManager<OpenIdConnectConfiguration>(
				wellknown,
                new OpenIdConnectConfigurationRetriever(), new HttpDocumentRetriever());
            CancellationToken ct = default(CancellationToken);
            RSACryptoServiceProvider rsa = new RSACryptoServiceProvider();
            var discoveryDocument = await configurationManager.GetConfigurationAsync(ct);
            var signingKeys = discoveryDocument.SigningKeys;
            return new TokenValidationParameters
            {
                RequireExpirationTime = true,
                RequireSignedTokens = true,
                ValidateIssuer = true,
                ValidIssuer = discoveryDocument.Issuer,
                ValidateIssuerSigningKey = true,
                IssuerSigningKeys = signingKeys,
                ValidateLifetime = true,
                // ValidAudiences = new List<string>() { }
                ValidateAudience = false
            };
		}

		protected OAuthAPISecurityProvider() { }
		public OAuthAPISecurityProvider(IEnumerable<ConfigurationItem> configs)
		{
			ConfigurationCollection settings = new ConfigurationCollection(configs);
			_validatorParams = GetValidatorParams(settings).Result;
			_defaultTTL = TimeSpan.FromSeconds(settings.Number(Consts.ConfigurationItemDefaultTTL, 3600));
		}

		private ClaimsPrincipal Validate(string rawtoken)
        {
            return new JwtSecurityTokenHandler()
                .ValidateToken(rawtoken, _validatorParams, out var rawValidatedToken);
        }

		public Task DisposeTokenAsync(string token)
		{
			return Task.Run(() => WynISUserTokenCache.Remove(token));
		}

		public async Task<string> GenerateTokenAsync(string username, string password, object customizedParam = null)
		{
			/*
				Given the footprint of ISecurityProvider.GenerateTokenAsync, we are shoehorning this as follows:
					- "customizedParam" will contain an OAuth access token from from the requestor app, requesting access 
					  to an api scope defined in the AAD B2C app registration of the Wyn Portal.
					- username and password are ignored
			*/
			// **TODO: remove this when not debugging
			Logger.Debug($"username: {username}");
			Logger.Debug($"password: {password}");
			Logger.Debug($"customizedParam: {customizedParam ?? "null"}");

			// this proceeds under the assumption that the posted value in customizedParam (which appears to follow name:value format) is named "accesstoken"
			string rawtoken = null; 
			if(customizedParam != null && customizedParam is Dictionary<string,string>)
				rawtoken = ((Dictionary<string,string>)customizedParam)["accesstoken"];
			Logger.Debug($"rawtoken: {(rawtoken ?? "null")}");

			if(rawtoken == null)
				throw new Exception("accesstoken value is required for oauth token generation");

			// validate token
			ClaimsPrincipal principal = null;

			try {
				principal = Validate(rawtoken);
			}
			catch(System.Exception ex){
				// TBD: is it better to let the exception occur and let the caller know there's an error, 
				// or just write the error to the console and treat it as a failure.
				Logger.Error(ex.Message,ex);
				return null;
			}

			// either an exception was thrown, or something is wiidly wrong with the access token.  bail out.
			if(principal == null || principal.Identity == null || !principal.Identity.IsAuthenticated)
				return null;
			
			/* 	At this point, we have a validated user, based on the oauth token.  We need to try to 
				pull them from Wyn to see if they exist in the user store, which is where AAD SSO creates
				the user footprints by default.

				In this sample implementation, we're directly connecting to the Wyn "wynis" identity server database to verify the user exists.
				Database credentials for that db are (hopefully) to be pulled from the wyn.conf in-memory footprint at some point, 
				but failing that, I've stored them as kubernetes secrets in the wyn namespace, and am pulling them that way.
			*/

			// pull object_id, which is what corresponds to the identity value in the "id" column of the "users" table in the wynis db
			var idp_oid = principal.Claims.FirstOrDefault(r=> r.Type == "http://schemas.microsoft.com/identity/claims/objectidentifier").Value;

			// pull the user snapshot (profile, extended fields, roles, orgs)
			var user = await WynISHelper.GetUserInfo(idp_oid);
			if (null != user)
			{
				var token = Guid.NewGuid().ToString();
				WynISUserTokenCache.Add(token, user, _defaultTTL);
				return token;
			}
			return null;
		}

		public Task<IExternalUserContext> GetUserContextAsync(string token)
		{
			return Task.Run(() =>
			{
				WynISUser user = WynISUserTokenCache.Get(token);
				return user != null ? user as IExternalUserContext : null;
			});
		}

		public Task<IExternalUserDescriptor> GetUserDescriptorAsync(string token)
		{
			return Task.Run(() =>
			{
				WynISUser user = WynISUserTokenCache.Get(token);
				return user != null ? user as IExternalUserDescriptor : null;
			});
		}

		public Task<string[]> GetUserOrganizationsAsync(string token)
		{
			return Task.Run(() =>
			{
				WynISUser user = WynISUserTokenCache.Get(token);
				return user != null ? user.Organizations.ToArray() : new string[0];
			});
		}

		public Task<string[]> GetUserRolesAsync(string token)
		{
			return Task.Run(() =>
			{
				WynISUser user = WynISUserTokenCache.Get(token);
				return user != null ? user.Roles.ToArray() : new string[0];
			});
		}

		public Task<bool> ValidateTokenAsync(string token)
		{
			return Task.Run(() => WynISUserTokenCache.Get(token) != null);
		}
	}
}
