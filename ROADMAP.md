# Roadmap

Umsetzung in Meilensteinen. Jeder Meilenstein wird auf einem eigenen Branch `AM-{n}` umgesetzt und per Pull Request in `main` gemergt. Alle Regeln stehen in `CLAUDE.md`.

**Übersicht**

| Teil | Meilensteine |
|---|---|
| Pflicht | AM-1 bis AM-12 |
| Bonus | AM-13 bis AM-18 |
| Optional | AM-19 |

---

## Pflicht

### AM-1 – Repository-Grundgerüst

Legt Solution, alle Projekte mit ihren Abhängigkeiten und die zentralen Build-Dateien an. Danach baut das Repo, und das Coverage-Skript läuft.

```text
Setze Meilenstein AM-1 laut ROADMAP.md um und halte dich an CLAUDE.md.

Voraussetzung: main enthält README.md, ROADMAP.md, CLAUDE.md und .editorconfig.

Aufgaben:
- Solution AddressManagement.slnx im Root.
- Projekte unter src/: LKCode.AddressManagement.Service (ASP.NET Core Web API mit Controllern, minimale Program.cs), .Core.Addresses, .Domain, .Data (Class Libraries).
- Projekte unter tests/: LKCode.AddressManagement.UnitTests (Unterordner Core.Addresses/, Domain/, Data/, Service/), LKCode.AddressManagement.IntegrationTests.
- Projektreferenzen exakt nach der Architektur in CLAUDE.md.
- global.json: .NET 10 SDK festlegen, Microsoft.Testing.Platform als Test-Runner für `dotnet test`.
- Directory.Build.props: net10.0, Nullable, ImplicitUsings, TreatWarningsAsErrors, aktuelle Analyzer, Code-Style im Build erzwingen, XML-Doku für src-Projekte.
- Directory.Packages.props: Central Package Management.
- Testprojekte: NUnit (mit Microsoft.Testing.Platform), AwesomeAssertions, NSubstitute, coverlet.MTP; IntegrationTests zusätzlich Microsoft.AspNetCore.Mvc.Testing.
- .gitignore (Vorlage `dotnet new gitignore`), .gitattributes mit `* text=auto eol=lf` (LF für alle Textdateien, passend zur .editorconfig).
- .config/dotnet-tools.json mit ReportGenerator.
- coverage.cs im Root als .NET File-based App: führt alle Tests mit Coverage aus, erzeugt mit ReportGenerator einen HTML-Report (coverage/report/) und gibt in der Konsole die Gesamtabdeckung und die Abdeckung pro Projekt (Service, Core.Addresses, Domain, Data) in Prozent aus. Testprojekte und Program.cs werden aus der Coverage ausgeschlossen.
- `dotnet test` und `dotnet run coverage.cs` dürfen nicht fehlschlagen, solange es noch keine Tests gibt. Keine Dummy-Tests.

Prüfe am Ende: `dotnet tool restore`, `dotnet build`, `dotnet test`, `dotnet run coverage.cs` laufen fehlerfrei.
```

### AM-2 – CI: Build und Tests

GitHub-Actions-Workflow, der bei jedem Push auf `main` und jedem Pull Request baut, testet und die Mindestabdeckung von 90 % erzwingt.

```text
Setze Meilenstein AM-2 laut ROADMAP.md um und halte dich an CLAUDE.md.

Aufgaben:
- Workflow .github/workflows/build-test.yml, Trigger: Push auf main und Pull Requests gegen main.
- Schritte: .NET SDK laut global.json einrichten, `dotnet tool restore`, Restore, Build (Warnungen als Fehler), Tests mit Coverage.
- Mindestabdeckung 90 % Line-Coverage erzwingen; bei Unterschreitung schlägt der Workflow fehl. Nutze die Threshold-Funktion von coverlet.MTP, falls vorhanden, sonst werte den Cobertura-Report in einem Workflow-Schritt aus. Solange es noch keinen abdeckbaren Code gibt, darf der Workflow nicht fehlschlagen.
- Coverage-Zusammenfassung mit ReportGenerator in die Job-Summary schreiben, HTML-Report als Artefakt hochladen.
- Parallele Läufe desselben Branches abbrechen (concurrency group).
- Prüfe, dass der Status-Badge in der README auf diesen Workflow zeigt.
```

### AM-3 – Domain: TextNormalizer

Normalisierung von Ort und PLZ für die Suche. „Köln“, „Koeln“ und „KOLN“ ergeben denselben Wert.

```text
Setze Meilenstein AM-3 laut ROADMAP.md um und halte dich an CLAUDE.md. Arbeite strikt nach TDD.

Aufgaben:
- Interface ITextNormalizer in Domain/Contracts, Implementierung TextNormalizer in Domain/Normalizers.
- Normalize(string) für Texte (z. B. Ort):
  1. trimmen, mehrfache Leerzeichen zu einem zusammenfassen
  2. Kleinbuchstaben (invariant)
  3. Sondertabelle: ß→ss, æ→ae, œ→oe, ø→o, ł→l, đ→d, þ→th (bei Bedarf erweitern)
  4. Akzente entfernen (Unicode-Zerlegung, diakritische Zeichen entfernen), z. B. ä→a, é→e, å→a
  5. danach ae→a, oe→o, ue→u, damit „Köln“, „Koeln“ und „Koln“ denselben Wert ergeben
- NormalizePostalCode(string): trimmen, alle Leerzeichen entfernen, Großbuchstaben („sw1a 1aa“ → „SW1A1AA“).
- null als Eingabe → ArgumentNullException. Leerer String → leerer String.
- Registrierung als Singleton über services.AddDomain() in Domain/ServiceCollectionExtensions.cs.
- Tests in UnitTests/Domain: viele Fälle (deutsche Umlaute, skandinavische, französische, polnische Zeichen, ß, Groß/Klein, Leerzeichen, Mischformen) und gezielte Negativfälle.
```

### AM-4 – Domain: Entity Address

Das Fachmodell mit seinen Regeln. Eine ungültige Adresse kann nicht entstehen, normalisierte Felder und Zeitstempel werden automatisch gepflegt.

```text
Setze Meilenstein AM-4 laut ROADMAP.md um und halte dich an CLAUDE.md. Arbeite strikt nach TDD.

Aufgaben:
- Domain/Addresses/AddressConstraints.cs: Maximallängen als Konstanten (FirstName 100, LastName 100, Street 200, HouseNumber 20, PostalCode 20, City 100, Country 2, Email 254). Diese Konstanten sind die einzige Quelle für Validierung, EF-Konfiguration und OpenAPI.
- Domain/Addresses/Address.cs, keine öffentlichen Setter:
  - Felder: Id (Guid), FirstName, LastName, Street, HouseNumber, PostalCode, City, Country, Email (optional), NormalizedCity, NormalizedPostalCode, CreatedAt, UpdatedAt (DateTimeOffset).
  - Address.Create(…, ITextNormalizer, TimeProvider): Id per Guid.CreateVersion7(), setzt CreatedAt/UpdatedAt und die normalisierten Felder.
  - address.Update(…, ITextNormalizer, TimeProvider): ändert alle Felder, aktualisiert normalisierte Felder und UpdatedAt.
  - Eingaben trimmen, Country in Großbuchstaben, leere E-Mail → null.
  - Schutzprüfungen: leeres Pflichtfeld oder Überschreitung der Maximallänge → ArgumentException (Programmierfehler, da vorher validiert wird).
  - Privater parameterloser Konstruktor für EF Core.
- Tests in UnitTests/Domain, Zeit über FakeTimeProvider (Microsoft.Extensions.TimeProvider.Testing).
```

### AM-5 – Data: DbContext und Repository

Persistenz mit EF Core InMemory. Das Repository kapselt alle Datenzugriffe, die Suche läuft über die normalisierten Spalten.

```text
Setze Meilenstein AM-5 laut ROADMAP.md um und halte dich an CLAUDE.md. Arbeite strikt nach TDD.

Aufgaben:
- Interface IAddressRepository in Core.Addresses/Contracts (Clean Architecture: Core definiert, Data implementiert).
  - GetByIdAsync(Guid) → Address?
  - ListAsync(normalizedCity?, normalizedPostalCode?) → IReadOnlyList<Address> (Contains-Suche, leere Filter werden ignoriert)
  - AddAsync, UpdateAsync, DeleteAsync (jeweils mit Speichern)
- Data/AddressDbContext.cs mit DbSet<Address>.
- Data/Configurations/AddressConfiguration.cs (Fluent API): Schlüssel, Pflichtfelder, Maximallängen aus AddressConstraints, Indizes auf NormalizedCity und NormalizedPostalCode.
- Data/Repositories/AddressRepository.cs.
- services.AddData(…) in Data/ServiceCollectionExtensions.cs, Datenbankname konfigurierbar.
- Tests in UnitTests/Data gegen InMemory, jeder Test mit eigener Datenbank: Speichern, Laden, Ändern, Löschen, Filter (Teilstring, Groß/Klein, „Koeln“ findet „Köln“, PLZ mit Leerzeichen), unbekannte Id → null.
```

### AM-6 – Core.Addresses: DTOs, Mapping, Validierung

Die Datenverträge der API, getrennt von der Entity, mit Eingabebereinigung und vollständiger Validierung über FluentValidation.

```text
Setze Meilenstein AM-6 laut ROADMAP.md um und halte dich an CLAUDE.md. Arbeite strikt nach TDD.

Aufgaben:
- DTOs in Core.Addresses/Dtos (records): CreateAddressRequest, UpdateAddressRequest, AddressResponse (Id, alle Felder, CreatedAt, UpdatedAt; keine normalisierten Felder), AddressListQuery (City, PostalCode).
- Bereinigung als Extension-Methoden: trimmen, Country in Großbuchstaben, leere E-Mail → null.
- Mapping als Extension-Methoden in Core.Addresses/Mappings/AddressMappingExtensions.cs.
- Validatoren in Core.Addresses/Validators (FluentValidation), gemeinsame Regeln für Create und Update nicht duplizieren:
  - Pflichtfelder nicht leer, Maximallängen aus AddressConstraints
  - Country: genau zwei Großbuchstaben und gültiger ISO-3166-1-Alpha-2-Code; Prüfung ohne Exceptions (z. B. einmalig berechnete Menge gültiger Codes)
  - Email: optional, wenn vorhanden gültiges Format
- services.AddCoreAddresses() in Core.Addresses/ServiceCollectionExtensions.cs, registriert die Validatoren.
- Tests in UnitTests/Core.Addresses: jede Regel einzeln positiv und negativ (u. a. Country „GER“, „D“, „de1“, „XX“, Maximallänge +1, nur Leerzeichen, ungültige E-Mails), Mapping und Bereinigung.
```

### AM-7 – Core.Addresses: AddressService

Die Geschäftslogik zwischen Controller und Repository. Erwartbare Fälle wie „nicht gefunden“ werden über Rückgabewerte gemeldet, nicht über Exceptions.

```text
Setze Meilenstein AM-7 laut ROADMAP.md um und halte dich an CLAUDE.md. Arbeite strikt nach TDD.

Aufgaben:
- Interface IAddressService in Core.Addresses/Contracts, Implementierung Core.Addresses/AddressService.cs.
  - GetByIdAsync(id) → AddressResponse? (null = nicht gefunden)
  - GetListAsync(AddressListQuery) → IReadOnlyList<AddressResponse> (Filter vorher normalisieren)
  - CreateAsync(CreateAddressRequest) → AddressResponse
  - UpdateAsync(id, UpdateAddressRequest) → AddressResponse? (null = nicht gefunden)
  - DeleteAsync(id) → bool (false = nicht gefunden)
- Der Service erwartet bereinigte und validierte Eingaben.
- Strukturiertes Logging über ILogger mit Message-Templates.
- Registrierung in AddCoreAddresses().
- Tests in UnitTests/Core.Addresses mit NSubstitute für Repository, Normalizer und TimeProvider: alle Methoden, alle Pfade, Weitergabe des CancellationToken.
```

### AM-8 – Service-Host: Infrastruktur

Der lauffähige REST-Host mit Problem Details, globalem Fehler-Handling, OpenAPI mit Scalar und strukturiertem Logging über Serilog.

```text
Setze Meilenstein AM-8 laut ROADMAP.md um und halte dich an CLAUDE.md. Arbeite strikt nach TDD.

Aufgaben:
- Program.cs: AddDomain(), AddCoreAddresses(), AddData(), TimeProvider.System, Controller mit System.Text.Json.
- AddProblemDetails() und UseStatusCodePages(), damit auch leere Fehlerantworten Problem Details liefern.
- Service/ExceptionHandlers/ProblemDetailsExceptionHandler.cs (IExceptionHandler): unerwartete Exceptions → 500 mit Problem Details und traceId, ohne Stacktrace nach außen; Client-Abbruch (OperationCanceledException) → Information-Log, kein 500.
- OpenAPI über Microsoft.AspNetCore.OpenApi, UI über Scalar (in Development).
- Serilog: nur Konsole, strukturiert, Request-Logging.
- `public partial class Program` für WebApplicationFactory.
- IntegrationTests: eigene WebApplicationFactory mit eindeutiger InMemory-Datenbank pro Test bzw. Fixture.
- Tests: Exception-Handler (Unit), OpenAPI-Dokument erreichbar, unbekannte Route → 404 mit Problem Details.

Prüfe am Ende: `dotnet run --project src/LKCode.AddressManagement.Service` startet, Scalar ist erreichbar.
```

### AM-9 – Endpunkte: POST und GET by Id

Adressen anlegen und einzeln abrufen, mit `Location`-Header, Validierungsfehlern als 400 und 404 für unbekannte Ids.

```text
Setze Meilenstein AM-9 laut ROADMAP.md um und halte dich an CLAUDE.md. Arbeite strikt nach TDD.

Aufgaben:
- Service/Controllers/AddressesController.cs, Route api/addresses, [ApiController].
- POST /api/addresses: bereinigen, mit IValidator validieren (Fehler → ValidationProblem 400), anlegen → 201 Created mit Location-Header und AddressResponse.
- GET /api/addresses/{id:guid}: 200 mit AddressResponse oder 404 mit Problem Details.
- OpenAPI vollständig: alle Statuscodes inkl. Problem-Details-Schemas, Location-Header, Schema-Einschränkungen (required, maxLength, pattern ^[A-Z]{2}$ für Country, format uuid und email, nullable) aus AddressConstraints, z. B. über einen Schema-Transformer. Keine zusätzliche Bibliothek ohne Rückfrage.
- Integrationstests für alle Fälle, inkl. Negativfälle: fehlende Pflichtfelder, zu lange Werte, ungültiges Country, ungültige E-Mail, ungültiges JSON, ungültige GUID in der Route, unbekannte Id.
```

### AM-10 – Endpunkt: GET Liste mit Filter

Listenabruf mit optionalen Filtern nach Ort und PLZ als Teilstring-Suche auf den normalisierten Werten.

```text
Setze Meilenstein AM-10 laut ROADMAP.md um und halte dich an CLAUDE.md. Arbeite strikt nach TDD.

Aufgaben:
- GET /api/addresses?city=&postalCode=: 200 mit Liste, leere Liste ist kein Fehler.
- Filter optional, kombinierbar, Teilstring-Suche auf den normalisierten Spalten (z. B. „sol“, „ingen“, „SOLin“ finden „Solingen“; „Koeln“ findet „Köln“).
- OpenAPI vollständig inkl. Query-Parameter.
- Integrationstests: ohne Filter, je ein Filter, beide Filter, Groß/Klein, Umlaute, Teilstrings, keine Treffer, leere Filterwerte.
```

### AM-11 – Endpunkte: PUT und DELETE

Vollständiges Update und Löschen, jeweils mit 404 für unbekannte Ids.

```text
Setze Meilenstein AM-11 laut ROADMAP.md um und halte dich an CLAUDE.md. Arbeite strikt nach TDD.

Aufgaben:
- PUT /api/addresses/{id:guid}: bereinigen, validieren (400), vollständiges Update → 200 mit AddressResponse, unbekannte Id → 404.
- DELETE /api/addresses/{id:guid}: 204 No Content, unbekannte Id → 404.
- OpenAPI vollständig.
- Integrationstests inkl. Negativfällen (ungültiger Body, unbekannte Id, ungültige GUID, Löschen und danach GET → 404, doppeltes Löschen → 404).
```

### AM-12 – Abschluss Pflichtteil

Prüft die OpenAPI-Definition automatisiert, vervollständigt die README und stellt sicher, dass der Pflichtteil vollständig und sauber ist.

```text
Setze Meilenstein AM-12 laut ROADMAP.md um und halte dich an CLAUDE.md.

Aufgaben:
- Integrationstest, der das generierte OpenAPI-Dokument prüft: alle Endpunkte, alle Statuscodes, Header, Problem-Details-Schemas, Schema-Einschränkungen.
- Review gegen die Aufgabenstellung (Pflichtteil) und gegen CLAUDE.md. Abweichungen kurz melden, bevor du etwas änderst.
- README.md mit der tatsächlichen Umsetzung abgleichen.
- Definition of Done prüfen: keine Warnungen, alle Tests grün, Coverage ≥ 90 %.
```

---

## Bonus

Bereits im Pflichtteil umgesetzt:

| Bonuspunkt | Umgesetzt in |
|---|---|
| Strikte Trennung zwischen Entity und DTO inkl. Mapping | AM-6 |
| Swagger/OpenAPI-Dokumentation | AM-8 bis AM-12 |
| Strukturierte Logging-Implementierung | AM-7, AM-8 |

### AM-13 – Partial Updates via PATCH

Teilweise Änderungen per JSON Patch (RFC 6902) über das offizielle System.Text.Json-Paket von Microsoft.

```text
Setze Meilenstein AM-13 laut ROADMAP.md um und halte dich an CLAUDE.md. Arbeite strikt nach TDD.

Aufgaben:
- PATCH /api/addresses/{id:guid} mit Content-Type application/json-patch+json über Microsoft.AspNetCore.JsonPatch.SystemTextJson.
- Ablauf: aktuelle Adresse laden → als UpdateAddressRequest abbilden → Patch anwenden → bereinigen → validieren → bestehenden Update-Pfad nutzen. Keine doppelte Update-Logik.
- Statuscodes: 200 mit AddressResponse; 404 unbekannte Id; 400 ungültiges Patch-Dokument oder ungültige Pfade/Operationen; 400 ValidationProblem, wenn das Ergebnis ungültig ist (z. B. Pflichtfeld entfernt); 409 bei fehlschlagender `test`-Operation (RFC 5789); 415 bei falschem Content-Type.
- OpenAPI vollständig, inkl. korrektem Schema für das Patch-Dokument (prüfe die automatische Generierung, ergänze bei Bedarf per Transformer).
- Integrationstests für alle Operationen (replace, add, remove, test) und alle Fehlerfälle.
```

### AM-14 – Optimistic Concurrency (RowVersion)

Schutz vor Lost Updates über eine Versionsnummer, REST-konform als `ETag` und `If-Match` (RFC 9110, RFC 6585).

```text
Setze Meilenstein AM-14 laut ROADMAP.md um und halte dich an CLAUDE.md. Arbeite strikt nach TDD.

Aufgaben:
- Address erhält Version (long), Start 1, jede Änderung erhöht sie in der Domain. Nicht in Request-DTOs.
- EF-Konfiguration: Version als Concurrency-Token. Das Repository setzt beim Speichern den Original-Wert auf die vom Client erwartete Version. DbUpdateConcurrencyException wird im Repository abgefangen und als erwartbares Ergebnis zurückgegeben.
- Kleiner Ergebnistyp ohne Bibliothek (z. B. enum Success/NotFound/VersionMismatch plus Wert) für Update, Patch und Delete.
- Strong ETag (`"3"`) in den Antworten von GET by Id, POST, PUT und PATCH.
- If-Match Pflicht bei PUT, PATCH und DELETE. Prüfreihenfolge laut RFC 9110: 404 (unbekannt) → 428 (If-Match fehlt) → 412 (Version passt nicht) → 400 (Body ungültig).
- If-Match: `*` ist erlaubt und bedeutet „ändern, wenn vorhanden“ ohne Versionsprüfung. Weak ETags passen nie (strong comparison) → 412. Syntaktisch ungültiger Header → 400.
- OpenAPI vollständig: If-Match als Pflicht-Header, ETag als Response-Header, 412 und 428.
- Integrationstests für alle Fälle, inkl. echter Gleichzeitigkeit (zwei Updates mit derselben Version), die beweist, dass EF InMemory den Token prüft.
```

### AM-15 – Soft-Delete

Löschen markiert Adressen nur als gelöscht. Nach außen verhalten sie sich wie nie vorhanden.

```text
Setze Meilenstein AM-15 laut ROADMAP.md um und halte dich an CLAUDE.md. Arbeite strikt nach TDD.

Aufgaben:
- Address erhält IsDeleted und DeletedAt; Methode Delete(TimeProvider) in der Domain, erhöht die Version.
- Global Query Filter in der EF-Konfiguration.
- DELETE markiert nur noch. Gelöschte Adressen liefern überall 404 (GET, PUT, PATCH, DELETE) und erscheinen nicht in Listen. Es darf nicht erkennbar sein, dass es sie gab.
- Tests in allen Ebenen, inkl. erneutem Löschen, Ändern nach Löschen und Listen mit gemischten Daten.
```

### AM-16 – Pagination und Sortierung

Seitenweiser Abruf mit Sortierung, vollständig in der Datenbank ausgeführt.

```text
Setze Meilenstein AM-16 laut ROADMAP.md um und halte dich an CLAUDE.md. Arbeite strikt nach TDD.

Aufgaben:
- Query-Parameter: page (Standard 1, ≥ 1), pageSize (Standard 20, 1–100), sortBy (lastName [Standard], firstName, city, postalCode, country, createdAt, updatedAt), sortDirection (asc [Standard], desc). Kombinierbar mit den Filtern.
- Ungültige Werte → 400 ValidationProblem.
- Sortieren, Überspringen und Begrenzen auf IQueryable im Repository, Gesamtanzahl als eigene Abfrage. Es werden nie alle Datensätze geladen.
- Stabile Sortierung: bei gleichen Werten zusätzlich nach Id.
- Antwort: { items, page, pageSize, totalCount, totalPages }.
- OpenAPI vollständig (Enums, Min/Max, Standardwerte).
- Tests: Grenzwerte, leere Seiten, Seite hinter dem Ende, jede Sortierung in beide Richtungen, Kombination mit Filtern, Negativfälle.
```

### AM-17 – Caching-Konzept

Serverseitiges Output Caching mit gezielter Invalidierung bei Änderungen.

```text
Setze Meilenstein AM-17 laut ROADMAP.md um und halte dich an CLAUDE.md. Arbeite strikt nach TDD.

Aufgaben:
- ASP.NET Core Output Caching für GET by Id (Tag `address-{id}`) und GET Liste (Tag `addresses`, Variation nach allen Query-Parametern).
- POST, PUT, PATCH und DELETE invalidieren die betroffenen Tags.
- Ablaufzeit als Absicherung, konfigurierbar (Standard 5 Minuten).
- Middleware so platzieren, dass Authentifizierung und Autorisierung (AM-18) vor dem Cache greifen.
- Integrationstests: Antwort wird gecacht, nach Änderungen kommt der neue Stand, Fehlerantworten werden nicht gecacht.
- README: Hinweis, dass der Cache pro Instanz im Speicher liegt und bei mehreren Instanzen ein verteilter Store nötig wäre.
```

### AM-18 – Authentifizierung und Autorisierung per JWT

JWT-Bearer-Authentifizierung mit Scopes für Lesen und Schreiben. Entwicklungs-Tokens kommen aus `dotnet user-jwts`.

```text
Setze Meilenstein AM-18 laut ROADMAP.md um und halte dich an CLAUDE.md. Arbeite strikt nach TDD.

Aufgaben:
- Microsoft.AspNetCore.Authentication.JwtBearer, Konfiguration aus Authentication:Schemes:Bearer (kompatibel mit `dotnet user-jwts`).
- Policies: AddressesRead (Scope addresses:read) für GET, AddressesWrite (Scope addresses:write) für POST, PUT, PATCH, DELETE. Scope-Claims sowohl einzeln als auch leerzeichengetrennt unterstützen.
- 401 ohne oder mit ungültigem Token, 403 bei fehlendem Scope, jeweils als Problem Details.
- Output Caching (AM-17) muss für authentifizierte Requests funktionieren; Autorisierung greift vor dem Cache.
- OpenAPI: Bearer-Security-Schema und Anforderungen pro Endpunkt inkl. 401/403. In Scalar ist der Token eintragbar.
- Integrationstests erzeugen Tokens mit einem Test-Schlüssel. Bestehende Tests anpassen. Neue Tests: kein Token, abgelaufen, falsche Signatur, falscher Issuer/Audience, fehlender Scope, nur Lesen, nur Schreiben.
- README: Abschnitt „Authentifizierung“ mit den Befehlen prüfen und ergänzen.
```

---

## Optional

### AM-19 – Docker-Image und Bereitstellung

Das Image wird ohne Dockerfile über das .NET SDK gebaut und nach erfolgreichem Build und Test automatisch in der GitHub Container Registry veröffentlicht.

```text
Setze Meilenstein AM-19 laut ROADMAP.md um und halte dich an CLAUDE.md.

Aufgaben:
- Container-Einstellungen in der Service-.csproj für `dotnet publish -t:PublishContainer` (Chiseled-Image, Non-Root, Repository ghcr.io/lk-code/buhl-app).
- Workflow .github/workflows/publish-container.yml:
  - startet nur nach erfolgreichem build-test.yml (workflow_run, conclusion success), nur für Pushes auf main und Tags v*, nicht für Pull Requests
  - checkt exakt den getesteten Commit aus (head_sha), führt keine Tests erneut aus
  - pusht nach ghcr.io mit GITHUB_TOKEN: bei main die Tags latest und Commit-SHA; bei Tag vX.Y.Z zusätzlich X.Y.Z und ein GitHub Release mit Link zum Image
- build-test.yml so erweitern, dass es auch für Tags v* läuft.
- Dokumentieren, wie JWT im Container konfiguriert wird (Umgebungsvariablen).
- README: Abschnitt „Docker“ mit docker pull und docker run prüfen und ergänzen.

Prüfe am Ende lokal: Image bauen, Container starten, API antwortet.
```