// Copyright 2014-2015 Aaron Stannard, Petabridge LLC
//  
// Licensed under the Apache License, Version 2.0 (the "License"); you may not use 
// this file except in compliance with the License. You may obtain a copy of the 
// License at 
// 
//     http://www.apache.org/licenses/LICENSE-2.0 
// 
// Unless required by applicable law or agreed to in writing, software distributed 
// under the License is distributed on an "AS IS" BASIS, WITHOUT WARRANTIES OR 
// CONDITIONS OF ANY KIND, either express or implied. See the License for the 
// specific language governing permissions and limitations under the License.

using System.Collections.Generic;
using System.Linq;
using Akka.Actor;
using Akka.Configuration;
using ConfigurationException = Akka.Configuration.ConfigurationException;

namespace AkkaCluster.Lighthouse.NetCoreApp
{
    /// <summary>
    /// Launcher for the Lighthouse <see cref="ActorSystem"/>
    /// </summary>
    public static class LighthouseHostFactory
    {
        public static ActorSystem LaunchLighthouse(string ipAddress = null, int? specifiedPort = null, IEnumerable<string> seedNodes =null)
        {
            var systemName = "lighthouse";
            var clusterConfig = GetConfig();

            var lighthouseConfig = clusterConfig.GetConfig("lighthouse");
            if (lighthouseConfig != null)
            {
                systemName = lighthouseConfig.GetString("actorsystem", systemName);
            }

            var remoteConfig = clusterConfig.GetConfig("akka.remote");
            ipAddress = ipAddress ??
                        remoteConfig.GetString("dot-netty.tcp.public-hostname") ??
                        "127.0.0.1"; //localhost as a final default
            int port = specifiedPort ?? remoteConfig.GetInt("dot-netty.tcp.port");

            if (port == 0) throw new ConfigurationException("Need to specify an explicit port for Lighthouse. Found an undefined port or a port value of 0 in App.config.");

            var selfAddress = string.Format("akka.tcp://{0}@{1}:{2}", systemName, ipAddress, port);
            var seeds = clusterConfig.GetStringList("akka.cluster.seed-nodes");

            foreach(var seedNode in seedNodes)
            {
                if(!string.IsNullOrEmpty(seedNode))
                  seeds.Add($"akka.tcp://{systemName}@{seedNode}");
            }

            if (!seeds.Contains(selfAddress))
                seeds.Add(selfAddress);        

            var injectedClusterConfigString = seeds.Aggregate("akka.cluster.seed-nodes = [", (current, seed) => current + (@"""" + seed + @""", "));
            injectedClusterConfigString += "]";

            var finalConfig = ConfigurationFactory.ParseString(
                string.Format(@"akka.remote.dot-netty.tcp.public-hostname = {0} 
akka.remote.dot-netty.tcp.port = {1} ", ipAddress, port))
                .WithFallback(ConfigurationFactory.ParseString(injectedClusterConfigString))
                .WithFallback(clusterConfig);

            return ActorSystem.Create(systemName, finalConfig);
        }

        private static Config GetConfig()
        {
            var configString = @"
                    lighthouse{
		                    actorsystem: ""webcrawler"" #POPULATE NAME OF YOUR ACTOR SYSTEM HERE
	                    }
			
                    akka {
	                    actor { 
		                    provider = ""Akka.Cluster.ClusterActorRefProvider, Akka.Cluster""
	                    }
						
	                    remote {
		                    log-remote-lifecycle-events = DEBUG
		                    dot-netty.tcp {
			                    transport-class = ""Akka.Remote.Transport.DotNetty.TcpTransport, Akka.Remote""
			                    applied-adapters = []
			                    transport-protocol = tcp
			                    #will be populated with a dynamic host-name at runtime if left uncommented
			                    #public-hostname = ""POPULATE STATIC IP HERE""
			                    hostname = ""0.0.0.0""
			                    port = 0
                                enforce-ip-family = true 
                                dns-use-ipv6 = false
		                    }
	                    }     
											
	                    cluster {
		                    #will inject this node as a self-seed node at run-time
		                    seed-nodes = [] #manually populate other seed nodes here, i.e. ""akka.tcp://lighthouse@127.0.0.1:4053"", ""akka.tcp://lighthouse@127.0.0.1:4044""
		                    roles = [lighthouse]
                            split-brain-resolver {
                                # Enable one of the available strategies (see descriptions below):
                                # static-quorum, keep-majority, keep-oldest, keep-referee 
                                active-strategy = off

                                # Decision is taken by the strategy when there has been no membership or
                                # reachability changes for this duration, i.e. the cluster state is stable.
                                stable-after = 20s
                            }  
	                    }
                    }
            ";
            return ConfigurationFactory.ParseString(configString);
        }
    }
}
