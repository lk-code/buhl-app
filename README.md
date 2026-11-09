# AddressManagement

[![Build & Test](https://github.com/lk-code/buhl-app/actions/workflows/build-test.yml/badge.svg)](https://github.com/lk-code/buhl-app/actions/workflows/build-test.yml)
[![Publish Container](https://github.com/lk-code/buhl-app/actions/workflows/publish-container.yml/badge.svg)](https://github.com/lk-code/buhl-app/actions/workflows/publish-container.yml)

REST-API zur Verwaltung von Adressen auf Basis von .NET 10. Entwickelt testgetrieben mit klarer Schichtentrennung, EF Core InMemory als Persistenz und einer vollständig dokumentierten OpenAPI-Schnittstelle. Alle Pflicht- und Bonuspunkte der Aufgabe sind umgesetzt.

## Schritte

Von einem frischen Checkout bis zum ersten Request:

1. **Repository klonen und Werkzeuge wiederherstellen**
   ```bash
   git clone https://github.com/lk-code/buhl-app.git
   cd buhl-app
   dotnet tool restore
   ```
2. **Token erzeugen** (legt beim ersten Aufruf auch den Signaturschlüssel in den User Secrets an)
   ```bash
   dotnet user-jwts create --project src/LKCode.AddressManagement.Service --scope "addresses:read" --scope "addresses:write"
   git checkout -- src/LKCode.AddressManagement.Service/appsettings.Development.json
   ```
   Das ausgegebene Token kopieren. Der zweite Befehl verwirft die Änderung, die `dotnet user-jwts` an `appsettings.Development.json` vornimmt.
3. **API starten**
   ```bash
   dotnet run --project src/LKCode.AddressManagement.Service
   ```
4. **Ausprobieren:** `http://localhost:5180/scalar` öffnen, das Token unter Authentifizierung eintragen und die Endpunkte aufrufen. Alternativ per `curl`:
   ```bash
   curl -i http://localhost:5180/api/addresses -H "Authorization: Bearer <token>"
   ```

## Befehle

Voraussetzung: .NET 10 SDK (Version siehe `global.json`). Weitere Abhängigkeiten gibt es nicht.

```bash
# einmalig nach dem Klonen
git clone https://github.com/lk-code/buhl-app.git
cd buhl-app
dotnet tool restore
dotnet restore

# bauen und starten
dotnet build
dotnet run --project src/LKCode.AddressManagement.Service

# Tests und Coverage
dotnet test                                  # alle Tests
dotnet run coverage.cs                       # Coverage gesamt und pro Projekt, Report unter coverage/report/index.html
dotnet run coverage.cs -- --threshold 90     # zusätzlich Mindestabdeckung prüfen (wie in der CI)

# JWT für die Entwicklung (legt beim ersten Aufruf den Schlüssel in den User Secrets an)
TOKEN=$(dotnet user-jwts create --project src/LKCode.AddressManagement.Service --scope "addresses:read" --scope "addresses:write" -o token)
dotnet user-jwts create --project src/LKCode.AddressManagement.Service --scope "addresses:read"    # nur Lesen, für 403
dotnet user-jwts list --project src/LKCode.AddressManagement.Service                              # erzeugte Tokens
dotnet user-jwts key --project src/LKCode.AddressManagement.Service                               # Signaturschlüssel

# Docker-Image lokal bauen
dotnet publish src/LKCode.AddressManagement.Service -t:PublishContainer
```

Das erste `dotnet user-jwts create` schreibt `appsettings.Development.json` neu (nur der abschließende Zeilenumbruch fehlt danach). Diese Änderung verwerfen: `git checkout -- src/LKCode.AddressManagement.Service/appsettings.Development.json`.

| Was | URL |
|---|---|
| API | `http://localhost:5180` |
| OpenAPI-Dokument | `http://localhost:5180/openapi/v1.json` |
| Scalar (nur in Development) | `http://localhost:5180/scalar` |

OpenAPI-Dokument und Scalar sind ohne Token erreichbar. In Scalar ist das Schema `Bearer` vorausgewählt, das Token wird dort eingetragen.

## Endpunkte

| Methode | Pfad | Scope | Erfolg |
|---|---|---|---|
| `POST` | `/api/addresses` | `addresses:write` | `201` mit `Location` und `ETag` |
| `GET` | `/api/addresses` | `addresses:read` | `200` mit einer Seite |
| `GET` | `/api/addresses/{id}` | `addresses:read` | `200` mit `ETag` |
| `PUT` | `/api/addresses/{id}` | `addresses:write` | `200` mit neuem `ETag` |
| `PATCH` | `/api/addresses/{id}` | `addresses:write` | `200` mit neuem `ETag` (JSON Patch) |
| `DELETE` | `/api/addresses/{id}` | `addresses:write` | `204` |

- **Liste:** Filter `city` und `postalCode` (Teilstring, normalisiert), `page` (Standard 1), `pageSize` (Standard 20, max. 100), `sortBy` (`lastName`, `firstName`, `city`, `postalCode`, `country`, `createdAt`, `updatedAt`), `sortDirection` (`asc`, `desc`).
- **Änderungen:** `PUT`, `PATCH` und `DELETE` verlangen `If-Match` mit dem `ETag` (z. B. `"1"`) oder `*`.
- **Fehler** sind immer Problem Details: `400` ungültige Eingabe, `401` kein gültiges Token, `403` Scope fehlt, `404` unbekannt oder gelöscht, `409` JSON-Patch-`test` fehlgeschlagen, `412` Version passt nicht, `415` falscher Content-Type, `428` `If-Match` fehlt.

```bash
curl -i -X POST http://localhost:5180/api/addresses \
  -H "Authorization: Bearer $TOKEN" -H "Content-Type: application/json" \
  -d '{"firstName":"Erika","lastName":"Mustermann","street":"Hauptstrasse","houseNumber":"17","postalCode":"51147","city":"Koeln","country":"DE","email":"erika.mustermann@example.com"}'

curl -i "http://localhost:5180/api/addresses?city=koeln&sortBy=updatedAt&sortDirection=desc" -H "Authorization: Bearer $TOKEN"

curl -i -X PATCH http://localhost:5180/api/addresses/<id> \
  -H "Authorization: Bearer $TOKEN" -H "Content-Type: application/json-patch+json" -H 'If-Match: "1"' \
  -d '[{"op":"replace","path":"/city","value":"Hamburg"},{"op":"remove","path":"/email"}]'
```

## Architektur

| Projekt | Inhalt |
|---|---|
| `LKCode.AddressManagement.Service` | REST-Host: Controller, Problem Details, OpenAPI, Caching, Authentifizierung |
| `LKCode.AddressManagement.Core.Addresses` | Geschäftslogik: Service, DTOs, Validierung, Mapping, Interfaces |
| `LKCode.AddressManagement.Domain` | Entity `Address`, Normalisierung, Domain-Regeln |
| `LKCode.AddressManagement.Data` | EF Core: DbContext, Konfiguration, Repository |

Abhängigkeiten: `Service` → `Core.*`, `Data` · `Core.*` → `Domain` · `Data` → `Core.*`, `Domain`. Das Repository-Interface liegt in `Core.Addresses`, die Implementierung in `Data`, die Geschäftslogik kennt EF Core also nicht. Weitere Fachbereiche bekämen eigene `.Core.{Feature}`-Projekte.

Tests: `LKCode.AddressManagement.UnitTests` (Domain, Service-Layer, Repository; Unterordner je Projekt) und `LKCode.AddressManagement.IntegrationTests` (alle Endpunkte über `WebApplicationFactory`).

## Vorgehen

- **TDD:** Pro Schritt erst ein Commit mit fehlschlagendem Test, dann die Implementierung, sichtbar in der Git-Historie.
- **Meilensteine:** Jeder Schritt aus [ROADMAP.md](ROADMAP.md) auf eigenem Branch `AM-{n}`, Pull Request mit CI-Prüfung, Merge-Commit. Commits im Format `AM-{n}: …`.
- **KI-gestützt:** Umgesetzt mit Claude Code. Planung, Architektur und Regeln kommen von mir: [ROADMAP.md](ROADMAP.md) enthält pro Meilenstein Beschreibung und Prompt, [CLAUDE.md](CLAUDE.md) die verbindlichen Regeln (Architektur, Konventionen, Tests, Lizenzen). Jeder Meilenstein wurde von mir geprüft, gepusht und gemergt.

## Technologien

| Technologie | Lizenz | Grund |
|---|---|---|
| .NET 10 (LTS), ASP.NET Core | MIT | aktuelles LTS-Release |
| EF Core InMemory | MIT | Persistenz laut Anforderung |
| Microsoft.AspNetCore.OpenApi, JsonPatch.SystemTextJson, JwtBearer, Output Caching | MIT | offizielle Bausteine des Frameworks |
| FluentValidation | Apache-2.0 | Validierungsregeln getrennt vom Modell, einzeln testbar |
| Scalar.AspNetCore | MIT | Oberfläche für das OpenAPI-Dokument |
| Serilog.AspNetCore | Apache-2.0 | strukturiertes Logging |
| NUnit, NUnit3TestAdapter, NUnit.Analyzers | MIT | Testframework |
| AwesomeAssertions | Apache-2.0 | Assertions |
| NSubstitute | BSD-3-Clause | Mocks (dazu NSubstitute.Analyzers.CSharp, MIT) |
| coverlet.MTP, ReportGenerator | MIT, Apache-2.0 | Coverage-Messung und Report |
| GitHub Actions, .NET SDK Container | – | Build, Tests, Coverage ≥ 90 %, Image in `ghcr.io` |

## Wichtige Designentscheidungen

- **Rückgabewerte statt Exceptions:** „Nicht gefunden“ und „Version passt nicht“ sind erwartbar und stehen in der Signatur. Exceptions nur für Unerwartetes (→ `500` mit `traceId`).
- **Normalisierte Suche:** Ort und PLZ werden zusätzlich normalisiert gespeichert; „Köln“, „Koeln“ und „KOLN“ finden sich gegenseitig.
- **Entity und DTO getrennt:** Die API arbeitet nur mit DTOs, Mapping manuell und testbar; Bereinigung und Validierung (FluentValidation) vor der Geschäftslogik.
- **Optimistic Concurrency:** `long`-Version als strong `ETag`, `If-Match` Pflicht (`428`), geprüft im Controller und zusätzlich per Concurrency-Token in EF Core.
- **Soft-Delete:** Gelöschte Adressen liefern `404` wie nie vorhandene; es ist nicht erkennbar, dass es sie gab.
- **Paging in der Datenbank:** Filter, Sortierung, `Skip`/`Take` und `Count` laufen auf `IQueryable`, stabile Sortierung über `Id`.
- **Output Caching:** serverseitig, Invalidierung per Tag bei jeder Änderung; Authentifizierung läuft vor dem Cache.
- **OpenAPI als Vertrag:** alle Statuscodes, Header und Einschränkungen dokumentiert und per Test abgesichert.
- **Logging ohne personenbezogene Daten:** nur Ids, Versionen und Anzahlen; JSON-Ausgabe außerhalb von Development.
- **SQLite als Alternative:** Ohne Vorgabe wäre SQLite In-Memory näher an einer echten relationalen Datenbank (Constraints, Transaktionen, SQL-Semantik).

Alle Details und Begründungen: [docs/DESIGN.md](docs/DESIGN.md).

## Authentifizierung in Produktion

In Produktion kommen die Tokens von einem Identity Provider (z. B. Entra ID oder Keycloak); es ändert sich nur die Konfiguration. Scopes stehen im Claim `scope` oder `scp`.

```json
"Authentication": {
  "Schemes": {
    "Bearer": {
      "Authority": "https://login.example.com/realms/addresses",
      "ValidIssuer": "https://login.example.com/realms/addresses",
      "ValidAudiences": [ "address-management-api" ]
    }
  }
}
```

Mit `Authority` lädt ASP.NET Core die Signaturschlüssel vom Identity Provider. `ValidIssuer` und `ValidAudiences` sind Pflicht, ohne sie startet die Anwendung nicht.

## Docker

```bash
docker pull ghcr.io/lk-code/buhl-app:latest
```

Tags: `latest` (neuester Stand von `main`), voller Commit-SHA, `X.Y.Z` (aus Git-Tag `vX.Y.Z`). Lokal gebaut (siehe **Befehle**) heißt das Image `lk-code/buhl-app:latest` statt `ghcr.io/lk-code/buhl-app:latest`.

Der Container läuft in `Production` und startet nur mit Issuer und Audience. Die Einstellungen kommen als Umgebungsvariablen (`__` statt `:`, Listen mit Index):

| Umgebungsvariable | Inhalt |
|---|---|
| `Authentication__Schemes__Bearer__Authority` | URL des Identity Providers; von dort lädt ASP.NET Core die Signaturschlüssel |
| `Authentication__Schemes__Bearer__ValidIssuer` | gültiger Issuer (Pflicht, alternativ `…__ValidIssuers__0`, `…__ValidIssuers__1` usw.) |
| `Authentication__Schemes__Bearer__ValidAudience` | gültige Audience (Pflicht, alternativ `…__ValidAudiences__0`, `…__ValidAudiences__1` usw.) |
| `Authentication__Schemes__Bearer__SigningKeys__0__Issuer` und `…__SigningKeys__0__Value` | symmetrischer Signaturschlüssel (Base64) und der Issuer, zu dem er gehört; nur zum Testen ohne Identity Provider |

Mit Identity Provider (Werte wie im Beispiel unter **Authentifizierung in Produktion**):

```bash
docker run --rm -p 8080:8080 \
  -e Authentication__Schemes__Bearer__Authority=https://login.example.com/realms/addresses \
  -e Authentication__Schemes__Bearer__ValidIssuer=https://login.example.com/realms/addresses \
  -e Authentication__Schemes__Bearer__ValidAudience=address-management-api \
  ghcr.io/lk-code/buhl-app:latest
```

Zum Ausprobieren mit Tokens von `dotnet user-jwts` (Schlüssel aus den lokalen User Secrets; die Audience `http://localhost:5180` steht in jedem dieser Tokens, unabhängig vom Port des Containers):

```bash
# $TOKEN wie unter Befehle erzeugen, dann Schlüssel lesen; die Ausgabe hat die Form: Signing Key: '<Schlüssel>'
KEY=$(dotnet user-jwts key --project src/LKCode.AddressManagement.Service | cut -d "'" -f 2)

docker run -d --name buhl-app -p 8080:8080 \
  -e Authentication__Schemes__Bearer__ValidIssuer=dotnet-user-jwts \
  -e Authentication__Schemes__Bearer__ValidAudience=http://localhost:5180 \
  -e Authentication__Schemes__Bearer__SigningKeys__0__Issuer=dotnet-user-jwts \
  -e Authentication__Schemes__Bearer__SigningKeys__0__Value="$KEY" \
  ghcr.io/lk-code/buhl-app:latest

curl -i -H "Authorization: Bearer $TOKEN" http://localhost:8080/api/addresses

# Container beenden und entfernen
docker rm -f buhl-app
```

Mit zusätzlich `-e ASPNETCORE_ENVIRONMENT=Development` gibt es Scalar unter `http://localhost:8080/scalar`. Issuer und Audiences kommen dann aus `appsettings.Development.json`, nur die beiden Variablen `SigningKeys__0__…` sind nötig.