# 09 — 前端基礎建設

## 模組目標

清理 Vue 範例 scaffold，建立座位表前端的基礎結構：API client（含 JWT 攔截）、
Pinia 狀態（auth）、路由與守衛、環境變數、Leaflet 依賴與樣式、共用版面，
並設定 GitHub Pages 部署所需的 `base` 路徑。為地圖瀏覽器（10）與管理後台（11）鋪底。

對應規格：登入方式（帳密）、使用者角色權限分流。

## 前置相依

- **02 身分驗證**（後端登入 API、JWT）。
- 現況：`FrontEnd/` 為 Vue 3 + Vite + Pinia + Vue Router 範例（含 HelloWorld、TheWelcome、counter store、Home/About view）。

## 現況檢查（需清理的 scaffold）

| 既有檔案 | 處置 |
|----------|------|
| `src/components/HelloWorld.vue`、`TheWelcome.vue`、`WelcomeItem.vue`、`components/icons/*` | 刪除 |
| `src/views/AboutView.vue` | 刪除 |
| `src/views/HomeView.vue` | 改為地圖頁（模組 10）或重導 |
| `src/stores/counter.ts` | 刪除 |
| `src/App.vue` | 改寫為應用版面（移除 logo/HelloWorld/nav 範例） |
| `src/assets/main.css`、`base.css`、`logo.svg` | 保留 CSS（精簡）、刪 logo |
| `src/router/index.ts` | 重寫路由表 |

---

## 詳細執行步驟

### 步驟 1：安裝相依套件

於 `FrontEnd/`：
```pwsh
npm install leaflet axios naive-ui @vicons/material vue-draggable-plus
npm install -D @types/leaflet
```
- `leaflet`：地圖渲染（模組 10）。
- `axios`：HTTP client（JWT 攔截）。
- `naive-ui`：UI 元件庫（表單、Dialog、Table、Upload、Menu 等）。
- `@vicons/material`：xicons Material Design 圖示集，與 Naive UI 搭配使用。
- `vue-draggable-plus`：拖曳排序套件，用於樓層清單排序（模組 11）。
- `@types/leaflet`：型別。

### 步驟 2：清理 scaffold

刪除上表標示「刪除」之檔案；移除 `App.vue`、`router` 對它們的 import。

### 步驟 3：環境變數

建立 `FrontEnd/.env.development` 與 `.env.production`：
- `VITE_API_BASE_URL`：
  - dev：`http://localhost:7176/api`（對應後端 launchSettings http port）。
  - prod：`https://<fly-app>.fly.dev/api`（模組 12 填入實際網域）。
- 於 `env.d.ts` 補上 `ImportMetaEnv` 型別宣告。

### 步驟 4：Vite base 路徑（GitHub Pages）

- GitHub Pages 專案站台路徑為 `/<repo-name>/`。
- 於 `vite.config.ts` 設定 `base`：
  - 以環境變數控制：production 設 `/<repo-name>/`，dev 維持 `/`。
- Vue Router 的 `createWebHistory(import.meta.env.BASE_URL)` 已使用 `BASE_URL`，會自動對齊 `base`。
- 需處理 SPA 在 Pages 的 fallback（404 → index.html）——以 `404.html` 複製方案（模組 12 詳述）。

### 步驟 5：API client

新增 `src/api/http.ts`：
- 建立 `axios` instance，`baseURL = import.meta.env.VITE_API_BASE_URL`。
- 請求攔截器：若 auth store 有 accessToken，注入 `Authorization: Bearer <accessToken>`。
- 回應攔截器：收到 401 時，若 auth store 有 refreshToken，先嘗試 `POST /api/auth/refresh` 換新 token 後重試原請求；refresh 失敗則清除 auth 並導向 `/login`；其他錯誤統一拋出可讀訊息（讀取 ProblemDetails 的 `title/detail`）。

新增各資源 API 模組（薄封裝）：
- `src/api/auth.ts`：`login`、`me`、`refresh`、`logout`。
- `src/api/floors.ts`：`list`、`create`、`rename`、`reorder`、`remove`、`getMap`、`uploadMap`、`deleteMap`。
- `src/api/seats.ts`：`listByFloor`、`get`、`create`、`update`、`remove`。
- `src/api/employees.ts`：`list`、`search`、`get`、`create`、`update`、`remove`。
- `src/api/assignments.ts`：`assign`、`unassignBySeat`。
- `src/api/attendance.ts`：`checkIn`、`checkOut`、`me`。

> 對應後端各模組 API 合約，型別以 `src/types/` 定義（與後端 DTO 對齊）。

### 步驟 6：型別定義

新增 `src/types/index.ts`：定義 `Floor`、`FloorMap`、`Seat`、`Employee`、`EmployeeSearchResult`、`Assignment`、`AttendanceStatus`、`AuthUser` 等介面，欄位與後端 DTO 一致（camelCase）。

### 步驟 7：Auth store（Pinia）

新增 `src/stores/auth.ts`：
- state：`accessToken`、`refreshToken`、`user`（`{userId,username,role,employeeId}`）。
- 持久化：`accessToken` 與 `refreshToken` 存 `localStorage`，啟動時還原。
- actions：
  - `login(username, password)`：呼叫 `POST /api/auth/login` → 從回應取 `{accessToken,refreshToken,expiresIn}` → 存 token/user（user 資訊再呼叫 `GET /api/auth/me` 取得）。
  - `refresh()`：以 `refreshToken` 呼叫 `POST /api/auth/refresh` 換新 `accessToken`；失敗則 `logout()`。
  - `fetchMe()`：以現有 accessToken 呼叫 `GET /api/auth/me` 還原使用者（啟動或重整時）。
  - `logout()`：清除 accessToken/refreshToken/user、導 `/login`。
- getters：`isAuthenticated`、`isAdmin`（`role === 'Admin'`）。

### 步驟 8：路由與守衛

重寫 `src/router/index.ts`：
- 路由：
  - `/login` → `LoginView`（匿名）。
  - `/`（或 `/map`）→ `MapView`（需登入；模組 10）。
  - `/admin` → `AdminView`（需 Admin；模組 11），可含子路由 `floors`/`employees`。
  - `:pathMatch(.*)*` → 重導 `/`。
- 全域前置守衛 `beforeEach`：
  - 未登入存取需授權頁 → 導 `/login`。
  - 已登入存取 `/login` → 導 `/`。
  - 非 Admin 存取 `/admin` → 導 `/`。
  - meta：`{ requiresAuth: true, requiresAdmin?: true }`。

### 步驟 9：App 版面

改寫 `src/App.vue`：
- 以 `NLayout` + `NLayoutHeader` 建立整體框架，`NLayoutContent` 包裹 `<RouterView />`。
- 頂部列：應用名稱（`NText`）、目前使用者名稱、打卡按鈕（`NButton`，員工，模組 08/10 串接）、登出按鈕（`NButton`）。
- 管理者顯示「管理後台」入口連結（`NButton` 以 `text` prop 或 `tag="RouterLink"` 呈現）。
- 根層需包裹 `NMessageProvider` 與 `NDialogProvider`，以支援全域 `useMessage()` / `useDialog()`。
- 引入 Leaflet CSS：`import 'leaflet/dist/leaflet.css'`（於 `main.ts` 或 App）。

### 步驟 10：LoginView

新增 `src/views/LoginView.vue`：
- 以 `NForm` + `NFormItem` 建立帳號／密碼表單，欄位使用 `NInput`（密碼欄位 `type="password"`）。
- 送出按鈕使用 `NButton`（`type="primary"`，`attr-type="submit"`）。
- 登入失敗（401）以 `NAlert`（`type="error"`）顯示錯誤訊息。
- 成功後 `authStore.login` 導向 `/`。

### 步驟 11：啟動時還原登入

- `main.ts`：建立 app 後、mount 前，若 localStorage 有 accessToken，呼叫 `authStore.fetchMe()`（失敗則嘗試 `authStore.refresh()`；仍失敗則清除）。

### 步驟 12：Lint / 格式

- 沿用既有 `eslint`/`oxlint`/`prettier` 設定。
- 確保新檔通過 `npm run lint` 與 `npm run type-check`。

---

## 涉及檔案

| 檔案 | 動作 |
|------|------|
| `FrontEnd/package.json` | 加 leaflet/axios/naive-ui/@vicons/material/vue-draggable-plus |
| `FrontEnd/.env.development`、`.env.production` | 新增 |
| `FrontEnd/env.d.ts` | 修改（env 型別） |
| `FrontEnd/vite.config.ts` | 修改（base） |
| `FrontEnd/src/api/*.ts` | 新增 |
| `FrontEnd/src/types/index.ts` | 新增 |
| `FrontEnd/src/stores/auth.ts` | 新增 |
| `FrontEnd/src/stores/counter.ts` | 刪除 |
| `FrontEnd/src/router/index.ts` | 重寫 |
| `FrontEnd/src/App.vue` | 改寫 |
| `FrontEnd/src/views/LoginView.vue` | 新增 |
| `FrontEnd/src/views/AboutView.vue` | 刪除 |
| `FrontEnd/src/views/HomeView.vue` | 改寫/移除 |
| `FrontEnd/src/components/HelloWorld.vue` 等範例 | 刪除 |
| `FrontEnd/src/main.ts` | 修改（Leaflet CSS、fetchMe、`app.use(naive)`） |
| `FrontEnd/index.html` | 改 title |

---

## 驗收條件（DoD）

- [ ] 範例元件/view/store 全部移除，`npm run build` 無殘留 import 錯誤。
- [ ] `npm run type-check` 與 `npm run lint` 通過。
- [ ] 未登入存取 `/` 會被導向 `/login`。
- [ ] 以正確帳密登入後導向地圖頁，`accessToken` 與 `refreshToken` 存於 localStorage。
- [ ] 重整頁面後仍維持登入（`fetchMe` 還原；accessToken 過期時 `refresh` 補位）。
- [ ] 非 Admin 存取 `/admin` 被導回首頁。
- [ ] axios 自動帶上 `Authorization` 標頭；收到 401 先嘗試 refresh，失敗才登出並導向登入。
- [ ] `VITE_API_BASE_URL` 可切換 dev/prod 後端位址。
- [ ] production build 的 `base` 對應 GitHub Pages 子路徑，資源路徑正確。
