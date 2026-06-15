# 12 — 部署與 CI/CD

## 模組目標

將三端部署到目標平台並串接：後端 ASP.NET Core（含 GDAL）以 Docker 部署到 **Fly.io**、
Tile 存於 Fly.io **persistent volume**；前端 Vue 建置後部署到 **GitHub Pages**；
資料庫使用 **Neon** PostgreSQL（啟用 PostGIS）。完成環境變數、CORS、Migration 與 CI/CD 流程。

對應 `others.md` 部署表與 SA 文件 Tile 架構。

## 前置相依

- 全部後端與前端模組（00–11）功能完成。
- 帳號：Fly.io、Neon、GitHub（repo 已存在）。

---

## 部署拓撲

```
[GitHub Pages]  前端 SPA (Vue) ──HTTPS──► [Fly.io] 後端 API + GDAL + Tile 靜態服務
                                              │
                                              ├─ persistent volume: /data (tiles + dxf)
                                              │
                                              └──SSL──► [Neon] PostgreSQL + PostGIS
```

---

## A. 資料庫（Neon）

### 步驟 A1：建立專案與資料庫
- 於 Neon 建立專案，取得連線字串（含 host、db、user、password、`sslmode=require`）。

### 步驟 A2：啟用 PostGIS
- 由 EF Migration 的 `migrationBuilder.EnsurePostgresExtension("postgis")` 自動處理（於首次 Migration 的 `Up()` 內或 `OnModelCreating` 使用 `modelBuilder.HasPostgresExtension("postgis")`）。

### 步驟 A3：連線字串格式（Npgsql）
```
Host=<ep>.neon.tech;Database=<db>;Username=<user>;Password=<pwd>;SSL Mode=Require;Trust Server Certificate=true
```
- 作為後端環境變數 `ConnectionStrings__Default`。

### 步驟 A4：套用 Migration
- 於 CI 部署流程中以 `dotnet ef database update` 對 Neon 執行（需在 CI 安裝 ef tool 與連線字串 secret）。

---

## B. 後端（Fly.io + Docker + GDAL）

### 步驟 B1：Dockerfile（多階段 + GDAL）
新增 `BackEnd/Dockerfile`：
- **build stage**：`mcr.microsoft.com/dotnet/sdk:10.0` → `dotnet restore` → `dotnet publish -c Release -o /app`。
- **runtime stage**：`mcr.microsoft.com/dotnet/aspnet:10.0`：
  - `apt-get update && apt-get install -y gdal-bin python3-gdal`（提供 `ogr2ogr`/`gdal_rasterize`/`gdal2tiles`）。
  - 複製 publish 輸出。
  - `ENV ASPNETCORE_URLS=http://+:8080`。
  - `EXPOSE 8080`。
  - `ENTRYPOINT ["dotnet","BackEnd.dll"]`。
- 確認容器內 `ogr2ogr --version`、`gdal2tiles --help` 可執行（`GdalOptions` 路徑用 PATH 名稱即可）。
- `.dockerignore`：排除 `bin/`、`obj/`、`_data/`。

### 步驟 B2：fly.toml
新增 `BackEnd/fly.toml`：
- `app = "<seatmap-api>"`、`primary_region`。
- `[http_service]`：`internal_port = 8080`、`force_https = true`、`auto_stop_machines`/`min_machines_running`（MVP 可設 1，避免 volume 多機與 migration 競爭）。
- `[[http_service.checks]]` 或 `[checks]`：HTTP `GET /health`。
- `[mounts]`：`source = "seatmap_data"`、`destination = "/data"`（掛 volume 給 tiles/dxf）。
- `[env]`：`ASPNETCORE_ENVIRONMENT = "Production"`、`Tiles__RootPath = "/data/tiles"`、`Gdal__Enabled = "true"`。

### 步驟 B3：建立 volume
```pwsh
fly volumes create seatmap_data --region <region> --size 3
```
- 容量依底圖 Tile 量估算（多樓層高解析度需放大）。

### 步驟 B4：設定 secrets（環境變數）
```pwsh
fly secrets set ConnectionStrings__Default="<neon-conn>"
fly secrets set SEED_ADMIN_PASSWORD="<initial-admin-pwd>"
fly secrets set Cors__AllowedOrigins__0="https://<user>.github.io"
```
- `Cors__AllowedOrigins__0` 對應前端 GitHub Pages 來源（含子路徑站台的 origin 僅到網域）。
- Identity Bearer Token 採用 ASP.NET Data Protection 加密，無需手動管理 JWT 簽章金鑰（不需 `Jwt__SigningKey`）。

### 步驟 B5：部署
```pwsh
fly deploy
```
- 驗證 `/health` 回 200、`/scalar` 在 Production 關閉、`/api/auth/login` 可用。

### 步驟 B6：CORS / HTTPS 對接
- 確認後端 `Cors:AllowedOrigins` 含前端正式網域。
- 後端在 Production 不強制 `UseHttpsRedirection`（Fly.io 邊緣已處理 TLS，`force_https=true`）。

---

## C. 前端（GitHub Pages）

### 步驟 C1：Vite base
- `vite.config.ts` 的 `base` 在 production 設 `/<repo-name>/`（與 Pages 子路徑一致）。
- `.env.production` 的 `VITE_API_BASE_URL = https://<seatmap-api>.fly.dev/api`。
- `API_ORIGIN`（tiles 用）= `https://<seatmap-api>.fly.dev`。

### 步驟 C2：SPA fallback
- GitHub Pages 無伺服器路由 → 直接深連結會 404。
- 解法：build 後將 `dist/index.html` 複製為 `dist/404.html`（於 workflow 步驟處理），讓任意路徑回 SPA。

### 步驟 C3：GitHub Actions workflow
新增 `.github/workflows/frontend-pages.yml`：
- 觸發：push 到 `main` 且影響 `FrontEnd/**`。
- 步驟：
  1. checkout。
  2. setup-node（版本符合 `package.json` engines）。
  3. `npm ci`（working-directory: FrontEnd）。
  4. `npm run build`。
  5. 複製 `dist/index.html` → `dist/404.html`。
  6. `actions/upload-pages-artifact`（path: `FrontEnd/dist`）。
  7. `actions/deploy-pages`。
- 設定 repo Pages 來源為 GitHub Actions。

### 步驟 C4：後端 CI/CD（Fly.io）workflow
新增 `.github/workflows/backend-fly.yml`：
- 觸發：push 到 `main` 且影響 `BackEnd/**`。
- 步驟：
  1. checkout。
  2. `superfly/flyctl-actions/setup-flyctl`。
  3. 以 ef tool 對 Neon 套用 migration（`dotnet ef database update`）。
  4. `flyctl deploy --remote-only`。
- secrets：`FLY_API_TOKEN`（repo secret）。

---

## D. 環境變數總表

| 變數 | 用途 | 設定處 |
|------|------|--------|
| `ConnectionStrings__Default` | Neon 連線 | Fly secret |
| `SEED_ADMIN_PASSWORD` | 初始管理者密碼 | Fly secret |
| `Cors__AllowedOrigins__0` | 允許前端來源 | Fly secret/env |
| `Tiles__RootPath` | Tile 根目錄 | fly.toml env（`/data/tiles`） |
| `Gdal__Enabled` | 啟用 GDAL 轉檔 | fly.toml env（`true`） |
| `ASPNETCORE_URLS` | 監聽埠 | Dockerfile（`http://+:8080`） |
| `VITE_API_BASE_URL` | 前端打 API | `.env.production` |
| `FLY_API_TOKEN` | 部署授權 | GitHub secret |

---

## 涉及檔案

| 檔案 | 動作 |
|------|------|
| `BackEnd/Dockerfile` | 新增 |
| `BackEnd/.dockerignore` | 新增 |
| `BackEnd/fly.toml` | 新增 |
| `FrontEnd/vite.config.ts` | 修改（base） |
| `FrontEnd/.env.production` | 確認/修改 |
| `.github/workflows/frontend-pages.yml` | 新增 |
| `.github/workflows/backend-fly.yml` | 新增 |

---

## 驗收條件（DoD）

- [ ] Neon DB 啟用 PostGIS，Migration 成功套用，7 張表存在。
- [ ] 後端 Docker image 內含 GDAL，`ogr2ogr`/`gdal2tiles` 可執行。
- [ ] `fly deploy` 成功，`/health` 回 200。
- [ ] Tile volume 掛載於 `/data`，上傳 DXF 後 Tile 寫入並可由 `/tiles/...` 取得。
- [ ] 重新部署後 volume 內 Tile 仍存在（持久化驗證）。
- [ ] 前端 GitHub Actions build 成功並部署到 Pages，站台可開啟。
- [ ] 深連結（如 `/admin`）在 Pages 不會 404（404.html fallback 生效）。
- [ ] 前端可成功呼叫 Fly.io 後端 API（CORS 通過、Identity Bearer Token 流程正常）。
- [ ] 前端正式站可載入後端 Tile 底圖並顯示座位地圖。
- [ ] 所有密鑰以 secret 管理，未進版控。
- [ ] push 到 main 觸發對應前/後端自動部署。
