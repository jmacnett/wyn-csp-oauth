using GrapeCity.Enterprise.Identity.ExternalIdentityProvider;
using GrapeCity.Enterprise.Identity.ExternalIdentityProvider.Configuration;
using GrapeCity.Enterprise.Identity.SecurityProvider;

using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Threading.Tasks;

namespace OAuthAPISecurityProvider
{
	public class OAuthAPISecurityProvider : ISecurityProvider
	{
		public string ProviderName => Consts.ProviderName;

		private TimeSpan _defaultTTL { get; set; }

		protected OAuthAPISecurityProvider() { }
		public OAuthAPISecurityProvider(IEnumerable<ConfigurationItem> configs)
		{
			ConfigurationCollection settings = new ConfigurationCollection(configs);
			_defaultTTL = TimeSpan.FromSeconds(settings.Number(Consts.ConfigurationItemDefaultTTL, 3600));

			OIDCUserInfoValidate.Configure(settings).Wait();
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
					- username and password are ignored; however, due to the requirements of IdentityServer4,
					  they are required and can not be empty strings.  It's suggested that you feed them known bogus values.

				The attentive user will notice that this method is liberally strewn with Debug-level logging.  This can obviously
				be removed at your discretion, but is very helpful when attempting to troubleshoot instances while deployed in k8s.
			*/
			Logger.Debug($"username: {username}");
			Logger.Debug($"password: {password}");
			Logger.Debug($"customizedParam: {customizedParam ?? "null"}");

			// this proceeds under the assumption that the posted value in customizedParam (which appears to follow name:value format) is named "accesstoken"
			string rawtoken = null; 
			if(customizedParam != null && customizedParam is Dictionary<string,string> && ((Dictionary<string,string>)customizedParam).ContainsKey("accesstoken"))
				rawtoken = ((Dictionary<string,string>)customizedParam)["accesstoken"];
			Logger.Debug($"rawtoken: {(rawtoken ?? "null")}");

			if(rawtoken == null)
				throw new Exception("accesstoken value is required for oauth token generation");

			// validate token
			string idp_oid = null;

			try {
				JwtSecurityToken parsedToken = await OIDCUserInfoValidate.Validate(rawtoken);

				Logger.Debug("Token validated!  Extracting idp_oid from JwtSecurityToken claims...");
				idp_oid = parsedToken.Claims.First(r=> r.Type == "oid").Value;
				Logger.Debug($"idp_oid: {idp_oid}");
			}
			catch(System.Exception ex){
				// TBD: will an unauthorized exception manifest to the user?
				Logger.Exception(ex);
				return null;
			}

			// either an exception was thrown, or something is wildly wrong with the access token.  bail out.
			if(idp_oid == null)
				return null;
			
			/* 	At this point, we have a validated user, based on the oauth token.  We need to try to 
				pull them from Wyn to see if they exist in the user store, which is where AAD SSO creates
				the user footprints by default.

				In this sample implementation, we're directly connecting to the Wyn "wynis" identity server database to verify the user exists.
				Database credentials for that db are (hopefully) to be pulled from the wyn.conf in-memory footprint at some point, 
				but failing that, I've stored them as kubernetes secrets in the wyn k8s namespace, and am pulling them that way.
			*/

			try 
			{
				var user = await WynISHelper.GetUserInfo(idp_oid);
				if (null != user)
				{
					var token = Guid.NewGuid().ToString();
					WynISUserTokenCache.Add(token, user, _defaultTTL);
					return token;
				}
			}
			catch(Exception ex)
			{
				Logger.Exception(ex);
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
