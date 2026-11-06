# MyPostgresApi

The ASP.NET Core 8 Web API behind **[machinemal.eu](https://machinemal.eu)**: user accounts and saved images, stored in SQLite.

(The name dates from when it used PostgreSQL; it has run on SQLite since 2025.)

## Endpoints

All routes are under `/api`.

| Method | Route | Auth | What it does |
|---|---|---|---|
| POST | `/users` | – | Sign up (rate limited) |
| POST | `/users/login` | – | Log in, returns a JWT (rate limited) |
| GET | `/users/{id}` | JWT | Your own account only |
| PUT | `/users/update` | JWT + password | Change your name |
| PUT | `/users/changepassword` | JWT + old password | Change password (min. 8 characters) |
| DELETE | `/users/delete` | JWT + password | Delete your own account |
| POST | `/images/save` | optional | Save an image; anonymous visitors get a guest account via cookie |
| GET | `/images/mine` | optional | Your saved images |
| DELETE | `/images/{id}` | optional | Delete one of your saved images |
| POST | `/images/image-user-count` | – | How many users saved an image |
| GET | `/health` | – | Health check (outside `/api`) |

**Security:** passwords are hashed with BCrypt, users can only read and change their own data, input is validated (email format, password 8–72 bytes, http(s) URLs only), and login, sign-up and image saving are rate limited per IP.

## Project layout

```
Controllers/   API endpoints (users, images) + shared input rules
Data/          EF Core DbContext
DTOs/ Models/  Request/response and domain models
Migrations/    SQLite schema migrations (applied automatically at startup in Production)
Tests/         xUnit integration tests against a temporary SQLite database
Program.cs     Host setup: JWT auth, CORS, rate limiting, health checks
```

## Run locally

Requires the .NET 8 SDK.

```bash
echo "JWT_SECRET_KEY=a-long-random-secret-for-local-use" > .env
dotnet run          # http://localhost:8080, Swagger UI at /swagger in Development
```

The database is created as `app.db` in the project folder.

## Tests

```bash
dotnet test
```

The tests also run in GitHub Actions on every push ([test.yml](.github/workflows/test.yml)), and a failing test stops a deploy.

## Deployment

The `server` branch deploys automatically ([deploy-server.yml](.github/workflows/deploy-server.yml)): test → publish a self-contained Linux build → copy it to the server → restart the systemd service → wait for `/health`. The database lives outside the app folder, so deploys never touch user data. `JWT_SECRET_KEY` comes from a repository secret.
