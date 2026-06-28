<#
.SYNOPSIS
    依「員工加入流程」批次產生模擬員工測試資料（透過後端 API，非直接寫 DB）。

.DESCRIPTION
    完整重現後端 EmployeeService.CreateAsync 的加入流程：
      1. 以管理者帳號登入  POST /api/auth/login            → 取得 Bearer token
      2. 逐筆呼叫          POST /api/employees (AdminOnly)  → 後端在單一交易內建立
         Employee（員工本體）+ ApplicationUser（登入帳號，掛 Employee 角色）+ AttendanceState（預設不在場）

    所有員工共用同一組密碼，方便用任一帳號登入測試。
    帳號若已存在（HTTP 409）會自動略過，因此本腳本可重複執行（idempotent）。

.PARAMETER BaseUrl
    後端 API 根路徑，預設 http://localhost:7176/api

.PARAMETER Count
    要產生的員工筆數，預設 60。帳號為 emp001 ~ empNNN。

.PARAMETER EmployeePassword
    所有測試員工共用的登入密碼（須 >= 8 碼且含英文字母與數字）。預設 Test@1234。

.PARAMETER WithAvatars
    是否為部分員工帶入頭像 URL（pravatar.cc，需要網路）以測試頭像渲染；
    其餘員工不帶頭像，測試「無頭像時顯示姓名首字」的路徑。預設 $true。

.EXAMPLE
    pwsh ./scripts/seed-test-employees.ps1

.EXAMPLE
    pwsh ./scripts/seed-test-employees.ps1 -Count 30 -BaseUrl http://localhost:7176/api
#>
[CmdletBinding()]
param(
    [string]$BaseUrl          = "http://localhost:7176/api",
    [string]$AdminUsername    = "admin",
    [string]$AdminPassword    = "Admin@12345",
    [int]   $Count            = 60,
    [string]$EmployeePassword = "Test@1234",
    [bool]  $WithAvatars      = $true
)

# 讓主控台正確輸出繁體中文
try { [Console]::OutputEncoding = [System.Text.Encoding]::UTF8 } catch {}

# ── 模擬資料來源 ────────────────────────────────────────────────────────────────
$surnames = @(
    '陳','林','黃','張','李','王','吳','劉','蔡','楊',
    '許','鄭','謝','郭','洪','曾','廖','賴','徐','周',
    '葉','蘇','莊','呂','江','何','蕭','羅','高','潘'
)
$givenNames = @(
    '家豪','志明','雅婷','怡君','淑芬','美玲','俊傑','建宏','雅雯','詩涵',
    '冠廷','宗翰','佳穎','思妤','柏翰','子涵','承恩','宇軒','欣怡','哲瑋',
    '庭安','嘉玲','心妍','偉誠','育琳','孟儒','彥廷','郁婷','婉婷','俊宏'
)
$departments = @('研發部','產品部','設計部','行銷部','業務部','人資部','財務部','客服部')

# 產生 $Count 筆「不重複」的姓名（HashSet 去重，foolproof）
function New-UniqueNames {
    param([int]$n)
    $used  = @{}
    $names = New-Object System.Collections.Generic.List[string]

    $max = $surnames.Count * $givenNames.Count
    if ($n -gt $max) {
        Write-Host ("⚠ 要求 {0} 筆，但姓名組合上限為 {1} 筆（{2} 姓 × {3} 名），將以 {1} 筆為上限。" `
            -f $n, $max, $surnames.Count, $givenNames.Count) -ForegroundColor Yellow
        $n = $max
    }

    $i = 0
    while ($names.Count -lt $n) {
        # 姓在內圈快速循環、名每跑完一輪「姓」才進位 → 保證 姓×名 組合不重複（最多 30×30=900 種）
        $surname = $surnames[$i % $surnames.Count]
        $given   = $givenNames[[math]::Floor($i / $surnames.Count) % $givenNames.Count]
        $full    = $surname + $given
        if (-not $used.ContainsKey($full)) {
            $used[$full] = $true
            $names.Add($full)
        }
        $i++
        if ($i -gt 100000) { break }  # 安全閥
    }
    return $names
}

# ── Helper：送出 JSON（明確使用 UTF-8 編碼，避免中文亂碼）─────────────────────────
function Invoke-JsonPost {
    param(
        [string]$Uri,
        [hashtable]$Body,
        [hashtable]$Headers = @{}
    )
    $json  = $Body | ConvertTo-Json -Compress
    $bytes = [System.Text.Encoding]::UTF8.GetBytes($json)
    return Invoke-RestMethod -Uri $Uri -Method Post -Headers $Headers `
        -Body $bytes -ContentType "application/json; charset=utf-8"
}

# ── 1. 管理者登入 ───────────────────────────────────────────────────────────────
Write-Host "→ 以管理者帳號登入 $BaseUrl/auth/login ..." -ForegroundColor Cyan
try {
    $login = Invoke-JsonPost -Uri "$BaseUrl/auth/login" -Body @{
        username = $AdminUsername
        password = $AdminPassword
    }
}
catch {
    Write-Host "✗ 管理者登入失敗：$($_.Exception.Message)" -ForegroundColor Red
    Write-Host "  請確認 (1) 後端已啟動 (2) BaseUrl 正確 (3) 管理者帳密正確。" -ForegroundColor Yellow
    exit 1
}
$token   = $login.accessToken
$headers = @{ Authorization = "Bearer $token" }
Write-Host "✓ 登入成功，已取得 token。" -ForegroundColor Green
Write-Host ""

# ── 2. 逐筆建立員工 ─────────────────────────────────────────────────────────────
$names   = New-UniqueNames -n $Count
$created = 0; $skipped = 0; $failed = 0

Write-Host "→ 開始建立 $Count 筆模擬員工 ..." -ForegroundColor Cyan
for ($i = 0; $i -lt $Count; $i++) {
    $username = "emp{0:D3}" -f ($i + 1)
    $fullName = $names[$i]
    $dept     = $departments[$i % $departments.Count]
    $avatar   = $null
    if ($WithAvatars -and ($i % 3 -ne 0)) {
        $avatar = "https://i.pravatar.cc/150?img=$((($i % 70) + 1))"
    }

    $payload = @{
        fullName   = $fullName
        department = $dept
        avatarUrl  = $avatar
        username   = $username
        password   = $EmployeePassword
    }

    try {
        $null = Invoke-JsonPost -Uri "$BaseUrl/employees" -Body $payload -Headers $headers
        $created++
        Write-Host ("  ✓ {0}  {1}  ({2})" -f $username, $fullName, $dept) -ForegroundColor Green
    }
    catch {
        $status = $null
        try { $status = $_.Exception.Response.StatusCode.value__ } catch {}
        if ($status -eq 409) {
            $skipped++
            Write-Host ("  - {0}  略過（帳號已存在）" -f $username) -ForegroundColor DarkGray
        }
        else {
            $failed++
            $detail = $_.ErrorDetails.Message
            try { $detail = ($detail | ConvertFrom-Json).detail } catch {}
            if (-not $detail) { $detail = $_.Exception.Message }
            Write-Host ("  ✗ {0}  失敗 (HTTP {1})：{2}" -f $username, $status, $detail) -ForegroundColor Red
        }
    }
}

# ── 3. 結果摘要 ─────────────────────────────────────────────────────────────────
Write-Host ""
Write-Host "===== 完成 =====" -ForegroundColor Cyan
Write-Host ("新增：{0} 筆 ｜ 略過（已存在）：{1} 筆 ｜ 失敗：{2} 筆" -f $created, $skipped, $failed)
Write-Host ("帳號範圍：emp001 ~ emp{0:D3}" -f $Count)
Write-Host ("共用登入密碼：$EmployeePassword")
Write-Host ""
Write-Host "驗證方式：以 admin 登入後到「員工管理」頁，或 GET $BaseUrl/employees。" -ForegroundColor Yellow
Write-Host "備註：新員工預設『不在場』且『未指派座位』；打卡與座位指派為另外的流程。" -ForegroundColor Yellow
