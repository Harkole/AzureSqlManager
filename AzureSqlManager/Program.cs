using Microsoft.Azure.Management.Fluent;
using Microsoft.Azure.Management.ResourceManager.Fluent;
using Microsoft.Azure.Management.ResourceManager.Fluent.Authentication;
using Microsoft.Azure.Management.ResourceManager.Fluent.Core;
using Microsoft.Azure.Management.Sql.Fluent;
using Microsoft.Extensions.Configuration;
using System;
using System.IO;

namespace AzureSqlManager
{
    class Program
    {
        private static string sqlSubscriptionId;
        private static string sqlElasticPoolName;
        private static string sourceDatabase;

        static void Main()
        {
            // Load the config from the appsettings.json file
            IConfigurationBuilder builder = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);

            // Get the configuration
            IConfigurationRoot configuration = builder.Build();

            // Load the configuration values in to the global values
            sqlSubscriptionId = configuration.GetSection("SqlServer")?.Value;
            sqlElasticPoolName = configuration.GetSection("ElasticPool")?.Value;
            sourceDatabase = configuration.GetSection("SourceDatabase")?.Value;

            // Load the credentials from somewhere - in this case a file (not recommended)
            AzureCredentials credentials = SdkContext.AzureCredentialsFactory.FromFile("azureCredentials.json");

            // Connect to Azure
            IAzure azure = Azure.Configure()
                .WithLogLevel(HttpLoggingDelegatingHandler.Level.Basic)
                .Authenticate(credentials)
                .WithDefaultSubscription();

            // Output the Subscription ID that was connected too (Sanity Check)
            Console.WriteLine($"Selected Subscription: {azure.SubscriptionId}");

            // Create the new Database from a restore point
            CreateDatabaseFromRestorePoint(azure);
        }


        private static void CreateDatabaseFromRestorePoint(IAzure azure)
        {
            ISqlServer sqlServer = azure.SqlServers.GetById(sqlSubscriptionId);
            ISqlDatabase database = sqlServer.Databases.Get(sourceDatabase);

            // Let the user know something is happening
            Console.WriteLine("Starting creation of new database from restore point of existing item");
            Console.WriteLine("This can take a long time...");

            // There only ever seems to be one come back...?
            IRestorePoint restorePoint = database.ListRestorePoints()[0];

            // Select the Elastic Pool to deploy too
            ISqlElasticPool elasticPool = sqlServer.ElasticPools.Get(sqlElasticPoolName);

            // Restore the database from 5 minutes ago to a random name prefixed with Example_
            string dbName = SdkContext.RandomResourceName("Example_", 20);

            ISqlDatabase newDatabase = sqlServer.Databases
                .Define(dbName)
                .WithExistingElasticPool(elasticPool)
                .FromRestorePoint(restorePoint, DateTime.UtcNow.AddMinutes(-5))
                .Create();

            // The process is finished...
            Console.WriteLine($"Database {newDatabase.Name} deployed to pool {elasticPool.Name}");
        }
    }
}
