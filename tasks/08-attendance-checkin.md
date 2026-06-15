# 08 — 打卡與在場狀態

## 模組目標

實作系統內建上/下班打卡（不整合外部考勤），登入員工可執行打卡，
打卡後更新其 `AttendanceState`，使地圖對應座位的在場狀態色彩即時反映
（綠＝在場 / 灰＝不在場）。

對應規格：2. 打卡功能、狀態流程；1.4 在場狀態色彩。

## 前置相依

- **01 資料庫**（`AttendanceState`）。
- **02 身分驗證**（打卡者為登入員工，JWT 帶 `employeeId`）。
- **06 員工管理**（員工與初始狀態存在）。

---

## 業務規則

- 打卡對象固定為「目前登入的員工本人」，由 `HttpContext.User` 的 `employeeId` claim 取得，不由前端傳入（避免代打）。
  - 此 claim 由模組 02 的 `AdditionalUserClaimsPrincipalFactory` 在登入時注入 bearer ticket，後端無需額外查 DB 即可讀取。
  - 若登入者為無對應員工的管理者帳號（`EmployeeId=null`，`employeeId` claim 不存在），打卡回 400「此帳號無對應員工」。
- 上班打卡：`IsPresent=true`、`LastCheckInAt=now`。
- 下班打卡：`IsPresent=false`、`LastCheckOutAt=now`。
- 冪等性：重複上班打卡僅更新時間戳，狀態維持 true（不報錯）。
- 「即時更新」定義：打卡 API 成功後，下一次地圖查詢（或前端輪詢 / 重新載入座位）即反映新狀態。
  - 在場狀態以前端輪詢（模組 10）達成即時更新。
- 規格：尚未打卡或已打下班卡皆顯示灰色（不在場）。初始 `IsPresent=false` 已滿足。

---

## 詳細執行步驟

### 步驟 1：DTO 定義

於 `BackEnd/Dtos/Attendance/`：
- `AttendanceStatusResponse`：`employeeId`、`isPresent`、`lastCheckInAt`、`lastCheckOutAt`、`updatedAt`。

### 步驟 2：Attendance 服務

新增 `BackEnd/Services/AttendanceService.cs`：
- `CheckInAsync(employeeId)`：取 `AttendanceState`（無則建立）→ 設 `IsPresent=true`、`LastCheckInAt=now`、`UpdatedAt=now`。
- `CheckOutAsync(employeeId)`：設 `IsPresent=false`、`LastCheckOutAt=now`、`UpdatedAt=now`。
- `GetStatusAsync(employeeId)`：回目前狀態。

### 步驟 3：Endpoints

新增 `BackEnd/Endpoints/AttendanceEndpoints.cs`，掛 `/api/attendance`：

- `POST /api/attendance/check-in`（需登入）：上班打卡（對象＝JWT employeeId）。
- `POST /api/attendance/check-out`（需登入）：下班打卡。
- `GET /api/attendance/me`（需登入）：查自己目前狀態（前端顯示「目前已打卡」按鈕狀態）。

> 取 `employeeId`：從 `HttpContext.User` 的 `employeeId` claim 解析（由 `AdditionalUserClaimsPrincipalFactory` 在登入時注入）；claim 不存在則 400。

### 步驟 4：與地圖狀態串接

- 在場狀態經由 `Seat` → `Assignment` → `Employee` → `AttendanceState` 關聯，已在模組 05 `SeatResponse.isPresent` 暴露。
- 打卡後，前端重新拉 `GET /api/floors/{floorId}/seats` 或輪詢即更新座位顏色。
- 提供 `GET /api/floors/{floorId}/seats` 為地圖唯一狀態來源，避免前端多處組裝。

### 步驟 5：時間與時區

- 所有時間以 UTC 寫入；前端顯示時轉本地時區。

---

## 涉及檔案

| 檔案 | 動作 |
|------|------|
| `BackEnd/Dtos/Attendance/*.cs` | 新增 |
| `BackEnd/Services/AttendanceService.cs` | 新增 |
| `BackEnd/Endpoints/AttendanceEndpoints.cs` | 新增 |
| `BackEnd/Program.cs` | 修改（DI、endpoint 掛載） |

---

## API 合約（本模組）

| Method | Path | 授權 | 請求 | 回應 |
|--------|------|------|------|------|
| POST | `/api/attendance/check-in` | 需登入 | — | `AttendanceStatusResponse` |
| POST | `/api/attendance/check-out` | 需登入 | — | `AttendanceStatusResponse` |
| GET | `/api/attendance/me` | 需登入 | — | `AttendanceStatusResponse` |

---

## 驗收條件（DoD）

- [ ] 登入員工執行上班打卡後，`AttendanceState.IsPresent=true` 且 `LastCheckInAt` 更新。
- [ ] 下班打卡後 `IsPresent=false` 且 `LastCheckOutAt` 更新。
- [ ] 打卡對象一律為 bearer ticket 內的 `employeeId` claim（由 `AdditionalUserClaimsPrincipalFactory` 注入），前端無法指定他人。
- [ ] 無對應員工的管理者帳號打卡回 400。
- [ ] 重複同類打卡為冪等（僅更新時間戳，不報錯）。
- [ ] 打卡後 `GET /api/floors/{floorId}/seats` 該員工座位的 `isPresent` 即時反映新值。
- [ ] `GET /api/attendance/me` 正確回傳自身狀態供前端按鈕切換。
- [ ] 未登入呼叫打卡回 401。
