# Rinha Decision Monitoring Tool (PowerShell) - Simple Version
# Usage: .\monitor-decisions-simple.ps1 action

$action = $args[0]
if (-not $action) { $action = "help" }

if ($action -eq "live") {
    Write-Host "🔍 Monitoring decision results in real-time..." -ForegroundColor Cyan
    Write-Host "Press Ctrl+C to stop" -ForegroundColor Yellow
    docker-compose logs -f api1 api2 | Select-String "DECISION_RESULT"
}
elseif ($action -eq "stats") {
    Write-Host "📊 Decision Statistics:" -ForegroundColor Green
    Write-Host "======================"
    
    $logs = docker-compose logs api1 api2 2>$null
    if ($logs) {
        $totalDecisions = ($logs | Select-String "DECISION_RESULT").Count
        $defaultProcessor = ($logs | Select-String "DECISION_RESULT.*Using default processor").Count
        $fallbackProcessor = ($logs | Select-String "DECISION_RESULT.*Using fallback processor").Count
        $failingCases = ($logs | Select-String "DECISION_RESULT.*failing").Count
        
        Write-Host "Total Decisions: $totalDecisions"
        Write-Host "Default Processor: $defaultProcessor" -ForegroundColor Blue
        Write-Host "Fallback Processor: $fallbackProcessor" -ForegroundColor Magenta
        Write-Host "Failing Cases: $failingCases" -ForegroundColor Red
        
        if ($totalDecisions -gt 0) {
            $defaultPct = [math]::Round(($defaultProcessor / $totalDecisions) * 100, 1)
            $fallbackPct = [math]::Round(($fallbackProcessor / $totalDecisions) * 100, 1)
            Write-Host ""
            Write-Host "Default Usage: $defaultPct%" -ForegroundColor Blue
            Write-Host "Fallback Usage: $fallbackPct%" -ForegroundColor Magenta
        }
    } else {
        Write-Host "No logs found - is Docker Compose running?" -ForegroundColor Yellow
    }
}
elseif ($action -eq "recent") {
    Write-Host "🕒 Recent decision results (last 50):" -ForegroundColor Yellow
    docker-compose logs --tail=50 api1 api2 | Select-String "DECISION_RESULT"
}
elseif ($action -eq "failures") {
    Write-Host "⚠️  Processor failures and issues:" -ForegroundColor Red
    docker-compose logs api1 api2 | Select-String "DECISION_RESULT.*(failing|attempt)"
}
elseif ($action -eq "save") {
    $timestamp = Get-Date -Format "yyyyMMdd_HHmmss"
    $filename = "decision_results_$timestamp.log"
    docker-compose logs --no-color api1 api2 | Select-String "DECISION_RESULT" | Out-File -FilePath $filename -Encoding UTF8
    Write-Host "💾 Decision results saved to: $filename" -ForegroundColor Green
}
else {
    Write-Host "🎯 Rinha Decision Monitoring Tool (PowerShell)" -ForegroundColor Cyan
    Write-Host "=============================================="
    Write-Host ""
    Write-Host "Usage: .\monitor-decisions-simple.ps1 [action]"
    Write-Host ""
    Write-Host "Actions:" -ForegroundColor Yellow
    Write-Host "  live     - Monitor decisions in real-time"
    Write-Host "  stats    - Show decision statistics"
    Write-Host "  recent   - Show recent decisions (last 50)"
    Write-Host "  failures - Show processor failures and retries"
    Write-Host "  save     - Save all decisions to a timestamped file"
    Write-Host ""
    Write-Host "Examples:" -ForegroundColor Green
    Write-Host "  .\monitor-decisions-simple.ps1 live      # Watch decisions as they happen"
    Write-Host "  .\monitor-decisions-simple.ps1 stats     # Get usage statistics"
    Write-Host "  .\monitor-decisions-simple.ps1 recent    # See what happened recently"
}
