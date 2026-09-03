$ErrorActionPreference = "Stop"
$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path


function Write-StudioStep([string]$Message) {
    [Console]::Out.WriteLine("[STUDIO:STEP] $Message")
    [Console]::Out.Flush()
}

function Run-Step {
    param(
        [string] $Name,
        [string] $Message,
        [scriptblock] $Action
    )

    Write-StudioStep $Message

    try {
        & $Action

        if ($LASTEXITCODE -ne 0) {
            throw "Step '$Name' exited with code $LASTEXITCODE"
        }
    }
    catch {
        [Console]::Error.WriteLine("Step '$Name' FAILED")
        exit -1
    }
}

Run-Step "Build" "Building solution" {
    Set-Location (Join-Path $scriptRoot "..\..\")
    dotnet build
}

Run-Step "InstallLibs" "Installing client-side libraries" {
    Set-Location (Join-Path $scriptRoot "..\..\")
    abp install-libs
}

Run-Step "DbMigrator" "Running database migrator" {
    Set-Location (Join-Path $scriptRoot "../../SmartPantry")
    dotnet run --migrate-database
    dotnet run --migrate-database
}

Run-Step "DevCert" "Creating development certificate" {
    Set-Location (Join-Path $scriptRoot "../../SmartPantry")
    dotnet dev-certs https -v -ep openiddict.pfx -p 3204f312-08ac-47a0-a965-184d3fe7329f
}

exit 0
