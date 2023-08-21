using System;
using System.Runtime.Caching;

namespace OAuthAPISecurityProvider
{
    public static class WynISUserTokenCache
    {
        private static MemoryCache _wynISUserTokenCache = new MemoryCache("WynISUserTokenCache");

        public static WynISUser Get(string token) 
        {
            return _wynISUserTokenCache.Contains(token) ? (WynISUser)_wynISUserTokenCache[token] : null;
        }

        /// <remarks>
        /// Using the MemoryCache "Set" method, insofar as MemoryCache is thread-safe, and overwrites
        /// aren't actually harmful in this scenario.
        /// Also applying a sliding cache expiration, to avoid potential user disgruntlement.
        /// </remarks>
        public static void Add(string token, WynISUser user, TimeSpan ttl)
        {
            _wynISUserTokenCache.Set(token, user, new CacheItemPolicy() {  SlidingExpiration = ttl });
        }

        public static void Remove(string token)
        {
            _wynISUserTokenCache.Remove(token, CacheEntryRemovedReason.Removed);
        }
    }
}