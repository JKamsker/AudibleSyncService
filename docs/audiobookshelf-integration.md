This guide assumes, you have a working instance of ``AudibleSyncService`` and ``audiobookshelf``. <br/>

Our goal here is, to link the output folder (``/data/out``) from ``AudibleSyncService`` and a library folder (``/audible``) of our ``audiobookshelf`` instance. <br/>
This can be archived pretty easily with ``docker-compose``

Let`s assume our existing ``audiobookshelf`` configuration looks like this:

```yml
version: '3.3'
services:
  audiobookshelf:
    image: advplyr/audiobookshelf
    user: "1002:1000"
    ports:
      - 13378:80
    volumes:
      - ./data/audiobooks:/audiobooks
      - ./data/audible:/audible
      - ./data/metadata:/metadata
      - ./data/config:/config
```
Note, that we have already prepared a folder ``/data/audible`` which is mounted to ``/audible``. We are going to use that as ountput from our ``AudibleSyncService`` container</br>


And our ``AudibleSyncService`` configuration looks like this: 
```yml
version: '3.3'
services:
    audible_sync:
        container_name: audible_sync
        volumes:
            - './data:/data'
        environment:
          - Audible__Locale=germany
          - Audible__Credentials__UserName=${AUDIBLE_USERNAME}
          - Audible__Credentials__Password=${AUDIBLE_PASSWORD}
        image: ghcr.io/jkamsker/audiblesyncservice/audible_sync_service:latest # Run with latest version
```

Then the only thing we have to do is, to merge both contents and adjust some variables in the ``AudibleSyncService`` part:
- Mount ``/data`` to ``./data/audible_data`` to allow persistance
- Mount ``/data/out`` to ``./data/audible``
- Disable the default RunOnce config using the environment setting: ``- Audible__RunOnce=false``
- Add a daily schedule: ``- "Audible__Schedule__Expression=0 0 1 ? * * *"``

**DONT FORGET TO COPY YOUR .ENV FILE**

The final yaml could look something like this:

```yml
version: '3.3'
services:
  audiobookshelf:
    image: advplyr/audiobookshelf
    user: "1002:1000"
    ports:
      - 13378:80
    volumes:
      - ./data/audiobooks:/audiobooks
      - ./data/audible:/audible
      - ./data/metadata:/metadata
      - ./data/config:/config
  audible_sync:
    container_name: audible_sync
    user: "1002:1000"
    volumes:
    - './data/audible_data:/data'
    - './data/audible:/data/out'
    environment:
    - Audible__Locale=${AUDIBLE_LOCALE} # See FAQ for more
    - Audible__Credentials__UserName=${AUDIBLE_USERNAME} # Replace with your username or use .env
    - Audible__Credentials__Password=${AUDIBLE_PASSWORD} # Replace with your password or use .env
    - Audible__RunOnce=false
    - "Audible__Schedule__Expression=0 0 1 ? * * *"
    image: ghcr.io/jkamsker/audiblesyncservice/audible_sync_service:latest
```