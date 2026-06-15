# 06 — 員工管理

## 模組目標

實作管理者對員工的新增、編輯、刪除與查詢，並提供員工搜尋（依姓名）供地圖定位使用。
員工資料含姓名、部門、頭像。新增員工時同時建立其登入帳號（`User`）與初始在場狀態。

對應規格：使用者角色（管理者可新增/刪除員工）、4. 員工查詢、1.4 座位標記（頭像/姓名/部門）。

## 前置相依

- **01 資料庫**（`Employee`、`ApplicationUser`、`AttendanceState`）。
- **02 身分驗證**（`UserManager<ApplicationUser>` 建立帳號、指派角色；管理者授權）。

---

## 業務規則

- 新增員工 = 建立 `Employee` + 對應 `ApplicationUser`（角色預設 `Employee`）+ 初始 `AttendanceState(IsPresent=false)`。
  - 三者於單一交易內建立（使用 `IDbContextTransaction`）。
- 員工帳號的初始密碼由管理者輸入。
- 刪除員工 → cascade 移除其 `SeatAssignment`、`AttendanceState`，並以 `UserManager.DeleteAsync` 刪除對應 `ApplicationUser`（不可直接用 EF cascade 刪 Identity 管理的 AspNetUsers 列，須透過 UserManager 確保 Identity 清理完整）。
- 頭像：以 `AvatarUrl`（外部圖片 URL）儲存；無頭像時前端顯示姓名首字。
- 搜尋：依姓名模糊比對，回傳員工及其座位位置（floorId + 座標）供前端 `setView` 定位。

---

## 詳細執行步驟

### 步驟 1：DTO 定義

於 `BackEnd/Dtos/Employees/`：
- `EmployeeResponse`：`id`、`fullName`、`department`、`avatarUrl`、`username`、`isPresent`、`seat`（null 或 `{seatId,floorId,seatNumber,x,y}`）。
- `CreateEmployeeRequest`：`fullName`、`department?`、`avatarUrl?`、`username`、`password`。
- `UpdateEmployeeRequest`：`fullName`、`department?`、`avatarUrl?`（不在此改密碼/帳號；MVP 不含改密碼功能）。
- `EmployeeSearchResult`：`employeeId`、`fullName`、`department`、`avatarUrl`、`isPresent`、`seat`（含 `floorId`、`x`、`y`，未指派則 null）。

### 步驟 2：Employee 服務

新增 `BackEnd/Services/EmployeeService.cs`：
- `GetAllAsync()`：列出所有員工（管理者後台用），含在場狀態與座位摘要。
- `GetByIdAsync(id)`。
- `CreateAsync(req)`（交易）：
  1. 檢查 `username` 未被占用（否則 409）。
  2. 建 `Employee`，`SaveChanges` 取得 `int` Id。
  3. 以 `UserManager.CreateAsync(user, password)` 建 `ApplicationUser`（`UserName=req.Username`、`EmployeeId=employee.Id`、`CreatedAt=now`）；失敗則回滾交易並回傳 400/409。
  4. 以 `UserManager.AddToRoleAsync(user, "Employee")` 指派角色。
  5. 建 `AttendanceState(IsPresent=false)`。
- `UpdateAsync(id, req)`：更新姓名/部門/頭像。
- `DeleteAsync(id)`：先以 `UserManager.DeleteAsync` 刪除對應 `ApplicationUser`，再刪除 `Employee`（cascade 清理 `SeatAssignment`、`AttendanceState`）。
- `SearchAsync(name)`：`WHERE FullName ILIKE %name%`，join 指派/座位，回 `EmployeeSearchResult[]`（限制筆數，如 20）。

### 步驟 3：Endpoints

新增 `BackEnd/Endpoints/EmployeeEndpoints.cs`，掛 `/api/employees`：

- `GET /api/employees`（AdminOnly）：員工清單（管理用）。
- `GET /api/employees/search?name=`（需登入）：員工搜尋（一般員工也可查同事位置 → 規格「員工可查詢其他員工位置」）。
- `GET /api/employees/{id}`（需登入）：單一員工資訊（座位浮窗可用）。
- `POST /api/employees`（AdminOnly）：新增員工（含建帳號）。
- `PUT /api/employees/{id}`（AdminOnly）：更新員工。
- `DELETE /api/employees/{id}`（AdminOnly）：刪除員工。

### 步驟 4：搜尋定位資料

- `SearchAsync` 回傳需包含足以定位的資訊：`seat.floorId`、`seat.x`、`seat.y`。
- 前端流程（模組 10）：選搜尋結果 → 切換到對應樓層 → `map.setView([y, x])` 並高亮座位。
- 未指派座位的員工搜尋結果 `seat=null`，前端提示「尚未指派座位」。

### 步驟 5：驗證與錯誤

- `fullName`、`username`、`password` 必填 → 400。
- `username` 重複 → 409。
- 找不到員工 → 404。
- 密碼強度：最小長度 ≥ 8，且須包含英文字母與數字（英數組合）；不符合回 400。

### 步驟 6：與其他模組串接

- `AttendanceState` 由本模組建立初值；狀態變更邏輯在模組 08。
- `seat` 摘要來自模組 07 的指派關係；本模組僅讀取。

---

## 涉及檔案

| 檔案 | 動作 |
|------|------|
| `BackEnd/Dtos/Employees/*.cs` | 新增 |
| `BackEnd/Services/EmployeeService.cs` | 新增 |
| `BackEnd/Endpoints/EmployeeEndpoints.cs` | 新增 |
| `BackEnd/Program.cs` | 修改（DI、endpoint 掛載） |

---

## API 合約（本模組）

| Method | Path | 授權 | 請求 | 回應 |
|--------|------|------|------|------|
| GET | `/api/employees` | Admin | — | `EmployeeResponse[]` |
| GET | `/api/employees/search?name=` | 需登入 | — | `EmployeeSearchResult[]` |
| GET | `/api/employees/{id}` | 需登入 | — | `EmployeeResponse` |
| POST | `/api/employees` | Admin | `{fullName,department?,avatarUrl?,username,password}` | `EmployeeResponse`（201） |
| PUT | `/api/employees/{id}` | Admin | `{fullName,department?,avatarUrl?}` | `EmployeeResponse` |
| DELETE | `/api/employees/{id}` | Admin | — | 204 |

---

## 驗收條件（DoD）

- [ ] 新增員工會同時建立可登入的 `ApplicationUser`（以該帳密可成功 `POST /api/auth/login`）。
- [ ] 新增員工會建立初始 `AttendanceState(IsPresent=false)`。
- [ ] `username` 重複時回 409，且不建立任何半套資料（交易回滾）。
- [ ] 可更新員工姓名/部門/頭像。
- [ ] 刪除員工會連帶移除其 `ApplicationUser`（透過 `UserManager.DeleteAsync`）、指派與在場狀態，座位回到未指派。
- [ ] `GET /api/employees/search?name=` 可依姓名模糊搜尋，回傳含座位定位資訊。
- [ ] 一般員工可使用 search 與 `GET /api/employees/{id}`，但無法 `POST/PUT/DELETE`（403）。
- [ ] 無頭像時 `avatarUrl` 為 null，前端據此顯示姓名首字。
