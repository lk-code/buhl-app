# CLAUDE.md – Regeln für dieses Repository

Dieses Dokument ist verbindlich. Bei Widersprüchen zwischen einem Prompt und diesem Dokument: nachfragen.

## Projekt

REST-API zur Verwaltung von Adressen (`LKCode.AddressManagement`), ASP.NET Core auf .NET 10, EF Core InMemory als Persistenz. Umsetzung erfolgt in Meilensteinen laut `ROADMAP.md`.

## Arbeitsweise

- Pro Meilenstein ein eigener Branch `AM-{n}` (z. B. `AM-3`), abgezweigt vom aktuellen `main`.
- Vor dem Start eines Meilensteins prüfen: vorheriger Meilenstein ist in `main` gemergt, `main` ist aktuell, Arbeitsverzeichnis ist sauber. Sonst abbrechen und kurz Bescheid geben.
- Nur den aktuellen Meilenstein umsetzen. Keine Vorgriffe auf spätere Meilensteine.
- Unklarheiten werden in der Planungsphase geklärt. Taucht während der Umsetzung trotzdem eine Unklarheit auf: stoppen und fragen, keine Annahmen treffen. Das gilt besonders für Architekturentscheidungen.
- Committen ist erlaubt, **pushen nie**. Push, Pull Request und Merge macht der Entwickler.
- Nach Abschluss eines Meilensteins kurz im Chat Bescheid geben. `ROADMAP.md` wird nicht verändert.

### Commits

- Format: `AM-{n}: {message}`, Nachricht auf Englisch, klein geschrieben, im Imperativ.
  - Beispiel: `AM-4: add tests for address entity`
- TDD muss in der Git-Historie sichtbar sein: erst ein Commit mit dem fehlschlagenden Test (rot), dann ein Commit mit der Implementierung (grün), optional ein Refactoring-Commit.

### Definition of Done (pro Meilenstein)

- Build ohne Warnungen
- Alle Tests grün
- Line-Coverage ≥ 90 % (`dotnet run coverage.cs`)
- `README.md` aktualisiert, falls der Meilenstein etwas Relevantes ändert
- Keine auskommentierten Code-Reste, keine TODOs

## Architektur

```
src/
  LKCode.AddressManagement.Service       REST-Host: Controller, Program.cs, Exception-Handler, OpenAPI
  LKCode.AddressManagement.Core.Addresses   Geschäftslogik Adressen: AddressService, DTOs, Validatoren, Mapping, Interfaces (inkl. IAddressRepository)
  LKCode.AddressManagement.Domain           Entity Address, TextNormalizer, Domain-Logik
  LKCode.AddressManagement.Data             EF Core: DbContext, Konfiguration, Repository-Implementierung
tests/
  LKCode.AddressManagement.UnitTests        Unterordner pro Projekt: Core.Addresses/, Domain/, Data/, Service/
  LKCode.AddressManagement.IntegrationTests WebApplicationFactory
```

Geschäftslogik liegt in Projekten `LKCode.AddressManagement.Core.{Aufgabe}`, ein Projekt pro Aufgabenbereich (z. B. `.Core.Addresses`, später etwa `.Core.Auth` oder `.Core.Calculations`). Ein neues `.Core.*`-Projekt wird nur nach Rückfrage angelegt und nur, wenn es echte Geschäftslogik enthält. Reine Host-Konfiguration (z. B. JWT-Setup, Policies) bleibt in `.Service`.

Abhängigkeiten nur in diese Richtung:
- `Service` → `Core.*`, `Data` (`Data` nur für die DI-Registrierung)
- `Core.*` → `Domain`
- `Data` → `Core.*` (nur für die Repository-Interfaces), `Domain`
- `Domain` → nichts

`Core.*`-Projekte haben keine Abhängigkeit zu EF Core. Die Domain enthält keine EF-Attribute; die Abbildung erfolgt ausschließlich per Fluent API (`IEntityTypeConfiguration`).

## Code-Konventionen

- Code, Kommentare, Commit-Messages und Texte in Problem Details: **Englisch**. `README.md`, `ROADMAP.md`, `CLAUDE.md` und andere Doku-Dateien: **Deutsch**.
- `.editorconfig` ist verbindlich und wird nicht verändert.
- Nullable Reference Types aktiv, Warnings as Errors.
- Für JSON ausschließlich `System.Text.Json`, kein Newtonsoft.
- Ordnernamen immer im **Plural** (`Controllers/`, `Validators/`, `Mappings/`). Ausnahme: Ordner, die Projektnamen abbilden (z. B. `UnitTests/Domain/`).
- Ordner nach Fachthema gliedern (`Addresses/`), darin nach Typ. In `.Core.*`-Projekten ist das Projekt selbst das Fachthema, dort direkt nach Typ gliedern (`Dtos/`, `Validators/`, `Mappings/`).
- Interfaces liegen in einem zentralen Ordner `Contracts/` pro Projekt.
- Klassen, die nur Extension-Methoden enthalten, heißen `*Extensions.cs` (Plural), z. B. `ServiceCollectionExtensions.cs`.
- DI-Registrierung pro Projekt über eine Extension-Methode (`AddDomain()`, `AddCoreAddresses()`, `AddData()`).
- XML-Dokumentation (`/// <summary>`) für alle öffentlichen Typen und Member.
- Zeitangaben ausschließlich über `TimeProvider`, nie `DateTime.Now`/`DateTimeOffset.UtcNow` direkt.
- Hilfsskripte im Repo-Root sind .NET File-based Apps (`*.cs`, ausführbar mit `dotnet run <datei>.cs`). Keine `.sh`- oder `.ps1`-Skripte.

## Fehlerbehandlung

- Exceptions nur bei tatsächlich unerwarteten Fehlern.
- Erwartbare Fälle (z. B. Datensatz nicht gefunden, Validierungsfehler) werden über Rückgabewerte abgebildet (`null`, `bool`, ein kleiner Ergebnistyp), nicht über Exceptions.
- Eine Methode, die zwingend ein Objekt erwartet, prüft es und wirft nur dann eine Exception, wenn es fehlt (z. B. `ArgumentNullException`).
- Keine Framework-APIs, die für erwartbare Fälle Exceptions werfen, als Kontrollfluss nutzen.
- Unerwartete Exceptions übersetzt ein globaler `IExceptionHandler` in **500** mit Problem Details (mit `traceId`, ohne Stacktrace nach außen, vollständig geloggt).
- `OperationCanceledException` durch Client-Abbruch: Log auf Information-Level, kein 500.

## Async und Abbruch

- Alle asynchronen Methoden erhalten einen `CancellationToken` als letzten Parameter und reichen ihn an alle aufgerufenen Methoden weiter, die ihn unterstützen (EF Core usw.).
- Bei längeren Abläufen an sinnvollen Stellen `cancellationToken.ThrowIfCancellationRequested()` aufrufen.

## API-Regeln

- REST-konforme Statuscodes, Fehler immer als Problem Details (RFC 7807 bzw. Nachfolger RFC 9457).
- Validierungsfehler → 400 als `ValidationProblemDetails` mit allen Feldfehlern.
- Gelöschte oder unbekannte Datensätze liefern immer 404. Es darf nicht erkennbar sein, ob ein Datensatz existiert hat.
- Die OpenAPI-Definition muss vollständig korrekt sein: alle Statuscodes, Header, Problem-Details-Schemas und alle Einschränkungen, die OpenAPI ausdrücken kann (`required`, `maxLength`, `pattern`, `format`, `nullable`, Enums, Min/Max-Werte).
- Eingaben bereinigen vor der Validierung: Strings trimmen, `Country` in Großbuchstaben, leere Strings bei optionalen Feldern wie `null` behandeln.

## Tests

- Frameworks: NUnit, AwesomeAssertions, NSubstitute (Mocks), `Microsoft.AspNetCore.Mvc.Testing` (Integrationstests).
- Benennung: `Methode_Szenario_Erwartung`, z. B. `UpdateAsync_UnknownId_ReturnsNull`.
- Aufbau: Arrange/Act/Assert mit den Kommentaren `// Arrange`, `// Act`, `// Assert`.
- Jede Klasse und jeder Endpunkt wird mit **allen gültigen Fällen** und gezielt mit **Fällen, die fehlschlagen müssen**, getestet. Beispiele: `Country = "GER"`, `"D"`, `"de1"`, `"XX"`; Strings über der Maximallänge; Pflichtfelder nur mit Leerzeichen; ungültige E-Mails; ungültige GUIDs in der Route.
- Integrationstests: Jeder Test bzw. jede Fixture nutzt eine eigene InMemory-Datenbank (eindeutiger Name), damit sich Tests nicht beeinflussen.

## NuGet-Pakete und Lizenzen

- Nur permissive Lizenzen (MIT, Apache 2.0, BSD). Keine Pakete mit kommerziellen oder unklaren Lizenzen (deshalb z. B. AwesomeAssertions statt FluentAssertions).
- Bewährte, verbreitete Pakete sind Eigenbau vorzuziehen.
- Freigegebene Pakete: NUnit, NUnit3TestAdapter, AwesomeAssertions, NSubstitute, coverlet.MTP, ReportGenerator (dotnet-Tool), FluentValidation (inkl. FluentValidation.DependencyInjectionExtensions), Serilog.AspNetCore, Scalar.AspNetCore sowie Microsoft-/.NET-Pakete.
- **Jedes weitere Paket nur nach Rückfrage.**
- Versionen zentral in `Directory.Packages.props`.
- Jedes Nicht-Microsoft-Paket wird mit Lizenz in der `README.md` aufgeführt.

## Kommunikation

- Fragen und Statusmeldungen kurz und knapp, nur das Wichtigste. Details nur auf Nachfrage.