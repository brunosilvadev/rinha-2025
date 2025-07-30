
using System.Diagnostics;
using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

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

        // Roll for stress conditions
        var random = new Random();
        var stressRoll = random.Next(1, 5); // 1d4 roll
        
        var stressCondition = stressRoll switch
        {
            1 => StressCondition.BothDelayAndFailure,
            2 => StressCondition.FailureOnly,
            3 => StressCondition.DelayOnly,
            4 => StressCondition.None,
            _ => StressCondition.None
        };

        _logger.LogInformation("🎲 Rolled {Roll}/4 for stress conditions", stressRoll);
        
        var stressMessage = stressCondition switch
        {
            StressCondition.BothDelayAndFailure => "🔥💥 Both HIGH LATENCY and FAILURE conditions will be applied randomly during test!",
            StressCondition.FailureOnly => "💥 FAILURE condition will be applied randomly during test!",
            StressCondition.DelayOnly => "🔥 HIGH LATENCY condition will be applied randomly during test!",
            StressCondition.None => "✅ No stress conditions will be applied - running clean test",
            _ => "✅ No stress conditions will be applied - running clean test"
        };
        
        _logger.LogInformation(stressMessage);

        // Handle different modes
        if (config.Mode == TestMode.SetDelay)
        {
            await SetProcessorDelay(config);
            return;
        }

        await RunStressTest(config, stressCondition);
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
                case "--set-delay":
                    if (i + 1 < args.Length && int.TryParse(args[i + 1], out int delay))
                    {
                        config.SetDelayMs = delay;
                        config.Mode = TestMode.SetDelay;
                    }
                    i++;
                    break;
                case "--processor":
                    if (i + 1 < args.Length)
                    {
                        config.ProcessorType = args[i + 1].ToLower() switch
                        {
                            "default" => ProcessorType.Default,
                            "fallback" => ProcessorType.Fallback,
                            _ => ProcessorType.Default
                        };
                    }
                    i++;
                    break;
                case "-h" or "--help":
                    return null;
            }
        }

        return config.RequestCount > 0 || config.Mode == TestMode.SetDelay ? config : null;
    }

    private static void ShowUsage()
    {
        Console.WriteLine("Rinha Stress Tester - API Load Testing Tool");
        Console.WriteLine();
        Console.WriteLine("Usage: RinhaStressTester [options]");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  -r, --requests <number>   Number of requests to send (required for stress test)");
        Console.WriteLine("  -t, --threads <number>    Number of concurrent threads (default: 10)");
        Console.WriteLine("  -u, --url <url>          Base URL for the API (default: http://localhost:9999)");
        Console.WriteLine("  --set-delay <ms>         Set delay on processor in milliseconds");
        Console.WriteLine("  --processor <type>       Processor type: default or fallback (default: default)");
        Console.WriteLine("  -h, --help               Show this help message");
        Console.WriteLine();
        Console.WriteLine("Stress Conditions:");
        Console.WriteLine("  The tool randomly applies stress conditions during the test:");
        Console.WriteLine("  - 1/4 chance: Both high latency (1250ms) and failures");
        Console.WriteLine("  - 1/4 chance: Failures only");
        Console.WriteLine("  - 1/4 chance: High latency (1250ms) only");
        Console.WriteLine("  - 1/4 chance: No stress conditions");
        Console.WriteLine();
        Console.WriteLine("Examples:");
        Console.WriteLine("  RinhaStressTester -r 1000 -t 20");
        Console.WriteLine("  RinhaStressTester --requests 5000 --threads 50 --url http://localhost:8080");
        Console.WriteLine("  RinhaStressTester --set-delay 1250 --processor default");
    }

    private static async Task SetProcessorDelay(StressTestConfig config)
    {
        try
        {
            var processorUrl = config.ProcessorType == ProcessorType.Default 
                ? "http://localhost:8001" 
                : "http://localhost:8002";
            
            var processorName = config.ProcessorType == ProcessorType.Default ? "Default" : "Fallback";
            
            _logger.LogInformation("Setting delay of {DelayMs}ms on {ProcessorName} processor at {Url}", 
                config.SetDelayMs, processorName, processorUrl);

            var delayRequest = new { delay = config.SetDelayMs };
            var json = JsonSerializer.Serialize(delayRequest);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            
            // Add the admin token header
            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add("X-Rinha-Token", "123");
            
            var response = await _httpClient.PutAsync($"{processorUrl}/admin/configurations/delay", content);
            
            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Successfully set delay on {ProcessorName} processor. Status: {StatusCode}", 
                    processorName, response.StatusCode);
            }
            else
            {
                _logger.LogError("Failed to set delay on {ProcessorName} processor. Status: {StatusCode}, Content: {Content}", 
                    processorName, response.StatusCode, await response.Content.ReadAsStringAsync());
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting processor delay");
        }
    }

    private static async Task RunStressTest(StressTestConfig config, StressCondition stressCondition)
    {
        var statistics = new StressTestStatistics();
        var semaphore = new SemaphoreSlim(config.ThreadCount);
        var tasks = new List<Task>();
        var stopwatch = Stopwatch.StartNew();
        var random = new Random();

        // Generate test payment requests
        var paymentRequests = GeneratePaymentRequests(config.RequestCount);

        _logger.LogInformation("Generated {Count} payment requests", paymentRequests.Count);

        // Determine random stress application points
        var stressApplicationPoints = new List<int>();
        if (stressCondition != StressCondition.None)
        {
            // Apply stress at 1-3 random points during the test
            var numStressPoints = random.Next(1, 4);
            for (int i = 0; i < numStressPoints; i++)
            {
                // Pick random request indices between 10% and 90% of total requests
                var minPoint = (int)(config.RequestCount * 0.1);
                var maxPoint = (int)(config.RequestCount * 0.9);
                var stressPoint = random.Next(minPoint, maxPoint);
                stressApplicationPoints.Add(stressPoint);
            }
            stressApplicationPoints.Sort();
            
            _logger.LogInformation("🎯 Stress conditions will be applied at request indices: {Points}", 
                string.Join(", ", stressApplicationPoints));
        }

        for (int i = 0; i < config.RequestCount; i++)
        {
            var requestIndex = i;
            var paymentRequest = paymentRequests[i];

            tasks.Add(Task.Run(async () =>
            {
                await semaphore.WaitAsync();
                try
                {
                    // Check if we should apply stress at this point
                    if (stressApplicationPoints.Contains(requestIndex))
                    {
                        await ApplyStressCondition(stressCondition, requestIndex);
                    }
                    
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

        // Reset any applied stress conditions
        if (stressCondition != StressCondition.None)
        {
            _logger.LogInformation("🔄 Resetting all stress conditions...");
            await ResetStressConditions();
        }

        // Display results
        DisplayResults(statistics, stopwatch.Elapsed, config);
    }

    private static async Task ApplyStressCondition(StressCondition condition, int requestIndex)
    {
        try
        {
            switch (condition)
            {
                case StressCondition.BothDelayAndFailure:
                    _logger.LogInformation("🔥💥 APPLYING STRESS at request {Index}: High latency + Failures", requestIndex);
                    await SetProcessorDelayInternal("http://localhost:8001", 1250, "Default");
                    await SetProcessorFailureInternal("http://localhost:8001", true, "Default");
                    break;
                    
                case StressCondition.FailureOnly:
                    _logger.LogInformation("💥 APPLYING STRESS at request {Index}: Failures only", requestIndex);
                    await SetProcessorFailureInternal("http://localhost:8001", true, "Default");
                    break;
                    
                case StressCondition.DelayOnly:
                    _logger.LogInformation("� APPLYING STRESS at request {Index}: High latency only", requestIndex);
                    await SetProcessorDelayInternal("http://localhost:8001", 1250, "Default");
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error applying stress condition at request {Index}", requestIndex);
        }
    }

    private static async Task ResetStressConditions()
    {
        try
        {
            await SetProcessorDelayInternal("http://localhost:8001", 0, "Default");
            await SetProcessorFailureInternal("http://localhost:8001", false, "Default");
            _logger.LogInformation("✅ All stress conditions reset");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resetting stress conditions");
        }
    }

    private static async Task SetProcessorDelayInternal(string processorUrl, int delayMs, string processorName)
    {
        try
        {
            var delayRequest = new { delay = delayMs };
            var json = JsonSerializer.Serialize(delayRequest);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            
            // Create a separate HttpClient for this operation to avoid conflicts
            using var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Add("X-Rinha-Token", "123");
            
            var response = await httpClient.PutAsync($"{processorUrl}/admin/configurations/delay", content);
            
            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("✅ Successfully set delay to {DelayMs}ms on {ProcessorName} processor", 
                    delayMs, processorName);
            }
            else
            {
                _logger.LogWarning("⚠️ Failed to set delay on {ProcessorName} processor. Status: {StatusCode}", 
                    processorName, response.StatusCode);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting delay on {ProcessorName} processor", processorName);
        }
    }

    private static async Task SetProcessorFailureInternal(string processorUrl, bool enableFailure, string processorName)
    {
        try
        {
            var failureRequest = new { failure = enableFailure };
            var json = JsonSerializer.Serialize(failureRequest);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            
            // Create a separate HttpClient for this operation to avoid conflicts
            using var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Add("X-Rinha-Token", "123");
            
            var response = await httpClient.PutAsync($"{processorUrl}/admin/configurations/failure", content);
            
            if (response.IsSuccessStatusCode)
            {
                var status = enableFailure ? "ENABLED" : "DISABLED";
                _logger.LogInformation("✅ Successfully {Status} failures on {ProcessorName} processor", 
                    status, processorName);
            }
            else
            {
                var status = enableFailure ? "enable" : "disable";
                _logger.LogWarning("⚠️ Failed to {Status} failures on {ProcessorName} processor. Status: {StatusCode}", 
                    status, processorName, response.StatusCode);
            }
        }
        catch (Exception ex)
        {
            var action = enableFailure ? "enabling" : "disabling"; 
            _logger.LogError(ex, "Error {Action} failures on {ProcessorName} processor", action, processorName);
        }
    }

    private static List<PaymentRequest> GeneratePaymentRequests(int count)
    {
        var requests = new List<PaymentRequest>();
        var random = new Random();
        
        for (int i = 0; i < count; i++)
        {
            // Generate realistic currency amounts with only 2 decimal places
            var randomCents = random.Next(1, 100000); // 1 cent to 1000.00
            var amount = Math.Round((decimal)randomCents / 100, 2);
            
            requests.Add(new PaymentRequest
            {
                CorrelationId = Guid.NewGuid().ToString(),
                Amount = amount
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
    public TestMode Mode { get; set; } = TestMode.StressTest;
    public ProcessorType ProcessorType { get; set; } = ProcessorType.Default;
    public int SetDelayMs { get; set; } = 0;
}

public enum TestMode
{
    StressTest,
    SetDelay
}

public enum ProcessorType
{
    Default,
    Fallback
}

public enum StressCondition
{
    None,
    DelayOnly,
    FailureOnly,
    BothDelayAndFailure
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
