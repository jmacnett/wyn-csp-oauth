using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Npgsql;

namespace OAuthAPISecurityProvider
{
    public static class WynISHelper 
    {
        private static object _cslock = new object();
        private static string _wynISConnectionString;

        private static string WynISConnectionString 
        {
            get 
            {
                lock(_cslock) 
                {
                    if (_wynISConnectionString == null) 
                    {
                        // pull cs values from mounted secrets
                        System.IO.DirectoryInfo secretdir = new System.IO.DirectoryInfo("/var/wyncs");
                        Dictionary<string,System.IO.FileInfo> secretdict = secretdir.GetFiles().ToDictionary(k=> k.Name);

                        NpgsqlConnectionStringBuilder csb = new NpgsqlConnectionStringBuilder();
                        csb.Host = System.IO.File.ReadAllText(secretdict["WYN_ADMIN_PG_HOST"].FullName);
                        csb.Port = secretdict.ContainsKey("WYN_ADMIN_PG_PORT") ? int.Parse(System.IO.File.ReadAllText(secretdict["WYN_ADMIN_PG_HOST"].FullName)) : 5432;
                        csb.Database = System.IO.File.ReadAllText(secretdict["WYN_ADMIN_PG_DB_IS"].FullName);
                        csb.Username = System.IO.File.ReadAllText(secretdict["WYN_ADMIN_PG_USERNAME"].FullName);
                        csb.Password = System.IO.File.ReadAllText(secretdict["WYN_ADMIN_PG_PASSWORD"].FullName);

                        if(secretdict.ContainsKey("WYN_ADMIN_PG_SSLMODE"))
                        {
                            csb.SslMode = (SslMode)Enum.Parse(typeof(SslMode),secretdict["WYN_ADMIN_PG_SSLMODE"].FullName);
                            csb.TrustServerCertificate = true;
                        }
                        csb.CommandTimeout = 300;

                        _wynISConnectionString = csb.ToString();
                    }

                    return _wynISConnectionString;
                }
            }
        }

        /// <remarks>
        /// Performing all queries under one connection, as postgresql tends to prefer persistant connections
        /// vs repeated spinup/teardown connections.  Your mileage may vary, depending on your datastore platform.
        /// </remarks>
        private static async Task<WynISUser> GetUserCore(string idp_oid)
        {

            // pull the primary table data first
            string uiquery = @"
SELECT id
    ,username
    ,password_hash
    ,email
    ,mobile
    ,provider_id
    ,avatar
    ,first_name
    ,last_name
    ,creation_time
    ,enabled
    ,full_name
    ,organization_id_path
FROM public.users
where provider_id = $1
    and id = $2
";

            WynISUser toRet = null;

            string userid = $"{idp_oid}_{Consts.ProviderName.ToLower()}_user";

            using(NpgsqlConnection conn = new NpgsqlConnection(WynISConnectionString))
            {
                await conn.OpenAsync();
                using(NpgsqlCommand cmd = new NpgsqlCommand(uiquery, conn))
                {
                    cmd.Parameters.Add(new NpgsqlParameter() { Value = Consts.ProviderName });
                    cmd.Parameters.Add(new NpgsqlParameter() { Value = userid });

                    using(IDataReader rdr = await cmd.ExecuteReaderAsync(CommandBehavior.SingleRow))
                    {
                        // get field to property mappings
                        var mappings = Enumerable.Range(0, rdr.FieldCount)
                            .Select(r=> new 
                            { 
                                ord=r, 
                                name=rdr.GetName(r),
                                prop = WynISUser.Properties.ContainsKey(rdr.GetName(r)) ? WynISUser.Properties[rdr.GetName(r)] : null
                            })
                            .Where(r=> r.prop != null)
                            .ToList();

                        while(rdr.Read())
                        {
                            toRet = new WynISUser();
                            mappings.ForEach(r=> { 
                                object raw = rdr.GetValue(r.ord);
                                if(raw == DBNull.Value) {
                                    raw = null;
                                }
                                r.prop.SetValue(toRet, raw);

                                // following de facto wyn sample implementation
                                toRet.UserContext[r.name] = raw != null ? new string[] { raw.ToString() } : new string[0];
                            });
                        }
                    }
                }

                // if we have a user, pull any extended properties as well
                if(toRet != null) 
                {
                    // get extended property names and attributes, for correct storage definitions
                    // *** TODO: could easily be cached
                    Dictionary<string,bool> customProps = new Dictionary<string, bool>(StringComparer.CurrentCultureIgnoreCase);
                    using(NpgsqlCommand cmd = new NpgsqlCommand("select name, multivalued from customize_properties", conn))
                    using(IDataReader rdr = await cmd.ExecuteReaderAsync())
                    {
                        int ord_name = rdr.GetOrdinal("name");
                        int ord_mv = rdr.GetOrdinal("multivalued");

                        while(rdr.Read())
                            customProps.Add(rdr.GetString(ord_name), rdr.GetBoolean(ord_mv));
                    }

                    // ensure we're not missing any keys
                    foreach(var kvp in customProps)
                        toRet.UserContext.Add(kvp.Key, new string[0]);

                    // get custom property values for user into temporary storage.  note: all values are stored as text in db
                    Dictionary<string,List<string>> tempProps = new Dictionary<string, List<string>>(StringComparer.CurrentCultureIgnoreCase);
                    using(NpgsqlCommand cmd = new NpgsqlCommand(@"
    select cp.""name"", up.property_value
    from users u
        inner join user_properties up
            on u.id  = up.user_id 
        inner join customize_properties cp 
            on up.property_id  = cp.id 
    where u.id = $1
    ", conn))
                    {
                        cmd.Parameters.Add(new NpgsqlParameter() { Value = userid });

                        using(IDataReader rdr = await cmd.ExecuteReaderAsync())
                        {
                            int ord_name = rdr.GetOrdinal("name");
                            int ord_pv = rdr.GetOrdinal("property_value");
                            while(rdr.Read())
                            {
                                if(!tempProps.ContainsKey(rdr.GetString(ord_name)))
                                    tempProps.Add(rdr.GetString(ord_name), new List<string>());
                                tempProps[rdr.GetString(ord_name)].Add(rdr.GetString(ord_pv));
                            }
                        }
                    }

                    foreach(var kvp in tempProps)
                        toRet.UserContext[kvp.Key] = kvp.Value.ToArray();

                                       // grab roles, organizations
                    using(NpgsqlCommand cmd = new NpgsqlCommand(@"
select ur.role_id 
	,r.""name"" as role_name
	,r.tenant_id
	,t.""name"" as tenant_name
    ,t.""path"" as tenant_path
from users u
	inner join user_roles ur
		on u.id  = ur.user_id 
	inner join roles r 
		on ur.role_id = r.id 
	inner join tenants t 
		on r.tenant_id  = t.id 
where u.id = $1 
", conn))
                    {
                        cmd.Parameters.Add(new NpgsqlParameter() { Value = userid });

                        using(IDataReader rdr = await cmd.ExecuteReaderAsync())
                        {
                            int ord_role_id = rdr.GetOrdinal("role_id");
                            int ord_role_name = rdr.GetOrdinal("role_name");
                            int ord_tenant_id = rdr.GetOrdinal("tenant_id");
                            int ord_tenant_name = rdr.GetOrdinal("tenant_name");
                            int ord_tenant_path = rdr.GetOrdinal("tenant_path");
                            while(rdr.Read())
                                toRet.TenantRoles.Add(new TenantRole
                                {
                                    role_id = rdr.GetString(ord_role_id),
                                    role_name = rdr.GetString(ord_role_name),
                                    tenant_id = rdr.GetString(ord_tenant_id),
                                    tenant_name = rdr.GetString(ord_tenant_name),
                                    tenant_path = rdr.GetString(ord_tenant_path)
                                });
                        }    
                    }
                    /* Note: there is a potentially seperate way of getting the organizations/tenants for a user,
                    via the user_tenants table; however, the list of organizations in that collection is not affected
                    by disassociating all roles for the organization from a user (and the role-joined version is),
                    so to avoid leaks, we're ignoring user_tenants for now.

                    It's worth noting that users created within an org as local accounts presumably use this information
                    without a role, which leads one to suspect the baseline provider DOES use that table somehow, but
                    given how we're controlling access, this is a sufficient list.
                    */
                    
                }
            }

            return toRet;
        }

        public static async Task<WynISUser> GetUserInfo(string idp_oid)
        {
            WynISUser toRet = await GetUserCore(idp_oid);
            if(toRet == null) 
            {
                Logger.Information($"User with AAD B2C object_id '{idp_oid}' could not be found!");
                return null;
            }
            return toRet;
        }
    }
}