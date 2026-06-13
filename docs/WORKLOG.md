# IndigoLabs.Api Worklog

Last updated: 2026-06-13

## Done

- Created ASP.NET Core Web API project at `C:\projects\IndigoLabs.Api`.
- Target framework is `.NET 10` / `net10.0`.
- Extracted assignment dataset to `Data/measurements.csv`.
- Created a modified dataset copy at `DataVariants/measurements_modified.csv`.
- Added `.gitignore` with standard .NET ignores and CSV/data-file ignores.
- Added `Data/.gitkeep` so the expected local data folder exists after clone.
- Added `README.md` with setup, runtime, endpoint, auth, cache, recalculation, HTTP file, and test details.
- Added `http-client.env.json` with development host addresses.
- Removed the generated WeatherForecast sample API.
- Converted endpoint implementation from Minimal APIs to controller-based ASP.NET Core API.
- Split code into separate folders:
  - `Authentication`
  - `Controllers`
  - `Models`
  - `Options`
  - `Services`
- Added Swagger/OpenAPI.
- Configured launch settings so the browser opens Swagger on startup.
- Added Basic authentication.
- Replaced plaintext password storage with PBKDF2-SHA256 hash + salt.
- Implemented in-memory measurement statistics cache.
- Implemented startup cache warmup using `MeasurementCacheWarmupService`.
- Implemented automatic file watching using `MeasurementFileWatcherService`.
- Implemented manual recalculation endpoint.
- Added cache status endpoint.
- Added source file metadata to cache status:
  - source last modified UTC
  - source file size in bytes
- Added extended cache status metadata:
  - source file name/path
  - data row count
  - skipped malformed row count
  - calculation duration
- Added CSV header validation for `datetime;city;temp_celsius`.
- Added handling for calculation-in-progress responses.
- Replaced `ProblemDetails` error bodies with a simple `ErrorResponse` model containing only `message`.
- Added xUnit test project at `tests/IndigoLabs.Api.Tests`.
- Added service tests for aggregation, city lookup, filters, recalculation, and missing CSV behavior.
- Added password hash verifier tests.
- Added endpoint tests for authentication, not found, calculation-in-progress, and successful JSON responses.
- Split manual HTTP requests into:
  - `IndigoLabs.Api.http` for success-path requests.
  - `IndigoLabs.Api.Scenarios.http` for Swagger/auth/error/calculation-in-progress scenarios.
- Disabled HTTPS redirection in Development so Swagger requests opened from the HTTP launch profile keep the Basic auth header.
- Verified the API with manual smoke checks.

## Current API

- `GET /api/measurements/cache`
  - Returns cache status.
  - Returns `503` while cache is warming.
- `GET /api/cities`
  - Returns min, max, and average temperatures for all cities.
- `GET /api/cities/{city}`
  - Returns min, max, and average temperatures for one city.
- `GET /api/cities/average-temperature/greater-than/{value}`
  - Returns cities whose average temperature is greater than `value`.
- `GET /api/cities/average-temperature/less-than/{value}`
  - Returns cities whose average temperature is less than `value`.
- `POST /api/measurements/recalculate`
  - Manually rebuilds the cache from the CSV file.

## Verified

- `dotnet build` succeeds.
- Swagger loads and lists all endpoints.
- Basic auth works:
  - `indigo:labs` succeeds.
  - wrong password returns `401`.
- Startup warmup builds cache for `99` cities.
- While calculation is running, data endpoints return `503` with a `{ "message": "..." }` response.
- After warmup, endpoints return from the in-memory cache.
- Cache status includes `sourceFileSizeBytes`.
- File watcher starts and watches `Data/measurements.csv`.
- `dotnet test tests/IndigoLabs.Api.Tests/IndigoLabs.Api.Tests.csproj` passes.
- Current automated test count: 16 passing.

## Needs Change Or Upgrade

- Consider returning clearer status during automatic file watcher recalculation:
  - current cache remains available, but manual recalculation lock can return `503`
  - decide whether reads should continue serving old cache during recalculation or return `503`
- Avoid exposing development credentials as real credentials.
  - Current `indigo:labs` is acceptable for assignment/demo only.
  - For real use, move hash/salt to user secrets or environment variables.
- Standardize file path formatting in logs.
  - Current watcher log can show mixed `\` and `/` path separators.
- Consider making cache warmup required before app reports ready.
  - Current implementation starts serving the app immediately and returns `503` until cache is ready.

## Still To Implement

- Automated file watcher behavior tests if practical.
- Git repository initialization and first commit, if desired.
- Optional: solution file with separate test project.
- Optional: API key or stronger auth if Basic auth is considered too weak.
- Optional: Docker support.

## Assignment Coverage

- Calculates max, min, and average temperatures of each city across the dataset: done.
- Caches calculated values so GET calls do not recalculate each time: done, in-memory cache.
- Provides all-city GET endpoint: done.
- Provides single-city GET endpoint: done.
- Provides larger/smaller-than average temperature filtering: done.
- Allows recalculation when CSV changes: done manually and automatically through watcher.
- Provides Swagger/OpenAPI: done.
- Basic API authentication: done.
- README: done.
- Tests: 16 passing service/auth-helper/endpoint tests.
