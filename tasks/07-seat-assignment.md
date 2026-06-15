# 07 — 座位指派

## 模組目標

實作管理者將員工指派到指定座位（固定座位制，非熱桌），以及解除指派。
維持「一座位最多一員工、一員工最多一座位」的一對一約束。無需保留變更歷史。

對應規格：3. 座位指派（管理者）、地圖設定流程步驟 4。

## 前置相依

- **01 資料庫**（`SeatAssignment` 與雙唯一索引）。
- **02 身分驗證**（指派為管理者操作）。
- **05 座位管理**（座位存在）。
- **06 員工管理**（員工存在）。

---

## 業務規則

- 指派為固定座位：`SeatAssignment` 在 `SeatId` 與 `EmployeeId` 皆唯一。
- 指派情境處理：
  - 座位已被他人占用 → 409（需先解除或改派）。
  - 員工已被指派到別的座位 → 自動移除員工原指派，改派到新座位，以單一交易完成「刪舊指派 + 建新指派」（一員工只會有一個座位）。
- 解除指派：刪除該 `SeatAssignment`，座位變空、員工變未指派。
- 無歷史：不寫任何 log 表（符合規格 out of scope）。

---

## 詳細執行步驟

### 步驟 1：DTO 定義

於 `BackEnd/Dtos/Assignments/`：
- `AssignSeatRequest`：`seatId`、`employeeId`。
- `AssignmentResponse`：`seatId`、`seatNumber`、`floorId`、`employeeId`、`fullName`、`assignedAt`。

### 步驟 2：Assignment 服務

新增 `BackEnd/Services/AssignmentService.cs`：
- `AssignAsync(seatId, employeeId)`（交易）：
  1. 驗證座位存在（否則 404）、員工存在（否則 404）。
  2. 若座位已被「他人」指派 → 409。
  3. 若員工已有其他座位指派 → 刪除該舊指派（策略 A）。
  4. 若員工已被指派到「同一座位」→ 視為冪等，直接回現況。
  5. 建立新 `SeatAssignment(AssignedAt=now)`。
- `UnassignBySeatAsync(seatId)`：刪除該座位的指派（無則 404 或冪等 204）。
- `UnassignByEmployeeAsync(employeeId)`：刪除該員工的指派（供員工管理連動）。

### 步驟 3：Endpoints

新增 `BackEnd/Endpoints/AssignmentEndpoints.cs`：

- `POST /api/assignments`（AdminOnly）：指派 `{seatId, employeeId}`。
- `DELETE /api/seats/{seatId}/assignment`（AdminOnly）：解除某座位的指派。
- `DELETE /api/employees/{employeeId}/assignment`（AdminOnly）：解除某員工的指派。

> 指派結果會反映在 `GET /api/floors/{floorId}/seats`（模組 05）與員工查詢（模組 06）。

### 步驟 4：併發與一致性

- 利用 DB 唯一索引作為最終防線：即使應用層檢查後仍競爭，`SaveChanges` 會因唯一鍵衝突丟例外 → 轉為 409。
- 策略 A 的「刪舊 + 建新」需在同一交易，避免員工短暫無座位或雙座位。

### 步驟 5：與座位/員工刪除的關係

- 座位刪除（模組 05）、員工刪除（模組 06）已透過 cascade 自動清理指派，本模組不需重複處理。

---

## 涉及檔案

| 檔案 | 動作 |
|------|------|
| `BackEnd/Dtos/Assignments/*.cs` | 新增 |
| `BackEnd/Services/AssignmentService.cs` | 新增 |
| `BackEnd/Endpoints/AssignmentEndpoints.cs` | 新增 |
| `BackEnd/Program.cs` | 修改（DI、endpoint 掛載） |

---

## API 合約（本模組）

| Method | Path | 授權 | 請求 | 回應 |
|--------|------|------|------|------|
| POST | `/api/assignments` | Admin | `{seatId,employeeId}` | `AssignmentResponse`（201/200） |
| DELETE | `/api/seats/{seatId}/assignment` | Admin | — | 204 |
| DELETE | `/api/employees/{employeeId}/assignment` | Admin | — | 204 |

---

## 驗收條件（DoD）

- [ ] 管理者可將員工指派到空座位，`GET /api/floors/{floorId}/seats` 該座位顯示該員工。
- [ ] 將已被占用的座位指派給他人 → 409。
- [ ] 將已有座位的員工改派到新座位 → 舊指派自動消失、新指派建立（同一員工僅一座位）。
- [ ] 重複指派同一員工到同一座位為冪等（不報錯、不重複建立）。
- [ ] 解除座位指派後，座位變空、員工變未指派。
- [ ] DB 唯一索引確實阻擋雙重指派（並發情境回 409 而非 500）。
- [ ] 非管理者呼叫回 403。
- [ ] 無任何歷史紀錄表被寫入。
