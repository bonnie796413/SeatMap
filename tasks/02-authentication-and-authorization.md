# 02 — 身分驗證與授權

## 模組目標

採用 ASP.NET Core Identity API Endpoints（`AddIdentityApiEndpoints` + `MapIdentityApi`），
以框架內建的登入/refresh/密碼管理流程處理帳密驗證，並以角色（`Employee` / `Admin`）
控制授權。提供自訂 `/api/auth/me` 端點、角色 Seed、自訂 Claims 注入，
供後續所有需保護的 API 使用。

## 前置相依

- **00 基礎建設**（pipeline、例外處理、CORS）。
- **01 資料庫**（`ApplicationUser`、`Employee` 實體與 `AppDbContext`）。

---

## 設計重點

- **Token 流派**：Identity Bearer Token（框架內建，server 端 ticket；含 `accessToken` + `refreshToken`；預設 1 小時，可設定）。
- **密碼雜湊**：Identity 內建 `IPasswordHasher<ApplicationUser>`（PBKDF2）；**不**另寫 `PasswordService`。
- **角色**：以 `AddRoles<IdentityRole>()` 啟用；`Admin`/`Employee` 兩個角色由 `RoleManager` 建立；授權以 `RequireRole("Admin")` 實施。
- **使用者識別**：登入後 server 端 `ClaimsPrincipal` 含 `sub`（UserId）、`name`（UserName）、`role`；另以自訂 `IUserClaimsPrincipalFactory` 注入 `employeeId` claim（供打卡等 API 直接從 `HttpContext.User` 讀取）。
- **無第三方 SSO**：不引入 OAuth/OIDC。
- **無自訂 JWT**：不引入 `Microsoft.AspNetCore.Authentication.JwtBearer`；token 由 Identity 管理。

---

## 詳細執行步驟

### 步驟 1：確認 NuGet 套件

`Microsoft.AspNetCore.Identity.EntityFrameworkCore` 已於模組 01 加入，本模組無需額外套件。

```pwsh
dotnet restore
```

### 步驟 2：自訂 Claims Factory

新增 `BackEnd/Infrastructure/AdditionalUserClaimsPrincipalFactory.cs`：

```csharp
public class AdditionalUserClaimsPrincipalFactory(
    UserManager<ApplicationUser> userManager,
    RoleManager<IdentityRole> roleManager,
    IOptions<IdentityOptions> optionsAccessor)
    : UserClaimsPrincipalFactory<ApplicationUser, IdentityRole>(
        userManager, roleManager, optionsAccessor)
{
    protected override async Task<ClaimsIdentity> GenerateClaimsAsync(ApplicationUser user)
    {
        var identity = await base.GenerateClaimsAsync(user);
        if (user.EmployeeId.HasValue)
            identity.AddClaim(new Claim("employeeId", user.EmployeeId.Value.ToString()));
        return identity;
    }
}
```

- 登入時 Identity 會呼叫此 Factory，產生的 claims（含 `employeeId`）被序列化進 bearer ticket 與 refresh token 存儲中。
- 後端收到 token 後，`HttpContext.User` 即可直接讀取 `employeeId` claim。
- 前端無法解碼 token 本體取得此值，需透過 `GET /api/auth/me` 查詢。

### 步驟 3：註冊 Identity + Bearer Token

於 `Program.cs`：

```csharp
builder.Services
    .AddIdentityApiEndpoints<ApplicationUser>(options =>
    {
        // 可依需求調整密碼規則
        options.Password.RequireDigit = true;
        options.Password.MinimumLength = 6;
    })
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<AppDbContext>()
    .AddClaimsPrincipalFactory<AdditionalUserClaimsPrincipalFactory>();

builder.Services
    .AddAuthentication()
    .AddBearerToken(IdentityConstants.BearerScheme, options =>
    {
        // 預設 1 小時，可依工時需求延長
        options.BearerTokenExpiration = TimeSpan.FromHours(8);
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", p => p.RequireRole("Admin"));
});
```

- `AddIdentityApiEndpoints` 內部已呼叫 `AddAuthentication().AddBearerToken()`，上方的 `AddBearerToken` 僅在需要覆寫 token 有效期時使用（可合併寫在 `AddIdentityApiEndpoints` 的 options 中）。
- pipeline 順序（於 `Program.cs`）：`UseCors` → `UseAuthentication` → `UseAuthorization`。

### 步驟 4：掛載 Identity 端點

```csharp
var api = app.MapGroup("/api");

// Identity 內建端點掛在 /api/auth
api.MapGroup("/auth")
   .MapIdentityApi<ApplicationUser>();
```

自動產生的端點（完整清單）：

| Method | Path | 說明 |
|--------|------|------|
| POST | `/api/auth/register` | 新建帳號 |
| POST | `/api/auth/login` | 登入，回傳 `{accessToken,refreshToken,expiresIn,tokenType}` |
| POST | `/api/auth/refresh` | 以 refreshToken 換新 accessToken |
| POST | `/api/auth/logout` | 登出（server 端撤銷 token） |
| GET/POST | `/api/auth/manage/info` | 查詢/更新目前使用者基本資料 |
| POST | `/api/auth/forgotPassword` | 忘記密碼（需 Email 設定；MVP 可不啟用） |
| POST | `/api/auth/resendConfirmationEmail` | 重送確認信（同上） |

> MVP 階段員工帳號由管理者在模組 06 建立，不對外開放 `/register`；以 route filter 封鎖該 endpoint。

### 步驟 5：自訂 `/api/auth/me` 端點

Identity 內建的 `/manage/info` 不含 `employeeId` 與角色整合視圖，額外建立：

新增 `BackEnd/Endpoints/AuthEndpoints.cs`：

```csharp
public static class AuthEndpoints
{
    public static RouteGroupBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/auth");

        group.MapGet("/me", async (
            ClaimsPrincipal principal,
            UserManager<ApplicationUser> userManager) =>
        {
            var user = await userManager.GetUserAsync(principal);
            if (user is null) return Results.Unauthorized();

            var roles = await userManager.GetRolesAsync(user);
            return Results.Ok(new
            {
                userId    = user.Id,
                username  = user.UserName,
                role      = roles.FirstOrDefault(),
                employeeId = user.EmployeeId
            });
        }).RequireAuthorization();

        return group;
    }
}
```

- 於 `Program.cs` 的 api group 下呼叫 `api.MapAuthEndpoints()`。
- 此 endpoint 從 DB 讀取最新 role，避免 role 變更後 token 中舊資料不一致（Identity bearer ticket 在 refresh 前不會自動更新 role）。

### 步驟 6：角色與管理者帳號 Seed

延續模組 01 `DbSeeder.cs`，補齊驗證邏輯：

```csharp
// 確保角色存在
foreach (var role in new[] { "Admin", "Employee" })
{
    if (!await roleManager.RoleExistsAsync(role))
        await roleManager.CreateAsync(new IdentityRole(role));
}

// 若無任何 Admin，建立預設管理者
var adminPassword = Environment.GetEnvironmentVariable("SEED_ADMIN_PASSWORD")
    ?? "Admin@12345"; // 開發預設，正式以環境變數覆寫並於啟動 log 警告

if (!(await userManager.GetUsersInRoleAsync("Admin")).Any())
{
    var admin = new ApplicationUser
    {
        UserName = "admin",
        CreatedAt = DateTime.UtcNow
    };
    var result = await userManager.CreateAsync(admin, adminPassword);
    if (result.Succeeded)
        await userManager.AddToRoleAsync(admin, "Admin");
}
```

- 密碼來自環境變數 `SEED_ADMIN_PASSWORD`；缺少時使用開發預設並於 log 發出 Warning。
- 正式環境首次部署後提醒透過 `/manage/info` 更換密碼。
- `EmployeeId` 留 null（管理者無對應員工，符合規格）。

### 步驟 7：授權套用約定

- 需管理者的 endpoint 加 `.RequireAuthorization("AdminOnly")`。
- 一般需登入加 `.RequireAuthorization()`。
- 公開 endpoint（如 `/maps` 底圖路由、`/health`）明確 `.AllowAnonymous()`。

### 步驟 8：錯誤回應一致性

- 401 / 403 由 Identity / Authorization middleware 觸發，統一以 `ProblemDetails` 呈現（延續模組 00）。
- 登入失敗由 Identity 內建端點回傳（`400` 搭配驗證錯誤，或 `401`）。

---

## 涉及檔案

| 檔案 | 動作 |
|------|------|
| `BackEnd/Infrastructure/AdditionalUserClaimsPrincipalFactory.cs` | 新增 |
| `BackEnd/Endpoints/AuthEndpoints.cs` | 新增（僅含 `/me`） |
| `BackEnd/Dtos/Auth/MeResponse.cs` | 新增 |
| `BackEnd/Data/DbSeeder.cs` | 修改（角色 seed + admin seed） |
| `BackEnd/Program.cs` | 修改（Identity 註冊、授權政策、endpoint 掛載） |

> 模組 02 **不**新增 `JwtOptions.cs`、`PasswordService.cs`、`TokenService.cs`（功能由 Identity 框架提供）。

---

## API 合約（本模組）

| Method | Path | 授權 | 請求 | 回應 |
|--------|------|------|------|------|
| POST | `/api/auth/login` | 匿名 | `{username,password}` | `{accessToken,refreshToken,expiresIn,tokenType}` |
| POST | `/api/auth/refresh` | 匿名（帶 refreshToken） | `{refreshToken}` | `{accessToken,refreshToken,expiresIn,tokenType}` |
| POST | `/api/auth/logout` | 需登入 | — | 200 |
| GET | `/api/auth/me` | 需登入 | — | `{userId,username,role,employeeId}` |
| GET/POST | `/api/auth/manage/info` | 需登入 | — | Identity 內建使用者資訊 |

---

## 驗收條件（DoD）

- [ ] 以正確帳密 `POST /api/auth/login` 取得 `accessToken` 與 `refreshToken`。
- [ ] 以錯誤密碼登入回非 200（Identity 預設 400），不透露帳號是否存在。
- [ ] `POST /api/auth/refresh` 以有效 refreshToken 換取新 accessToken。
- [ ] 帶有效 accessToken 存取受保護 endpoint 成功；無 token 回 401；非 Admin 存取 `AdminOnly` 回 403。
- [ ] 過期 accessToken 被拒（可調短 `BearerTokenExpiration` 測試）。
- [ ] 密碼以 Identity PBKDF2 雜湊儲存，DB 中查無明文。
- [ ] `GET /api/auth/me` 正確回傳 `userId`、`username`、`role`、`employeeId`（有對應員工時非 null）。
- [ ] `HttpContext.User` 的 `employeeId` claim 在有員工帳號登入時存在（供打卡模組使用）。
- [ ] Seed 初始 `Admin` 角色與預設管理者帳號；密碼來自 `SEED_ADMIN_PASSWORD` 環境變數。
- [ ] `/api/auth/register` 在 MVP 階段以 route filter 封鎖，外部無法呼叫。
