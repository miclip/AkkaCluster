using System;
using System.Threading;
using System.Threading.Tasks;
using AkkaCluster.SeedNodeManagement;
using AkkaCluster.SeedNodeRepository;

namespace AkkaCluster.Lighthouse.NetCoreApp
{
    class Program
    {
    private static string port;
    private static string externalIpAddress;
    private static string internalIpAddress;
    private static string instancePorts;
    private static string applicationName;
    private static string redisConnectionString;

    static void Main(string[] args)
    {
        ReadEnvironmentVariables();
        var seedNodeRepository = new RedisClient(redisConnectionString);
        var seedNodeManager = new SeedNodeManager("lighthouse", applicationName, internalIpAddress, port,seedNodeRepository);
        Task seedNodeManagerTask;
        var cancellationTokenSource = new CancellationTokenSource();
        
        try
        {
            var seedNodes = seedNodeManager.RetrieveAllSeedNodes(true);
            var lighthouseService = new LighthouseService(internalIpAddress, port != null ? (int?)Convert.ToInt32(port): null, seedNodes);
            lighthouseService.Start();

            seedNodeManagerTask = seedNodeManager.StartRegisteringSeedNodeTask(cancellationTokenSource);   
            
            Console.ReadLine();
            lighthouseService.StopAsync().Wait();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error occurred, {ex}");
        }
        finally
        {
            cancellationTokenSource.Cancel();
            seedNodeManager.DeregisterSeedNode();
        }
    }

        private static void ReadEnvironmentVariables()
        {
            port = Environment.GetEnvironmentVariable("PORT");
            externalIpAddress = Environment.GetEnvironmentVariable("CF_INSTANCE_IP");
            internalIpAddress = Environment.GetEnvironmentVariable("CF_INSTANCE_INTERNAL_IP");
            instancePorts = Environment.GetEnvironmentVariable("CF_INSTANCE_PORTS");
            applicationName = Environment.GetEnvironmentVariable("NAME");
            redisConnectionString = Environment.GetEnvironmentVariable("REDISCONNECTIONSTRING");
            Console.WriteLine($"Redis {redisConnectionString}");
            Console.WriteLine($"CF Instance IP: {internalIpAddress}, Name: {applicationName}, Port: {port}");
        }
    }
}