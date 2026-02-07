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

## Update Ids for Users

BEGIN;

DO $$
DECLARE
  target uuid := '';
  old1   uuid := '';
  r record;
BEGIN
  FOR r IN
    SELECT table_schema, table_name, udt_name
    FROM information_schema.columns
    WHERE column_name = 'user_id'
      AND table_schema = 'public'
  LOOP
    IF r.udt_name = 'uuid' THEN
      EXECUTE format('UPDATE %I.%I SET user_id = $1 WHERE user_id = $2',
                     r.table_schema, r.table_name)
      USING target, old1;
    ELSE
      EXECUTE format('UPDATE %I.%I SET user_id = $1 WHERE user_id = $2',
                     r.table_schema, r.table_name)
      USING target::text, old1::text;
    END IF;
  END LOOP;
END $$;

COMMIT;
