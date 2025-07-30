# Rinha Stress Tester

A comprehensive .NET 9 load testing tool for the Rinha de Backend 2025 challenge with advanced features including **mid-test delay simulation**.

## 🔥 New Features

### Random Stress Condition System
The stress tester uses a **dice roll system** to randomly apply stress conditions during tests:

- **1d4 Roll** determines which stress condition to apply
- **25% chance each** for: Both delay+failure, Failure only, Delay only, or No stress
- **Can be disabled** with `--no-stress` flag for guaranteed clean tests
- **Applied at random points** during the test (between 10% and 90% completion)

### Stress Conditions Applied
When stress is triggered, it can include:

- **High Latency**: Sets 1250ms delay on default processor
- **Failures**: Enables failures on default processor  
- **Random Timing**: Applied at 1-3 random points during test execution
- **Automatic Reset**: All conditions are reset after test completion

This simulates real-world unpredictable scenarios during peak load!

## Usage

### Basic Usage
```bash
# Basic stress test with random stress conditions (1d4 roll system)
dotnet run -- -r 1000 -t 20

# Clean test with NO stress conditions (guaranteed)
dotnet run -- -r 1000 -t 20 --no-stress

# Higher load test with random conditions
dotnet run -- -r 5000 -t 50 --url http://localhost:9999

# Manually set processor delay (utility mode - no stress test)
dotnet run -- --set-delay 1250 --processor default
dotnet run -- --set-delay 500 --processor fallback
dotnet run -- --set-delay 0 --processor default  # Reset delay
```

### Command Line Options

| Option | Short | Description | Default |
|--------|-------|-------------|---------|
| `--requests` | `-r` | Number of requests to send (required for stress test) | - |
| `--threads` | `-t` | Number of concurrent threads | 10 |
| `--url` | `-u` | Base URL for the API | http://localhost:9999 |
| `--set-delay` | - | Set delay on processor in milliseconds (utility mode) | - |
| `--processor` | - | Processor type: default or fallback | default |
| `--no-stress` | - | Disable random stress conditions (force clean test) | false |
| `--help` | `-h` | Show help message | - |

**Note**: By default, stress conditions are applied randomly via 1d4 dice roll. Use `--no-stress` to guarantee no stress conditions.

## Demo Scripts

Since stress conditions are now random, demo scripts would show different stress patterns:

1. **`basic-test.bat`** - Basic stress test with random conditions
2. **`high-load-test.bat`** - High load test with random conditions  
3. **`utility-delay-test.bat`** - Manual delay setting utilities
4. **`clean-test.bat`** - Reset all delays before testing

## How Random Stress Works

### Dice Roll System
1. At test start, rolls **1d4** to determine stress condition:
   - **Roll 1**: Both high latency (1250ms) and failures
   - **Roll 2**: Failures only
   - **Roll 3**: High latency (1250ms) only  
   - **Roll 4**: No stress conditions

### Random Application Points
2. If stress is enabled, it applies at **1-3 random request indices**:
   - Applied between 10% and 90% of total requests
   - Multiple stress points possible in one test
   - Each application point shows in logs

3. **Automatic cleanup** resets all conditions after test completion

This simulates real-world scenarios like:
- Unpredictable database slowdowns during peak traffic
- Random network latency spikes
- Sudden resource constraints
- External service degradations
- **Random processor failures and recovery**
- **Unexpected service circuit breaker activation**
- **Real-world chaos engineering scenarios**

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
