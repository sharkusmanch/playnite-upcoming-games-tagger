# Upcoming Games Tagger

A Playnite extension that automatically tags games with upcoming release dates for easy filtering.

## Features

- **Automatic Tag Management**: Creates and maintains an "Upcoming" tag (name customizable)
- **Smart Filtering**: Tags only games with future release dates
- **Configurable Threshold**: Set how far ahead to look for upcoming releases (default: 1 year)
- **Auto-Update**: Automatically updates tags when your library changes
- **Manual Control**: Force update the tags via the main menu
- **Flexible Options**: Choose whether to include games without release dates
- **Notifications**: Get notified when tags are updated (optional)

## Installation

1. Build the extension using Task
2. Install the generated `.pext` file in Playnite

## Building

This project uses [Task](https://taskfile.dev/) for build automation. Available commands:

```bash
# Build, pack, and install
task all

# Individual commands
task build      # Build the project
task pack       # Create .pext package
task install    # Install the package in Playnite
task dev-install # Install directly to extensions folder for development
task clean      # Clean build artifacts
task logs       # Watch Playnite extension logs
```

## Configuration

The extension provides several configuration options:

- **Tag Name**: Customize the name of the upcoming games tag (default: "Upcoming")
- **Auto-Update**: Enable/disable automatic updates when library changes
- **Days Ahead Threshold**: Limit how far into the future to look for releases (0 = no limit)
- **Include Games Without Release Date**: Whether to include games that don't have a release date set
- **Show Notifications**: Enable/disable update notifications

## Usage

1. Install and enable the extension
2. The extension will automatically create an "Upcoming" tag
3. Games with future release dates are automatically tagged
4. Use Playnite's filter system to filter by the "Upcoming" tag to see only unreleased games
5. The tags are updated automatically when your library changes (if enabled)
6. You can manually update via "Extensions" → "Upcoming Games Tagger" → "Update Upcoming Games Tag"

## Requirements

- Playnite 6.12.0 or later
- .NET Framework 4.6.2 or later

## License

[Add your license here]