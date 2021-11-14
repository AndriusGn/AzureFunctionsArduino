using System;
using System.Collections.Generic;
using Microsoft.Azure.Documents;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;
using Microsoft.Azure.Cosmos;
using Microsoft.Data.SqlClient;
using System.Data;
using Newtonsoft.Json;

namespace Arduino
{
    public static class ArduinoTempSensorData_ToSQL
    {
        // Initialise CosmosDB client connection:
        private static CosmosClient cosmosClient = GetCosmosClient();
        private static CosmosClient GetCosmosClient()
        {
            // Taking info from config file:
            var Cosmos_URI = Environment.GetEnvironmentVariable("Cosmos_URI");
            var authorization_KEY = Environment.GetEnvironmentVariable("authorization_KEY");

            return new CosmosClient(Cosmos_URI.ToString(), authorization_KEY, new CosmosClientOptions
            {
                ConnectionMode = ConnectionMode.Direct
            });
        }

        [FunctionName("ArduinoTempSensorData_ToSQL")]
        public static async Task Run([CosmosDBTrigger(
            databaseName: "sensor_temp",
            collectionName: "TemperatureSensor",
            ConnectionStringSetting = "arduinoprojectscosmos_DOCUMENTDB",
            LeaseCollectionName = "leases",
            CreateLeaseCollectionIfNotExists = true)]IReadOnlyList<Document> input, ILogger log)
        {
            // Initialise SQL Server client connection:
            var SQLConnectionString =   $"Data Source = {Environment.GetEnvironmentVariable("SQLDatabaseConnection")}" + 
                                        $"Initial Catalog = {Environment.GetEnvironmentVariable("SQLDatabaseName")}" +
                                        $"User ID = {Environment.GetEnvironmentVariable("SQLServerUserID")}" +
                                        $"Password = {Environment.GetEnvironmentVariable("SQLServerUserPass")}" +
                                        "Pooling = True; Connection Timeout = 30; ConnectRetryCount = 10; ConnectRetryInterval = 1;";

            if (input != null && input.Count > 0)
            {
                log.LogInformation("Documents modified " + input.Count);
                
                // First we need to catch modified/added document unique key (id):
                string uique_cosmos_key = input[0].Id;
                log.LogInformation("Unique document Id " + uique_cosmos_key);

                // Following steps: create data table while selecting data from table stage.LM35_SensorTemperatureData
                DataTable IncomingTeperatureSensorRecord = new DataTable();
                using (var adapter = new SqlDataAdapter($"SELECT TOP 0 * FROM [stage].[LM35_SensorTemperatureData]", SQLConnectionString))  
                {
                    adapter.Fill(IncomingTeperatureSensorRecord);
                }

                foreach (Document document in input)
                {
                    // Now let's send new row to the Azure SQL Database
                    var row = IncomingTeperatureSensorRecord.NewRow();
                    row["id"]           = document.GetPropertyValue<string>("id");
                    row["deviceId"]     = document.GetPropertyValue<string>("deviceId");
                    row["timestamp"]    = document.GetPropertyValue<string>("timestamp");
                    row["temp_cel"]     = document.GetPropertyValue<string>("temp_cel");
                    row["temp_fah"]     = document.GetPropertyValue<string>("temp_fah");
                    row["temp_k"]       = document.GetPropertyValue<string>("temp_k");

                    IncomingTeperatureSensorRecord.Rows.Add(row);

                    // Also let's try to use simple INSERT statement:
                    string insertQuery =    "INSERT INTO [stage].[LM35_SensorTemperatureData] " +
                                            $"VALUES('{document.GetPropertyValue<string>("id")}', " +
                                            $"'{document.GetPropertyValue<string>("deviceId")}', " +
                                            $"'{document.GetPropertyValue<string>("timestamp")}', " +
                                            $"'{document.GetPropertyValue<string>("temp_cel")}', " +
                                            $"'{document.GetPropertyValue<string>("temp_fah")}', " +
                                            $"'{document.GetPropertyValue<string>("temp_k")}')";


                    using (SqlConnection sqlConnection = new SqlConnection(SQLConnectionString))
                    {
                        sqlConnection.Open();
                        using(SqlCommand cmd = new SqlCommand(insertQuery, sqlConnection))
                        {
                            await cmd.ExecuteNonQueryAsync();
                        }
                        log.LogInformation($"Record with id {input[0].Id} was inserted into Azure SQL Server");

                        /*
                        using(var bulk = new SqlBulkCopy(SQLConnectionString))
                        {
                            bulk.DestinationTableName = "stage.LM35_SensorTemperatureData";
                            bulk.WriteToServer(IncomingTeperatureSensorRecord);
                            log.LogInformation($"{IncomingTeperatureSensorRecord.Rows.Count} rows we inserted for document id {input[0].Id} into Azure SQL Server");
                        }
                        */
                    }
                }                          
            }
        }
    }

    public class TemperatureSensorRecord
    {
        public string id { get; set; }
        public string deviceId { get; set; }
        public string timestamp { get; set; }
        public string temp_cel { get; set; }
        public string temp_fah { get; set; }
        public string temp_k { get; set; }
    }

    public class Root
    {
        public TemperatureSensorRecord TemperatureSensorRecord { get; set; } 
    }
}
