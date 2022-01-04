
# AudibleSyncService

## QuickStart
(For a quickstart click here)[/docs/getting-started-docker.md]

https://github.com/JKamsker/AudibleSyncService/tree/Scheduler

## 1. Setup

You can either  [1.0.1 Run ready-made container ](#101-run-ready-made-container ) or [1.0.2 Run from source](#102-run-from-source) </br>
Then continue with [### 1.0.3 Configure environment variables](#103-configure-environment-variables)

### 1.0.1 Run ready-made container
````yaml
version: '3.3'
services:
 audible_sync:
  container_name: audible_sync
  volumes:
   - './data:/data'
  environment:
   - Audible__Locale=${AUDIBLE_LOCALE} # See FAQ for more
   - Audible__Credentials__UserName=${AUDIBLE_USERNAME} # Replace with your username or use .env
   - Audible__Credentials__Password=${AUDIBLE_PASSWORD} # Replace with your password or use .env
  image: jkamsker/audible_sync_service:latest
````

Head over to [### 1.0.3 Configure environment variables](#103-configure-environment-variables)



### 1.0.2 Run from source

- Execute following commands
```bash
git clone https://github.com/JKamsker/AudibleSyncService.git --recursive
cd AudibleSyncService
docker-compose build
```


### 1.0.3 Configure environment variables

Create a ``.env`` file with following contents (Replace with your credentials): 
```text
AUDIBLE_LOCALE=germany
AUDIBLE_USERNAME=jkamsker@fakemail.com
AUDIBLE_PASSWORD=MySuperSecurePassword
```

To setup the worker, you can use this command to log into your account
```bash
docker-compose run --rm audible_sync bash -setup
```

Please note, that the worker will exit with exit code ``-1`` in non-setup & non-headless mode when audible prompts for 2FA. So, watch out for those events.

## Starting service
```bash
docker-compose up -d
```

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
