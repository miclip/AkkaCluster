using System;
using System.Collections.Generic;

namespace AkkaCluster.SeedNodeRepository
{ 
    public interface ISeedNodeRepository
    {
        void DeleteSeedAddress(string name);

        void SetSeedAddress(string name, string ipAddress, string port, TimeSpan expiration);

        string GetSeedAddress(string name);

        IEnumerable<string> GetAllSeedAddresses(string name, string excludedSelf = null);
    }
    
}