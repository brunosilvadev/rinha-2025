# Rinha Stress Tester

A comprehensive .NET 9 load testing tool for the Rinha de Backend 2025 challenge with advanced features including **mid-test delay simulation**.

## 🔥 New Features

### Mid-Test Delay Change
The stress tester includes a powerful feature that simulates real-world latency scenarios:

- **Automatically triggers** during the middle of your stress test
- **Sets delay to 1250ms** on the default processor (`http://localhost:8001`)
- **Waits 3 seconds** with the increased delay
- **Resets delay back to 0ms** to continue normal operation
- **Enabled by default** for all stress tests (can be disabled with `--no-mid-test-delay`)

### Mid-Test Failure Simulation
Additionally, the tool can simulate processor failures during execution:

- **Automatically triggers** during the middle of your stress test (offset from delay change)
- **Enables failures** on the default processor (`http://localhost:8001`)
- **Waits 3 seconds** with failures enabled
- **Disables failures** back to normal operation
- **Enabled by default** for all stress tests (can be disabled with `--no-mid-test-failure`)

These features help you test how your system handles sudden latency spikes AND processor failures during peak load!

## Usage

### Basic Usage
```bash
# Basic stress test with BOTH delay and failure changes (default behavior)
dotnet run -- -r 1000 -t 20

# Stress test with ONLY delay changes
dotnet run -- -r 1000 -t 20 --no-mid-test-failure

# Stress test with ONLY failure simulation  
dotnet run -- -r 1000 -t 20 --no-mid-test-delay

# Stress test with NO mid-test changes
dotnet run -- -r 1000 -t 20 --no-mid-test-delay --no-mid-test-failure

# Custom URL with both mid-test changes
dotnet run -- -r 5000 -t 50 --url http://localhost:8080

# Manually set processor delay (utility mode)
dotnet run -- --set-delay 1250 --processor default
dotnet run -- --set-delay 500 --processor fallback
```

### Command Line Options

| Option | Short | Description | Default |
|--------|-------|-------------|---------|
| `--requests` | `-r` | Number of requests to send (required for stress test) | - |
| `--threads` | `-t` | Number of concurrent threads | 10 |
| `--url` | `-u` | Base URL for the API | http://localhost:9999 |
| `--set-delay` | - | Set delay on processor in milliseconds | - |
| `--processor` | - | Processor type: default or fallback | default |
| `--no-mid-test-delay` | - | Disable mid-test delay change | false |
| `--no-mid-test-failure` | - | Disable mid-test failure simulation | false |
| `--help` | `-h` | Show help message | - |

## Demo Scripts

Four demo scripts are included:

1. **`demo-mid-test-delay.bat`** - Demonstrates BOTH delay and failure changes (full resilience test)
2. **`demo-delay-only.bat`** - Shows only delay changes
3. **`demo-failure-only.bat`** - Shows only failure simulation
4. **`demo-normal-test.bat`** - Runs without any mid-test changes

## How Mid-Test Changes Work

### Delay Changes
1. The stress test starts normally
2. After approximately half the estimated execution time (minimum 5 seconds), a background task:
   - 🔄 Sets delay to 1250ms on the default processor
   - ⏳ Waits 3 seconds
   - 🔄 Resets delay back to 0ms
   - ✅ Completes the sequence

### Failure Simulation  
1. The stress test starts normally
2. After approximately half the estimated execution time + 2 seconds (offset from delay), a background task:
   - 💥 Enables failures on the default processor
   - ⏳ Waits 3 seconds
   - 🔄 Disables failures back to normal
   - ✅ Completes the sequence

3. The main stress test continues and completes
4. Results include the impact of both temporary delay and failure scenarios

This simulates real-world scenarios like:
- Database slowdowns during peak traffic
- Network latency spikes
- Temporary resource constraints
- External service degradations
- **Processor failures and recovery**
- **Service circuit breaker activation**

## Processor URLs

The tool targets these processor endpoints:
- **Default Processor**: `http://localhost:8001`
- **Fallback Processor**: `http://localhost:8002`
- **Main API**: `http://localhost:9999` (default)

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

## Example Output

```
info: RinhaStressTester.Program[0]
      Starting stress test with 500 requests using 20 threads
info: RinhaStressTester.Program[0]
      Target URL: http://localhost:9999
info: RinhaStressTester.Program[0]
      🔥 Both mid-test DELAY and FAILURE changes are ENABLED - Dynamic resilience testing active!
info: RinhaStressTester.Program[0]
      Generated 500 payment requests
info: RinhaStressTester.Program[0]
      Mid-test delay change will trigger in approximately 12 seconds
info: RinhaStressTester.Program[0]
      Mid-test failure change will trigger in approximately 14 seconds
info: RinhaStressTester.Program[0]
      🔄 TRIGGERING MID-TEST DELAY CHANGE - Setting delay to 1250ms on default processor
info: RinhaStressTester.Program[0]
      ✅ Successfully set delay to 1250ms on Default processor
info: RinhaStressTester.Program[0]
      ⏳ Waiting 3 seconds with increased delay...
info: RinhaStressTester.Program[0]
      � TRIGGERING MID-TEST FAILURE CHANGE - Enabling failures on default processor
info: RinhaStressTester.Program[0]
      ✅ Successfully ENABLED failures on Default processor
info: RinhaStressTester.Program[0]
      �🔄 RESETTING DELAY - Setting delay back to 0ms on default processor
info: RinhaStressTester.Program[0]
      ✅ Successfully set delay to 0ms on Default processor
info: RinhaStressTester.Program[0]
      ✅ Mid-test delay change sequence completed
info: RinhaStressTester.Program[0]
      ⏳ Waiting 3 seconds with failures enabled...
info: RinhaStressTester.Program[0]
      🔄 RESETTING FAILURE - Disabling failures on default processor
info: RinhaStressTester.Program[0]
      ✅ Successfully DISABLED failures on Default processor
info: RinhaStressTester.Program[0]
      ✅ Mid-test failure change sequence completed

=== STRESS TEST RESULTS ===
Total Time: 15.67 seconds
Total Requests: 500
Requests per Second: 31.91
Concurrent Threads: 20

=== RESPONSE STATISTICS ===
Successful Requests: 487 (97.40%)
Failed Requests: 8 (1.60%)
Error Requests: 5 (1.00%)

=== RESPONSE TIME STATISTICS ===
Average Response Time: 456.78 ms
Minimum Response Time: 45.23 ms
Maximum Response Time: 1,678.90 ms
50th Percentile (Median): 234.56 ms
95th Percentile: 1,345.67 ms
99th Percentile: 1,567.89 ms

=== STATUS CODE BREAKDOWN ===
201: 487 requests
429: 8 requests
500: 5 requests
```

## Features

- **Configurable Request Volume**: Specify the number of requests to send
- **Concurrent Threading**: Control the number of concurrent threads for load testing
- **Mid-Test Delay Simulation**: Automatically tests system resilience during latency spikes
- **Mid-Test Failure Simulation**: Automatically tests system resilience during processor failures
- **Processor Configuration**: Manually set delays on default/fallback processors
- **Detailed Statistics**: Get comprehensive performance metrics including:
  - Response time percentiles (50th, 95th, 99th)
  - Success/failure rates
  - HTTP status code breakdown
  - Requests per second
- **Realistic Test Data**: Generates random payment requests with varying amounts
- **Real-time Progress**: Shows progress updates during test execution

## Notes

- The stress tester generates realistic payment data with random amounts between R$ 0.01 and R$ 1,000.00
- Each request includes a unique correlation ID
- Progress updates are shown every 100 requests
- All requests are made concurrently within the specified thread limit
- Mid-test delay change requires the default processor to be running on `http://localhost:8001`
- Mid-test failure simulation requires the default processor to be running on `http://localhost:8001`
- Uses separate HttpClient instances to avoid interference between stress test and configuration calls
