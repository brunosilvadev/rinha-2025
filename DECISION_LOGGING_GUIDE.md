# Decision Service Log Filtering Guide (Docker Compose)

## Overview
The DecisionService now includes **DECISION_RESULT** keyword in all decision logs to help you track which payment processor was chosen for each request when running via Docker Compose.

## Docker Compose Log Filtering Commands

### View All Decision Results
```bash
# View all decision results in real-time from your API services
docker-compose logs -f api1 api2 | grep "DECISION_RESULT"

# Or view recent decision results (last 100 lines)
docker-compose logs --tail=100 api1 api2 | grep "DECISION_RESULT"

# View all services but filter for decisions
docker-compose logs -f | grep "DECISION_RESULT"
```

### Specific Decision Types

**Default Processor Chosen (Low Latency):**
```bash
docker-compose logs api1 api2 | grep "DECISION_RESULT.*Using default processor - healthy and low latency"
```

**Fallback Processor Chosen (Better Latency):**
```bash
docker-compose logs api1 api2 | grep "DECISION_RESULT.*Using fallback processor - better latency"
```

**Default Processor Chosen (Fallback Too Slow):**
```bash
docker-compose logs api1 api2 | grep "DECISION_RESULT.*Using default processor - fallback latency not better"
```

**Fallback Processor Chosen (Default Failing):**
```bash
docker-compose logs api1 api2 | grep "DECISION_RESULT.*Using fallback processor - default is failing"
```

**Both Processors Failing:**
```bash
docker-compose logs api1 api2 | grep "DECISION_RESULT.*Both processors are failing"
```

### Advanced Docker Compose Log Filtering

**Follow logs with timestamps:**
```bash
docker-compose logs -f -t api1 api2 | grep "DECISION_RESULT"
```

**View logs from specific time:**
```bash
docker-compose logs --since="1h" api1 api2 | grep "DECISION_RESULT"
```

**View logs with service names:**
```bash
docker-compose logs -f api1 api2 | grep "DECISION_RESULT"
```

**Count decision results:**
```bash
docker-compose logs api1 api2 | grep -c "DECISION_RESULT"
```

## Decision Log Examples

You'll see logs like these in Docker Compose:

```
api1_1  | [15:30:45 INF] DECISION_RESULT: Using default processor - healthy and low latency (45ms)
api2_1  | [15:30:46 INF] DECISION_RESULT: Using fallback processor - better latency (250ms vs 1200ms)
api1_1  | [15:30:47 WRN] DECISION_RESULT: Default processor is failing or unreachable, checking fallback
api1_1  | [15:30:47 INF] DECISION_RESULT: Using fallback processor - default is failing
api2_1  | [15:30:48 WRN] DECISION_RESULT: Both processors are failing - attempt 2/3
```

## Real-Time Monitoring Setup

### Terminal 1: Monitor Decision Results
```bash
docker-compose logs -f api1 api2 | grep --line-buffered "DECISION_RESULT"
```

### Terminal 2: Run Your Stress Test
```bash
cd RinhaStressTester
dotnet run -- --requests 1000 --threads 20
```

### Terminal 3: Monitor All Services
```bash
docker-compose logs -f
```

## Decision Logic Summary

1. **Default Healthy + Low Latency (<1000ms)** → Use Default ✅
2. **Default Healthy + High Latency + Fallback Faster** → Use Fallback 🔄
3. **Default Healthy + High Latency + Fallback Slower** → Use Default ✅
4. **Default Failing + Fallback Healthy** → Use Fallback 🔄
5. **Both Failing** → Retry, then Default as Last Resort ⚠️

## Testing Different Scenarios

Use your `processor.http` file to simulate different conditions, then monitor with Docker Compose logs:

**Make Default Slow (>1000ms):**
```http
PUT http://localhost:8001/admin/configurations/delay
Content-Type: application/json
X-Rinha-Token: 123

{"delay": 1500}
```

**Make Default Fail:**
```http
PUT http://localhost:8001/admin/configurations/failure
Content-Type: application/json
X-Rinha-Token: 123

{"failure": true}
```

**Monitor the decisions in real-time:**
```bash
docker-compose logs -f api1 api2 | grep "DECISION_RESULT"
```

## PowerShell Alternative (Windows)

**Recommended for Windows users:**

```powershell
# Using the PowerShell script (recommended)
.\monitor-decisions.ps1 live      # Real-time monitoring
.\monitor-decisions.ps1 stats     # Get statistics
.\monitor-decisions.ps1 recent    # Recent decisions
.\monitor-decisions.ps1 failures  # Show problems
.\monitor-decisions.ps1 save      # Save to file

# Or using direct PowerShell commands
docker-compose logs -f api1 api2 | Select-String "DECISION_RESULT"
docker-compose logs --tail=100 api1 api2 | Select-String "DECISION_RESULT"
(docker-compose logs api1 api2 | Select-String "DECISION_RESULT").Count
```

**Using Windows Batch Script:**
```cmd
monitor-decisions.bat live      # Real-time monitoring
monitor-decisions.bat stats     # Get statistics  
monitor-decisions.bat recent    # Recent decisions
monitor-decisions.bat failures  # Show problems
monitor-decisions.bat save      # Save to file
```

## Production Monitoring Tips

**Save decision results to file:**
```bash
docker-compose logs --no-color api1 api2 | grep "DECISION_RESULT" > decision_results.log
```

**Monitor processor usage patterns:**
```bash
# Count default processor usage
docker-compose logs api1 api2 | grep -c "DECISION_RESULT.*Using default processor"

# Count fallback processor usage  
docker-compose logs api1 api2 | grep -c "DECISION_RESULT.*Using fallback processor"
```

**Check for system issues:**
```bash
# Look for failing processors
docker-compose logs api1 api2 | grep "DECISION_RESULT.*failing"

# Monitor retry attempts
docker-compose logs api1 api2 | grep "DECISION_RESULT.*attempt"
```
