param([switch]$RelayOnly, [switch]$SmokeTest)

$ErrorActionPreference = 'Stop'
$privateRelay = Join-Path $env:USERPROFILE '.nara\relay'
New-Item -ItemType Directory -Path $privateRelay -Force | Out-Null
$python = Join-Path $privateRelay 'venv\Scripts\python.exe'
$voiceModel = Join-Path $privateRelay 'models\supertonic-3'
$sttModel = Join-Path $privateRelay 'models\whisper-tiny'
$textModel = if ($env:NARA_AISUBS_MODEL) { $env:NARA_AISUBS_MODEL } else { 'gpt-6.1-sol' }
$fallbackModel = $env:NARA_AISUBS_FALLBACK_MODEL
$port = 8765
$player = Join-Path (Split-Path -Parent $PSScriptRoot) 'NARA\BuildFinalProject\Windows\NARA.exe'
$configPath = Join-Path $privateRelay 'runtime.json'
if (Test-Path -LiteralPath $configPath) {
    $config = Get-Content -Raw -LiteralPath $configPath | ConvertFrom-Json
    if ($config.python) { $python = $config.python }
    if ($config.voiceModel) { $voiceModel = $config.voiceModel }
    if ($config.sttModel) { $sttModel = $config.sttModel }
    if ($config.aisubsModel) { $textModel = $config.aisubsModel }
    if ($config.playerPath) { $player = $config.playerPath }
    if ($config.port) { $port = [int]$config.port }
    if ($config.PSObject.Properties['aisubsFallbackModel']) { $fallbackModel = $config.aisubsFallbackModel }
}
$relay = Join-Path $PSScriptRoot 'nara_voice_server.py'
$endpoint = "http://127.0.0.1:$port"
$logName = if ($port -eq 8765) { 'relay' } else { "relay-$port" }

if (-not $RelayOnly -and -not (Test-Path -LiteralPath $player -PathType Leaf)) {
    throw 'Build NARA.exe in Unity before running the live launcher.'
}

function Read-RelaySecret([string]$Path) {
    try {
        $sealed = ConvertTo-SecureString -String ((Get-Content -Raw -LiteralPath $Path).Trim())
        $buffer = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($sealed)
        try { return [Runtime.InteropServices.Marshal]::PtrToStringBSTR($buffer) }
        finally { [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($buffer) }
    } catch { throw 'Windows could not read the saved credential for this user.' }
}

function Get-RelayHealth([string]$Token) {
    try {
        Invoke-RestMethod -Uri "$endpoint/health" -TimeoutSec 2 -Headers @{ Authorization = "Bearer $Token" }
    } catch { $null }
}

$authPath = Join-Path $privateRelay 'relay-auth.dpapi'
if (-not (Test-Path -LiteralPath $authPath)) {
    $randomness = [byte[]]::new(32)
    $rng = [Security.Cryptography.RandomNumberGenerator]::Create()
    try { $rng.GetBytes($randomness) } finally { $rng.Dispose() }
    $token = ConvertTo-SecureString ([Convert]::ToBase64String($randomness)) -AsPlainText -Force
    ConvertFrom-SecureString $token | Set-Content -LiteralPath $authPath -Encoding ascii
}
$access = Read-RelaySecret $authPath
function Test-RelayHealth($Health) {
    $Health -and $Health.source -eq 'live' -and $Health.provider -eq 'aisubs' -and
        $Health.api -eq 4 -and $Health.model -eq $textModel -and ([string]$Health.fallbackModel) -eq ([string]$fallbackModel)
}

$health = Get-RelayHealth $access
if ($health -and -not (Test-RelayHealth $health)) {
    throw "Port $port already has a different relay. Close it before starting NARA Live."
}

$previousAccess = $env:NARA_ACCESS_TOKEN
$previousKey = $env:AISUBS_API_KEY
$previousEndpoint = $env:NARA_ENDPOINT
$previousModel = $env:NARA_AISUBS_MODEL
$previousFallback = $env:NARA_AISUBS_FALLBACK_MODEL
try {
    $env:NARA_ACCESS_TOKEN = $access
    if (-not $health) {
        foreach ($path in @($python, $voiceModel, $sttModel)) {
            if (-not (Test-Path -LiteralPath $path)) {
                throw "Missing local relay dependency at $path. See docs/SUARA-KARAKTER.md."
            }
        }
        $credential = Join-Path $privateRelay 'aisubs-key.dpapi'
        if ($previousKey) {
            $env:AISUBS_API_KEY = $previousKey
        } elseif (Test-Path -LiteralPath $credential) {
            $env:AISUBS_API_KEY = Read-RelaySecret $credential
        } else {
            $entered = Read-Host 'AISUBS API key' -AsSecureString
            ConvertFrom-SecureString $entered | Set-Content -LiteralPath $credential -Encoding ascii
            $env:AISUBS_API_KEY = Read-RelaySecret $credential
        }
        if ($env:AISUBS_API_KEY -notmatch '^sk-[A-Za-z0-9_-]+$') {
            throw 'The AISUBS credential has an unexpected format.'
        }
        $arguments = @('-u', ('"{0}"' -f $relay), '--provider', 'aisubs', '--port', $port.ToString(),
                       '--voice-model', ('"{0}"' -f $voiceModel), '--stt-model', ('"{0}"' -f $sttModel))
        $startup = @{
            FilePath = $python
            ArgumentList = $arguments
            WindowStyle = 'Hidden'
            PassThru = $true
            RedirectStandardOutput = Join-Path $privateRelay "$logName.log"
            RedirectStandardError = Join-Path $privateRelay "$logName-error.log"
        }
        $env:NARA_AISUBS_FALLBACK_MODEL = $fallbackModel
        $env:NARA_AISUBS_MODEL = $textModel
        $server = Start-Process @startup
        $env:AISUBS_API_KEY = $null
        for ($i = 0; $i -lt 40 -and -not $health; $i++) {
            if ($server.HasExited) { throw "Relay exited. Check the logs in $privateRelay." }
            Start-Sleep -Milliseconds 400
            $health = Get-RelayHealth $access
        }
        if (-not (Test-RelayHealth $health)) {
            throw "The authenticated AISUBS relay did not start. Check the logs in $privateRelay."
        }
    }
    Write-Output "NARA Live ready ($textModel). Import a presentation under Materi latihan."
    if (-not $RelayOnly) {
        $env:AISUBS_API_KEY = $null
        $env:NARA_ENDPOINT = $endpoint
        $workingDirectory = Split-Path -Parent $player
        if ($SmokeTest) {
            $playerLog = Join-Path $privateRelay 'windows-smoke.log'
            $testArguments = @('-naraSmoke', '-naraLive', '-batchmode', '-force-d3d11',
                               '-logFile', ('"{0}"' -f $playerLog))
            $test = Start-Process -FilePath $player -WorkingDirectory $workingDirectory `
                                  -ArgumentList $testArguments -WindowStyle Hidden -PassThru -Wait
            exit $test.ExitCode
        }
        Start-Process -FilePath $player -WorkingDirectory $workingDirectory
    }
} finally {
    $env:AISUBS_API_KEY = $previousKey
    $env:NARA_ACCESS_TOKEN = $previousAccess
    $env:NARA_ENDPOINT = $previousEndpoint
    $env:NARA_AISUBS_MODEL = $previousModel
    $env:NARA_AISUBS_FALLBACK_MODEL = $previousFallback
}
