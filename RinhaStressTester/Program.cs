using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace RinhaStressTester;

public class Program
{
    private static readonly HttpClient _httpClient = new();
    private static ILogger<Program>? _logger;

    public static async Task Main(string[] args)
    {
        // Setup logging
        var serviceProvider = new ServiceCollection()
            .AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Information))
            .BuildServiceProvider();
        
        _logger = serviceProvider.GetRequiredService<ILogger<Program>>();

        // Parse command line arguments
        var config = ParseArguments(args);
        if (config == null)
        {
            ShowUsage();
            return;
        }

        _logger.LogInformation("Starting stress test with {RequestCount} requests using {ThreadCount} threads", 
            config.RequestCount, config.ThreadCount);
        _logger.LogInformation("Target URL: {BaseUrl}", config.BaseUrl);

        await RunStressTest(config);
    }

    private static StressTestConfig? ParseArguments(string[] args)
    {
        var config = new StressTestConfig();

        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i].ToLower())
            {
                case "-r" or "--requests":
                    if (i + 1 < args.Length && int.TryParse(args[i + 1], out int requests))
                        config.RequestCount = requests;
                    i++;
                    break;
                case "-t" or "--threads":
                    if (i + 1 < args.Length && int.TryParse(args[i + 1], out int threads))
                        config.ThreadCount = threads;
                    i++;
                    break;
                case "-u" or "--url":
                    if (i + 1 < args.Length)
                        config.BaseUrl = args[i + 1];
                    i++;
                    break;
                case "-h" or "--help":
                    return null;
            }
        }

        return config.RequestCount > 0 ? config : null;
    }

    private static void ShowUsage()
    {
        Console.WriteLine("Rinha Stress Tester - API Load Testing Tool");
        Console.WriteLine();
        Console.WriteLine("Usage: RinhaStressTester [options]");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  -r, --requests <number>   Number of requests to send (required)");
        Console.WriteLine("  -t, --threads <number>    Number of concurrent threads (default: 10)");
        Console.WriteLine("  -u, --url <url>          Base URL for the API (default: http://localhost:9999)");
        Console.WriteLine("  -h, --help               Show this help message");
        Console.WriteLine();
        Console.WriteLine("Examples:");
        Console.WriteLine("  RinhaStressTester -r 1000 -t 20");
        Console.WriteLine("  RinhaStressTester --requests 5000 --threads 50 --url http://localhost:8080");
    }

    private static async Task RunStressTest(StressTestConfig config)
    {
        var statistics = new StressTestStatistics();
        var semaphore = new SemaphoreSlim(config.ThreadCount);
        var tasks = new List<Task>();
        var stopwatch = Stopwatch.StartNew();

        // Generate test payment requests
        var paymentRequests = GeneratePaymentRequests(config.RequestCount);

        _logger.LogInformation("Generated {Count} payment requests", paymentRequests.Count);

        for (int i = 0; i < config.RequestCount; i++)
        {
            var requestIndex = i;
            var paymentRequest = paymentRequests[i];

            tasks.Add(Task.Run(async () =>
            {
                await semaphore.WaitAsync();
                try
                {
                    await SendPaymentRequest(config.BaseUrl, paymentRequest, statistics, requestIndex);
                }
                finally
                {
                    semaphore.Release();
                }
            }));
        }

        await Task.WhenAll(tasks);
        stopwatch.Stop();

        // Display results
        DisplayResults(statistics, stopwatch.Elapsed, config);
    }

    private static List<PaymentRequest> GeneratePaymentRequests(int count)
    {
        var requests = new List<PaymentRequest>();
        var random = new Random();
        
        for (int i = 0; i < count; i++)
        {
            requests.Add(new PaymentRequest
            {
                CorrelationId = Guid.NewGuid().ToString(),
                Amount = (decimal)(random.NextDouble() * 999.99 + 0.01) // Random amount between 0.01 and 1000.00
            });
        }

        return requests;
    }

    private static async Task SendPaymentRequest(string baseUrl, PaymentRequest paymentRequest, 
        StressTestStatistics statistics, int requestIndex)
    {
        var requestStopwatch = Stopwatch.StartNew();
        
        try
        {
            var json = JsonSerializer.Serialize(paymentRequest);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            
            var response = await _httpClient.PostAsync($"{baseUrl}/payments", content);
            requestStopwatch.Stop();

            statistics.RecordRequest(response.StatusCode, requestStopwatch.ElapsedMilliseconds);

            if (requestIndex % 100 == 0)
            {
                _logger.LogInformation("Completed request {Index}: {StatusCode} ({Duration}ms)", 
                    requestIndex + 1, response.StatusCode, requestStopwatch.ElapsedMilliseconds);
            }
        }
        catch (Exception ex)
        {
            requestStopwatch.Stop();
            statistics.RecordError(requestStopwatch.ElapsedMilliseconds);
            
            if (requestIndex % 100 == 0)
            {
                _logger.LogWarning("Request {Index} failed: {Error}", requestIndex + 1, ex.Message);
            }
        }
    }

    private static void DisplayResults(StressTestStatistics statistics, TimeSpan totalTime, StressTestConfig config)
    {
        Console.WriteLine();
        Console.WriteLine("=== STRESS TEST RESULTS ===");
        Console.WriteLine($"Total Time: {totalTime.TotalSeconds:F2} seconds");
        Console.WriteLine($"Total Requests: {config.RequestCount:N0}");
        Console.WriteLine($"Requests per Second: {(config.RequestCount / totalTime.TotalSeconds):F2}");
        Console.WriteLine($"Concurrent Threads: {config.ThreadCount}");
        Console.WriteLine();
        
        Console.WriteLine("=== RESPONSE STATISTICS ===");
        Console.WriteLine($"Successful Requests: {statistics.SuccessfulRequests:N0} ({(statistics.SuccessfulRequests * 100.0 / config.RequestCount):F2}%)");
        Console.WriteLine($"Failed Requests: {statistics.FailedRequests:N0} ({(statistics.FailedRequests * 100.0 / config.RequestCount):F2}%)");
        Console.WriteLine($"Error Requests: {statistics.ErrorRequests:N0} ({(statistics.ErrorRequests * 100.0 / config.RequestCount):F2}%)");
        Console.WriteLine();

        if (statistics.ResponseTimes.Count > 0)
        {
            var sortedTimes = statistics.ResponseTimes.OrderBy(x => x).ToList();
            Console.WriteLine("=== RESPONSE TIME STATISTICS ===");
            Console.WriteLine($"Average Response Time: {statistics.ResponseTimes.Average():F2} ms");
            Console.WriteLine($"Minimum Response Time: {sortedTimes.First():F2} ms");
            Console.WriteLine($"Maximum Response Time: {sortedTimes.Last():F2} ms");
            Console.WriteLine($"50th Percentile (Median): {GetPercentile(sortedTimes, 0.5):F2} ms");
            Console.WriteLine($"95th Percentile: {GetPercentile(sortedTimes, 0.95):F2} ms");
            Console.WriteLine($"99th Percentile: {GetPercentile(sortedTimes, 0.99):F2} ms");
        }

        Console.WriteLine();
        Console.WriteLine("=== STATUS CODE BREAKDOWN ===");
        foreach (var statusCode in statistics.StatusCodes.OrderBy(x => x.Key))
        {
            Console.WriteLine($"{statusCode.Key}: {statusCode.Value:N0} requests");
        }
    }

    private static double GetPercentile(List<long> sortedValues, double percentile)
    {
        int index = (int)Math.Ceiling(sortedValues.Count * percentile) - 1;
        return sortedValues[Math.Max(0, Math.Min(index, sortedValues.Count - 1))];
    }
}

public class StressTestConfig
{
    public int RequestCount { get; set; } = 0;
    public int ThreadCount { get; set; } = 10;
    public string BaseUrl { get; set; } = "http://localhost:9999";
}

public class PaymentRequest
{
    public string CorrelationId { get; set; } = "";
    public decimal Amount { get; set; }
}

public class StressTestStatistics
{
    private readonly object _lock = new();
    public int SuccessfulRequests => _successfulRequests;
    public int FailedRequests => _failedRequests;
    public int ErrorRequests => _errorRequests;
    public List<long> ResponseTimes => _responseTimes.ToList();
    public Dictionary<int, int> StatusCodes => new(_statusCodes);

    private int _successfulRequests = 0;
    private int _failedRequests = 0;
    private int _errorRequests = 0;
    private readonly List<long> _responseTimes = new();
    private readonly Dictionary<int, int> _statusCodes = new();

    public void RecordRequest(System.Net.HttpStatusCode statusCode, long responseTimeMs)
    {
        lock (_lock)
        {
            _responseTimes.Add(responseTimeMs);
            
            var statusCodeInt = (int)statusCode;
            _statusCodes[statusCodeInt] = _statusCodes.GetValueOrDefault(statusCodeInt, 0) + 1;

            if (statusCodeInt >= 200 && statusCodeInt < 300)
                _successfulRequests++;
            else
                _failedRequests++;
        }
    }

    public void RecordError(long responseTimeMs)
    {
        lock (_lock)
        {
            _responseTimes.Add(responseTimeMs);
            _errorRequests++;
        }
    }
}
