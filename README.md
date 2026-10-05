# SpellMage

SpellMage is a Dalamud plugin for reviewing FFXIV chat spelling and grammar with [LanguageTool](https://languagetool.org). Check text, choose individual corrections, and copy the reviewed message back into chat.

## Features

- `/spellmage` opens the editor; `/spell <message>` opens and checks a message.
- Each suggestion is unchecked initially. Only selected corrections change the reviewed output.
- Protects slash-command tokens, URLs, email addresses, numbered tokens, placeholders, and dictionary terms from correction.
- Saved API enablement, language, server endpoint, and native-reading preferences apply to both commands and windows.
- Optional experimental native chat reading and a chat-anchored suggestions overlay.
- Live checks run after 900 ms without a text change, **only against a configured private LanguageTool server**.
- Does not send messages or write to the game chat input. Use the clipboard to transfer reviewed text.

## Privacy and server configuration

API checking and native reading are off by default. Enable checking in Settings after choosing a server. Manual checking uses `https://api.languagetool.org/v2/check` by default; entered text is sent to that service. Review [LanguageTool's privacy policy](https://languagetool.org/legal/privacy). SpellMage does not log message bodies or server response bodies.

The [public API rules](https://dev.languagetool.org/public-http-api.html) prohibit automated requests. Live checking is therefore blocked for `api.languagetool.org`. To check while typing, configure your own compatible server (for example, `http://localhost:8081/v2/check`), enable API checking, enable native reading, and enable live checking. Custom remote endpoints must use HTTPS. Authentication for paid API accounts is not implemented.

Live checking sends focused chat drafts to your configured server after a pause, even before you send the chat message. Leaving the chat input, clearing the draft, changing settings, or unloading the plugin cancels pending checks and clears the overlay. An in-flight request may already have reached the server before cancellation.

## Build and test

Prerequisites: .NET 10 SDK and Dalamud development assemblies for API level 15. Normally XIVLauncher supplies these after the game has run with Dalamud enabled. For a custom location, set `DALAMUD_HOME` to the folder containing `Dalamud.dll` and `FFXIVClientStructs.dll`.

```sh
dotnet restore SpellMage.sln --locked-mode
dotnet build SpellMage.sln --configuration Release --no-restore
dotnet run --project SpellMage.Tests/SpellMage.Tests.csproj --configuration Release
```

The tests use fake HTTP handlers and require neither a running game nor an external spell-check server. They exercise the actual correction, API, and request lifecycle source files.

Build outputs:

- Dev plugin: `SpellMage/bin/x64/Release/SpellMage.dll`
- Release package: `SpellMage/bin/x64/Release/SpellMage/latest.zip`
- The ZIP includes the manifest, assembly, dependency metadata, and `Dictionaries/ffxiv-ignore.txt`.

CI builds on Windows, runs the regression executable, verifies the ZIP contents, and uploads the release package. CI runs on PRs, pushes to `main`, `master`, or `dev/**`, and manual dispatch.

## Load in-game

1. Open `/xlsettings`, then Experimental, and add the full path to `SpellMage.dll` under Dev Plugin Locations.
2. Enable SpellMage through `/xlplugins` → Dev Tools.
3. Open `/spellmage`, select Settings, and enable API checking.
4. Enter a test message, press Check, select desired corrections, and copy the reviewed text.

If upgrading from the prototype, replace the old `SamplePlugin.dll` dev-plugin entry. The assembly/internal name changed, so existing settings may not migrate automatically; reconfigure SpellMage. Do not load both copies.

## Native integration status

Native reading uses the typed `AddonChatLog.TextInput` structure from the FFXIVClientStructs assembly supplied by Dalamud. It accesses native pointers only on framework updates and never retains them. The previous reflection-based pointer guessing was removed. Auto-translate/link payloads are skipped.

This path **compiles against the downloaded Dalamud assemblies but has not been validated in a running game**. Native layouts and the input-active flag are game-version dependent. Keep it experimental until the [in-game checklist](docs/IN_GAME_TESTS.md) is completed. The overlay is informational; use the main editor to review individual corrections.

## Layout

- `Plugin.cs`: services, commands, framework updates, and cleanup.
- `Services/`: LanguageTool HTTP client, protected correction logic, cancellable requests, and typed native input access.
- `Windows/`: editor, settings, and native overlay.
- `Models/`: API responses, suggestions, and input snapshots.
- `SpellMage.Tests/`: standalone regression checks.

Licensed under AGPL-3.0-or-later; see [LICENSE.md](LICENSE.md).
