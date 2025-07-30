# Rinha Stress Tester

A .NET 9 console application designed to stress test the Rinha payment API with configurable concurrency and request volume.

## Features

- **Configurable Request Volume**: Specify the number of requests to send
- **Concurrent Threading**: Control the number of concurrent threads for load testing
- **Detailed Statistics**: Get comprehensive performance metrics including:
  - Response time percentiles (50th, 95th, 99th)
  - Success/failure rates
  - HTTP status code breakdown
  - Requests per second
- **Realistic Test Data**: Generates random payment requests with varying amounts
- **Real-time Progress**: Shows progress updates during test execution

## Usage

### Basic Usage
```bash
dotnet run -- --requests 1000 --threads 20
```

### Command Line Options

| Option | Short | Description | Default |
|--------|-------|-------------|---------|
| `--requests` | `-r` | Number of requests to send (required) | - |
| `--threads` | `-t` | Number of concurrent threads | 10 |
| `--url` | `-u` | Base URL for the API | http://localhost:9999 |
| `--help` | `-h` | Show help message | - |

### Examples

**Basic stress test with 1000 requests:**
```bash
dotnet run -- -r 1000
```

**High concurrency test:**
```bash
dotnet run -- --requests 5000 --threads 50
```

**Test against different endpoint:**
```bash
dotnet run -- --requests 2000 --threads 25 --url http://localhost:8080
```

**Help:**
```bash
dotnet run -- --help
```

## Building and Running

### Build the project:
```bash
dotnet build
```

### Run the stress tester:
```bash
dotnet run -- [options]
```

### Run from published executable:
```bash
dotnet publish -c Release
./bin/Release/net9.0/RinhaStressTester.exe [options]
```

## Sample Output

```
Starting stress test with 1,000 requests using 20 threads
Target URL: http://localhost:9999
Generated 1,000 payment requests
Completed request 1: Created (245ms)
Completed request 101: Created (198ms)
...

=== STRESS TEST RESULTS ===
Total Time: 12.45 seconds
Total Requests: 1,000
Requests per Second: 80.32
Concurrent Threads: 20

=== RESPONSE STATISTICS ===
Successful Requests: 995 (99.50%)
Failed Requests: 3 (0.30%)
Error Requests: 2 (0.20%)

=== RESPONSE TIME STATISTICS ===
Average Response Time: 234.56 ms
Minimum Response Time: 45.23 ms
Maximum Response Time: 1,234.56 ms
50th Percentile (Median): 201.34 ms
95th Percentile: 567.89 ms
99th Percentile: 892.45 ms

=== STATUS CODE BREAKDOWN ===
201: 995 requests
400: 2 requests
500: 3 requests
```

## Notes

- The stress tester generates realistic payment data with random amounts between R$ 1.00 and R$ 1,000.00
- Each request includes a unique payment ID and user ID
- Progress updates are shown every 100 requests to avoid overwhelming the console
- All requests are made concurrently within the specified thread limit
- The tool uses HttpClient for efficient connection reuse
