using Microsoft.Extensions.Hosting;

namespace workerBinanceKaydet
{
    public class Program
    {
        public static string _connectionString = ""; //Server=localhost;Database=Ocean;User Id=sa;Password=zeka7744;Trusted_Connection=True;TrustServerCertificate=True;";
        public static string _dbName = "";
        public static void Main(string[] args)
        {
            _connectionString = args[0] + " " + args[1];
            _dbName = args[2];
            var builder = Host.CreateApplicationBuilder(args);
            builder.Services.AddHttpClient();
            builder.Services.AddHostedService<Worker>();

            var host = builder.Build();
            host.Run();
        }
    }
}