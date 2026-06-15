# 01 — 資料庫與領域模型

## 模組目標

建立 PostgreSQL + PostGIS 資料庫結構與 EF Core Code First 領域模型。
定義所有實體（樓層、座位、員工、使用者、座位指派、打卡狀態），整合 NetTopologySuite
讓座位以 `Point` 幾何儲存，並完成首次 Migration 與 Neon 連線。

## 前置相依

- **00 基礎建設**（專案骨架、設定、例外處理）。

## 資料模型總覽

```
ApplicationUser : IdentityUser (登入帳號，繼承 Identity 標準表 AspNetUsers)
  └─ Guid? EmployeeId ─1:1─ Employee (員工本體，可選；管理者帳號可無對應員工)
                            └─1:1─ SeatAssignment ─1:1─ Seat
                            └─1:1─ AttendanceState (目前在場狀態)

Floor (樓層) ─1:N─ Seat (座位，含 Point 幾何)
Floor ─1:1─ FloorMap (底圖/Tile 中繼資料)

Identity 系列表（自動產生）：AspNetUsers、AspNetRoles、AspNetUserRoles、
  AspNetUserClaims、AspNetUserLogins、AspNetUserTokens、AspNetRoleClaims
```

> 規格「無需保留座位變更歷史」「無報表」，故 **不建** 歷史/稽核表。
> 打卡狀態以「目前狀態」單筆表示即可（規格只需地圖即時反映在場與否）。

---

## 實體定義

### ApplicationUser（`AspNetUsers`，繼承 `IdentityUser`）
| 欄位 | 型別 | 說明 |
|------|------|------|
| Id | string PK（GUID） | 繼承自 IdentityUser；Identity 標準主鍵 |
| UserName | varchar(256) unique | 繼承自 IdentityUser；登入帳號；Identity 框架預設上限 256 |
| PasswordHash | text | 繼承自 IdentityUser；Identity 自管雜湊（PBKDF2），長度不固定，不限制 |
| （其他 Identity 欄位） | — | NormalizedUserName、Email、SecurityStamp 等由 Identity 管理 |
| EmployeeId | Guid? FK→employees | 自訂欄位：對應員工（可空，管理者帳號可無） |
| CreatedAt | timestamptz | 自訂欄位 |

> 角色（`Employee`/`Admin`）**不**存於 `AspNetUsers` 欄位，由 Identity 標準角色表（`AspNetRoles`/`AspNetUserRoles`）管理。

### Employee（`employees`）
| 欄位 | 型別 | 說明 |
|------|------|------|
| Id | Guid PK | |
| FullName | varchar(100) | 姓名；中英文姓名上限 100 字元 |
| Department | varchar(100)? | 部門；部門名稱上限 100 字元 |
| AvatarUrl | varchar(2048)? | 頭像 URL（無則前端顯示姓名首字）；遵循 URL 最大長度慣例 |
| CreatedAt | timestamptz | |

### Floor（`floors`）
| 欄位 | 型別 | 說明 |
|------|------|------|
| Id | Guid PK | |
| Name | varchar(100) | 樓層名稱（例：`3F 研發部`）；上限 100 字元 |
| DisplayOrder | int | 輪播/排序用，可拖曳調整 |
| CreatedAt | timestamptz | |

### FloorMap（`floor_maps`）— 底圖/Tile 中繼資料
| 欄位 | 型別 | 說明 |
|------|------|------|
| Id | Guid PK | |
| FloorId | Guid FK→floors unique | 一樓層一底圖 |
| OriginalDxfPath | varchar(512) | 原始 DXF 儲存路徑；檔案路徑上限 512 字元 |
| TileDirectory | varchar(512)? | Tile 輸出目錄（相對 volume）；路徑上限 512 字元 |
| MinZoom | int | Leaflet zoom 下限 |
| MaxZoom | int | zoom 上限 |
| Width | int? | 像素寬（柵格化後） |
| Height | int? | 像素高 |
| BoundsJson | varchar(100)? | Leaflet `CRS.Simple` bounds（[[0,0],[h,w]]）；格式固定，100 字元有餘 |
| Status | varchar(20) | `Pending`/`Processing`/`Ready`/`Failed`；最長值 10 字元，預留至 20 |
| ErrorMessage | text? | 轉檔失敗訊息；錯誤訊息長度不可預測，不限制 |
| UpdatedAt | timestamptz | |

> 詳細 Tile 流程於模組 04；本模組僅建表。

### Seat（`seats`）
| 欄位 | 型別 | 說明 |
|------|------|------|
| Id | Guid PK | |
| FloorId | Guid FK→floors | 所屬樓層 |
| SeatNumber | varchar(20) | 座位編號（同樓層內唯一）；如「A-101」，上限 20 字元 |
| Location | geometry(Point) | PostGIS Point（CRS.Simple 像素座標，SRID 0） |
| CreatedAt | timestamptz | |

### SeatAssignment（`seat_assignments`）
| 欄位 | 型別 | 說明 |
|------|------|------|
| Id | Guid PK | |
| SeatId | Guid FK→seats unique | 一座位最多一員工 |
| EmployeeId | Guid FK→employees unique | 一員工最多一座位（固定座位） |
| AssignedAt | timestamptz | |

### AttendanceState（`attendance_states`）— 目前在場狀態
| 欄位 | 型別 | 說明 |
|------|------|------|
| Id | Guid PK | |
| EmployeeId | Guid FK→employees unique | |
| IsPresent | bool | true=在場（綠）/false=不在場（灰） |
| LastCheckInAt | timestamptz? | |
| LastCheckOutAt | timestamptz? | |
| UpdatedAt | timestamptz | |

---

## 詳細執行步驟

### 步驟 1：加入 NuGet 套件

於 `BackEnd/BackEnd.csproj` 加入：
- `Microsoft.AspNetCore.Identity.EntityFrameworkCore`（Identity 核心 + EF 整合）
- `Microsoft.EntityFrameworkCore.Design`
- `Npgsql.EntityFrameworkCore.PostgreSQL`
- `Npgsql.EntityFrameworkCore.PostgreSQL.NetTopologySuite`（空間支援）

並安裝工具：
```pwsh
dotnet tool install --global dotnet-ef
dotnet restore
```

### 步驟 2：建立實體類別

於 `BackEnd/Entities/` 各建一檔：`ApplicationUser.cs`、`Employee.cs`、`Floor.cs`、
`FloorMap.cs`、`Seat.cs`、`SeatAssignment.cs`、`AttendanceState.cs`。

- `ApplicationUser` 繼承 `IdentityUser`（命名空間 `Microsoft.AspNetCore.Identity`），新增 `Guid? EmployeeId` 與 `DateTime CreatedAt` 兩個自訂欄位；**不**加 `Role`（角色由 Identity 角色表管理）。
- 座位的 `Location` 型別為 `NetTopologySuite.Geometries.Point`。
- 其他實體的狀態以 C# `enum` + EF `HasConversion<string>()` 存為文字（如 `FloorMap` 的 `Status`）。
- 導覽屬性雙向設定（Floor.Seats、Seat.Floor 等）。

### 步驟 3：建立 DbContext

新增 `BackEnd/Data/AppDbContext.cs`：
- 繼承 `IdentityDbContext<ApplicationUser, IdentityRole, string>`（命名空間 `Microsoft.AspNetCore.Identity.EntityFrameworkCore`），而非直接繼承 `DbContext`。
- `DbSet<>` 涵蓋業務實體（`Employee`、`Floor`、`FloorMap`、`Seat`、`SeatAssignment`、`AttendanceState`）；`ApplicationUser` 的 `DbSet` 由 `IdentityDbContext` 基底類別提供（`Users`）。
- `OnModelCreating`：
  - **必須先呼叫** `base.OnModelCreating(modelBuilder);`（讓 Identity 設定優先套用）。
  - 各表唯一索引：`Seat(FloorId, SeatNumber)`、`SeatAssignment.SeatId`、`SeatAssignment.EmployeeId`、`AttendanceState.EmployeeId`、`FloorMap.FloorId`。（`ApplicationUser.UserName` 的唯一索引由 Identity 自動建立，不需手寫。）
  - 刪除行為：`Floor` → `Seat` 設 `Cascade`（刪樓層連帶刪座位，呼應規格警告）；`Seat` → `SeatAssignment` 設 `Cascade`；`Employee` → `SeatAssignment`/`AttendanceState` 設 `Cascade`。
  - 資料庫命名採 **PascalCase**，與後端 C# 實體命名保持一致（EF Core 預設對應行為，不額外套用 snake_case 慣例）；Identity 標準表（`AspNetUsers` 等）維持 Identity 框架預設命名。
  - `Seat.Location` 指定 `HasColumnType("geometry (Point)")`。
- 拆分設定建議：以 `IEntityTypeConfiguration<T>` 分檔放 `BackEnd/Data/Configurations/`。

### 步驟 4：註冊 Npgsql + NetTopologySuite

於 `Program.cs`：
```
builder.Services.AddDbContext<AppDbContext>(opt =>
    opt.UseNpgsql(
        builder.Configuration.GetConnectionString("Default"),
        o => o.UseNetTopologySuite()));
```
- 連線字串放 `appsettings.json` 的 `ConnectionStrings:Default`；正式以環境變數覆寫（Neon）。
- Identity 服務（`AddIdentityApiEndpoints<ApplicationUser>` 等）於模組 02 在此 DbContext 之後註冊。

### 步驟 5：啟用 PostGIS 擴充

- 在首個 Migration 的 `Up()` 內加入 `migrationBuilder.EnsurePostgresExtension("postgis");`（NetTopologySuite 寫入 geometry 前必須）。
- 或於 `OnModelCreating` 使用 `modelBuilder.HasPostgresExtension("postgis");` 讓 migration 自動產生。

### 步驟 6：連線設定（本機 / Neon）

- **本機開發**：可用 Docker 跑 `postgis/postgis` image（附 PostGIS），或連 Neon dev 分支。
  - 範例本機連線：`Host=localhost;Port=5432;Database=seatmap;Username=postgres;Password=post0414gres`。
- **Neon**：連線字串需含 `SSL Mode=Require;Trust Server Certificate=true`（細節見模組 12）。
- 將敏感連線字串放 user-secrets 或環境變數，勿提交。
  ```pwsh
  dotnet user-secrets init
  dotnet user-secrets set "ConnectionStrings:Default" "<neon-conn-string>"
  ```

### 步驟 7：建立首次 Migration

```pwsh
dotnet ef migrations add InitialCreate
dotnet ef database update
```
- 確認 `Migrations/` 產生且包含 PostGIS 擴充與 geometry 欄位。

### 步驟 8：Seed 初始資料

新增 `BackEnd/Data/DbSeeder.cs`：
- 啟動時確保 Identity 角色 `Admin`、`Employee` 存在（`RoleManager.RoleExistsAsync`）。
- 若無任何 `Admin` 角色使用者，以 `UserManager.CreateAsync` 建立預設管理者帳號，並以 `UserManager.AddToRoleAsync(user, "Admin")` 指派角色（帳密由模組 02 補齊詳細；此處先預留 hook）。
- 於 `Program.cs` 啟動時呼叫（`app.Services.CreateScope()` → 套用 `Database.Migrate()` 並 seed）。

### 步驟 9：DB 健康檢查

- 補上 `AddHealthChecks().AddDbContextCheck<AppDbContext>()`（延續模組 00 的 `/health`）。

---

## 涉及檔案

| 檔案 | 動作 |
|------|------|
| `BackEnd/BackEnd.csproj` | 加套件 |
| `BackEnd/Entities/ApplicationUser.cs` | 新增（繼承 IdentityUser，加 EmployeeId、CreatedAt） |
| `BackEnd/Entities/Employee.cs`、`Floor.cs`、`FloorMap.cs`、`Seat.cs`、`SeatAssignment.cs`、`AttendanceState.cs`（6 檔） | 新增 |
| `BackEnd/Data/AppDbContext.cs` | 新增（繼承 IdentityDbContext） |
| `BackEnd/Data/Configurations/*.cs` | 新增 |
| `BackEnd/Data/DbSeeder.cs` | 新增 |
| `BackEnd/Migrations/*`（自動） | 新增 |
| `BackEnd/Program.cs` | 修改（註冊 DbContext、seed、health） |
| `BackEnd/appsettings.json` | 加 `ConnectionStrings` |

---

## 驗收條件（DoD）

- [ ] `dotnet ef database update` 成功，DB 內建立 6 張業務表 + Identity 系列表（`AspNetUsers`、`AspNetRoles`、`AspNetUserRoles` 等）+ `spatial_ref_sys`（PostGIS）。
- [ ] `seats.location` 欄位型別為 `geometry(Point)`。
- [ ] 唯一索引全數建立（重複 `Username`、同樓層重複 `SeatNumber`、重複指派皆被 DB 擋下）。
- [ ] 刪除一個 Floor 會連帶刪除其 Seats 與相關 SeatAssignment（cascade 驗證）。
- [ ] 可用 EF 寫入一筆含 `Point(x, y)` 的 Seat 並讀回相同座標。
- [ ] `/health` 在 DB 連線正常時回 Healthy、斷線時回 Unhealthy。
- [ ] 連線字串未硬編在版控檔案中。
