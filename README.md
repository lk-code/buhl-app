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
| `POST` | `/api/addresses` | `201 Created` mit der Adresse und ihrer URL im `Location`-Header | `400` ungültige Werte oder ungültiges JSON, `415` Body ist kein JSON |
| `GET` | `/api/addresses?city=&postalCode=` | `200 OK` mit der Liste der passenden Adressen, auch leer | – |
| `GET` | `/api/addresses/{id}` | `200 OK` mit der Adresse | `404` unbekannte Id oder keine GUID |
| `PUT` | `/api/addresses/{id}` | `200 OK` mit der geänderten Adresse | `400` ungültige Werte oder ungültiges JSON, `404` unbekannte Id oder keine GUID, `415` Body ist kein JSON |
| `PATCH` | `/api/addresses/{id}` | `200 OK` mit der geänderten Adresse | `400` ungültiges Patch-Dokument oder ungültige Werte, `404` unbekannte Id oder keine GUID, `409` `test`-Operation fehlgeschlagen, `415` Body ist kein JSON Patch |
| `DELETE` | `/api/addresses/{id}` | `204 No Content` | `404` unbekannte Id oder keine GUID |

Fehler sind immer Problem Details (`application/problem+json`), unerwartete Fehler liefern `500`.

```bash
curl -i -X POST http://localhost:5180/api/addresses \
  -H "Content-Type: application/json" \
  -d '{"firstName":"Erika","lastName":"Mustermann","street":"Hauptstrasse","houseNumber":"17","postalCode":"51147","city":"Koeln","country":"DE","email":"erika.mustermann@example.com"}'

# Adressen, deren Ort „Koeln“ (also auch „Köln“) und deren PLZ „511“ enthält
curl -i "http://localhost:5180/api/addresses?city=koeln&postalCode=511"

# alle Werte einer Adresse ersetzen; ohne "email" wird eine vorhandene E-Mail entfernt
curl -i -X PUT http://localhost:5180/api/addresses/<id> \
  -H "Content-Type: application/json" \
  -d '{"firstName":"Max","lastName":"Mustermann","street":"Hauptstrasse","houseNumber":"1","postalCode":"10115","city":"Berlin","country":"DE"}'

# einzelne Werte ändern (JSON Patch): nur wenn der Vorname noch "Max" ist, Ort ersetzen und E-Mail entfernen
curl -i -X PATCH http://localhost:5180/api/addresses/<id> \
  -H "Content-Type: application/json-patch+json" \
  -d '[{"op":"test","path":"/firstName","value":"Max"},{"op":"replace","path":"/city","value":"Hamburg"},{"op":"remove","path":"/email"}]'

# Adresse löschen
curl -i -X DELETE http://localhost:5180/api/addresses/<id>
```

## Tests und Coverage

```bash
dotnet test                                  # alle Tests
dotnet run coverage.cs                       # Tests mit Coverage, Ausgabe gesamt und pro Projekt
dotnet run coverage.cs -- --threshold 90     # zusätzlich Mindestabdeckung prüfen (wie in der CI)
```

Der HTML-Report liegt danach unter `coverage/report/index.html`. Mit `--threshold` schlägt das Skript fehl, wenn die gesamte Line-Coverage unter dem Wert liegt; solange es keinen abdeckbaren Code gibt, wird die Prüfung übersprungen.

Die Integrationstests starten den Dienst über `AddressManagementWebApplicationFactory` im Speicher. Jede Instanz der Factory verwendet eine eigene InMemory-Datenbank mit eindeutigem Namen; Tests bzw. Fixtures, die eine eigene Factory erzeugen, beeinflussen sich dadurch nicht. Eine Deklaration `public partial class Program` ist dafür nicht nötig: Seit .NET 10 erzeugt ein Source-Generator die Klasse selbst als `public`, eine eigene Deklaration meldet der Analyzer ASP0027 als überflüssig.

## CI

Der Workflow [`build-test.yml`](.github/workflows/build-test.yml) läuft bei jedem Push auf `main` und jedem Pull Request gegen `main`: Build mit Warnungen als Fehler, danach `coverage.cs` mit mindestens 90 % Line-Coverage. Die Coverage-Zusammenfassung steht in der Job-Summary, der HTML-Report liegt als Artefakt `coverage-report` bereit. Neuere Läufe desselben Branches brechen laufende ab.

## Architektur

| Projekt | Inhalt |
|---|---|
| `LKCode.AddressManagement.Service` | REST-Host: Controller, Problem Details, OpenAPI |
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
- `Address` hat keine öffentlichen Setter. Eine Adresse entsteht nur über `Address.Create(…)` und ändert sich nur über `address.Update(…)`.
- Die Entity bereinigt ihre Eingaben selbst: Strings werden getrimmt, `Country` wird in Großbuchstaben umgewandelt, eine leere E-Mail wird zu `null`. Die normalisierten Felder für die Suche und `CreatedAt`/`UpdatedAt` pflegt sie automatisch.
- Die Maximallängen stehen zentral in `AddressConstraints`. Sie sind die einzige Quelle für Validierung, EF-Konfiguration und OpenAPI.
- Schutzprüfungen (leeres Pflichtfeld, Maximallänge überschritten) werfen eine `ArgumentException`. Da Eingaben vorher validiert werden, ist eine Verletzung ein Programmierfehler und kein erwartbarer Fall. Schlägt ein Update fehl, bleibt die Adresse unverändert.
- Zeitangaben kommen ausschließlich aus dem `TimeProvider`, auch der Zeitanteil der Id (`Guid.CreateVersion7(now)`).

**Persistenz**
- Ohne die Vorgabe wäre **SQLite In-Memory** (`DataSource=:memory:`) meine Wahl: Es ist eine relationale Datenbank mit Constraints, Transaktionen und SQL-Semantik (z. B. bei Groß-/Kleinschreibung) und kommt dem Verhalten in Produktion deutlich näher. Microsoft rät selbst davon ab, den InMemory-Provider für Tests zu nutzen. Einschränkungen von SQLite: keine automatische Row-Version, `DateTimeOffset` nicht sortierbar, die Verbindung muss vor allem dauerhaft offen bleiben.
- Ids sind GUID Version 7 (zeitlich sortiert). Gegenüber zufälligen GUIDs fragmentiert ein Index weniger. Bei SQL Server bringt das wenig, da `uniqueidentifier` nach den letzten Bytes sortiert wird; PostgreSQL profitiert direkt.
- Pagination, Sortierung und Filter laufen vollständig in der Datenbank.
- Der Datenbankname kommt aus der Konfiguration (`Data:DatabaseName`) und wird beim Start geprüft; ohne Namen startet die Anwendung nicht. So bekommt jeder Test eine eigene Datenbank.
- Die Abbildung erfolgt ausschließlich per Fluent API (`AddressConfiguration`), die Maximallängen kommen aus `AddressConstraints`. `NormalizedPostalCode` ist nie länger als die PLZ, `NormalizedCity` hat dagegen keine Maximallänge, weil die Normalisierung Werte verlängern kann (z. B. ß→ss). Der InMemory-Provider erzwingt Längen nicht; die Konfiguration beschreibt das Modell trotzdem vollständig.
- `GetByIdAsync` liefert eine verfolgte Entity, damit Änderungen über `UpdateAsync` gespeichert werden. Listenabfragen laufen ohne Change-Tracking.

**Suche**
- Ort und PLZ werden zusätzlich normalisiert gespeichert. Die Suche ist eine Teilstring-Suche auf diesen Werten. „Köln“, „Koeln“ und „KOLN“ finden sich gegenseitig, auch Akzente und Sonderzeichen anderer Sprachen werden aufgelöst. Seltene Fehltreffer durch die Vereinfachung sind bewusst in Kauf genommen.
- Regeln Ort: Leerzeichen trimmen und zusammenfassen, Kleinbuchstaben, Sonderzeichen ersetzen (ß→ss, æ→ae, œ→oe, ø→o, ł→l, đ→d, þ→th, ð→d, ı→i, ħ→h, ŧ→t), Akzente entfernen, danach ae→a, oe→o, ue→u. Regeln PLZ: alle Leerzeichen entfernen, Großbuchstaben (`sw1a 1aa` → `SW1A1AA`).
- Das Repository erwartet bereits normalisierte Filterwerte. Leere Filter werden ignoriert, Ort und PLZ sind UND-verknüpft.
- Bei einer echten Datenbank kann `LIKE '%…%'` keinen Index nutzen. Für große Datenmengen wäre eine Volltextsuche sinnvoll.

**Validierung**
- Die API arbeitet mit eigenen DTOs (`CreateAddressRequest`, `UpdateAddressRequest`, `AddressResponse`, `AddressListQuery`), nie direkt mit der Entity. Das Mapping liegt in `AddressMappingExtensions`; die normalisierten Felder werden nicht ausgeliefert.
- Ablauf: erst bereinigen (`Sanitize()`: trimmen, `Country` in Großbuchstaben, leere E-Mail → `null`), dann validieren. Ein fehlendes Pflichtfeld wird beim Bereinigen zu einem leeren String und von der Validierung gemeldet.
- Die Regeln für Anlegen und Ändern stehen einmal in `AddressRequestValidator<TRequest>`; beide Requests implementieren `IAddressRequest`. Pro Feld gibt es höchstens einen Fehler, gemeldet werden aber alle fehlerhaften Felder.
- `Country` muss aus genau zwei Großbuchstaben bestehen und ein vergebener ISO-3166-1-Alpha-2-Code sein. Die 249 Codes stehen als feste Liste in `CountryCodes` und werden einmalig in ein `FrozenSet` geladen. Bewusst nicht aus `RegionInfo`/`CultureInfo` berechnet: Die Daten des Betriebssystems sind unvollständig, enthalten inoffizielle Codes (z. B. `XK`) und fehlen im Invariant-Globalization-Modus (z. B. in Chiseled-Containern) ganz.
- E-Mail: Prüfung über `MailAddress.TryCreate`, das keine Exception wirft. Die erkannte Adresse muss exakt der Eingabe entsprechen; so werden Anzeigenamen, Kommentare und Adresslisten abgelehnt. RFC-gültige Sonderformen wie `"erika mustermann"@example.com` oder Umlaute in der Domain sind erlaubt.
- Die Validatoren sind zustandslos und werden über `AddCoreAddresses()` als Singleton registriert.

**Geschäftslogik**
- `AddressService` (`IAddressService`) liegt zwischen Controller und Repository und kennt weder HTTP noch EF Core. Er erwartet bereinigte und validierte Requests; eine ungültige Eingabe ist ein Programmierfehler und führt zur `ArgumentException` der Entity.
- „Nicht gefunden“ ist ein erwartbarer Fall und wird über den Rückgabewert gemeldet: `GetByIdAsync` und `UpdateAsync` liefern `null`, `DeleteAsync` liefert `false`.
- Die Listenfilter werden vor der Suche mit `ITextNormalizer` normalisiert. Fehlende oder leere Filter werden ignoriert.
- Der Service wird über `AddCoreAddresses()` als Scoped registriert, passend zum Repository. Repository, `ITextNormalizer`, `TimeProvider` und Logging registrieren `AddData()`, `AddDomain()` und der Host.

**Service-Host**
- `Program.cs` registriert `AddDomain()`, `AddCoreAddresses()`, `AddData()` und `TimeProvider.System`. Die Controller nutzen System.Text.Json, den Standard von ASP.NET Core; Newtonsoft.Json wird nicht verwendet.
- Das OpenAPI-Dokument (`Microsoft.AspNetCore.OpenApi`) ist der Vertrag der API und in jeder Umgebung unter `/openapi/v1.json` erreichbar. Die Oberfläche Scalar gibt es nur in `Development`.
- Der Datenbankname steht in `appsettings.json` (`Data:DatabaseName`).

**API**
- Der `AddressesController` (`[ApiController]`, Route `api/addresses`) enthält keine Geschäftslogik. Er bereinigt den Request (`Sanitize()`), validiert ihn mit `IValidator<T>` und ruft dann `IAddressService` auf. Bei ungültigen Werten antwortet er mit **400** als `ValidationProblemDetails`, und zwar mit allen Feldfehlern auf einmal.
- Die Keys der Feldfehler sind die Namen im JSON (`firstName`). `ModelStateDictionaryExtensions.AddValidationErrors` wandelt sie über die Naming-Policy der JSON-Optionen um. Fehler im JSON selbst stehen unter ihrem JSON-Pfad (`$.firstName`).
- MVC behandelt nicht-nullable Properties nicht als implizite Pflichtfelder (`SuppressImplicitRequiredAttributeForNonNullableReferenceTypes`). Sonst würde MVC fehlende oder leere Felder schon vor der Action ablehnen: ohne Bereinigung, nur mit einem Teil der Fehler und mit anderen Meldungen.
- Bei ungültigem JSON lautet die Meldung nur „The input was not valid.“ (`AllowInputFormatterExceptionMessages = false`), denn die Meldungen von System.Text.Json enthalten interne Typnamen. Ein leerer Body oder `null` ergibt „A non-empty request body is required.“.
- Validierungsmeldungen sind immer englisch. Die Lokalisierung von FluentValidation ist abgeschaltet (`ValidatorOptions.Global.LanguageManager.Enabled = false`), sonst richteten sich die Standardmeldungen nach der Sprache des Servers.
- Der Request-Body von `POST` und `PUT` muss JSON sein (`[Consumes("application/json")]`), sonst kommt **415**. Wie überall in ASP.NET Core gelten auch Medientypen mit dem Suffix `+json` als JSON; das OpenAPI-Dokument nennt deshalb zusätzlich `application/*+json`. `PATCH` nimmt nur `application/json-patch+json` an (siehe **PATCH**).
- `201 Created` enthält im `Location`-Header die absolute URL der neuen Adresse. Die URL entsteht über die benannte Route `GetAddressById`, weil MVC das Suffix `Async` aus Action-Namen entfernt und `CreatedAtAction(nameof(GetByIdAsync))` deshalb keine Route fände.
- Unbekannte Ids und Werte, die keine GUID sind (Constraint `{id:guid}`), liefern **404** mit denselben Problem Details wie eine unbekannte Route.
- Die Liste (`GET /api/addresses`) hat die optionalen Query-Parameter `city` und `postalCode`. Beide suchen Teilstrings in den normalisierten Werten (siehe **Suche**): `sol`, `ingen` und `SOLin` finden `Solingen`, `Koeln` findet `Köln`. Leere Werte oder Werte nur aus Leerzeichen werden ignoriert, zwei Filter sind UND-verknüpft. Gibt es keinen Treffer, kommt **200** mit leerem Array, nicht 404.
- Die Filter werden bewusst nicht validiert. Ein Suchbegriff ist kein gespeicherter Wert, deshalb gilt die Maximallänge der Felder für ihn nicht. Beim Ort ginge das auch fachlich nicht, weil die Normalisierung Werte verlängern kann (ß→ss). Ein Filter, der zu nichts passt, liefert einfach eine leere Liste.
- Die Parameter sind einzeln an der Action deklariert (nicht als `[FromQuery] AddressListQuery`). So heißen sie im OpenAPI-Dokument `city` und `postalCode`, und `Core.Addresses` braucht keine ASP.NET-Attribute. Die Reihenfolge der Liste ist noch nicht festgelegt.
- `PUT /api/addresses/{id}` ist ein vollständiges Update: Alle Werte werden ersetzt, Bereinigung und Validierung laufen wie beim Anlegen. Fehlt die E-Mail oder ist sie leer, wird eine vorhandene E-Mail entfernt. `Id` und `CreatedAt` bleiben erhalten, `UpdatedAt` wird neu gesetzt. Die Antwort ist **200** mit der geänderten Adresse, ohne `Location`-Header.
- `PUT` legt keine Adresse an: Ids vergibt der Server (GUID Version 7), eine unbekannte Id liefert **404**.
- Prüfreihenfolge bei `PUT`: **415** (kein JSON) → **400** (ungültiges JSON) → **400** (ungültige Werte) → **404** (unbekannte Id). Der Body wird also vor der Id geprüft; ein ungültiger Body ergibt auch bei unbekannter Id **400**. 415 und Fehler im JSON entstehen in ASP.NET Core ohnehin vor der Action, und die Validierung braucht keinen Datenbankzugriff. Daran ist nicht erkennbar, ob eine Adresse existiert.
- `DELETE /api/addresses/{id}` entfernt die Adresse und antwortet mit **204** ohne Body. Danach liefern `GET`, `PUT`, `PATCH` und erneutes `DELETE` für diese Id **404** mit denselben Problem Details wie für eine nie vergebene Id.

**OpenAPI**
- Statuscodes und Content-Types stehen per `ProducesResponseType` an den Actions, **500** einmal am Controller. Die Beschreibungen kommen aus der XML-Doku der Actions (`<summary>`, `<remarks>`, `<param>`, `<response code="…">`).
- Die `operationId`s kommen aus den Routennamen der Actions: `CreateAddress`, `GetAddressById`, `ListAddresses`, `UpdateAddress`, `PatchAddress` und `DeleteAddress`. Die Antwort **204** von `DELETE` hat keinen Body und deshalb kein Schema.
- `LocationHeaderOperationTransformer` beschreibt den `Location`-Header jeder 201-Antwort, denn Attribute können keine Header beschreiben.
- `AddressSchemaTransformer` ergänzt die Schemas der Requests und von `AddressResponse` aus `AddressConstraints`: `required`, `minLength`/`maxLength`, `pattern` `^[A-Z]{2}$` für `country` sowie `format: email` und `null` als erlaubten Typ für `email`. Die Schemas beschreiben bereinigte Werte; die API nimmt auch Werte mit Leerzeichen am Rand oder `country` in Kleinbuchstaben an.
- Für jedes JSON-Patch-Dokument erzeugt ASP.NET Core dasselbe generische Schema `JsonPatchDocument`: ein Array von Operationen (`oneOf` aus `add`/`replace`/`test`, `move`/`copy` und `remove`), in dem `path` und `from` beliebige Strings und `value` beliebige Werte sind. `AddressPatchDocumentSchemaTransformer` schränkt `path` und `from` auf die JSON Pointer der acht Felder ein (`enum`) und `value` auf `string` oder `null`. Die Pointer kommen aus den JSON-Optionen, also aus derselben Quelle wie bei der Prüfung zur Laufzeit. Die Regeln der einzelnen Felder (Länge, Pattern, Format) gelten für die geänderte Adresse und stehen im Schema `UpdateAddressRequest`. Pro Feld als eigenes `oneOf` wären sie zwar ausdrückbar, das Schema würde dadurch aber unlesbar.
- `ProblemDetailsSchemaTransformer` beschreibt `ProblemDetails` und `ValidationProblemDetails` so, wie die API sie schreibt: `type`, `title`, `status` und `traceId` (bei Validierungsfehlern zusätzlich `errors`) sind immer enthalten, `status` ist eine Ganzzahl zwischen 100 und 599, `type` und `instance` sind URI-Referenzen. Ohne den Transformer fehlte `traceId`, und `status` wäre auch als String beschrieben, weil die JSON-Optionen Zahlen aus Strings lesen dürfen.
- `OpenApiDocumentTests` prüft das generierte Dokument als Ganzes: gültiges OpenAPI 3.1 (Reader von `Microsoft.OpenApi` ohne Fehler und Warnungen), genau die sechs Endpunkte, Beschreibungen, **500** und Problem Details bei jeder Fehlerantwort, Header nur bei **201** (`Location`), keine unbekannten oder unbenutzten Schemas sowie die Schemas der Problem Details. Die Details je Endpunkt und die Einschränkungen der Adress-Schemas prüft `AddressesOpenApiTests`.
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

**Concurrency**
- Die Version ist eine fortlaufende Zahl (`long`). Sie verrät die Anzahl der Änderungen eines Datensatzes; das ist bewusst in Kauf genommen, da der Datensatz ohnehin gelesen werden darf.
- Die Version wird als strong ETag ausgeliefert. `If-Match` vergleicht laut RFC 9110 immer strong, weak ETags passen daher nie.
- `If-Match` ist bei PUT, PATCH und DELETE Pflicht (428, RFC 6585). `If-Match: *` ist RFC-konform erlaubt und ändert ohne Versionsprüfung.
- Prüfreihenfolge: 404 → 428 → 412 → 400.
- EF Core InMemory erzeugt keine Version automatisch, sie wird in der Domain hochgezählt.

**PATCH**
- JSON Patch (RFC 6902) statt JSON Merge Patch: eindeutiges Format für generierte Clients und offizielle Unterstützung mit System.Text.Json.
- Ablauf: Patch-Dokument prüfen → aktuelle Adresse laden → als `UpdateAddressRequest` abbilden (`ToUpdateRequest()`) → Operationen anwenden → bereinigen, validieren und speichern auf demselben Weg wie bei `PUT`. Es gibt keine eigene Update-Logik für `PATCH`. Die Operationen ändern nur diese Kopie, deshalb wirken alle zusammen oder keine.
- Unterstützt werden alle sechs Operationen: `add`, `remove`, `replace`, `move`, `copy` und `test`. Da eine Adresse feste Felder hat, setzen `add` und `replace` beide den Wert eines Felds, und `remove` setzt ihn auf `null`. Bei Pflichtfeldern führt das zu **400** mit dem Feldfehler, bei `email` wird die E-Mail entfernt.
- Die Operationen werden vor dem Anwenden streng geprüft (`AddressPatchDocumentExtensions.ValidateOperations`), denn die Bibliothek ist an mehreren Stellen nachsichtig oder fehlerhaft: Ein fehlendes `op` behandelt sie als `add`, Groß-/Kleinschreibung bei `op` und in Pfaden ignoriert sie, ein fehlendes `path` oder `from` führt zu einer `ArgumentNullException` (also **500**), und bei `test` unterscheidet sie einen unbekannten Pfad nicht von einem abweichenden Wert. Erlaubt sind deshalb nur die sechs Operationen in Kleinbuchstaben, als Pfade nur die JSON Pointer der acht Felder mit exaktem Namen (`/firstName`, nicht `/FirstName`, RFC 6901 ist case-sensitiv) und als `value` nur ein String oder `null`. Alle Fehler kommen zusammen als **400**, jeweils unter dem JSON-Pfad im Patch-Dokument (`$[0].path`).
- Ein fehlendes `value` kann die Bibliothek nicht von `null` unterscheiden; es wird wie `null` behandelt. Das OpenAPI-Schema verlangt `value` trotzdem, wie RFC 6902.
- `test` vergleicht exakt, also ohne Bereinigung und mit Groß-/Kleinschreibung. Schlägt ein Test fehl, antwortet die API mit **409** (RFC 5789: Der Patch passt nicht zum Zustand der Ressource), und `detail` nennt Index und Pfad der Operation. Die Bibliothek wendet nach einem fehlgeschlagenen Test die weiteren Operationen trotzdem an. Das schadet nicht, weil die Kopie dann verworfen wird.
- Prüfreihenfolge: **415** (kein JSON Patch) → **400** (ungültiges JSON) → **400** (ungültige Operationen) → **404** (unbekannte Id) → **409** (Test fehlgeschlagen) → **400** (ungültige Werte). Wie bei `PUT` wird der Body vor der Id geprüft, soweit das ohne die Adresse möglich ist.
- Ein leeres Patch-Dokument `[]` ist gültig und wirkt wie `PUT` mit den aktuellen Werten; `UpdatedAt` wird neu gesetzt.

**Caching**
- Serverseitiges Output Caching mit Invalidierung per Tag bei jeder Änderung. Der Cache liegt im Speicher der jeweiligen Instanz; bei mehreren Instanzen wäre ein verteilter Store (z. B. Redis) nötig.

**Logging**
- Strukturiertes Logging über `ILogger` mit Message-Templates. Die Log-Methoden erzeugt der Source-Generator `[LoggerMessage]`: Die Templates werden beim Build geprüft, und die Argumente werden nur verarbeitet, wenn das Level aktiv ist.
- Geloggt werden nur Ids, Anzahlen und normalisierte Filter, keine personenbezogenen Daten wie Namen oder E-Mail-Adressen. Änderungen (Anlegen, Ändern, Löschen) laufen auf `Information`, Lesezugriffe und „nicht gefunden“ auf `Debug`.
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