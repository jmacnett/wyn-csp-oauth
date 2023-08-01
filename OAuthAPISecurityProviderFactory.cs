using GrapeCity.Enterprise.Identity.ExternalIdentityProvider.Configuration;
using GrapeCity.Enterprise.Identity.SecurityProvider;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Logging;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace OAuthAPISecurityProvider
{
	public class OAuthAPISecurityProviderFactory : ISecurityProviderFactory
	{
		public string ProviderName => Consts.ProviderName;

		public string Description => Consts.ProviderDescription;

		public IEnumerable<ConfigurationItem> SupportedSettings => new List<ConfigurationItem>
		{
			new ConfigurationItem(Consts.ConfigurationItemOAuthWellKnownUri, "Well-known openid configuration", "Url for your B2C tenant's openid configuration") {
				Restriction = ConfigurationItemRestriction.Mandatory,
				ValueType = ConfigurationItemValueType.Text,
				Value = "https://yourb2ctenant.b2clogin.com/yourb2ctenant.onmicrosoft.com/B2C_1A_SIGNUP_SIGNIN/v2.0/.well-known/openid-configuration"
			},
			new ConfigurationItem(Consts.ConfigurationItemDefaultTTL, "User token TTL", "User Token time to live, in seconds") {
				Restriction = ConfigurationItemRestriction.Mandatory,
				ValueType = ConfigurationItemValueType.Number,
				Value = 3600
			},
		};

		public Task<ISecurityProvider> CreateAsync(IEnumerable<ConfigurationItem> settings, ILogger logger)
		{
			IdentityModelEventSource.ShowPII = true;
			Logger.SetLogger(logger);
			return Task.Run(() =>
			{
				try
				{
					// TODO: remove when not debugging
					Logger.Debug($"Creating security provider '{Consts.ProviderName}'...");
					var securityProvider = new OAuthAPISecurityProvider(settings);
					return securityProvider as ISecurityProvider;
				}
				catch (Exception e)
				{
					Logger.Exception(e, $"Create security provider '{Consts.ProviderName}' failed.");
					return null;
				}
			});
		}
	}
}
