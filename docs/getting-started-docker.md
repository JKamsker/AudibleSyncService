## 1. Setup

Create a ``docker-compose.yml`` file with following content
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