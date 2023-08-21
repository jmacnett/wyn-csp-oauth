namespace OAuthAPISecurityProvider
{
	public static class Consts
	{
		// this key should match the SystemConfig/Services/Server/Authentication/SSO/Scheme value used in Wyn.conf
		public static readonly string ProviderName = "AzureAD B2C";
		
		public static readonly string ProviderDescription = "Provider that works in lockstep w/ configuration-based AAD B2C SSO, allowing api token access for those users.";

		public static readonly string ConfigurationItemOAuthWellKnownUri = "OAuthWellKnown";
		public static readonly string ConfigurationItemDefaultTTL = "DefaultTTL";
		public static readonly string ConfigurationItemValidAudiences = "ValidAudiences";
	}
}
