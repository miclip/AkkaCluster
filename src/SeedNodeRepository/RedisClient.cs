using System;
using System.Collections.Generic;
using StackExchange.Redis;

namespace AkkaCluster.SeedNodeRepository
{
    public class RedisClient : ISeedNodeRepository
    {
        private Lazy<ConnectionMultiplexer> lazyConnection;

        public RedisClient(string connectionString) 
        {
            lazyConnection = new Lazy<ConnectionMultiplexer>(() =>
            {
                return ConnectionMultiplexer.Connect(connectionString);
            });
            
        } 
        public  ConnectionMultiplexer Connection
        {
            get
            {
                return this.lazyConnection.Value;
            }
        }

        public void DeleteSeedAddress(string name)
        {
            var cache = Connection.GetDatabase();
            cache.KeyDelete(name);
        }
        
        public void SetSeedAddress(string name, string ipAddress, string port, TimeSpan expiration)
        {
            var cache = Connection.GetDatabase();
            cache.StringSet(name, $"{ipAddress}:{port}", expiration);
        }

        public string GetSeedAddress(string name)
        {
            var cache = Connection.GetDatabase();
            return cache.StringGet(name);
        }

        public IEnumerable<string> GetAllSeedAddresses(string name, string excludedSelf = null)
        {
            var seedNodes = new List<string>();
            var cache = Connection.GetDatabase();
            
            for(int i=1;;i++)
            {
                var key = string.Concat(name,i);
                if(!cache.KeyExists(key))
                    break;
                if(key != excludedSelf)
                    seedNodes.Add(cache.StringGet(key));
            }
            return seedNodes;
        }
    }
}