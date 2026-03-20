# Daily To Do List

A native Windows desktop to-do list concept built with **C# and WPF**. The app is designed to live on the right side of the screen, stay always on top, smoothly collapse into a bottom-right tab when not hovered, and expand again when the user moves the mouse back over it.

## Features

- Always-on-top right-docked panel sized to roughly one fifth of the desktop width.
- Smooth collapse/expand animation so the app gets out of the way while staying accessible.
- Add tasks quickly with Enter or the add button.
- Drag active tasks to reorder them.
- Completed tasks are moved out of the active list and shown in a grouped expandable section.
- Adjustable width and height using the left-edge and top-left resize handles.
- Local JSON persistence in `%LocalAppData%\DailyToDoList\tasks.json`.

## Project structure

- `DailyToDoList/` — WPF application source.
- `DailyToDoList/Models/TaskItem.cs` — task model.
- `DailyToDoList/ViewModels/MainViewModel.cs` — task state management, grouping, and persistence triggers.
- `DailyToDoList/Services/TaskStorageService.cs` — local save/load service.

## Build on Windows

1. Install the **.NET 8 SDK** and Visual Studio workload for **Desktop development with .NET**.
2. Open `DailyToDoList.sln` in Visual Studio 2022 or newer.
3. Build and run the `DailyToDoList` project.

## Notes

This repository was created in a non-Windows environment, so the source is ready for Windows but should be built on Windows where WPF is supported.
