
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

        // Log enabled features
        if (config.EnableMidTestDelayChange && config.EnableMidTestFailureChange)
        {
            _logger.LogInformation("🔥 Both mid-test DELAY and FAILURE changes are ENABLED - Dynamic resilience testing active!");
        }
        else if (config.EnableMidTestDelayChange)
        {
            _logger.LogInformation("🔥 Mid-test DELAY change is ENABLED - Will set 1250ms delay mid-test, then reset to 0ms");
        }
        else if (config.EnableMidTestFailureChange)
        {
            _logger.LogInformation("💥 Mid-test FAILURE change is ENABLED - Will enable failures mid-test, then disable them");
        }
        else
        {
            _logger.LogInformation("ℹ️ Mid-test changes are DISABLED - Running standard stress test");
        }
        
        if (config.EnableMidTestDelayChange)
        {
            _logger.LogInformation("🔥 Mid-test delay change is ENABLED - Will set 1250ms delay mid-test, then reset to 0ms");
        }
        else
        {
            _logger.LogInformation("Mid-test delay change is disabled");
        }

        // Handle different modes
        if (config.Mode == TestMode.SetDelay)
        {
            await SetProcessorDelay(config);
            return;
        }

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
                case "--no-mid-test-delay":
                    config.EnableMidTestDelayChange = false;
                    break;
                case "--no-mid-test-failure":
                    config.EnableMidTestFailureChange = false;
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
        Console.WriteLine("  --no-mid-test-delay      Disable mid-test delay change (enabled by default)");
        Console.WriteLine("  --no-mid-test-failure    Disable mid-test failure simulation (enabled by default)");
        Console.WriteLine("  -h, --help               Show this help message");
        Console.WriteLine();
        Console.WriteLine("Examples:");
        Console.WriteLine("  RinhaStressTester -r 1000 -t 20");
        Console.WriteLine("  RinhaStressTester --requests 5000 --threads 50 --url http://localhost:8080");
        Console.WriteLine("  RinhaStressTester -r 2000 -t 30 --no-mid-test-delay");
        Console.WriteLine("  RinhaStressTester -r 1500 -t 25 --no-mid-test-failure");
        Console.WriteLine("  RinhaStressTester -r 3000 -t 40 --no-mid-test-delay --no-mid-test-failure");
        Console.WriteLine("  RinhaStressTester --set-delay 1250 --processor default");
        Console.WriteLine("  RinhaStressTester --set-delay 500 --processor fallback");
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

    private static async Task RunStressTest(StressTestConfig config)
    {
        var statistics = new StressTestStatistics();
        var semaphore = new SemaphoreSlim(config.ThreadCount);
        var tasks = new List<Task>();
        var stopwatch = Stopwatch.StartNew();

        // Generate test payment requests
        var paymentRequests = GeneratePaymentRequests(config.RequestCount);

        _logger.LogInformation("Generated {Count} payment requests", paymentRequests.Count);

        // Start mid-test delay change task if enabled
        Task? delayTask = null;
        if (config.EnableMidTestDelayChange)
        {
            delayTask = StartMidTestDelayChange(config.RequestCount);
        }

        // Start mid-test failure change task if enabled
        Task? failureTask = null;
        if (config.EnableMidTestFailureChange)
        {
            failureTask = StartMidTestFailureChange(config.RequestCount);
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

        // Wait for delay task to complete if it was started
        if (delayTask != null)
        {
            await delayTask;
        }

        // Wait for failure task to complete if it was started
        if (failureTask != null)
        {
            await failureTask;
        }

        // Display results
        DisplayResults(statistics, stopwatch.Elapsed, config);
    }

    private static async Task StartMidTestDelayChange(int totalRequests)
    {
        try
        {
            // Calculate when to trigger the delay change (middle of execution)
            // We'll estimate based on request processing time, but also use a minimum delay
            var estimatedTimePerRequest = 50; // Estimate 50ms per request on average
            var estimatedTotalTime = totalRequests * estimatedTimePerRequest / 1000; // Convert to seconds
            var delayTime = Math.Max(5, estimatedTotalTime / 2); // Wait at least 5 seconds or half the estimated time

            _logger.LogInformation("Mid-test delay change will trigger in approximately {DelayTime} seconds", delayTime);
            
            // Wait for the middle of the test
            await Task.Delay(TimeSpan.FromSeconds(delayTime));
            
            // Set delay to 1250ms on default processor
            _logger.LogInformation("🔄 TRIGGERING MID-TEST DELAY CHANGE - Setting delay to 1250ms on default processor");
            await SetProcessorDelayInternal("http://localhost:8001", 1250, "Default");
            
            // Wait for 3 seconds
            _logger.LogInformation("⏳ Waiting 3 seconds with increased delay...");
            await Task.Delay(TimeSpan.FromSeconds(3));
            
            // Reset delay back to 0
            _logger.LogInformation("🔄 RESETTING DELAY - Setting delay back to 0ms on default processor");
            await SetProcessorDelayInternal("http://localhost:8001", 0, "Default");
            
            _logger.LogInformation("✅ Mid-test delay change sequence completed");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during mid-test delay change");
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

    private static async Task StartMidTestFailureChange(int totalRequests)
    {
        try
        {
            // Calculate when to trigger the failure change (middle of execution, but offset from delay change)
            var estimatedTimePerRequest = 50; // Estimate 50ms per request on average
            var estimatedTotalTime = totalRequests * estimatedTimePerRequest / 1000; // Convert to seconds
            var delayTime = Math.Max(7, (estimatedTotalTime / 2) + 2); // Wait at least 7 seconds or half + 2 seconds (offset from delay change)

            _logger.LogInformation("Mid-test failure change will trigger in approximately {DelayTime} seconds", delayTime);
            
            // Wait for the middle of the test (offset from delay change)
            await Task.Delay(TimeSpan.FromSeconds(delayTime));
            
            // Enable failure on default processor
            _logger.LogInformation("💥 TRIGGERING MID-TEST FAILURE CHANGE - Enabling failures on default processor");
            await SetProcessorFailureInternal("http://localhost:8001", true, "Default");
            
            // Wait for 3 seconds
            _logger.LogInformation("⏳ Waiting 3 seconds with failures enabled...");
            await Task.Delay(TimeSpan.FromSeconds(3));
            
            // Disable failure back to normal
            _logger.LogInformation("🔄 RESETTING FAILURE - Disabling failures on default processor");
            await SetProcessorFailureInternal("http://localhost:8001", false, "Default");
            
            _logger.LogInformation("✅ Mid-test failure change sequence completed");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during mid-test failure change");
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
    public bool EnableMidTestDelayChange { get; set; } = true; // Enable by default for dynamic testing
    public bool EnableMidTestFailureChange { get; set; } = true; // Enable by default for dynamic testing
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
