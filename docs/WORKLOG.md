# IndigoLabs.Api Worklog

Last updated: 2026-06-13

## Done

- Created ASP.NET Core Web API project at `C:\projects\IndigoLabs.Api`.
- Target framework is `.NET 10` / `net10.0`.
- Extracted assignment dataset to `Data/measurements.csv`.
- Created a modified dataset copy at `DataVariants/measurements_modified.csv`.
- Added `.gitignore` with standard .NET ignores and CSV/data-file ignores.
- Removed the generated WeatherForecast sample API.
- Split code into separate folders:
  - `Authentication`
  - `Endpoints`
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
- Added xUnit test project at `tests/IndigoLabs.Api.Tests`.
- Added service tests for aggregation, city lookup, filters, recalculation, and missing CSV behavior.
- Added password hash verifier tests.
- Added endpoint tests for authentication, not found, calculation-in-progress, and successful JSON responses.
- Expanded `IndigoLabs.Api.http` with manual HTTP scenarios for Swagger, authentication, cache status, retrieval endpoints, filtering, recalculation, and calculation-in-progress behavior.
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
- While calculation is running, data endpoints return `503` with a problem response.
- After warmup, endpoints return from the in-memory cache.
- Cache status includes `sourceFileSizeBytes`.
- File watcher starts and watches `Data/measurements.csv`.
- `dotnet test tests/IndigoLabs.Api.Tests/IndigoLabs.Api.Tests.csproj` passes.
- Current automated test count: 14 passing.

## Needs Change Or Upgrade

- Add a proper `README.md` with:
  - setup instructions
  - credentials
  - endpoint examples
  - cache behavior
  - file watcher behavior
  - assumptions about CSV format
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

- Automated tests:
  - Basic authentication handler tests
  - file watcher behavior tests if practical
- `README.md`.
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
- README: not done.
- Tests: started, 14 passing service/auth-helper/endpoint tests.
