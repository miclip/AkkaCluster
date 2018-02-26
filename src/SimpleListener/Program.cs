using System;
using System.Configuration;
using System.Linq;
using System.Collections.Generic;
using Akka.Actor;
using Akka.Configuration;
using Akka.Configuration.Hocon;
using AkkaCluster.SeedNodeManagement;
using AkkaCluster.SeedNodeRepository;

namespace AkkaCluster.SimpleListener
{
    class Program
    {
        private static void Main(string[] args)
        {
          var seedNodeRepository = new RedisClient(Environment.GetEnvironmentVariable("REDISCONNECTIONSTRING"));
          
          var seedNodeManager = new SeedNodeManager("lighthouse", seedNodeRepository);

          Console.WriteLine("version 1.0");
            var configString = @"akka {
            actor {
              provider = ""Akka.Cluster.ClusterActorRefProvider, Akka.Cluster""
            }
            
            remote {
              log-remote-lifecycle-events = DEBUG
              dot-netty.tcp {
                hostname = 0.0.0.0
                public-hostname=10.0.12.18
                port = 0
                dns-use-ipv6 = false
                enforce-ip-family = true
              }
            }
            cluster {
              seed-nodes = []
              auto-down-unreachable-after = 30s
            }
          }";

            var seedNodes = seedNodeManager.RetrieveAllSeedNodes(false).ToList();
            seedNodes.ForEach(s=>Console.WriteLine($"Seed Address: {s}"));
            
            var config = ConfigurationFactory.ParseString(configString);
            StartUp(config,seedNodes);

            seedNodeManager.OnSplitCluster((IEnumerable<string> newSeedNodes)=>{
                seedNodes.AddRange(newSeedNodes);
                Console.Error.WriteLine("Split Cluster Detected");
                newSeedNodes.ToList().ForEach(s=>Console.WriteLine($"New Seed Node Detected {s}"));
            }, seedNodes);

            Console.WriteLine("Press any key to exit");
            Console.ReadLine();
        }

        public static void StartUp(Config config, IEnumerable<string> seedAddresses)
        {
            //Override the configuration of the port
            string port = Environment.GetEnvironmentVariable("PORT");
            string externalIpAddress = Environment.GetEnvironmentVariable("CF_INSTANCE_IP");
            string internalIpAddress = Environment.GetEnvironmentVariable("CF_INSTANCE_INTERNAL_IP");
            string instancePorts = Environment.GetEnvironmentVariable("CF_INSTANCE_PORTS");
            Console.WriteLine($"Internal IP Address {internalIpAddress}");
            Console.WriteLine($"External IP Address {externalIpAddress}");
            Console.WriteLine($"Instance Ports {instancePorts}");

            var seeds = config.GetStringList("akka.cluster.seed-nodes");
            foreach(var seedNode in seedAddresses)
            {
                if(!string.IsNullOrEmpty(seedNode))
                  seeds.Add($"akka.tcp://webcrawler@{seedNode}");
            }

            var injectedClusterConfigString = seeds.Aggregate("akka.cluster.seed-nodes = [", (current, seed) => current + (@"""" + seed + @""", "));
            injectedClusterConfigString += "]";

            var finalConfig =
                ConfigurationFactory.ParseString(string.Format(@"akka.remote.dot-netty.tcp.port={0}
                akka.remote.dot-netty.tcp.public-hostname=""{1}""",port,internalIpAddress) )
                .WithFallback(ConfigurationFactory.ParseString(injectedClusterConfigString))
                .WithFallback(config);

            Console.WriteLine($"Final Config {finalConfig}");


            //create an Akka system
            var system = ActorSystem.Create("webcrawler", finalConfig);
            
            //create an actor that handles cluster domain events
            system.ActorOf(Props.Create(typeof(SimpleListener)), "clusterListener");
            
        }
    }
}
