# MyPostgresApi

An ASP.NET Core 8 Web API that manages users, saved images and public board posts. The
application now ships with Entity Framework Core + SQLite so that it can run on a
single-file database when hosted on Azure App Service or other PaaS platforms.

## Project layout

```
MyPostgresApi/
├── Controllers/          # API endpoints for users, saved images and board posts
├── Data/                 # EF Core DbContext
├── DTOs/                 # Request/response models
├── Migrations/           # SQLite schema migrations
├── Models/               # Domain models
├── Scripts/              # Utility SQL/DDL scripts for SQLite
├── Tests/                # xUnit integration tests (run against SQLite)
└── Program.cs            # ASP.NET Core host configuration
```

## Prerequisites

* .NET 8 SDK
* SQLite 3 (for inspecting the generated database file)
* Optional: Azure CLI (`az`) for deployment

## Local development

1. Restore packages and build the solution:
   ```bash
   dotnet restore
   dotnet build
   ```
2. Create an `.env` file (copy from `.env.example` if you have one) and set:
   ```bash
   JWT_SECRET_KEY=replace-with-a-strong-secret
   SQLITE_PATH=app.db             # optional – defaults to app.db in the project root
   PORT=8080                      # optional – defaults to 8080 which matches Azure App Service Linux
   ```
3. Apply migrations. This creates `app.db` locally:
   ```bash
   dotnet ef database update
   ```
4. Run the API:
   ```bash
   dotnet run
   ```
5. Navigate to `http://localhost:8080/swagger` for interactive docs.

## Running the test suite

The integration tests now run against a temporary SQLite database:

```bash
dotnet test
```

## Migrating data from PostgreSQL to SQLite

1. **Export from PostgreSQL (on Linode):**
   ```bash
   pg_dump "postgres://user:password@linode-host:5432/database" \
     --schema=maskinen --data-only --column-inserts \
     --table=users --table=saved_images --table=board_posts \
     > postgres-data.sql
   ```
2. **Create a fresh SQLite database locally:**
   ```bash
   dotnet ef database update
   ```
3. **Import the dump into SQLite:**
   *Install `pgloader` (or `sqlite-utils` from Python) on your workstation, then run one
   of the tools to translate PostgreSQL inserts into SQLite format.* For example using
   `pgloader`:
   ```lisp
   load database
     from postgresql://user:password@localhost/database
     into sqlite:///app.db
     with include drop, create tables, create indexes;
   ```
   Alternatively, use the provided `Scripts/create_board_posts_table.sql` to create tables
   manually and import CSV files via the `sqlite3` shell (`.mode csv`, `.import ...`).
4. **Copy `app.db` to your Azure deployment** (see next section). The application runs
   migrations at startup, so any future schema changes will be applied automatically.

## Deploying to Azure App Service (Linux)

1. Publish the application:
   ```bash
   dotnet publish -c Release -o publish
   ```
2. Create Azure resources:
   ```bash
   az group create --name MyPostgresApi-rg --location westeurope
   az appservice plan create --name MyPostgresApi-plan --resource-group MyPostgresApi-rg --sku B1 --is-linux
   az webapp create --resource-group MyPostgresApi-rg --plan MyPostgresApi-plan --name <your-webapp-name> --runtime "DOTNET:8.0"
   ```
3. Configure application settings so the Web App knows where the SQLite file lives:
   ```bash
   az webapp config appsettings set \
     --resource-group MyPostgresApi-rg --name <your-webapp-name> \
     --settings JWT_SECRET_KEY="<strong-secret>" SQLITE_PATH="/home/site/wwwroot/app.db"
   ```
4. Deploy the published artifacts:
   ```bash
   az webapp deploy --resource-group MyPostgresApi-rg --name <your-webapp-name> --src-path publish
   ```
5. Upload an existing `app.db` (if you migrated data) by using `az webapp ssh` or the
   Kudu console. Place the database at `/home/site/wwwroot/app.db`.

The application exposes health checks at `/health` and `/health-ui`.

## Observability endpoints

* `GET /health` – machine-readable health check (includes SQLite health probe)
* `GET /health-ui` – HealthChecks.UI dashboard (disabled in test environment)

Azure App Service surfaces platform metrics through Azure Monitor, so no additional
Prometheus endpoint is exposed by this API.

## Resources

* `Scripts/create_board_posts_table.sql` – SQLite schema snippet if you need to create
  tables manually.
* `app.db` – default SQLite database file generated during development. Delete it if you
  prefer to recreate from migrations.
