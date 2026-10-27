# Mam Xanh - tao khung du an (chay 1 lan o thu muc goc)
$ErrorActionPreference = "Stop"

function Add-Keep($path) {
    New-Item -ItemType Directory -Force -Path $path | Out-Null
    New-Item -ItemType File -Force -Path (Join-Path $path ".gitkeep") | Out-Null
}

Write-Host "== Tao .gitignore va solution ==" -ForegroundColor Green
dotnet new gitignore --force
dotnet new sln -n MamXanh

Write-Host "== Tao thu vien dung chung ==" -ForegroundColor Green
dotnet new classlib -n MamXanh.Contracts -o shared/MamXanh.Contracts
dotnet sln add shared/MamXanh.Contracts
Add-Keep "shared/MamXanh.Contracts/Events"

$services = @(
    @{ Folder = "identity";      Name = "Identity" },
    @{ Folder = "plant-catalog"; Name = "PlantCatalog" },
    @{ Folder = "garden";        Name = "Garden" },
    @{ Folder = "reminder";      Name = "Reminder" },
    @{ Folder = "notification";  Name = "Notification" },
    @{ Folder = "ai-assistant";  Name = "AiAssistant" },
    @{ Folder = "forum";         Name = "Forum" },
    @{ Folder = "sensor";        Name = "Sensor" }
)

foreach ($s in $services) {
    $api   = "services/$($s.Folder)/$($s.Name).Api"
    $tests = "services/$($s.Folder)/$($s.Name).Tests"
    Write-Host "== Tao service $($s.Name) ==" -ForegroundColor Green

    dotnet new webapi -n "$($s.Name).Api" -o $api --use-controllers
    dotnet new xunit  -n "$($s.Name).Tests" -o $tests
    dotnet sln add $api $tests
    dotnet add $tests reference $api
    dotnet add $api reference shared/MamXanh.Contracts

    foreach ($sub in @("Services", "Data", "Models", "DTOs", "Messaging")) {
        Add-Keep "$api/$sub"
    }
}

Write-Host "== Tao API Gateway ==" -ForegroundColor Green
dotnet new webapi -n Gateway.Api -o services/gateway/Gateway.Api --use-controllers
dotnet sln add services/gateway/Gateway.Api

Write-Host "== Tao cac thu muc con lai ==" -ForegroundColor Green
foreach ($dir in @(
    "frontend",
    "firmware/esp32-moisture",
    "tools/sensor-simulator",
    "infra/postgres",
    "infra/rabbitmq",
    ".github/workflows",
    "docs"
)) {
    Add-Keep $dir
}

Write-Host "== Build thu toan bo ==" -ForegroundColor Green
dotnet build

Write-Host "Xong! Mo GitHub Desktop de commit va push." -ForegroundColor Cyan
