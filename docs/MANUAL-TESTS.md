# Manual test checklist

1. Launch ClipVault and confirm one process/window only; launch a second instance and confirm it exits.
2. Copy Unicode and multiline text from Notepad, VS Code, Unity, Office, and two browsers. Confirm one saved entry per unique payload and no raw payload in `%LOCALAPPDATA%\ClipVault\Logs`.
3. Copy an HTTPS URL, File Explorer file selection, and screenshot. Confirm correct content type and persistence after restart.
4. Hold the clipboard open from a test utility while copying; confirm ClipVault remains responsive and later captures succeed.
5. Toggle monitoring off and copy data; toggle it on and verify the missed data was not queued. Repeat with Private Mode.
6. Search multiple terms, pin/favorite independently, soft-delete, and restart. Confirm state persists.
7. Focus Notepad, press `Ctrl+Shift+V`, choose an entry, then Paste. Confirm only the remembered Notepad window receives input. Switch targets during the operation and verify safe copy-only fallback.
8. Toggle each window's pin independently. Minimize/restore and confirm each setting persists after restart.
9. Exercise compact search, keyboard focus, copy/paste, minimize, close/hide, expand, resize, and Windows snapping.
10. Repeat on two monitors at 100%, 150%, and 200% scaling; disconnect the secondary display and check window recoverability.
11. Suspend/resume Windows and retest capture/hotkeys.
12. Populate 5,000+ items and inspect launch/search/scroll responsiveness and idle CPU/RAM.
