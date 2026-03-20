# Daily To Do List

A native Windows desktop to-do list app built with **C# and WPF**. The app is designed to live on the right side of the screen, stay always on top, smoothly collapse into a bottom-right tab when not hovered, and expand again when the user moves the mouse back over it.

## Features

- Always-on-top right-docked panel sized to roughly one fifth of the desktop width.
- Smooth collapse/expand animation so the app gets out of the way while staying accessible.
- Add tasks quickly with Enter or the add button.
- Drag active tasks to reorder them.
- Completed tasks are moved out of the active list and shown in a grouped expandable section.
- Adjustable width and height using the left-edge and top-left resize handles.
- Local JSON persistence in `%LocalAppData%\DailyToDoList\tasks.json`.

## What you can run on Windows

You do **not** need Visual Studio to generate the app executable.

If you have the **.NET 8 SDK** installed on Windows, you can build a real packaged executable by double-clicking:

- `scripts\publish-win-x64.bat`

That script will produce:

- `dist\win-x64\DailyToDoList.exe`

It will also keep the command window open on errors and write a publish log to:

- `dist\win-x64\publish.log`

After publishing, you can launch the app by double-clicking:

- `scripts\run-published.bat`

## Quick start on Windows

1. Install the **.NET 8 SDK**.
2. Double-click `scripts\publish-win-x64.bat`.
3. Wait for publish to finish.
4. If the publisher reports an error, read the same window or open `dist\win-x64\publish.log`.
5. Open `dist\win-x64\DailyToDoList.exe` directly, or double-click `scripts\run-published.bat`.

## Project structure

- `DailyToDoList/` — WPF application source.
- `DailyToDoList/Models/TaskItem.cs` — task model.
- `DailyToDoList/ViewModels/MainViewModel.cs` — task state management, grouping, and persistence triggers.
- `DailyToDoList/Services/TaskStorageService.cs` — local save/load service.
- `scripts/publish-win-x64.bat` — one-click Windows publish script.
- `scripts/publish-win-x64.ps1` — PowerShell version of the publish script.
- `scripts/run-published.bat` — launches the generated executable.

## Notes

- This repository was prepared in a non-Windows environment, so I could not generate the final `.exe` artifact here.
- The project is configured for **self-contained single-file** publishing so the published output is a real Windows executable.
