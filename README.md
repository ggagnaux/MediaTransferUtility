# Media Transfer Utility

Media Transfer Utility is a Windows desktop app for organizing photos and videos from a source folder into a dated destination structure.

## Features

- Browse and select **Source** and **Destination** folders
- Organize media into folders by date:
  - `YYYY/YYYYMMDD/<CustomFolderName>`
- Optional creation of:
  - `Edits` folder
  - `Final` folder
- Optional removal of source files after successful copy
- Collision-safe copy (auto-appends `_1`, `_2`, etc. when file names already exist)
- Transfer progress and detailed log view
- Cancel in-progress transfer
- Optional log file export after run
- Light/Dark theme toggle
- Window and option state persistence between app launches

## Screenshot

![Media Transfer Utility screenshot](Screenshots/2026-04-17%2014_28_49-Media%20Transfer%20Utility.png)


## Supported Media Types

Images:
- `.jpg`, `.jpeg`, `.png`, `.bmp`, `.gif`, `.tif`, `.tiff`, `.heic`, `.webp`

Videos:
- `.mp4`, `.mov`, `.avi`, `.mkv`, `.wmv`, `.m4v`, `.3gp`, `.mts`, `.m2ts`

## Date Resolution Priority

For each file, the app determines destination date using:
1. Date detected from filename pattern `yyyyMMdd`
2. EXIF metadata (`DateTimeOriginal`, fallback EXIF date tags)
3. File last write time

## Requirements

- Windows
- .NET 10 SDK/runtime

## Build and Run

From the project root:

1. Restore/build:
   - `dotnet build`
2. Run:
   - `dotnet run --project MediaTransferUtility.csproj`

## Usage

1. Launch the application.
2. Select a source folder.
3. Select a destination folder.
4. (Optional) Change destination media folder name (default: `Original`).
5. (Optional) Enable desired options:
   - Remove source file after successful copy
   - Create `Edits` folder
   - Create `Final` folder
   - Save log file after run
6. Click **Start Transfer**.
7. Monitor progress/log and cancel if needed.

## App State Storage

Application state is saved to:

- `%LocalAppData%\MediaTransferUtility\appstate.json`

## Notes

- Existing files in destination are never overwritten.
- If log export is enabled, a timestamped log file is created in the destination root.
