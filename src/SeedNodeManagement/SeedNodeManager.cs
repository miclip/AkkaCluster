using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AkkaCluster.SeedNodeRepository;

namespace AkkaCluster.SeedNodeManagement
{
        public class SeedNodeManager
        {
            private ISeedNodeRepository seedNodeRepository;
            private string seedNodeName;
            private string generalSeedNodeName;
            private string ipAddress;
            private string port;

            public SeedNodeManager(string generalSeedNodeName, ISeedNodeRepository seedNodeRepository)
            {
                this.generalSeedNodeName = generalSeedNodeName;
                this.seedNodeRepository = seedNodeRepository;
            }

            public SeedNodeManager(string generalSeedNodeName, string seedNodeName, string ipAddress, string port, ISeedNodeRepository seedNodeRepository)
            {
                this.generalSeedNodeName = generalSeedNodeName;
                this.seedNodeName = seedNodeName;
                this.ipAddress = ipAddress;
                this.port = port;
                this.seedNodeRepository = seedNodeRepository;
            }

            public void RegisterSeedNode()
            {
                seedNodeRepository.SetSeedAddress(seedNodeName, ipAddress, port, TimeSpan.FromSeconds(30));    
            }

            public Task StartRegisteringSeedNodeTask(CancellationTokenSource cancellationTokenSource)
            {
                CancellationToken cancellationToken = cancellationTokenSource.Token;
                return Task.Factory.StartNew(()=>{
                    while(true)
                    {
                        this.RegisterSeedNode();
                        Thread.Sleep(TimeSpan.FromSeconds(15));
                    }
                    },cancellationToken);  
            }

            public void DeregisterSeedNode()
            {
                seedNodeRepository.DeleteSeedAddress(seedNodeName);
            }

            public IEnumerable<string> RetrieveAllSeedNodes(bool excludeSelf = true)
            {
                var excludedSelfName = excludeSelf ? seedNodeName : null;
                return seedNodeRepository.GetAllSeedAddresses(generalSeedNodeName, excludedSelfName);
            }

            public void OnSeedNodeChanges(Action<IEnumerable<string>> action,  IEnumerable<string> knownSeedNodes = null)
            {
                if(knownSeedNodes == null)
                    knownSeedNodes = this.RetrieveAllSeedNodes(false);

                Task.Run(()=>{
                 while(true)
                 {
                     var seedNodes = this.RetrieveAllSeedNodes(false).ToList();
                     var newSeedNodes = seedNodes.Except(knownSeedNodes);
                     if(newSeedNodes.Any())
                        action(newSeedNodes);
                     Thread.Sleep(TimeSpan.FromSeconds(30));
                 }
                });
            }
    }

}
