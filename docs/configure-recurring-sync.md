AudibleSyncService also supports Synching periodically. To do this, you only have to another environment variable to your docker-compose.yml:

```yml
        environment:
          - "Audible__Schedule__Expression=0 0 1 ? * * *"
```
Running the tool now, will echo the next occuring executions.
In this example, this will happen once a day at 0:01 local time
To generate your own expression, you can use [cronmaker](http://www.cronmaker.com/)

If you don't want to  run sync immediately, you can turn it of by using the following switch
```yml
        environment:
          - "Audible__Schedule__RunImmediately=false"
```