# AudibleSyncService

## 1. Setup


### 1.0.1 Run ready-made container

#### 1.0.1.0 Login to the github registry
- https://github.com/settings/tokens
- Generate new token with the ``read:packages`` scope
- Login using the following command while replacing ``ghp_SUPERSECURETOKEN`` and ``JKamsker`` with your values
-- ``echo "ghp_SUPERSECURETOKEN" | docker login docker.pkg.github.com -u "JKamsker" --password-stdin``

#### 1.0.1.1 Create a docker-compose.yml
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
		image: ghcr.io/jkamsker/audiblesyncservice/audible_sync_service:latest
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

- Create a ``.env`` file with following contents (Replace with your credentials): 
```text
AUDIBLE_LOCALE=germany
AUDIBLE_USERNAME=jonas.kamsker@mail.com
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
Coming soon™


## FAQ
Q: **Which Locales are available?**
A: ``germany``, ``us``, ``uk``, ``australia``, ``canada``, ``france``,  ``india``, ``italy``, ``japan``, ``spain`` ( see [Source](https://github.com/JKamsker/AudibleApi/blob/dbb51c6183db831c2c1b518d613978df6e7d4061/AudibleApi/Localization.cs#L20)) 
