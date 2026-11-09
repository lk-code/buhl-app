# AddressManagement

[![Build & Test](https://github.com/lk-code/buhl-app/actions/workflows/build-test.yml/badge.svg)](https://github.com/lk-code/buhl-app/actions/workflows/build-test.yml)

REST-API zur Verwaltung von Adressen auf Basis von .NET 10. Entwickelt testgetrieben mit klarer Schichtentrennung, EF Core InMemory als Persistenz und einer vollständig dokumentierten OpenAPI-Schnittstelle.

## Erste Schritte

Voraussetzung: .NET 10 SDK (Version siehe `global.json`). Weitere Abhängigkeiten gibt es nicht.

```bash
git clone https://github.com/lk-code/buhl-app.git
cd buhl-app
dotnet tool restore
dotnet restore
dotnet run --project src/LKCode.AddressManagement.Service
```

`dotnet run` nutzt das Profil `http` aus `launchSettings.json` (Umgebung `Development`):

| Was | URL |
|---|---|
| API | `http://localhost:5180` |
| OpenAPI-Dokument | `http://localhost:5180/openapi/v1.json` |
| Scalar (nur in Development) | `http://localhost:5180/scalar` |

## Endpunkte

| Methode | Pfad | Erfolg | Fehler |
|---|---|---|---|
| `POST` | `/api/addresses` | `201 Created` mit der Adresse, ihrer URL im `Location`-Header und ihrer Version im `ETag`-Header | `400` ungültige Werte oder ungültiges JSON, `415` Body ist kein JSON |
| `GET` | `/api/addresses?city=&postalCode=&page=&pageSize=&sortBy=&sortDirection=` | `200 OK` mit einer Seite der passenden Adressen (`items`, `page`, `pageSize`, `totalCount`, `totalPages`), auch leer | `400` ungültige Seite, Seitengröße oder Sortierung |
| `GET` | `/api/addresses/{id}` | `200 OK` mit der Adresse und ihrer Version im `ETag`-Header | `404` unbekannte Id oder keine GUID |
| `PUT` | `/api/addresses/{id}` | `200 OK` mit der geänderten Adresse und ihrer neuen Version im `ETag`-Header | `400` ungültiges `If-Match`, ungültige Werte oder ungültiges JSON, `404` unbekannte Id oder keine GUID, `412` Version passt nicht, `415` Body ist kein JSON, `428` `If-Match` fehlt |
| `PATCH` | `/api/addresses/{id}` | `200 OK` mit der geänderten Adresse und ihrer neuen Version im `ETag`-Header | `400` ungültiges `If-Match`, ungültiges Patch-Dokument oder ungültige Werte, `404` unbekannte Id oder keine GUID, `409` `test`-Operation fehlgeschlagen, `412` Version passt nicht, `415` Body ist kein JSON Patch, `428` `If-Match` fehlt |
| `DELETE` | `/api/addresses/{id}` | `204 No Content` | `400` ungültiges `If-Match`, `404` unbekannte Id oder keine GUID, `412` Version passt nicht, `428` `If-Match` fehlt |

Fehler sind immer Problem Details (`application/problem+json`), unerwartete Fehler liefern `500`.

Die Antworten der beiden `GET` können aus dem serverseitigen Cache kommen und tragen dann den Header `Age`. Jede Änderung wirkt trotzdem sofort (siehe **Caching**).

`PUT`, `PATCH` und `DELETE` verlangen den Header `If-Match` mit dem `ETag` der Version, auf die sich die Änderung bezieht (z. B. `"1"`), oder `*` für jede Version (siehe **Optimistic Concurrency**).

```bash
curl -i -X POST http://localhost:5180/api/addresses \
  -H "Content-Type: application/json" \
  -d '{"firstName":"Erika","lastName":"Mustermann","street":"Hauptstrasse","houseNumber":"17","postalCode":"51147","city":"Koeln","country":"DE","email":"erika.mustermann@example.com"}'

# Adressen, deren Ort „Koeln“ (also auch „Köln“) und deren PLZ „511“ enthält
curl -i "http://localhost:5180/api/addresses?city=koeln&postalCode=511"

# zweite Seite mit je 10 Adressen, die zuletzt geänderten zuerst
curl -i "http://localhost:5180/api/addresses?page=2&pageSize=10&sortBy=updatedAt&sortDirection=desc"

# alle Werte der Version 1 einer Adresse ersetzen; ohne "email" wird eine vorhandene E-Mail entfernt
curl -i -X PUT http://localhost:5180/api/addresses/<id> \
  -H "Content-Type: application/json" \
  -H 'If-Match: "1"' \
  -d '{"firstName":"Max","lastName":"Mustermann","street":"Hauptstrasse","houseNumber":"1","postalCode":"10115","city":"Berlin","country":"DE"}'

# einzelne Werte der Version 2 ändern (JSON Patch): nur wenn der Vorname noch "Max" ist, Ort ersetzen und E-Mail entfernen
curl -i -X PATCH http://localhost:5180/api/addresses/<id> \
  -H "Content-Type: application/json-patch+json" \
  -H 'If-Match: "2"' \
  -d '[{"op":"test","path":"/firstName","value":"Max"},{"op":"replace","path":"/city","value":"Hamburg"},{"op":"remove","path":"/email"}]'

# Adresse löschen, egal in welcher Version
curl -i -X DELETE http://localhost:5180/api/addresses/<id> -H 'If-Match: *'
```

## Tests und Coverage

```bash
dotnet test                                  # alle Tests
dotnet run coverage.cs                       # Tests mit Coverage, Ausgabe gesamt und pro Projekt
dotnet run coverage.cs -- --threshold 90     # zusätzlich Mindestabdeckung prüfen (wie in der CI)
```

Der HTML-Report liegt danach unter `coverage/report/index.html`. Mit `--threshold` schlägt das Skript fehl, wenn die gesamte Line-Coverage unter dem Wert liegt; solange es keinen abdeckbaren Code gibt, wird die Prüfung übersprungen.

Die Integrationstests starten den Dienst über `AddressManagementWebApplicationFactory` im Speicher. Jede Instanz der Factory verwendet eine eigene InMemory-Datenbank mit eindeutigem Namen und, da sie einen eigenen Host startet, einen eigenen Output-Cache; Tests bzw. Fixtures, die eine eigene Factory erzeugen, beeinflussen sich dadurch nicht. Eine Deklaration `public partial class Program` ist dafür nicht nötig: Seit .NET 10 erzeugt ein Source-Generator die Klasse selbst als `public`, eine eigene Deklaration meldet der Analyzer ASP0027 als überflüssig.

## CI

Der Workflow [`build-test.yml`](.github/workflows/build-test.yml) läuft bei jedem Push auf `main` und jedem Pull Request gegen `main`: Build mit Warnungen als Fehler, danach `coverage.cs` mit mindestens 90 % Line-Coverage. Die Coverage-Zusammenfassung steht in der Job-Summary, der HTML-Report liegt als Artefakt `coverage-report` bereit. Neuere Läufe desselben Branches brechen laufende ab.

## Architektur

| Projekt | Inhalt |
|---|---|
| `LKCode.AddressManagement.Service` | REST-Host: Controller, Problem Details, OpenAPI, Output Caching |
| `LKCode.AddressManagement.Core.Addresses` | Geschäftslogik Adressen: Service, DTOs, Validierung, Mapping, Interfaces |
| `LKCode.AddressManagement.Domain` | Entity `Address`, Normalisierung, Domain-Regeln |
| `LKCode.AddressManagement.Data` | EF Core: DbContext, Konfiguration, Repository |

Geschäftslogik ist nach Aufgabenbereichen in eigene Projekte `.Core.{Feature}` geschnitten; aktuell gibt es `.Core.Addresses`. Weitere Bereiche bekämen eigene Projekte, ohne bestehende zu berühren.

Das Repository-Interface liegt in `Core.Addresses`, die Implementierung in `Data` (Dependency Inversion). Die Geschäftslogik kennt EF Core dadurch nicht. Hinweis zur Benennung: Das Projekt `.Service` ist der REST-Host, die Klasse `AddressService` liegt in `.Core.Addresses`.

## Technologien

| Technologie | Grund |
|---|---|
| .NET 10 (LTS) | aktuelles LTS-Release, Controller-Schicht laut Anforderung |
| EF Core InMemory | Persistenz laut Anforderung, keine externe Datenbank nötig (Alternative SQLite In-Memory, siehe Designentscheidungen) |
| FluentValidation | Validierungsregeln getrennt vom Modell und einzeln testbar |
| System.Text.Json | Standard in .NET, durchgehend für JSON |
| Microsoft.AspNetCore.OpenApi + Scalar | offizieller OpenAPI-Weg seit .NET 9, moderne Oberfläche |
| Microsoft.AspNetCore.JsonPatch.SystemTextJson | offizielle JSON-Patch-Unterstützung (RFC 6902) auf Basis von System.Text.Json seit .NET 10 |
| ASP.NET Core Output Caching | im Framework enthalten, serverseitiger Cache mit gezielter Invalidierung per Tag |
| Serilog | strukturiertes Logging |
| NUnit, AwesomeAssertions, NSubstitute | Unit- und Integrationstests |
| coverlet + ReportGenerator | Coverage-Messung und Reports |
| GitHub Actions | Build, Tests und Coverage-Prüfung bei jedem Push und Pull Request |

## Pakete und Lizenzen

Verwendet werden nur Pakete mit permissiven Lizenzen. Microsoft-Pakete sind nicht aufgeführt.

| Paket | Lizenz | Verwendung |
|---|---|---|
| FluentValidation | Apache-2.0 | Validierung der Requests |
| Scalar.AspNetCore | MIT | Oberfläche für das OpenAPI-Dokument |
| Serilog.AspNetCore | Apache-2.0 | Strukturiertes Logging und Request-Logging |
| NUnit | MIT | Testframework |
| NUnit3TestAdapter | MIT | NUnit-Runner für Microsoft.Testing.Platform |
| NUnit.Analyzers | MIT | Analyzer für NUnit-Tests |
| AwesomeAssertions | Apache-2.0 | Assertions in Tests |
| NSubstitute | BSD-3-Clause | Mocks in Tests |
| NSubstitute.Analyzers.CSharp | MIT | Analyzer für NSubstitute |
| coverlet.MTP | MIT | Coverage-Messung |
| ReportGenerator (dotnet-Tool) | Apache-2.0 | Coverage-Report |

## Designentscheidungen

**Fachmodell**
- `Address` hat keine öffentlichen Setter. Eine Adresse entsteht nur über `Address.Create(…)`, ändert sich nur über `address.Update(…)` und wird nur über `address.Delete(…)` gelöscht (siehe **Soft-Delete**).
- Die Entity bereinigt ihre Eingaben selbst: Strings werden getrimmt, `Country` wird in Großbuchstaben umgewandelt, eine leere E-Mail wird zu `null`. Die normalisierten Felder für die Suche, `CreatedAt`/`UpdatedAt` und die `Version` pflegt sie automatisch: `Create` setzt die Version auf 1, jedes `Update` und das `Delete` erhöhen sie um 1, auch wenn sich kein Wert ändert.
- Die Maximallängen stehen zentral in `AddressConstraints`. Sie sind die einzige Quelle für Validierung, EF-Konfiguration und OpenAPI.
- Schutzprüfungen (leeres Pflichtfeld, Maximallänge überschritten) werfen eine `ArgumentException`. Da Eingaben vorher validiert werden, ist eine Verletzung ein Programmierfehler und kein erwartbarer Fall. Schlägt ein Update fehl, bleibt die Adresse unverändert.
- Zeitangaben kommen ausschließlich aus dem `TimeProvider`, auch der Zeitanteil der Id (`Guid.CreateVersion7(now)`).

**Persistenz**
- Ohne die Vorgabe wäre **SQLite In-Memory** (`DataSource=:memory:`) meine Wahl: Es ist eine relationale Datenbank mit Constraints, Transaktionen und SQL-Semantik (z. B. bei Groß-/Kleinschreibung) und kommt dem Verhalten in Produktion deutlich näher. Microsoft rät selbst davon ab, den InMemory-Provider für Tests zu nutzen. Einschränkungen von SQLite: keine automatische Row-Version, `DateTimeOffset` nicht sortierbar, die Verbindung muss vor allem dauerhaft offen bleiben.
- Ids sind GUID Version 7 (zeitlich sortiert). Gegenüber zufälligen GUIDs fragmentiert ein Index weniger. Bei SQL Server bringt das wenig, da `uniqueidentifier` nach den letzten Bytes sortiert wird; PostgreSQL profitiert direkt.
- Pagination, Sortierung und Filter laufen vollständig in der Datenbank (siehe **Pagination und Sortierung**).
- Der Datenbankname kommt aus der Konfiguration (`Data:DatabaseName`) und wird beim Start geprüft; ohne Namen startet die Anwendung nicht. So bekommt jeder Test eine eigene Datenbank.
- Die Abbildung erfolgt ausschließlich per Fluent API (`AddressConfiguration`), die Maximallängen kommen aus `AddressConstraints`. `NormalizedPostalCode` ist nie länger als die PLZ, `NormalizedCity` hat dagegen keine Maximallänge, weil die Normalisierung Werte verlängern kann (z. B. ß→ss). Der InMemory-Provider erzwingt Längen nicht; die Konfiguration beschreibt das Modell trotzdem vollständig.
- `GetByIdAsync` liefert eine verfolgte Entity, damit Änderungen über `UpdateAsync` gespeichert werden. Listenabfragen laufen ohne Change-Tracking.
- Ein Global Query Filter (`!IsDeleted`) in `AddressConfiguration` blendet gelöschte Adressen aus allen Abfragen aus, auch aus `GetByIdAsync` und `ListAsync`. Nur die Tests lesen sie mit `IgnoreQueryFilters()`.

**Suche**
- Ort und PLZ werden zusätzlich normalisiert gespeichert. Die Suche ist eine Teilstring-Suche auf diesen Werten. „Köln“, „Koeln“ und „KOLN“ finden sich gegenseitig, auch Akzente und Sonderzeichen anderer Sprachen werden aufgelöst. Seltene Fehltreffer durch die Vereinfachung sind bewusst in Kauf genommen.
- Regeln Ort: Leerzeichen trimmen und zusammenfassen, Kleinbuchstaben, Sonderzeichen ersetzen (ß→ss, æ→ae, œ→oe, ø→o, ł→l, đ→d, þ→th, ð→d, ı→i, ħ→h, ŧ→t), Akzente entfernen, danach ae→a, oe→o, ue→u. Regeln PLZ: alle Leerzeichen entfernen, Großbuchstaben (`sw1a 1aa` → `SW1A1AA`).
- Das Repository erwartet bereits normalisierte Filterwerte. Leere Filter werden ignoriert, Ort und PLZ sind UND-verknüpft.
- Bei einer echten Datenbank kann `LIKE '%…%'` keinen Index nutzen. Für große Datenmengen wäre eine Volltextsuche sinnvoll.

**Pagination und Sortierung**
- Die Liste liefert immer eine Seite: `{ items, page, pageSize, totalCount, totalPages }`. Query-Parameter: `page` (Standard 1, ≥ 1), `pageSize` (Standard 20, 1–100), `sortBy` (`lastName` [Standard], `firstName`, `city`, `postalCode`, `country`, `createdAt`, `updatedAt`) und `sortDirection` (`asc` [Standard], `desc`), beliebig kombinierbar mit `city` und `postalCode`.
- Standardwerte und Grenzen stehen zentral in `AddressListConstraints`, die Namen der Sortierung in `AddressSortFields` und `SortDirections`. Sie sind die einzige Quelle für Validierung, Service und OpenAPI.
- Leere Werte (`page=`, `sortBy=%20`) gelten wie fehlende als Standard. `sortBy` und `sortDirection` werden getrimmt und ohne Beachtung der Groß-/Kleinschreibung erkannt (`LastName`, `DESC`); das OpenAPI-Schema nennt die kanonischen Werte, wie bei `country`. Zahlen (`1`) und Listen (`lastName,city`) sind ungültig. Deshalb werden die beiden Parameter als String gebunden und nicht als Enum: Der Enum-Converter von MVC nähme auch Zahlen und kommagetrennte Werte an.
- `page` und `pageSize` sind `int?`. Ist ein Wert keine Ganzzahl oder zu groß (`abc`, `1.5`, `2147483648`), trägt MVC den Fehler in den Model State ein („The value 'abc' is not valid.“). Die Action ergänzt die Fehler von `AddressListQueryValidator` und antwortet mit **400** und allen Fehlern zusammen, jeweils unter dem Namen des Parameters (`pageSize`).
- Das Repository arbeitet mit zwei Abfragen auf demselben gefilterten `IQueryable`: `CountAsync` für `totalCount`, danach `OrderBy` → `ThenBy(Id)` → `Skip` → `Take` → `ToListAsync`. Es werden nie alle Datensätze geladen. Liegt die Seite hinter dem Ende, entfällt die zweite Abfrage; die Zahl der übersprungenen Datensätze wird als `long` berechnet, sodass auch `page=2147483647` nicht überläuft.
- Stabile Sortierung: Bei gleichen Werten wird zusätzlich nach `Id` sortiert, in derselben Richtung. `desc` ist damit genau die Umkehrung von `asc`, und jede Adresse steht auf genau einer Seite, solange sich die Daten zwischen den Abrufen nicht ändern. Gegen Einfügen und Löschen zwischen zwei Seiten hilft Offset-Paging grundsätzlich nicht; dafür wäre Keyset-Paging nötig.
- Eine Seite hinter dem Ende ist kein Fehler: **200** mit leerem `items`, `page` wie angefragt, `totalCount` und `totalPages` stimmen. Ohne Treffer ist `totalPages` 0. `totalPages` wird aufgerundet, ohne dass die Addition überlaufen kann.
- Sortiert wird nach den gespeicherten Werten, nicht nach den normalisierten Suchfeldern. Wie Texte verglichen werden (Groß-/Kleinschreibung, Umlaute), bestimmt die Datenbank: Der InMemory-Provider nutzt den Standardvergleich von .NET in der Kultur des Servers, eine relationale Datenbank ihre Collation.
- Count und Seite laufen nicht in einer gemeinsamen Transaktion. Ändern sich die Daten genau dazwischen, kann `totalCount` kurz nicht zu `items` passen; das ist hingenommen.

**Validierung**
- Die API arbeitet mit eigenen DTOs (`CreateAddressRequest`, `UpdateAddressRequest`, `AddressResponse`, `AddressListQuery`, `AddressListResponse`), nie direkt mit der Entity. Das Mapping liegt in `AddressMappingExtensions`; die normalisierten Felder werden nicht ausgeliefert.
- Ablauf: erst bereinigen (`Sanitize()`: trimmen, `Country` in Großbuchstaben, leere E-Mail → `null`), dann validieren. Ein fehlendes Pflichtfeld wird beim Bereinigen zu einem leeren String und von der Validierung gemeldet.
- Die Regeln für Anlegen und Ändern stehen einmal in `AddressRequestValidator<TRequest>`; beide Requests implementieren `IAddressRequest`. Pro Feld gibt es höchstens einen Fehler, gemeldet werden aber alle fehlerhaften Felder.
- `Country` muss aus genau zwei Großbuchstaben bestehen und ein vergebener ISO-3166-1-Alpha-2-Code sein. Die 249 Codes stehen als feste Liste in `CountryCodes` und werden einmalig in ein `FrozenSet` geladen. Bewusst nicht aus `RegionInfo`/`CultureInfo` berechnet: Die Daten des Betriebssystems sind unvollständig, enthalten inoffizielle Codes (z. B. `XK`) und fehlen im Invariant-Globalization-Modus (z. B. in Chiseled-Containern) ganz.
- E-Mail: Prüfung über `MailAddress.TryCreate`, das keine Exception wirft. Die erkannte Adresse muss exakt der Eingabe entsprechen; so werden Anzeigenamen, Kommentare und Adresslisten abgelehnt. RFC-gültige Sonderformen wie `"erika mustermann"@example.com` oder Umlaute in der Domain sind erlaubt.
- `AddressListQueryValidator` prüft Seite, Seitengröße und Sortierung der Liste, nachdem `Sanitize()` die Texte getrimmt und leere Werte zu `null` gemacht hat. Fehlende Werte sind gültig und stehen für die Standardwerte. Die Meldungen zu `sortBy` und `sortDirection` nennen die erlaubten Werte.
- Die Validatoren sind zustandslos und werden über `AddCoreAddresses()` als Singleton registriert.

**Geschäftslogik**
- `AddressService` (`IAddressService`) liegt zwischen Controller und Repository und kennt weder HTTP noch EF Core. Er erwartet bereinigte und validierte Requests; eine ungültige Eingabe ist ein Programmierfehler und führt zur `ArgumentException` der Entity.
- „Nicht gefunden“ und „Version passt nicht“ sind erwartbare Fälle und werden über den Rückgabewert gemeldet: `GetByIdAsync` liefert `null`, `UpdateAsync` ein `AddressChangeResult` (Status und bei Erfolg die geänderte Adresse), `DeleteAsync` einen `ChangeStatus` (`Success`, `NotFound`, `VersionMismatch`). Die beiden kleinen Typen in `Core.Addresses/Results` kommen ohne Bibliothek aus.
- `DeleteAsync` markiert die geladene Adresse mit `address.Delete(…)` und speichert sie über `UpdateAsync` des Repositorys. Löschen ist also eine Änderung wie jede andere, mit derselben Versionsprüfung; ein eigenes `DeleteAsync` im Repository gibt es nicht.
- Die Listenfilter werden vor der Suche mit `ITextNormalizer` normalisiert. Fehlende oder leere Filter werden ignoriert.
- Für die Liste setzt der Service die Standardwerte, wandelt die Namen der Sortierung in `AddressSortField` und `SortDirection` um und gibt alles als `AddressListCriteria` an das Repository. Das Repository liefert eine `AddressPage` (Adressen der Seite und Gesamtzahl), `ToListResponse` bildet daraus die Antwort mit `totalPages`. Eine Seite, Seitengröße oder Sortierung außerhalb der Grenzen ist im Service ein Programmierfehler (`ArgumentException`), denn der Controller validiert vorher.
- Der Service wird über `AddCoreAddresses()` als Scoped registriert, passend zum Repository. Repository, `ITextNormalizer`, `TimeProvider` und Logging registrieren `AddData()`, `AddDomain()` und der Host.

**Service-Host**
- `Program.cs` registriert `AddDomain()`, `AddCoreAddresses()`, `AddData()`, `AddAddressCaching()` (Output Caching, siehe **Caching**) und `TimeProvider.System`. Die Controller nutzen System.Text.Json, den Standard von ASP.NET Core; Newtonsoft.Json wird nicht verwendet.
- Das OpenAPI-Dokument (`Microsoft.AspNetCore.OpenApi`) ist der Vertrag der API und in jeder Umgebung unter `/openapi/v1.json` erreichbar. Die Oberfläche Scalar gibt es nur in `Development`.
- Der Datenbankname steht in `appsettings.json` (`Data:DatabaseName`), ebenso die Ablaufzeit des Caches (`Caching:Expiration`).

**API**
- Der `AddressesController` (`[ApiController]`, Route `api/addresses`) enthält keine Geschäftslogik. Er bereinigt den Request (`Sanitize()`), validiert ihn mit `IValidator<T>` und ruft dann `IAddressService` auf. Bei ungültigen Werten antwortet er mit **400** als `ValidationProblemDetails`, und zwar mit allen Feldfehlern auf einmal.
- Die Keys der Feldfehler sind die Namen im JSON (`firstName`). `ModelStateDictionaryExtensions.AddValidationErrors` wandelt sie über die Naming-Policy der JSON-Optionen um. Fehler im JSON selbst stehen unter ihrem JSON-Pfad (`$.firstName`).
- MVC behandelt nicht-nullable Properties nicht als implizite Pflichtfelder (`SuppressImplicitRequiredAttributeForNonNullableReferenceTypes`). Sonst würde MVC fehlende oder leere Felder schon vor der Action ablehnen: ohne Bereinigung, nur mit einem Teil der Fehler und mit anderen Meldungen.
- Bei ungültigem JSON lautet die Meldung nur „The input was not valid.“ (`AllowInputFormatterExceptionMessages = false`), denn die Meldungen von System.Text.Json enthalten interne Typnamen. Ein leerer Body oder `null` ergibt „A non-empty request body is required.“.
- Validierungsmeldungen sind immer englisch. Die Lokalisierung von FluentValidation ist abgeschaltet (`ValidatorOptions.Global.LanguageManager.Enabled = false`), sonst richteten sich die Standardmeldungen nach der Sprache des Servers.
- Der Request-Body von `POST` und `PUT` muss JSON sein (`[Consumes("application/json")]`), sonst kommt **415**. Wie überall in ASP.NET Core gelten auch Medientypen mit dem Suffix `+json` als JSON; das OpenAPI-Dokument nennt deshalb zusätzlich `application/*+json`. `PATCH` nimmt nur `application/json-patch+json` an (siehe **PATCH**).
- `201 Created` enthält im `Location`-Header die absolute URL der neuen Adresse. Die URL entsteht über die benannte Route `GetAddressById`, weil MVC das Suffix `Async` aus Action-Namen entfernt und `CreatedAtAction(nameof(GetByIdAsync))` deshalb keine Route fände.
- Unbekannte Ids und Werte, die keine GUID sind (Constraint `{id:guid}`), liefern **404** mit denselben Problem Details wie eine unbekannte Route.
- Die Liste (`GET /api/addresses`) hat die optionalen Filter `city` und `postalCode`. Beide suchen Teilstrings in den normalisierten Werten (siehe **Suche**): `sol`, `ingen` und `SOLin` finden `Solingen`, `Koeln` findet `Köln`. Leere Werte oder Werte nur aus Leerzeichen werden ignoriert, zwei Filter sind UND-verknüpft. Gibt es keinen Treffer, kommt **200** mit leerem `items`, nicht 404. Seite und Sortierung: siehe **Pagination und Sortierung**.
- Die Filter werden bewusst nicht validiert. Ein Suchbegriff ist kein gespeicherter Wert, deshalb gilt die Maximallänge der Felder für ihn nicht. Beim Ort ginge das auch fachlich nicht, weil die Normalisierung Werte verlängern kann (ß→ss). Ein Filter, der zu nichts passt, liefert einfach eine leere Seite.
- Die Parameter sind einzeln an der Action deklariert (nicht als `[FromQuery] AddressListQuery`). So heißen sie im OpenAPI-Dokument `city`, `postalCode`, `page`, `pageSize`, `sortBy` und `sortDirection`, und `Core.Addresses` braucht keine ASP.NET-Attribute.
- `PUT /api/addresses/{id}` ist ein vollständiges Update: Alle Werte werden ersetzt, Bereinigung und Validierung laufen wie beim Anlegen. Fehlt die E-Mail oder ist sie leer, wird eine vorhandene E-Mail entfernt. `Id` und `CreatedAt` bleiben erhalten, `UpdatedAt` wird neu gesetzt. Die Antwort ist **200** mit der geänderten Adresse, ohne `Location`-Header.
- `PUT` legt keine Adresse an: Ids vergibt der Server (GUID Version 7), eine unbekannte Id liefert **404**.
- Prüfreihenfolge bei `PUT`: **415** (kein JSON) → **404** (unbekannte Id) → **428**/**400**/**412** (`If-Match`, siehe **Optimistic Concurrency**) → **400** (ungültiges JSON) → **400** (ungültige Werte). Die Id wird also vor dem Body geprüft; ein ungültiger Body ergibt bei unbekannter Id **404**.
- Der automatische Model-State-Filter von `[ApiController]` ist abgeschaltet (`SuppressModelStateInvalidFilter`), denn er beantwortet ungültiges JSON mit **400**, bevor die Action läuft, also auch vor **404**, **428** und **412**. Die Actions prüfen den Model State deshalb selbst: `POST` sofort, `PUT` und `PATCH` erst nach den Vorbedingungen. Die Antwort ist dieselbe `ValidationProblemDetails` wie vorher.
- `DELETE /api/addresses/{id}` markiert die Adresse als gelöscht (siehe **Soft-Delete**) und antwortet mit **204** ohne Body. Danach liefern `GET`, `PUT`, `PATCH` und erneutes `DELETE` für diese Id **404** mit denselben Problem Details wie für eine nie vergebene Id.

**OpenAPI**
- Statuscodes und Content-Types stehen per `ProducesResponseType` an den Actions, **500** einmal am Controller. Die Beschreibungen kommen aus der XML-Doku der Actions (`<summary>`, `<remarks>`, `<param>`, `<response code="…">`).
- Die `operationId`s kommen aus den Routennamen der Actions: `CreateAddress`, `GetAddressById`, `ListAddresses`, `UpdateAddress`, `PatchAddress` und `DeleteAddress`. Die Antwort **204** von `DELETE` hat keinen Body und deshalb kein Schema.
- `LocationHeaderOperationTransformer` beschreibt den `Location`-Header jeder 201-Antwort, denn Attribute können keine Header beschreiben.
- `ETagHeaderOperationTransformer` beschreibt den Pflicht-Header `ETag` (`pattern` `^"[1-9][0-9]*"$`) jeder Antwort, deren Body eine einzelne `AddressResponse` ist: `GET` by Id, `POST`, `PUT` und `PATCH`. Die Liste hat keinen `ETag`.
- `AgeHeaderOperationTransformer` beschreibt den optionalen Header `Age` (`integer`, `int64`, `minimum` 0) der **200**-Antwort jeder Action mit `[OutputCache]`, also der beiden `GET` (siehe **Caching**). Die Remarks der beiden Actions erklären, dass die Antwort aus dem Cache kommen kann und bedingte Requests nicht ausgewertet werden.
- `IfMatchHeaderOperationTransformer` beschreibt den Pflicht-Parameter `If-Match` im Header jeder Operation mit einer Antwort **428**, also von `PUT`, `PATCH` und `DELETE`. Das `pattern` beschreibt die Syntax nach RFC 9110: `*` oder eine kommagetrennte Liste aus `"…"` und `W/"…"`. Ein Test stellt sicher, dass es für alle geprüften Werte genau dann passt, wenn die Auswertung zur Laufzeit den Header als gültig erkennt. Der Header wird nicht als Action-Parameter gebunden, weil ein Binding-Fehler die Prüfreihenfolge bräche.
- `AddressSchemaTransformer` ergänzt die Schemas der Requests und von `AddressResponse` aus `AddressConstraints`: `required`, `minLength`/`maxLength`, `pattern` `^[A-Z]{2}$` für `country` sowie `format: email` und `null` als erlaubten Typ für `email`. `version` ist eine Ganzzahl (`int64`) ab 1; ohne den Transformer wäre sie auch als String beschrieben, weil die JSON-Optionen Zahlen aus Strings lesen dürfen. Die Schemas beschreiben bereinigte Werte; die API nimmt auch Werte mit Leerzeichen am Rand oder `country` in Kleinbuchstaben an.
- Für jedes JSON-Patch-Dokument erzeugt ASP.NET Core dasselbe generische Schema `JsonPatchDocument`: ein Array von Operationen (`oneOf` aus `add`/`replace`/`test`, `move`/`copy` und `remove`), in dem `path` und `from` beliebige Strings und `value` beliebige Werte sind. `AddressPatchDocumentSchemaTransformer` schränkt `path` und `from` auf die JSON Pointer der acht Felder ein (`enum`) und `value` auf `string` oder `null`. Die Pointer kommen aus den JSON-Optionen, also aus derselben Quelle wie bei der Prüfung zur Laufzeit. Die Regeln der einzelnen Felder (Länge, Pattern, Format) gelten für die geänderte Adresse und stehen im Schema `UpdateAddressRequest`. Pro Feld als eigenes `oneOf` wären sie zwar ausdrückbar, das Schema würde dadurch aber unlesbar.
- `AddressListParametersOperationTransformer` beschreibt die Query-Parameter der Liste: `page` als `int32` mit `minimum` 1, `maximum` 2147483647 und `default` 1, `pageSize` als `int32` mit `minimum` 1, `maximum` 100 und `default` 20, `sortBy` und `sortDirection` als `enum` der kanonischen Werte mit `default` `lastName` bzw. `asc`. Ohne den Transformer wären die Zahlen auch als String beschrieben (die JSON-Optionen dürfen Zahlen aus Strings lesen), und die Sortierung hätte keine erlaubten Werte. Die Werte kommen aus `AddressListConstraints`, `AddressSortFields` und `SortDirections`.
- `AddressListResponseSchemaTransformer` beschreibt `page`, `pageSize`, `totalCount` und `totalPages` von `AddressListResponse` als `int32` mit ihren Bereichen (`page` ≥ 1, `pageSize` 1–100, die Zählungen ≥ 0) und macht alle fünf Felder zu `required`.
- `ProblemDetailsSchemaTransformer` beschreibt `ProblemDetails` und `ValidationProblemDetails` so, wie die API sie schreibt: `type`, `title`, `status` und `traceId` (bei Validierungsfehlern zusätzlich `errors`) sind immer enthalten, `status` ist eine Ganzzahl zwischen 100 und 599, `type` und `instance` sind URI-Referenzen. Ohne den Transformer fehlte `traceId`, und `status` wäre auch als String beschrieben, weil die JSON-Optionen Zahlen aus Strings lesen dürfen.
- `OpenApiDocumentTests` prüft das generierte Dokument als Ganzes: gültiges OpenAPI 3.1 (Reader von `Microsoft.OpenApi` ohne Fehler und Warnungen), genau die sechs Endpunkte, Beschreibungen, **500** und Problem Details bei jeder Fehlerantwort, Header nur bei **201** (`Location`, `ETag`), den übrigen Antworten mit einer Adresse (`ETag`) und den **200**-Antworten der beiden `GET` (`Age`), **428** genau bei `PUT`, `PATCH` und `DELETE`, keine unbekannten oder unbenutzten Schemas sowie die Schemas der Problem Details. Die Details je Endpunkt und die Einschränkungen der Adress-Schemas prüft `AddressesOpenApiTests`.
- Fallstrick im XML-Kommentar-Generator von .NET 10: Er schreibt die Beschreibung jedes `<param>`, der kein Operation-Parameter ist, an den Request-Body, also auch die des `CancellationToken`. Der Body-Parameter wird deshalb als letzter dokumentiert.

**Fehlerbehandlung**
- Fehler werden als Problem Details nach RFC 7807 (bzw. Nachfolger RFC 9457) zurückgegeben.
- Erwartbare Fälle (nicht gefunden, ungültige Eingabe) laufen über Rückgabewerte, Exceptions nur bei unerwarteten Fehlern.
- Unerwartete Exceptions übersetzt der globale `ProblemDetailsExceptionHandler` (`IExceptionHandler`) in **500** mit Problem Details. Die Antwort enthält nur `type`, `title`, `status` und die `traceId`, über die sich der Eintrag im Log finden lässt; Message, Exception-Typ und Stacktrace gehen nicht nach außen, auch nicht in `Development`. Der Handler loggt die Exception vollständig auf `Error`, denn seit .NET 10 loggt die Middleware Exceptions nicht mehr, die ein Handler behandelt hat.
- Fehlerantworten ohne Body (z. B. 404 für eine unbekannte Route) ergänzt `UseStatusCodePages()` um Problem Details.
- Controller mit `[ApiController]` antworten auch Clients, die nur `text/html` akzeptieren, mit Problem Details. Für andere Endpunkte hält sich ASP.NET Core an den `Accept`-Header; ohne JSON antwortet es dort ohne Problem Details.
- Client-Abbruch: Bricht der Client eine Anfrage ab, ist die folgende `OperationCanceledException` kein Fehler. In .NET 10 erkennt das bereits die `ExceptionHandlerMiddleware`, bevor sie einen `IExceptionHandler` aufruft: Sie setzt **499** (Client Closed Request) und loggt nur auf `Debug`. Das Serilog-Request-Log protokolliert den Abbruch mit Status 499 auf `Information`. Der Handler behandelt den Fall trotzdem selbst (Information-Log, 499, kein Body), damit er auch für sich allein korrekt ist. Eine `OperationCanceledException` ohne Client-Abbruch (z. B. ein interner Timeout) gilt als unerwarteter Fehler und ergibt 500.
- Gelöschte Adressen liefern 404 wie unbekannte. Es ist nicht erkennbar, ob ein Datensatz existiert hat.
- `Country` ist ein ISO-3166-1-Alpha-2-Code (z. B. `DE`).

**Optimistic Concurrency**
- Schutz vor Lost Updates: Jede Adresse hat eine `Version` (`long`), die mit 1 beginnt und bei jeder Änderung in der Domain um 1 steigt. EF Core InMemory erzeugt keine Version automatisch (keine Row-Version). Die Version verrät die Anzahl der Änderungen eines Datensatzes; das ist bewusst in Kauf genommen, da der Datensatz ohnehin gelesen werden darf.
- Die Version steht als `version` in jeder `AddressResponse`, auch in der Liste, damit Clients ohne zusätzliches `GET` ändern können. In keinem Request-DTO steht sie; ein Client kann sie also nicht setzen.
- `GET` by Id, `POST`, `PUT` und `PATCH` liefern die Version als strong ETag im Header `ETag`, z. B. `"3"`. Die Liste hat keinen `ETag`, weil sie mehrere Versionen enthält.
- `If-Match` ist bei `PUT`, `PATCH` und `DELETE` Pflicht. Erlaubt sind eine kommagetrennte Liste von ETags (auch über mehrere Header-Zeilen) oder `*`. Gelesen und verglichen wird mit `EntityTagHeaderValue` aus ASP.NET Core. `If-Match` vergleicht laut RFC 9110 immer strong, ein weak ETag wie `W/"3"` passt daher nie. `*` bedeutet „ändern, wenn vorhanden“ ohne Versionsprüfung durch den Client.
- Prüfreihenfolge nach RFC 9110 (Abschnitt 13.2): **404** (unbekannte Id; Vorbedingungen werden ignoriert, wenn die Antwort ohne sie kein Erfolg wäre) → **428** (`If-Match` fehlt, RFC 6585) → **400** (`If-Match` ist leer, kein ETag oder enthält `*` neben anderen Werten; der Fehler steht unter dem Schlüssel `If-Match`) → **412** (kein ETag passt zur aktuellen Version) → **400** (Body ungültig) → bei `PATCH` **409** (`test` fehlgeschlagen). **415** kommt wie bisher vor allem anderen, weil `[Consumes]` vor der Action greift. Fehlerantworten haben keinen `ETag`.
- Für **428** kennt MVC keine Standardwerte; `Program.cs` ergänzt `type` (RFC 6585, Abschnitt 3) und `title` in `ClientErrorMapping`, damit auch diese Problem Details vollständig sind.
- Der Controller prüft `If-Match` gegen die gerade geladene Version. Damit dazwischen keine Änderung verloren geht, prüft zusätzlich die Datenbank: `Version` ist in der EF-Konfiguration ein Concurrency-Token. Das Repository setzt beim Speichern den Original-Wert auf die vom Client erwartete Version, EF Core speichert also nur, wenn die gespeicherte Version noch dieselbe ist. Bei `*` bleibt der Original-Wert die geladene Version, sodass auch dann eine gleichzeitige Änderung erkannt wird.
- Die `DbUpdateConcurrencyException`, mit der EF Core einen fehlgeschlagenen Vergleich meldet, fängt das Repository ab und gibt sie als `ChangeStatus` zurück: `NotFound`, wenn die Adresse nicht gespeichert ist (`GetDatabaseValuesAsync` liefert `null`) oder inzwischen gelöscht wurde (`IsDeleted` in den gespeicherten Werten; `GetDatabaseValuesAsync` ignoriert den Query Filter), sonst `VersionMismatch`. Der Eintrag wird danach nicht mehr verfolgt. Die API antwortet mit **404** bzw. **412**.
- `ConcurrentAddressChangeTests` beweist, dass EF InMemory den Token prüft: Zwei Requests mit derselben Version bestehen alle Prüfungen des Controllers und warten dann im `RendezvousAddressRepository` aufeinander, bevor beide speichern. Genau einer ist erfolgreich, der andere bekommt **412** (bei zwei `DELETE` **404**). Die Repository-Tests zeigen dasselbe mit zwei `DbContext`-Instanzen.

**Soft-Delete**
- Löschen entfernt eine Adresse nicht, sondern markiert sie: `address.Delete(timeProvider)` setzt `IsDeleted`, `DeletedAt` und `UpdatedAt` auf den Zeitpunkt der Löschung und erhöht die `Version`. Die Werte der Adresse bleiben erhalten.
- Nach außen verhält sich eine gelöschte Adresse, als hätte es sie nie gegeben: `GET`, `PUT`, `PATCH` und `DELETE` liefern **404** mit denselben Problem Details wie eine nie vergebene Id, unabhängig vom `If-Match`-Header (auch mit dem ETag der Löschung oder `*`), und die Liste enthält sie nicht. `IsDeleted` und `DeletedAt` stehen in keinem DTO und nicht im OpenAPI-Dokument.
- Grundlage ist der Global Query Filter (siehe **Persistenz**): Der Service bekommt eine gelöschte Adresse gar nicht erst zu sehen. Zusätzlich schützt die Entity sich selbst: `Update` und ein erneutes `Delete` auf einer gelöschten Adresse werfen eine `InvalidOperationException` und lassen sie unverändert. Über die API ist das nicht erreichbar; es wäre ein Programmierfehler (**500**).
- Wird eine Adresse gelöscht, während eine andere Anfrage sie gerade ändert oder löscht, erkennt das der Concurrency-Token; die andere Anfrage bekommt **404**, die erste Löschung bleibt erhalten.
- Es gibt bewusst keinen Endpunkt zum Wiederherstellen oder endgültigen Entfernen. Gelöschte Datensätze bleiben gespeichert; für den produktiven Einsatz wären eine Aufbewahrungsfrist und ein Bereinigungsjob nötig (z. B. wegen DSGVO).

**PATCH**
- JSON Patch (RFC 6902) statt JSON Merge Patch: eindeutiges Format für generierte Clients und offizielle Unterstützung mit System.Text.Json.
- Ablauf: aktuelle Adresse laden → `If-Match` prüfen → Patch-Dokument prüfen → als `UpdateAddressRequest` abbilden (`ToUpdateRequest()`) → Operationen anwenden → bereinigen, validieren und speichern auf demselben Weg wie bei `PUT`. Es gibt keine eigene Update-Logik für `PATCH`. Die Operationen ändern nur diese Kopie, deshalb wirken alle zusammen oder keine.
- Unterstützt werden alle sechs Operationen: `add`, `remove`, `replace`, `move`, `copy` und `test`. Da eine Adresse feste Felder hat, setzen `add` und `replace` beide den Wert eines Felds, und `remove` setzt ihn auf `null`. Bei Pflichtfeldern führt das zu **400** mit dem Feldfehler, bei `email` wird die E-Mail entfernt.
- Die Operationen werden vor dem Anwenden streng geprüft (`AddressPatchDocumentExtensions.ValidateOperations`), denn die Bibliothek ist an mehreren Stellen nachsichtig oder fehlerhaft: Ein fehlendes `op` behandelt sie als `add`, Groß-/Kleinschreibung bei `op` und in Pfaden ignoriert sie, ein fehlendes `path` oder `from` führt zu einer `ArgumentNullException` (also **500**), und bei `test` unterscheidet sie einen unbekannten Pfad nicht von einem abweichenden Wert. Erlaubt sind deshalb nur die sechs Operationen in Kleinbuchstaben, als Pfade nur die JSON Pointer der acht Felder mit exaktem Namen (`/firstName`, nicht `/FirstName`, RFC 6901 ist case-sensitiv) und als `value` nur ein String oder `null`. Alle Fehler kommen zusammen als **400**, jeweils unter dem JSON-Pfad im Patch-Dokument (`$[0].path`).
- Ein fehlendes `value` kann die Bibliothek nicht von `null` unterscheiden; es wird wie `null` behandelt. Das OpenAPI-Schema verlangt `value` trotzdem, wie RFC 6902.
- `test` vergleicht exakt, also ohne Bereinigung und mit Groß-/Kleinschreibung. Schlägt ein Test fehl, antwortet die API mit **409** (RFC 5789: Der Patch passt nicht zum Zustand der Ressource), und `detail` nennt Index und Pfad der Operation. Die Bibliothek wendet nach einem fehlgeschlagenen Test die weiteren Operationen trotzdem an. Das schadet nicht, weil die Kopie dann verworfen wird.
- Prüfreihenfolge: **415** (kein JSON Patch) → **404** (unbekannte Id) → **428**/**400**/**412** (`If-Match`) → **400** (ungültiges JSON) → **400** (ungültige Operationen) → **409** (Test fehlgeschlagen) → **400** (ungültige Werte).
- Ein leeres Patch-Dokument `[]` ist gültig und wirkt wie `PUT` mit den aktuellen Werten; `UpdatedAt` wird neu gesetzt.

**Caching**
- Serverseitiges Output Caching von ASP.NET Core für `GET /api/addresses/{id}` und `GET /api/addresses`. Die beiden Policies registriert `AddAddressCaching()`. Die Actions verweisen per `[OutputCache(PolicyName = …)]` darauf, die Namen stehen in `AddressOutputCachePolicyNames`.
- Tags (`AddressCacheTags`): Eine einzelne Adresse trägt `address-{id}`, jede Seite der Liste `addresses`. `AddressIdTagPolicy` schreibt die Id im Tag immer klein und mit Bindestrichen, auch wenn der Request sie groß oder in einem anderen GUID-Format enthält (`{id:guid}` akzeptiert z. B. auch `N` und `B`). Ohne gültige Id cacht die Policy nichts, denn einen Eintrag ohne Tag könnte keine Änderung entfernen.
- Die Liste variiert nach allen Query-Parametern (`SetVaryByQuery("*")`). Jede Kombination aus Filter, Seite und Sortierung ist ein eigener Eintrag, ebenso unbekannte Parameter und andere Schreibweisen eines Werts (`city=Köln` und `city=köln`).
- Invalidierung: Nach einer erfolgreichen Änderung entfernt der Controller die betroffenen Einträge mit `IOutputCacheStore.EvictByTagAsync`. `POST` (**201**) entfernt `addresses`, denn eine neue Adresse kann auf jeder Seite auftauchen; gecachte einzelne Adressen bleiben erhalten. `PUT`, `PATCH` (**200**) und `DELETE` (**204**) entfernen `address-{id}` und `addresses`. Fehlgeschlagene Änderungen (400, 404, 409, 412, 415, 428) lassen den Cache unverändert.
- Beide Policies bauen auf der Standard-Policy von ASP.NET Core auf. Gecacht werden deshalb nur Antworten mit **200**, nie Fehlerantworten wie 400, 404 oder 500. Requests mit `Authorization`-Header und Antworten mit Cookies werden ebenfalls nicht gecacht.
- Eine Antwort aus dem Cache trägt den Header `Age` mit den Sekunden seit dem Speichern (RFC 9111, Abschnitt 5.1). `AgeHeaderOperationTransformer` beschreibt ihn im OpenAPI-Dokument.
- Bedingte Requests (`If-None-Match`, `If-Modified-Since`) beantwortet der Cache nie (`ConditionalRequestBypassPolicy`). Die Middleware würde sonst mit **304** antworten, aber nur, wenn gerade ein passender Eintrag existiert, während die Actions die Header nicht auswerten. So gibt es nie 304, und die Antwort hängt nicht vom Zustand des Caches ab. Die frische Antwort wird trotzdem gespeichert.
- Die Ablaufzeit ist nur eine Absicherung: `Caching:Expiration` in `appsettings.json`, Standard `00:05:00`. Erlaubt sind Werte größer 0 und höchstens ein Tag; sonst startet die Anwendung nicht (`ValidateOnStart`). Sehr große Werte würden im Speicher-Cache überlaufen und bei jedem `GET` zu **500** führen. Die Ablaufzeit begrenzt, wie lange eine Antwort veraltet sein kann, wenn die Invalidierung nicht greift. Beispiel: Ein `GET` liest eine Adresse, ein `PUT` ändert sie und leert den Cache, danach speichert das `GET` den alten Stand.
- Position in der Pipeline: `UseOutputCache()` steht hinter Exception-Handler und Status-Code-Pages und vor den Endpunkten. Authentifizierung und Autorisierung gehören davor, damit der Cache nie einen Request beantwortet, der abgelehnt würde.
- Der Cache liegt im Speicher der jeweiligen Instanz (Standard-Store von ASP.NET Core). Bei mehreren Instanzen hätte jede ihren eigenen Cache, und eine Änderung über eine Instanz entfernte die Einträge der anderen nicht. Dann wäre ein verteilter Store nötig, z. B. Redis mit `Microsoft.AspNetCore.OutputCaching.StackExchangeRedis` (`AddStackExchangeRedisOutputCache`). Policies, Tags und Invalidierung bleiben dabei gleich.
- `AddressOutputCacheTests` erkennt einen Treffer am Header `Age` und prüft Tags und Ablaufzeit über einen aufzeichnenden Store (`RecordingOutputCacheStore`). Jeder Test hat einen eigenen Host und damit einen leeren Cache.

**Logging**
- Strukturiertes Logging über `ILogger` mit Message-Templates. Die Log-Methoden erzeugt der Source-Generator `[LoggerMessage]`: Die Templates werden beim Build geprüft, und die Argumente werden nur verarbeitet, wenn das Level aktiv ist.
- Geloggt werden nur Ids, Versionen, Anzahlen, Seite, Sortierung und normalisierte Filter, keine personenbezogenen Daten wie Namen oder E-Mail-Adressen. Änderungen (Anlegen, Ändern, Löschen) und abgelehnte Änderungen wegen einer anderen Version laufen auf `Information`, Lesezugriffe und „nicht gefunden“ auf `Debug`.
- Serilog schreibt aktuell nur in die Konsole: in `Development` als lesbarer Text inklusive der strukturierten Properties, in allen anderen Umgebungen als kompaktes JSON (`CompactJsonFormatter`), das Log-Aggregatoren direkt verarbeiten können. Die Level stehen im Abschnitt `Serilog:MinimumLevel` der Konfiguration; `Microsoft.AspNetCore` und `Microsoft.EntityFrameworkCore` laufen auf `Warning`, in `Development` die eigenen Namespaces auf `Debug`.
- Das Request-Logging von Serilog schreibt pro Anfrage einen Eintrag mit Methode, Pfad, Status und Dauer (500 und mehr auf `Error`). Es steht ganz außen in der Pipeline und sieht deshalb den endgültigen Status.
- Der statische Serilog-Logger bleibt unberührt, damit sich mehrere Hosts in einem Prozess (z. B. in Tests) keinen Logger teilen. Zusätzliche Sinks lassen sich auch per Dependency Injection registrieren (`ReadFrom.Services`); die Integrationstests nutzen das, um Log-Einträge zu prüfen.
- Langfristig ist die Ausgabe in Dateien oder zentrale Dienste (z. B. Seq, Elasticsearch, Application Insights) sinnvoll; dafür sind nur das Sink-Paket und Konfiguration nötig.


## Authentifizierung

Die API ist per JWT geschützt. Scopes: `addresses:read` für GET, `addresses:write` für POST, PUT, PATCH und DELETE.

Für die Entwicklung erzeugt das im .NET SDK enthaltene Werkzeug `dotnet user-jwts` Tokens. Der Signaturschlüssel liegt dabei in den lokalen User Secrets, nicht im Repository.

```bash
# Token mit Lese- und Schreibrechten
dotnet user-jwts create --project src/LKCode.AddressManagement.Service --scope "addresses:read" --scope "addresses:write"

# Token nur mit Leserechten (zum Ausprobieren von 403)
dotnet user-jwts create --project src/LKCode.AddressManagement.Service --scope "addresses:read"

# erzeugte Tokens anzeigen
dotnet user-jwts list --project src/LKCode.AddressManagement.Service
dotnet user-jwts print <id> --project src/LKCode.AddressManagement.Service
```

Verwendung in Scalar: Token im Auth-Feld eintragen. Mit `curl`:

```bash
curl -H "Authorization: Bearer <token>" https://localhost:<port>/api/addresses
```

In Produktion kommen die Tokens von einem Identity Provider (z. B. Entra ID oder Keycloak). Dafür ändert sich nur die Konfiguration, nicht der Code.

## Docker (eigener bonus - optional)

Das Image wird ohne Dockerfile über das .NET SDK gebaut und nach erfolgreichem Build und Test automatisch in der GitHub Container Registry veröffentlicht.

```bash
docker pull ghcr.io/lk-code/buhl-app:latest
docker run --rm -p 8080:8080 -e ASPNETCORE_ENVIRONMENT=Development ghcr.io/lk-code/buhl-app:latest
```

Die API ist dann unter `http://localhost:8080` erreichbar, Scalar unter `/scalar` (nur in Development).