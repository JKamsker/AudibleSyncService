
# AudibleSyncService

## QuickStart
[For a quickstart click here](/docs/getting-started-docker.md)

# Build & Run from Source
[For building and running from Source, click here](/docs/getting-started-docker-source.md)

## Without Docker
Open in VS and create ``AudibleSyncService/appsettings.Development.json`` 
with following contents: 
```json
{
  "Audible": {
    "Headless": false,
    "Setup": true,
    "Locale": "germany",
    "Credentials": {
      "UserName": "jonas.kamsker@mail.com",
      "Password": "MySuperSecurePassword"
    },
    "Environment": {
      "SettingsBasePath": "path\\to\\settings",
      "TempPath": "path\\to\\Temp",
      "OutputPath": "path\\to\\Output",
      "OutputPattern": "%Author%\\%Series%\\%title%\\%title%.%ext%"
    }
  }
}
```
After setup invert ``Audible.Headless`` and ``Audible.Setup``. </br>
``Audible.Environment.TempPath`` and ``Audible.Environment.SettingsBasePath`` will be created and filled automatically if not set, ``Audible.Environment.OutputPath``  and ``Audible.Environment.OutputPath``are obligatory and the tool will crash with a great ``kaboom`` if not set or misconfigured ¯\\\_(ツ)_/¯

## FAQ
Q: **Which Locales are available?** </br>
A: ``germany``, ``us``, ``uk``, ``australia``, ``canada``, ``france``,  ``india``, ``italy``, ``japan``, ``spain`` (see [Source](https://github.com/JKamsker/AudibleApi/blob/dbb51c6183db831c2c1b518d613978df6e7d4061/AudibleApi/Localization.cs#L20)) 
