## 1. Setup

To install docker, head over to [Get-Docker](https://docs.docker.com/get-docker/)

On linux, you can use this script to insall all dependencies:
```bash
curl -fsSL https://get.docker.com | bash
apt update && apt install -y docker-compose

Create a ``docker-compose.yml`` file with following content:
```

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

Please note, that the worker will exit with exit code ``-1`` in non-setup & non-headless mode when audible prompts for 2FA. So, watch out for those events. </br>

To run the service in headless mode afterwards use:
```bash
docker-compose up -d
```