$unityPath = "D:\Unity\Editor\6000.4.7f1\Editor\Unity.exe"
$projectPath = Get-Location
$logFile = "$projectPath\test-build.log"

Remove-Item -Path $logFile -ErrorAction SilentlyContinue

Write-Host "=================================================="
Write-Host "  CHO NOI MIEN TAY -- Build & Test Check (Windows)"
Write-Host "=================================================="
Write-Host "  Project : $projectPath"
Write-Host "  Log     : $logFile"
Write-Host ""

if (-not (Test-Path $unityPath)) {
    Write-Host "  [LOI] Khong tim thay Unity tai: $unityPath" -ForegroundColor Red
    Exit 1
}

Write-Host "Dang chay Unity compilation va test suite, vui long doi..."
$process = Start-Process -FilePath $unityPath -ArgumentList "-batchmode", "-quit", "-projectPath", "$projectPath", "-executeMethod", "ChoNoi.Editor.BoatTestRunner.Run", "-logFile", "$logFile" -NoNewWindow -Wait -PassThru

if (Test-Path $logFile) {
    $compileErrors = Get-Content $logFile | Where-Object { $_ -match "error CS[0-9]+" } | Select-Object -Unique
    
    Write-Host ""
    Write-Host "--- 1. KIEM TRA COMPILE ---"
    if ($compileErrors) {
        Write-Host ""
        Write-Host "  x  BUILD FAILED -- co loi compile:" -ForegroundColor Red
        $compileErrors | ForEach-Object { Write-Host "     $_" -ForegroundColor Red }
        Exit 1
    } else {
        Write-Host "  v  Khong co loi compile (error CS)." -ForegroundColor Green
    }
    Write-Host ""

    Write-Host "--- 2. KET QUA UNIT TEST ---"
    $hasTests = $false
    Get-Content $logFile | Where-Object { $_ -match "\[(PASS|FAIL)\]" } | ForEach-Object {
        $hasTests = $true
        if ($_ -match "\[PASS\]") {
            Write-Host "  v  [PASS] $($_ -replace '.*\[PASS\]', '')" -ForegroundColor Green
        } elseif ($_ -match "\[FAIL\]") {
            Write-Host "  x  [FAIL] $($_ -replace '.*\[FAIL\]', '')" -ForegroundColor Red
        }
    }
    
    if (-not $hasTests) {
        $locked = Get-Content $logFile | Where-Object { $_ -match "lock" -or $_ -match "already open" }
        if ($locked) {
            Write-Host "  [CANH BAO] Unity Editor dang mo du an nay. Vui long dong Unity Editor truoc khi chay test batchmode de nhan ket qua." -ForegroundColor Yellow
        } else {
            Write-Host "  v  Khong co loi bien dich nao duoc ghi nhan." -ForegroundColor Green
        }
    }
} else {
    Write-Host "  [LOI] Khong tao duoc log file tai: $logFile" -ForegroundColor Red
    Exit 1
}
