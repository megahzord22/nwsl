using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using CoverageCalculationService.Models;
using CoverageCalculationService.WorkQueue;
using CoverageCalculationService.RequestProcessor;
using CoverageCalculationService.Utils;
using CoverageCalculationService.OsmDownloadService;

// this file currently doesn't work. it is meant to be a skeleton to call the microservice and get
// the building data the user has asked for 
public class BuildingReturnWorker : BackgroundService
{
    private readonly ILogger<BuildingReturnWorker> _logger;
    private readonly IDataReturnQueue _workQueue;
    private readonly ICompletedBuildingReturnHandler _completedBuildingReturnHandler;
    private readonly IServiceProvider _serviceProvider;
    private readonly HttpClient _httpClient;

    public BuildingReturnWorker(IDataReturnQueue queue,
                                ICompletedBuildingReturnHandler buildingReturnHandler,
                                IServiceProvider services,
                                ILogger<BuildingReturnWorker> logger,
                                HttpClient httpClient)
    {
        _logger = logger;
        _workQueue = queue;
        _completedBuildingReturnHandler = buildingReturnHandler;
        _serviceProvider = services;
        _httpClient = httpClient;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Starting Background Coverage Service");
        await ProcessQueue(stoppingToken);
    }

    private async Task ProcessQueue(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            // Dequeue a building data return from the queue
            CoverageDataReturn request = await _workQueue.DequeueAsync(stoppingToken);
            // Create a new building result with initial status as incomplete
            BuildingResult buildingResult = new BuildingResult(request) { Status = BuildingDataResultStatus.Incomplete };
            try
            {
                // Prepare the request payload
                var requestPayload = new
                {
                    StateCode = request.StateCode,
                    BoundingBox = new
                    {
                        MinLat = request.bbox.MinLat,
                        MinLon = request.bbox.MinLon,
                        MaxLat = request.bbox.MaxLat,
                        MaxLon = request.bbox.MaxLon
                    }
                };
                // this includes the state code and bounding box 
                var jsonPayload = JsonSerializer.Serialize(requestPayload);
                var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

                // This will send the post to the microservice
                var response = await _httpClient.PostAsync("http://microservice-url/fetch", content, stoppingToken);

                response.EnsureSuccessStatusCode();
                
                // Read the response content
                var responseContent = await response.Content.ReadAsStringAsync(stoppingToken);
                
                // Deserialize the response content to GeoJsonData
                GeoJsonData buildingsData = JsonSerializer.Deserialize<GeoJsonData>(responseContent);
                
                buildingResult.Status = BuildingDataResultStatus.Success;
                buildingResult.Data = buildingsData; // this will be where the data needs to be sent to a database
            }
            catch (Exception ex)
            {
                // Log any errors that occur during data retrieval
                _logger.LogError("Error handling building request {request}\n{exception}", request, ex);
                // Update the status of building result to failure
                buildingResult.Status = BuildingDataResultStatus.Failure;
            }
            finally
            {
                // Handle the completed request
                _completedBuildingReturnHandler.HandleCompletedBuildingReturn(buildingResult);
            }
        }
    }
}
