using IoTHubTrigger = Microsoft.Azure.WebJobs.EventHubTriggerAttribute;

using System;
using Microsoft.Azure.Cosmos;
using Microsoft.AspNetCore.Mvc;
using System.Linq;
using Newtonsoft.Json.Linq;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Azure.EventHubs;
using System.Threading.Tasks;
using System.Text;
using System.Net.Http;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;

namespace Arduino
{
    public class ArduinoTempSensorData
    {
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
        private static HttpClient client = new HttpClient();

        [FunctionName("ArduinoTempSensorData")]
        public async Task /*public static void*/ Run([IoTHubTrigger("messages/events", Connection = "AzureEventHubConnectionString")]EventData message, ILogger log)
        {
            log.LogInformation($"C# IoT Hub trigger function processed a message: {Encoding.UTF8.GetString(message.Body.Array)}");
            
            /* ---------------------------------------------------------------------------
            Working with CosmosDB. Right now we are using functions strait forward. But in
            the future separate method should be finished. Also add separate builder for
            building Cosmos DB Client once.
            ------------------------------------------------------------------------------*/
            JObject json_message = JObject.Parse(Encoding.UTF8.GetString(message.Body.Array));
            string deviceId = (string)json_message["deviceId"];

            PartitionKey partitionKey = new PartitionKey(deviceId);
            Container cont = cosmosClient.GetDatabase(Environment.GetEnvironmentVariable("CosmosDB")).GetContainer(Environment.GetEnvironmentVariable("CosmosContainer"));

            log.LogInformation($"Json file: {json_message}");

            await cont.CreateItemAsync(json_message, partitionKey, new ItemRequestOptions() { EnableContentResponseOnWrite = false});
            log.LogInformation($"Message was sent to CosmosDB: {json_message}");

        }

        /*
        public async Task<IActionResult> SendDocumentAsync(string message, ILogger log)
        {
            JObject json_message = JObject.Parse(message);
            string timestamp = (string)json_message["timestamp"];

            PartitionKey partitionKey = new PartitionKey(timestamp);
            Container cont = cosmosClient.GetDatabase("sensor_temp").GetContainer("temp");

            try
            {
                ItemResponse<TempSensorData> TempSensorFile = await cont.ReadItemAsync<TempSensorData>(timestamp, partitionKey);
                log.LogInformation($"CosmosDB was read for file on timestamp: {timestamp}");

                await cont.CreateItemAsync(message, partitionKey, new ItemRequestOptions() { EnableContentResponseOnWrite = false});
                log.LogInformation($"New document was sent to CosmosDB");
                
                // Code has to return value:
                return new OkObjectResult("Response from function which handles IoT Hub messages.");
            }
            catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                await cont.CreateItemAsync(message, partitionKey, new ItemRequestOptions() { EnableContentResponseOnWrite = false});
                log.LogInformation($"New Json file was added to Cosmos DB");
                return new OkObjectResult("Response from function which handles IoT Hub messages.");
            }
        }
        */
    }
}

// Not used right now.
/*
internal class TempSensorData
{
    public int deviceId { get; set; }
    public string timestamp { get; set; }
    public string temp_cel { get; set; }
    public string temp_fah { get; set; }
    public string temp_k { get; set; }
}
*/