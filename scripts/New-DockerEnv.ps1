$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$examplePath = Join-Path $root '.env.docker.example'
$envPath = Join-Path $root '.env'

if (Test-Path $envPath) {
    throw '.env already exists. Preserve it and add any missing Docker variables from .env.docker.example, or move it aside before generating a new file.'
}
if (-not (Test-Path $examplePath)) {
    throw "Docker environment template not found: $examplePath"
}

function New-RandomToken([int]$byteCount) {
    $bytes = New-Object byte[] $byteCount
    $rng = [System.Security.Cryptography.RandomNumberGenerator]::Create()
    try {
        $rng.GetBytes($bytes)
    }
    finally {
        $rng.Dispose()
    }
    [Convert]::ToBase64String($bytes).TrimEnd('=')
}

$values = @{
    MSSQL_SA_PASSWORD = [guid]::NewGuid().ToString('N') + 'Aa1!'
    JWT_KEY = New-RandomToken 48
    SEED_ADMIN_PASSWORD = New-RandomToken 24
    SEED_MANAGER_PASSWORD = New-RandomToken 24
    SEED_WAITER_PASSWORD = New-RandomToken 24
    SEED_BARISTA_PASSWORD = New-RandomToken 24
    SEED_CASHIER_PASSWORD = New-RandomToken 24
    SEED_WAREHOUSE_PASSWORD = New-RandomToken 24
}

$lines = foreach ($line in Get-Content $examplePath) {
    if ($line -match '^([^#=]+)=') {
        $name = $Matches[1]
        if ($values.ContainsKey($name)) {
            "$name=$($values[$name])"
        }
        else {
            $line
        }
    }
    else {
        $line
    }
}

$encoding = New-Object System.Text.UTF8Encoding($false)
[System.IO.File]::WriteAllLines($envPath, [string[]]$lines, $encoding)
Write-Host "Created $envPath with random local-only SQL, JWT, and demo account credentials."
