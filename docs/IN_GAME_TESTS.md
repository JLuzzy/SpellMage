# In-game validation

Status: **not run**. Compilation and fake-server tests do not establish game runtime safety, correct native focus detection, or layout quality.

Record the game version, Dalamud version, display resolution, and UI scale when performing these checks. Use synthetic text; do not include private chat content in screenshots or logs.

- [ ] Install the release ZIP or enable the dev DLL; confirm no old SamplePlugin instance is loaded.
- [ ] Open `/spellmage` and Settings. Check readability, resizing, scrolling, and button layout at normal and high UI scale.
- [ ] With API disabled, use both Check and `/spell teh party is redy`. Both should show a disabled message without making a request.
- [ ] Enable manual API checking. Check the same test sentence; suggestions begin unselected. Select and deselect corrections; clipboard text must match the preview.
- [ ] Test `/party teh`, `P12S`, `prog`, `WHM`, a URL with a misspelled segment, and emoji. Protected text must remain unchanged.
- [ ] Change the editor text while a request is pending. No result from the previous draft should appear or be copyable.
- [ ] Use a private test server to simulate offline, malformed, slow, and rate-limited responses. Confirm useful status text and no stale output.
- [ ] Change language and endpoint, then reload the plugin. Settings should persist and `/spell` should honor them.
- [ ] Enable native reading only. Type a synthetic draft and use Read current chat draft. Confirm text matches, including slash commands and Unicode.
- [ ] Enable live checking while using the public endpoint. Confirm no automatic network requests occur.
- [ ] Configure a private server and enable live checking. Confirm one request after typing pauses, with suggestions near chat. Keep typing or change the draft during a slow request; old suggestions must never reappear.
- [ ] Send, clear, or unfocus the draft. The overlay should disappear. Refocus the same unsent draft and confirm checking resumes.
- [ ] Test auto-translate and linked item payloads; these must not be sent for checking.
- [ ] Move/resize chat; test another resolution and UI scale. Confirm overlay remains on-screen and does not capture typing or mouse input.
- [ ] Disable native reading, log out, reload UI, and unload during a slow request. Confirm no continuing reads, requests, or stale overlay.
- [ ] Check plugin logs: message bodies and corrected text must be absent.

Automatic chat sending and native text modification are intentionally absent. Review and clipboard copy form the supported correction workflow.
