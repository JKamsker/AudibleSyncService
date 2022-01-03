# AudibleSyncService

## Setup

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