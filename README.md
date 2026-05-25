# DataViz — Система визуализации данных

Полнофункциональное приложение к курсовой работе **«Проектирование системы визуализации данных»**.

![Дашборд](docs/screenshots/dashboard.png)

- **Backend:** ASP.NET Core 8 Web API (C#), Entity Framework Core 8 (Npgsql), PostgreSQL, JWT (HS256), Swagger.
- **Frontend:** Next.js 14 (App Router, TypeScript), Tailwind CSS, Plotly.js, SWR.
- **БД:** PostgreSQL 16.
- **Запуск:** Docker Compose (одна команда поднимает БД, API и фронт).

## Структура репозитория

```
dataviz/
├─ backend/
│  ├─ DataViz.sln
│  └─ DataViz.Api/                 # ASP.NET Core Web API
│     ├─ Controllers/              # Auth, Categories, Products, Orders, Dashboard
│     ├─ Models/                   # User, Category, Product, Order, OrderItem, DashboardView
│     ├─ Dtos/                     # DTO для API (с DataAnnotations)
│     ├─ Data/                     # ApplicationDbContext
│     ├─ Auth/                     # JwtOptions, JwtTokenService (IOptionsMonitor)
│     ├─ Infrastructure/           # GlobalExceptionHandler, DatabaseInitializer,
│     │                            # AuthorizationPolicies
│     ├─ Services/                 # DataSeeder (категории, товары, ~1500 заказов)
│     ├─ Migrations/               # EF Core миграции
│     └─ Dockerfile
├─ frontend/                       # Next.js 14 + TypeScript + Tailwind
│  ├─ src/app/                     # /login, /register, /dashboard, /products,
│  │                               # /orders, /admin, /unauthorized
│  │  ├─ error.tsx, not-found.tsx, loading.tsx
│  ├─ src/components/              # NavBar, AuthGuard, KpiCard, Plot, SwrProvider
│  ├─ src/lib/                     # API клиент (ProblemDetails), JWT хранилище, типы
│  └─ Dockerfile                   # output: standalone, non-root user
├─ docker-compose.yml              # с healthchecks для db / api / web
├─ .env.example
└─ README.md
```

## Быстрый запуск (Docker Compose)

Требуется Docker и Docker Compose v2.

```bash
git clone https://github.com/mgdov/dataviz.git
cd dataviz
cp .env.example .env       # отредактируйте JWT_KEY (≥ 32 байт) и пароли
docker compose up --build
```

После сборки доступны:

- Frontend: <http://localhost:3000>
- Backend API: <http://localhost:5080>
- Swagger UI: <http://localhost:5080/swagger>
- Liveness: <http://localhost:5080/healthz>
- Readiness (включая БД): <http://localhost:5080/readyz>
- PostgreSQL: `localhost:5433` (внутри сети — `db:5432`)

Бэк автоматически:

1. Дожидается готовности PostgreSQL (с экспоненциальным retry).
2. Применяет EF Core миграции (`InitialCreate`).
3. Создаёт администратора (`admin@example.com` / `ADMIN_PASSWORD` либо сгенерированный 20-символьный пароль, выводимый в лог при первом запуске).
4. Если `SEED_DATA=true` — заполняет БД: 5 категорий, 20 товаров, ~1500 заказов за год и демо-пользователя.

**Демо-аккаунт:** `demo@example.com` / `demo12345` (предзаполнен на странице логина).

Останов и очистка:

```bash
docker compose down            # остановить сервисы
docker compose down -v         # удалить и том с данными PostgreSQL
```

## Запуск локально без Docker

### Backend

Требуется .NET SDK 8.0+ и работающий PostgreSQL.

```bash
cd backend/DataViz.Api

export ConnectionStrings__DefaultConnection="Host=localhost;Port=5432;Database=dataviz;Username=dataviz;Password=dataviz"
export Jwt__Key="dev-secret-please-override-min-32-bytes"   # обязательно ≥ 32 байт

dotnet tool install --global dotnet-ef --version 8.0.10   # один раз
dotnet ef database update                                  # применит миграции
dotnet run --launch-profile http                           # http://localhost:5080
```

В `appsettings.Development.json` уже зашит безопасный для разработки `Jwt:Key` — переменная окружения нужна только если вы не пользуетесь Development профилем.

### Frontend

Требуется Node.js 20+.

```bash
cd frontend
npm install
NEXT_PUBLIC_API_BASE=http://localhost:5080 npm run dev   # http://localhost:3000
```

## API: основные эндпоинты

| Метод | URL | Описание |
|------|-----|----------|
| POST | `/api/auth/register` | Регистрация (name, email, password) → JWT. **Rate limit:** 10 req/min/IP. |
| POST | `/api/auth/login`    | Логин → JWT. **Rate limit:** 10 req/min/IP. |
| GET  | `/api/auth/me`       | Профиль текущего пользователя (Bearer). |
| GET  | `/api/categories`    | Список категорий. |
| POST | `/api/categories`    | Создать категорию. **Admin only.** |
| DELETE | `/api/categories/{id}` | Удалить категорию. **Admin only.** |
| GET  | `/api/products`      | Список товаров (фильтр `?categoryId=`). |
| POST | `/api/products`      | Создать товар. **Admin only.** |
| PUT  | `/api/products/{id}` | Обновить товар. **Admin only.** |
| DELETE | `/api/products/{id}` | Удалить товар. **Admin only.** |
| GET  | `/api/orders`        | Заказы (свои или все для admin) (Bearer). |
| POST | `/api/orders`        | Создать заказ — транзакционно, с проверкой остатков (Bearer). |
| DELETE | `/api/orders/{id}` | Удалить свой заказ, остатки восстанавливаются транзакционно (Bearer). |
| GET  | `/api/dashboard/sales` | Все агрегаты дашборда: KPI, ряды, доли категорий, heatmap, топ-10. Фильтры `from`, `to`, `regions`, `categoryId` (Bearer). Агрегации выполняются на PostgreSQL. |
| GET  | `/healthz`           | Liveness probe (без проверки БД). |
| GET  | `/readyz`            | Readiness probe — включает проверку соединения с PostgreSQL. |

Все ошибки возвращаются в формате RFC 7807 ProblemDetails. Полная спецификация (с XML-документацией методов) — в Swagger UI.

## Безопасность и устойчивость

- **JWT (HS256).** `Jwt:Key` валидируется на старте: запуск приложения падает, если ключ короче 32 байт или совпадает с известными dev-плейсхолдерами. Issuer/Audience/Lifetime/Signature — все проверяются строго.
- **Rate limiting.** Эндпоинты `/auth/register` и `/auth/login` защищены fixed-window лимитом (10 запросов в минуту на IP, политика `auth-strict`). Превышение → 429.
- **Authorization Policies.** Доступ к мутациям каталога ограничен policy `AdminOnly` (на бэке) и `requireRole="admin"` на `AuthGuard` (на фронте). Магические строки `"admin"` исключены.
- **Transactional consistency.** Создание и удаление заказов выполняется внутри транзакции с `ExecutionStrategy`. Остатки на складе атомарно списываются/восстанавливаются.
- **Connection pooling.** Используется `AddDbContextPool` с retry-стратегией Npgsql (5 ретраев, max 10s).
- **Global exception handler.** Все необработанные исключения превращаются в ProblemDetails 500 c `traceId` для корреляции в логах.
- **Health checks.** `/healthz` — liveness, `/readyz` — readiness с проверкой PostgreSQL. Используются в docker-compose `healthcheck`.
- **Frontend.** SWR provider с разумными дефолтами (no refocus revalidate, 2 retry, fail-fast на 4xx), error-boundary (`app/error.tsx`), 404 страница, /unauthorized для не-админов, валидация интервала дат на дашборде (от ≤ до), aria-атрибуты на навигации/формах.
- **Docker.** API и web запускаются от не-root пользователя. Web использует Next.js `output: "standalone"` (минимальный runtime-образ).

## Дашборд (frontend)

- KPI: выручка, число заказов, средний чек, уникальные клиенты.
- График «Динамика выручки» (line chart, Plotly).
- Pie chart «Доля категорий».
- Heatmap «Регион × Категория».
- Bar chart «Топ-10 товаров по выручке» + соответствующая таблица.
- Фильтры: диапазон дат (с валидацией), мультивыбор регионов, выбор категории.

Все агрегации выполняются в PostgreSQL — в приложение возвращаются только готовые DTO, без выгрузки всей таблицы заказов.

Авторизация хранится в `localStorage` (Bearer-токен). Запросы к API делаются через единый `api()`-клиент в `frontend/src/lib/api.ts`, который умеет разбирать ProblemDetails и автоматически очищает токен при 401.

## Переменные окружения

См. `.env.example`. Главные значения:

- `JWT_KEY` — секрет для подписи JWT (≥ 32 байта). **Обязательно поменяйте в проде** (`openssl rand -base64 48`).
- `JWT_ISSUER`, `JWT_AUDIENCE`, `JWT_EXPIRE_MINUTES` — параметры выпуска токена.
- `POSTGRES_*` — креды БД.
- `NEXT_PUBLIC_API_BASE` — где фронт ищет API (по умолчанию `http://localhost:5080`).
- `CORS_ORIGINS` — список разрешённых Origin для бэка (через запятую).
- `SEED_DATA` — `true` для автоматического сидинга демо-данных.
- `ADMIN_EMAIL`, `ADMIN_PASSWORD` — креды администратора по умолчанию. Если пароль не задан, будет сгенерирован случайный 20-символьный и выведен в лог один раз.

## Курсовая работа

Текст курсовой работы — `courseArsen2DataViz.docx` (передаётся отдельно).
