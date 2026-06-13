# IndigoLabs.Api

ASP.NET Core Web API for calculating cached temperature statistics from a semicolon-delimited measurements CSV.

## Requirements

- .NET 10 SDK
- `measurements.csv` dataset placed at `Data/measurements.csv`

The CSV is not committed because it is large. The expected header is:

```text
datetime;city;temp_celsius
```

## Quick Start

Clone the repository:

```bash
git clone https://github.com/rokfurlan123/IndigoLabs.Api.git
cd IndigoLabs.Api
```

Place the dataset here:

```text
Data/measurements.csv
```

Run the API:

```bash
dotnet run
```

Swagger opens at:

```text
https://localhost:7093/swagger/index.html
```

If prompted, trust the local ASP.NET development certificate.

## Authentication

The API uses Basic authentication.

Demo credentials:

```text
username: indigo
password: labs
```

The password is stored as a PBKDF2-SHA256 hash and salt in `appsettings.json`.

For raw HTTP requests, the Basic auth header for the demo credentials is:

```http
Authorization: Basic aW5kaWdvOmxhYnM=
```

## Endpoints

The API uses an ASP.NET Core controller:

```text
Controllers/MeasurementsController.cs
```

```http
GET  /api/measurements/cache
GET  /api/cities
GET  /api/cities/{city}
GET  /api/cities/average-temperature/greater-than/{value}
GET  /api/cities/average-temperature/less-than/{value}
POST /api/measurements/recalculate
```

## Caching

On startup, a background service reads `Data/measurements.csv`, calculates min, max, and average temperature per city, and stores the results in memory.

Normal GET endpoints read from the in-memory cache and do not recalculate on each call.

While startup calculation or recalculation is running, endpoints can return:

```http
503 Service Unavailable
```

Error responses use this shape:

```json
{
  "message": "Temperature calculations are currently happening. Try again shortly."
}
```

## Recalculation

The cache can be refreshed manually:

```http
POST /api/measurements/recalculate
```

The app also watches `Data/measurements.csv`. If the file is replaced or changed, the watcher debounces file events and recalculates automatically when source file metadata changes.

## HTTP Files

The project includes:

```text
IndigoLabs.Api.http
IndigoLabs.Api.Scenarios.http
http-client.env.json
```

`IndigoLabs.Api.http` contains success-path requests.

`IndigoLabs.Api.Scenarios.http` contains expected auth/error scenarios.

The `.http` files default to:

```text
https://localhost:7093
```

If your HTTP client supports environments, `http-client.env.json` also defines:

- `dev` -> `https://localhost:7093`
- `dev-http` -> `http://localhost:5154`

Prefer HTTPS for authenticated requests because HTTP can redirect to HTTPS and some clients drop the `Authorization` header during redirect.

## Tests

Run:

```bash
dotnet test tests/IndigoLabs.Api.Tests/IndigoLabs.Api.Tests.csproj
```

The tests use small temporary CSV files and do not require the large real dataset.
