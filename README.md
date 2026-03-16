# INFO7255 – Advanced Big Data Application & Indexing

A RESTful API for managing healthcare insurance plans, built with **ASP.NET Core 6.0**. The service provides full CRUD operations on hierarchical plan data, enforces optimistic concurrency control via ETags, and synchronises data between **Redis** (primary store) and **Elasticsearch** (search index) through an event-driven pipeline powered by **RabbitMQ / MassTransit**.

---

## Table of Contents

- [Project Overview](#project-overview)
- [Architecture](#architecture)
- [Tech Stack](#tech-stack)
- [Prerequisites](#prerequisites)
- [Getting Started](#getting-started)
- [Configuration](#configuration)
- [API Reference](#api-reference)
- [Authentication](#authentication)
- [Data Model](#data-model)
- [ETag & Conditional Requests](#etag--conditional-requests)
- [Project Structure](#project-structure)

---

## Project Overview

This API manages insurance **Plans** and their nested entities:

- **PlanCostShares** – member-level cost details (deductible, copay)
- **LinkedPlanServices** – services covered under a plan
- **LinkedService** – individual service information
- **PlanServiceCostShares** – service-level cost details

Every write operation publishes an event to RabbitMQ, which is consumed to keep the Elasticsearch index in sync, enabling advanced full-text and hierarchical search over plan data.

---

## Architecture

```
Client
  │
  ▼
ASP.NET Core API  ──(JWT/Google OAuth)──  Authentication
  │
  ├──► Redis          (primary key-value store)
  │
  └──► RabbitMQ       (message broker)
         │
         ├──► PlanUpdatedConsumer  ──► Elasticsearch (index upsert)
         └──► PlanDeletedConsumer  ──► Elasticsearch (index delete)
```

**Data-flow summary**

1. The API validates the JWT on every request.
2. Write operations (POST / PUT / PATCH) persist the plan to Redis and compute a fresh ETag.
3. An event is published to RabbitMQ.
4. Background consumers update the `plans` index in Elasticsearch, maintaining parent–child join-field relationships so hierarchical queries work correctly.
5. DELETE operations cascade the removal through Redis and the Elasticsearch index.

---

## Tech Stack

| Layer | Technology |
|---|---|
| Framework | ASP.NET Core 6.0 |
| Primary storage | Redis (StackExchange.Redis 2.8.24) |
| Search index | Elasticsearch 7.x (NEST 7.17.5) |
| Message broker | RabbitMQ via MassTransit 8.4.0 |
| ORM / DB context | Entity Framework Core 6 (InMemory) |
| Authentication | JWT Bearer + Google OAuth 2.0 (`Google.Apis.Auth` 1.69.0) |
| Serialisation | Newtonsoft.Json |
| API docs | Swagger / Swashbuckle 6.5.0 |

---

## Prerequisites

Make sure the following are installed and running before starting the application:

- [.NET 6.0 SDK](https://dotnet.microsoft.com/download/dotnet/6.0)
- **Redis** – listening on `localhost:6379`
- **Elasticsearch 7.x** – listening on `localhost:9200`
- **RabbitMQ** – listening on `localhost:5672` with default credentials (`guest` / `guest`)
- A **Google OAuth 2.0** client ID (for issuing and validating tokens)

---

## Getting Started

```bash
# 1. Clone the repository
git clone https://github.com/Nikesh984/INFO7255_RESTAPI.git
cd INFO7255_RESTAPI/RestAPI_INFO7255

# 2. Restore dependencies
dotnet restore

# 3. Build the project
dotnet build

# 4. Run the application (development mode)
dotnet run
```

The API will start on **http://localhost:8080**.  
Interactive Swagger documentation is available at **http://localhost:8080/swagger** when running in the `Development` environment.

---

## Configuration

### `appsettings.json`

```json
{
  "ConnectionStrings": {
    "Redis": "localhost:6379"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*"
}
```

### Key settings

| Setting | Default | Description |
|---|---|---|
| `ConnectionStrings:Redis` | `localhost:6379` | Redis connection string |
| Elasticsearch URL | `http://localhost:9200` | Configured in `WebApplicationBuilderExt.cs` |
| RabbitMQ host | `localhost` | Configured via MassTransit in `WebApplicationBuilderExt.cs` |
| Google OAuth audience | *(see source)* | Validated JWT audience (Google Client ID) |

---

## API Reference

**Base URL:** `http://localhost:8080/v1/plan`  
**Authentication:** All endpoints require a valid JWT Bearer token (see [Authentication](#authentication)).

---

### POST `/v1/plan` – Create a plan

Creates a new insurance plan. Returns an `ETag` header with the resource fingerprint.

**Request body** (`application/json`):

```json
{
  "planCostShares": {
    "deductible": 2000,
    "_org": "example.com",
    "copay": 23,
    "objectId": "1234vxc2324sdf-501",
    "objectType": "membercostshare"
  },
  "linkedPlanServices": [
    {
      "linkedService": {
        "_org": "example.com",
        "objectId": "1234520xvc30asdf-502",
        "objectType": "service",
        "name": "Yearly physical"
      },
      "planserviceCostShares": {
        "deductible": 10,
        "_org": "example.com",
        "copay": 0,
        "objectId": "1234512xvc1314asdfs-503",
        "objectType": "membercostshare"
      },
      "_org": "example.com",
      "objectId": "27283xvx9asdff-504",
      "objectType": "planservice"
    }
  ],
  "_org": "example.com",
  "objectId": "12xvxc345ssdsds-508",
  "objectType": "plan",
  "planType": "inNetwork",
  "creationDate": "12-12-2017"
}
```

| Response code | Meaning |
|---|---|
| `201 Created` | Plan created; `ETag` header set |
| `400 Bad Request` | Invalid or missing request body |
| `401 Unauthorized` | Missing or invalid JWT |
| `409 Conflict` | A plan with the same `objectId` already exists |

---

### GET `/v1/plan/{id}` – Retrieve a plan

Returns the plan with the given `objectId`.  
Supports conditional GET via the `If-None-Match` header.

| Request header | Description |
|---|---|
| `Authorization: Bearer <token>` | Required |
| `If-None-Match: "<etag>"` | Optional – enables 304 caching |

| Response code | Meaning |
|---|---|
| `200 OK` | Plan returned; `ETag` header set |
| `304 Not Modified` | Plan unchanged since the supplied ETag |
| `401 Unauthorized` | Missing or invalid JWT |
| `404 Not Found` | No plan with the given ID |

---

### PUT `/v1/plan/{id}` – Full update

Replaces an existing plan entirely. Requires the current `ETag` in the `If-Match` header.

| Request header | Description |
|---|---|
| `Authorization: Bearer <token>` | Required |
| `If-Match: "<etag>"` | Required – must match the current ETag |

| Response code | Meaning |
|---|---|
| `200 OK` | Plan updated; new `ETag` header set |
| `400 Bad Request` | Invalid body or ID mismatch |
| `401 Unauthorized` | Missing or invalid JWT |
| `404 Not Found` | Plan not found |
| `412 Precondition Failed` | ETag mismatch (resource was modified) |

---

### PATCH `/v1/plan/{id}` – Partial update (merge)

Merges the provided fields into the existing plan. Requires the current `ETag`.

| Request header | Description |
|---|---|
| `Authorization: Bearer <token>` | Required |
| `If-Match: "<etag>"` | Required |

| Response code | Meaning |
|---|---|
| `200 OK` | Plan updated; new `ETag` header set |
| `400 Bad Request` | Invalid body or ID mismatch |
| `401 Unauthorized` | Missing or invalid JWT |
| `404 Not Found` | Plan not found |
| `412 Precondition Failed` | ETag mismatch |
| `428 Precondition Required` | `If-Match` header is missing |

---

### DELETE `/v1/plan/{id}` – Delete a plan

Deletes the plan and all associated documents from Redis and Elasticsearch.

| Response code | Meaning |
|---|---|
| `204 No Content` | Plan successfully deleted |
| `401 Unauthorized` | Missing or invalid JWT |
| `404 Not Found` | Plan not found |

---

## Authentication

The API uses **Google OAuth 2.0** via JWT Bearer authentication.

1. Obtain a Google ID token for your application's Client ID.
2. Pass the token in every request as an `Authorization` header:

```
Authorization: Bearer <google-id-token>
```

The API validates:
- **Issuer**: `https://accounts.google.com`
- **Audience**: the configured Google Client ID
- **Token lifetime**: token must not be expired
- **Signing key**: Google's public keys

Requests without a valid token receive `401 Unauthorized` with a `ProblemDetails` response body.

---

## Data Model

```
Plan
├── objectId        (string, required)  – unique identifier
├── objectType      (string, required)
├── planType        (string, required)  – e.g. "inNetwork"
├── _org            (string, required)  – owning organisation
├── creationDate    (string, required)
├── planCostShares  (PlanCostShares)
│   ├── objectId
│   ├── objectType
│   ├── _org
│   ├── deductible  (int)
│   └── copay       (int)
└── linkedPlanServices[]  (LinkedPlanService)
    ├── objectId
    ├── objectType
    ├── _org
    ├── linkedService
    │   ├── objectId
    │   ├── objectType
    │   ├── _org
    │   └── name
    └── planserviceCostShares
        ├── objectId
        ├── objectType
        ├── _org
        ├── deductible  (int)
        └── copay       (int)
```

---

## ETag & Conditional Requests

ETags are **SHA-256 hashes** of the serialised plan object, Base64-encoded. They uniquely identify the current state of a resource.

| Pattern | Header | Behaviour |
|---|---|---|
| Conditional GET | `If-None-Match: "<etag>"` | Returns `304` if resource is unchanged |
| Conditional update | `If-Match: "<etag>"` | Returns `412` if resource has changed since the ETag was issued |
| Required precondition | *(PATCH without `If-Match`)* | Returns `428 Precondition Required` |

This ensures safe concurrent updates without requiring distributed locks.

---

## Project Structure

```
RestAPI_INFO7255/
├── Program.cs                        # Application entry point
├── appsettings.json                  # Production configuration
├── appsettings.Development.json      # Development configuration
├── Config/
│   ├── App.cs                        # Middleware & routing setup
│   └── WebApplicationBuilderExt.cs   # Redis, Elasticsearch & RabbitMQ DI registration
├── Controllers/
│   └── PlanController.cs             # REST endpoints (POST, GET, PUT, PATCH, DELETE)
├── Services/
│   ├── IPlanService.cs               # Service interface
│   └── PlanService.cs                # Business logic
├── Repositories/
│   ├── IPlanRepository.cs            # Repository interface
│   ├── PlanRepository.cs             # Redis & Elasticsearch data access
│   ├── PlanUpdatedConsumer.cs        # MassTransit consumer – indexes updated plans
│   └── PlanDeletedConsumer.cs        # MassTransit consumer – removes deleted plans
├── Data/
│   └── PlanContext.cs                # Entity Framework DbContext
├── Models/
│   ├── Plan.cs
│   ├── PlanCostShares.cs
│   ├── LinkedPlanService.cs
│   ├── LinkedService.cs
│   └── PlanServiceCostShares.cs
├── Helpers/
│   └── HttpResponseExtenstions.cs    # ETag response helper
└── Resources/
    └── planSchema.json               # Example plan JSON payload
```
