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

## Tests und Coverage

```bash
dotnet test                                  # alle Tests
dotnet run coverage.cs                       # Tests mit Coverage, Ausgabe gesamt und pro Projekt
dotnet run coverage.cs -- --threshold 90     # zusätzlich Mindestabdeckung prüfen (wie in der CI)
```

Der HTML-Report liegt danach unter `coverage/report/index.html`. Mit `--threshold` schlägt das Skript fehl, wenn die gesamte Line-Coverage unter dem Wert liegt; solange es keinen abdeckbaren Code gibt, wird die Prüfung übersprungen.

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
| System.Text.Json | Standard in .NET, durchgehend für JSON und JSON Patch |
| Microsoft.AspNetCore.OpenApi + Scalar | offizieller OpenAPI-Weg seit .NET 9, moderne Oberfläche |
| Serilog | strukturiertes Logging |
| NUnit, AwesomeAssertions, NSubstitute | Unit- und Integrationstests |
| coverlet + ReportGenerator | Coverage-Messung und Reports |
| GitHub Actions | Build, Tests und Coverage-Prüfung bei jedem Push und Pull Request |

## Pakete und Lizenzen

Verwendet werden nur Pakete mit permissiven Lizenzen. Microsoft-Pakete sind nicht aufgeführt.

| Paket | Lizenz | Verwendung |
|---|---|---|
| FluentValidation | Apache-2.0 | Validierung der Requests |
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

**Fehlerbehandlung**
- Fehler werden als Problem Details nach RFC 7807 (bzw. Nachfolger RFC 9457) zurückgegeben.
- Erwartbare Fälle (nicht gefunden, ungültige Eingabe) laufen über Rückgabewerte, Exceptions nur bei unerwarteten Fehlern.
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

**Caching**
- Serverseitiges Output Caching mit Invalidierung per Tag bei jeder Änderung. Der Cache liegt im Speicher der jeweiligen Instanz; bei mehreren Instanzen wäre ein verteilter Store (z. B. Redis) nötig.

**Logging**
- Strukturiertes Logging über `ILogger` mit Message-Templates. Die Log-Methoden erzeugt der Source-Generator `[LoggerMessage]`: Die Templates werden beim Build geprüft, und die Argumente werden nur verarbeitet, wenn das Level aktiv ist.
- Geloggt werden nur Ids, Anzahlen und normalisierte Filter, keine personenbezogenen Daten wie Namen oder E-Mail-Adressen. Änderungen (Anlegen, Ändern, Löschen) laufen auf `Information`, Lesezugriffe und „nicht gefunden“ auf `Debug`.
- Serilog schreibt aktuell nur in die Konsole. Langfristig ist die Ausgabe in Dateien oder zentrale Dienste (z. B. Seq, Elasticsearch, Application Insights) sinnvoll; dafür ist nur Konfiguration nötig.

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