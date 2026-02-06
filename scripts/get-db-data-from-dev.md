## Get a dump of data from server

```
docker exec -it <container_name> bash -lc 'pg_dump "CONNECTION_STRING_HERE" \
  --no-owner --no-privileges \
  -f do_dump.sql'
```

## Restore locally

```
docker exec -it <container_name> bash -lc 'psql -U user -d db-fairwayfinder -f do_dump.sql'
```