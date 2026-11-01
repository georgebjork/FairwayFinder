# FairwayFinder documentation

FairwayFinder is a golf stat tracker. Golfers log rounds hole by hole from an iOS app, which talks to a JWT-secured REST API (`FairwayFinder.Api`). An internal Blazor Server console (`FairwayFinder.Admin`) handles everything a phone shouldn't: user management, course data, round repair, diagnostics, and import tools. Both hosts share one domain layer (`FairwayFinder.Features`) over PostgreSQL.

Start with [Getting started](getting-started.md), then [Architecture overview](architecture/overview.md).

## Architecture

| Doc | Covers |
|---|---|
| [Overview](architecture/overview.md) | System context, the two hosts, projects, dependency flow, layering rules |
| [Hosting and configuration](architecture/hosting-and-configuration.md) | Both `Program.cs` pipelines, Aspire AppHost, ServiceDefaults, DI registration, every config key |
| [Deployment](architecture/deployment.md) | CI/CD workflows, containers, environments, the static landing site |

## Data

| Doc | Covers |
|---|---|
| [Database](data/database.md) | The two databases, conventions, audit stamping, date rules, migrations |
| [Schema](data/schema.md) | Every entity, relationship, and index, with ER diagrams |

## API

| Doc | Covers |
|---|---|
| [API overview](api/overview.md) | Request pipeline, JWT auth and refresh tokens, errors, validation, public web surface, adding an endpoint |
| [Endpoint reference](api/endpoints.md) | Every endpoint: route, auth, request, response, service called |

## Features

| Doc | Covers |
|---|---|
| [Rounds](features/rounds.md) | Hole-by-hole entry, round lifecycle, scoring rules, shots and hole stats |
| [Stats and strokes gained](features/stats-and-strokes-gained.md) | Stats computation, strokes gained per shot, golfer-level baselines |
| [Games](features/games.md) | Side games with friends: match play, skins, scoring engines, live scoreboards |
| [Courses](features/courses.md) | Courses, teeboxes, holes, search |
| [Users and social](features/users-and-social.md) | Identity, invitations, profiles, friends, password reset |
| [Notifications and email](features/notifications-and-email.md) | APNs push, device registration, email providers |
| [Course imports](features/course-imports.md) | GolfCourseAPI import pipeline and the TGTR transfer tool |

## Admin console

| Doc | Covers |
|---|---|
| [Admin console](admin/console.md) | Every admin page, auth, shared components, adding a page |

## Operations

| Doc | Covers |
|---|---|
| [Logging and observability](operations/logging-and-observability.md) | Serilog, `app_log`, request logs, OpenTelemetry, health checks |
| [Testing](testing.md) | Test projects, Testcontainers fixtures, fakes, conventions tests |

## Design records

Written before or during implementation. Kept for the reasoning; where they disagree with the code, the code is right.

- [Strokes gained spec](strokes-gained-spec.md)
- [Games implementation plan](games-implementation-plan.md)
- [API implementation plan](api-implementation-plan.md) (historical; references the deleted `FairwayFinder.Web` project)

`CLAUDE.md` at the repo root is the condensed version of these rules for AI coding agents. Keep it in sync when a convention changes.
