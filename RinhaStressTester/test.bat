@echo off
echo Rinha Stress Tester - Quick Test Scripts
echo ======================================
echo.

:menu
echo Select a test scenario:
echo 1. Light load test (100 requests, 5 threads)
echo 2. Medium load test (1000 requests, 10 threads)  
echo 3. Heavy load test (5000 requests, 25 threads)
echo 4. Custom test (you specify parameters)
echo 5. Help
echo 6. Exit
echo.

set /p choice="Enter your choice (1-6): "

if "%choice%"=="1" goto light
if "%choice%"=="2" goto medium
if "%choice%"=="3" goto heavy
if "%choice%"=="4" goto custom
if "%choice%"=="5" goto help
if "%choice%"=="6" goto exit

echo Invalid choice. Please try again.
echo.
goto menu

:light
echo Running light load test...
dotnet run -- --requests 100 --threads 5
goto menu

:medium  
echo Running medium load test...
dotnet run -- --requests 1000 --threads 10
goto menu

:heavy
echo Running heavy load test...
dotnet run -- --requests 5000 --threads 25
goto menu

:custom
set /p requests="Enter number of requests: "
set /p threads="Enter number of threads: "
set /p url="Enter API URL (or press Enter for http://localhost:9999): "

if "%url%"=="" set url=http://localhost:9999

echo Running custom test with %requests% requests, %threads% threads, targeting %url%...
dotnet run -- --requests %requests% --threads %threads% --url %url%
goto menu

:help
dotnet run -- --help
goto menu

:exit
echo Goodbye!
pause
