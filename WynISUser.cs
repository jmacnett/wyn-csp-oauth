using GrapeCity.Enterprise.Identity.ExternalIdentityProvider;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace OAuthAPISecurityProvider
{
    public class TenantRole 
    {
        public string role_id { get; set; }
        public string role_name { get; set; }
        public string tenant_id { get; set; }
        public string tenant_name { get; set; }
        public string tenant_path { get; set; }
    }

	public class WynISUser : IExternalUserContext, IExternalUserDescriptor
	{
        // IDataReader fill shortcut
        public static Dictionary<string,PropertyInfo> Properties = typeof(WynISUser).GetProperties()
            .Where(r=> r.CanWrite)
            .ToDictionary(k=> k.Name, StringComparer.CurrentCultureIgnoreCase);

        public string id { get; set; }
        public string username { get; set; }
        public string email { get; set; }
        public string mobile { get; set; }
        public string provider_id { get; set; }
        public string avatar { get; set; }
        public string first_name { get; set; }
        public string last_name { get; set; }
        public DateTime creation_time { get; set; }
        public bool enabled { get; set; }
        public string full_name { get; set; }
        public string organization_id_path { get; set; }

        // IExternalUserDescriptor requirements
        public string ExternalUserId => this.id;
        public string ExternalUserName => this.username;
        public string ExternalProvider => this.provider_id;

        // IExternalUserContext requirements
        public Dictionary<string,string[]> UserContext = new Dictionary<string, string[]>(StringComparer.CurrentCultureIgnoreCase);
        public IEnumerable<string> Keys => UserContext.Keys;
        public Task<string> GetValueAsync(string key)
        {
            /*  It could be argued that this should throw an exception if there is more than one value;
                for the purposes of this demo, it's not going to do so. */

            return Task.Run(()=> this.UserContext.ContainsKey(key) && this.UserContext[key].Length > 0 ? this.UserContext[key].First() : string.Empty);
        }
        public Task<IEnumerable<string>> GetValuesAsync(string key)
        {
            return Task.Run(()=> this.UserContext.ContainsKey(key) ? this.UserContext[key] : Enumerable.Empty<string>());
        }

        // role/org lists, formatted to follow Wyn sql example
        public List<TenantRole> TenantRoles = new List<TenantRole>();

        public IEnumerable<string> Roles => TenantRoles.Select(r=> r.role_name).Distinct();
        public IEnumerable<string> Organizations => TenantRoles.Select(r=> r.tenant_path).Distinct();

        public WynISUser() {}
    }
}